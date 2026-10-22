# Gateway ve yerel geliştirme

Visual Studio ile ilk kurulum için [İlk çalıştırma rehberini](first-run.md) kullan.

## Ayarlar ve çalıştırma

Aşağıdaki komutlar PowerShell içindir ve depo kökünden çalıştırılır. Önce `.env.example` dosyasını `.env` olarak kopyalayıp bağlantıları düzenle; mevcut `.env` dosyanı tekrar kopyalayarak ezme. `.env` sağlayıcısı göreli yolu süreç çalışma dizininden okur. Bu nedenle aşağıda uygulamalar depo kökünden derlenmiş DLL ile çalıştırılır; `--contentRoot` ilgili projenin JSON dosyalarını seçer.

```powershell
dotnet build AdNoir.sln
$env:ASPNETCORE_ENVIRONMENT = "Development"
```

MongoDB replica set hazır olduktan sonra, başlangıç verisi ve index'ler için:

```powershell
$env:DOTNET_ENVIRONMENT = "Development"
dotnet src/Tools/AdNoir.DbMigrator/bin/Debug/net9.0/AdNoir.DbMigrator.dll
```

Üç ayrı terminalde, her biri depo kökünde ve Development ortamıyla:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet src/Services/IdentityAndOrg/AdNoir.IdentityAndOrg.Api/bin/Debug/net9.0/AdNoir.IdentityAndOrg.Api.dll --contentRoot src/Services/IdentityAndOrg/AdNoir.IdentityAndOrg.Api
```

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet src/Services/Sample/AdNoir.Sample.Api/bin/Debug/net9.0/AdNoir.Sample.Api.dll --contentRoot src/Services/Sample/AdNoir.Sample.Api --urls http://localhost:5090
```

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet src/Gateway/AdNoir.Gateway/bin/Debug/net9.0/AdNoir.Gateway.dll --contentRoot src/Gateway/AdNoir.Gateway --urls http://localhost:5070
```

Identity'nin Development Kestrel ayarları REST/JWKS için 5080, HTTP/2 gRPC için 5082 kullanır. Gerçek Identity/Sample event alışverişi için ikisi de aynı erişilebilir RabbitMQ'ya bağlanmalıdır; iki ayrı süreçteki in-memory transport birbirleriyle konuşmaz.

## Yönlendirme ve kimlik

Gateway açıkça listelenen auth, tenant, organizasyon, kullanıcı, rol, Sample ve JWKS yollarını yönlendirir. Servislerin health, Scalar/OpenAPI ve gRPC uçlarını genel bir catch-all ile dışarı açmaz.

- Auth: register/login/refresh/logout anonim; change-password token ister.
- Tenant/organizasyon/kullanıcı/rol/Sample yolları token ister.
- JWT imzası, issuer, audience, süre ve RS256 algoritması kontrol edilir.
- Authorization başlığı downstream'e aktarılır; yetki ve veri kapsamı kontrolleri servislerde kalır.
- JWKS adresi Production'da HTTPS olmalıdır; Development yalnız loopback HTTP'ye izin verir.
- Token imzalıdır, şifreli değildir. İçindeki `hierarchy_path` gibi claim'ler token sahibince okunabilir.
- Forwarded header'lar güvenilir kabul edilmez. IP kotası doğrudan ağ eşinin adresini kullanır. Gateway başka proxy arkasına konulacaksa güvenilir proxy adresleri açıkça tasarlanmalıdır; mevcut uygulama istemcinin X-Forwarded-For başlığına göre kota seçmez.

## İstek limitleri

Varsayılan pencere 60 saniyedir. Limitler `AdNoir:Gateway` altında değiştirilir.

| Trafik | Partition | Varsayılan limit |
|---|---|---|
| Login + register + refresh | IP başına ortak auth kotası; token olsa da geçerli | 10 / pencere |
| Diğer anonim istekler | IP | 30 / pencere |
| Doğrulanmış kimlik | Tenant GUID + kullanıcı GUID | 300 / pencere |
| Gateway health uçları | Limit dışında | — |

Redis yapılandırılmışsa Redis sliding window kullanılır. Tüm gateway örnekleri aynı Redis'i, `RateLimitPrefix` değerini ve limit ayarlarını paylaşmalıdır. Bağlantı hatasında bellek içi moda geçilmez; 503 zarfı döner. Production'da Redis bağlantı dizesi boşsa uygulama başlamaz.

Development'ta Redis ayarı boşsa altı segmentli yerel sliding window kullanılır. Bu, süreç başına limittir ve dağıtık davranışın kanıtı değildir. Redis kotası dolunca 429 ve `Retry-After` döner; Redis kütüphanesi kesin bekleme süresi vermediğinden tüm pencere uzunluğu kullanılır.

CORS varsayılanında hiçbir origin izinli değildir. Frontend için örneğin `.env` dosyasına:

```dotenv
AdNoir__Gateway__AllowedOrigins__0=http://localhost:5173
```

Origin tam olmalı; sonuna slash veya path eklenmez. CORS tarayıcı erişim politikasıdır, sunucu yetkilendirmesinin yerine geçmez.

## HTTP yanıtları ve health

Gateway kaynaklı 404/405/415/429/502/503/504 hataları merkezi katalogdan gelen `BaseResponse` zarfıyla döner. Mevcut enum değerleri korunarak 405, 415, 503 ve 504 karşılıkları sona eklendi. Servislerin kendi yanıt gövdeleri değiştirilmez. İstek gövdesi taşıyan POST/PUT/PATCH proxy çağrıları JSON gerektirir.

- `GET /health/live`: süreç cevap verebiliyor mu?
- `GET /health/ready`: Redis yapılandırıldıysa ping kontrolü; Redis yoksa yerel geliştirme hazır kabul edilir.

Readiness bütün downstream servislerin, JWKS'nin veya broker'ın hazır olduğunu kanıtlamaz.

## Örnek API akışı

Ön koşullar: migrator çalışmış, tenant etkin, Identity/Sample/Gateway ayakta ve bağlantılar doğru. Bu akışın register/login/refresh ve rol bulunmayan kullanıcı için 403 davranışı ayrı geliştirme veritabanında doğrulandı. Yetkili Sample okumasında bulunmayan kayıt için 404 doğrulandı; var olan kayıtla CRUD bu doğrulamanın kapsamında değildi. Windows'ta PowerShell alias'ı yerine `curl.exe` kullanılır.

```powershell
$baseUrl = "http://localhost:5070"
$email = Read-Host "Yeni kullanıcı e-postası"
$password = Read-Host "Geliştirme kullanıcısı parolası"

$registerBody = @{
    tenantSlug = "adnoir"
    email = $email
    password = $password
    displayName = "Demo User"
} | ConvertTo-Json -Compress
$registerBody | curl.exe -sS "$baseUrl/api/v1/auth/register" -H "Content-Type: application/json" --data-binary "@-"

$loginBody = @{
    tenantSlug = "adnoir"
    email = $email
    password = $password
} | ConvertTo-Json -Compress
$login = ($loginBody | curl.exe -sS "$baseUrl/api/v1/auth/login" -H "Content-Type: application/json" --data-binary "@-") | ConvertFrom-Json
if (-not $login.isSuccess) { throw "Giriş başarısız; response mesajını kontrol et." }

curl.exe -sS "$baseUrl/api/v1/sample-items/00000000-0000-0000-0000-000000000001" -H "Authorization: Bearer $($login.data.accessToken)" -H "Accept-Language: tr"

$refreshBody = @{ refreshToken = $login.data.refreshToken } | ConvertTo-Json -Compress
$refresh = ($refreshBody | curl.exe -sS "$baseUrl/api/v1/auth/refresh" -H "Content-Type: application/json" --data-binary "@-") | ConvertFrom-Json
```

Yeni kayıt otomatik olarak yönetici veya `item:read` yetkisi vermez. Korumalı çağrıda rol atanmamış kullanıcı için 403 beklenebilir. Yetkili kullanıcı için bu örnek ID yoksa 404 beklenir. Başarılı 200 akışı için izinli kullanıcı ve gerçekten var olan kayıt gerekir. Refresh rotasyonludur; sonraki çağrıda yeni refresh token kullanılmalıdır.

## Container seçeneği

Bu geliştirme makinesine Docker kurulmaz. Docker bulunan başka bir makinede gateway profili:

```powershell
docker compose --profile gateway up -d --build gateway
```

Bu profil Redis'i başlatır; Identity/Sample uygulamalarını başlatmaz. `ADNOIR_GATEWAY_IDENTITY_URL`, `ADNOIR_GATEWAY_SAMPLE_URL`, `ADNOIR_GATEWAY_JWKS_URI` ile container'dan erişilebilen adresleri ayarla. Host'taki yalnız loopback'e bağlı servisler container'dan erişilemeyebilir. Production JWKS için güvenilen HTTPS sertifikası gerekir; sertifika doğrulaması kapatılmaz. Varsayılan HTTPS örnek portu 5081'de dinleyici kurulması bu profilin yaptığı bir iş değildir.

Mongo replica set üyesi `mongo:27017` olduğunda host'un hosts dosyasına `127.0.0.1 mongo` eklenmelidir. Üye adresi hem host'tan hem container ağından çözülebilmelidir.

`.dockerignore` .env, özel anahtar, Git ve yerel derleme çıktılarını build context dışında bırakır. Bu oturumda container build/run yapılmadı.
