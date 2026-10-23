# İlk çalıştırma — Visual Studio ve DbMigrator

Bu rehber, ADNoir'ı ilk kez yerel ortamda çalıştıranlar içindir. Önce MongoDB ve başlangıç verileri hazırlanır, ardından Identity, Sample ve Gateway birlikte başlatılır. Docker bu akış için gerekli değildir.

## 1. Ön koşullar

- .NET 9 hedefini derleyebilen Visual Studio/SDK ve .NET 9 ASP.NET Core runtime kurulu olmalı.
- MongoDB çalışmalı ve replica set yapılandırması hazır olmalı. Varsayılan bağlantı: `mongodb://localhost:27017/?replicaSet=rs0`.
- Depo kökündeki `AdNoir.sln` Visual Studio ile açılmış olmalı.
- `.env.example` dosyasından `.env` oluşturulup bağlantılar düzenlenmeli. Mevcut `.env` dosyasının üzerine yazılmamalı; gerçek `.env` Git'e eklenmemeli.

Development ortamında Redis ve RabbitMQ ayarları boş kalabilir. Bu durumda cache ve limitler süreç belleğinde çalışır; ayrı süreçlerdeki in-memory mesajlaşma servisler arasında event teslim etmez. Gerçek dağıtık davranış için Redis ve RabbitMQ gerekir.

## 2. Çözümü derle

Visual Studio üst çubuğunda **Debug** yapılandırmasını seç. **Derle → Çözümü Derle** veya **Ctrl + Shift + B** ile derlemenin başarıyla bitmesini bekle.

Alternatif olarak depo kökünde terminalden:

```powershell
dotnet build AdNoir.sln
```

Aşağıdaki komutlar Debug/net9.0 derleme çıktısını kullanır.

## 3. DbMigrator'ı Visual Studio terminalinden çalıştır

Başlatma profili veya çalışma dizini menüsünü bulmak zorunda değilsin. **Görünüm → Terminal** menüsünden **Developer PowerShell** aç.

Önce depoyu klonladığın dizine geçin. Aşağıdaki yolu kendi konumunuzla değiştirin:

```powershell
cd <DEPO_KOKU>

$env:DOTNET_ENVIRONMENT = "Development"

dotnet .\src\Tools\AdNoir.DbMigrator\bin\Debug\net9.0\AdNoir.DbMigrator.dll
```

**Neden depo kökü?** Bu komutlardaki DLL yolları depo köküne göredir. Development ortamında varsayılan `.env` önce uygulamanın content root dizininde aranır; bulunamazsa üst dizinlerdeki en yakın `AdNoir.sln` dosyasının yanından yüklenir.

**Neden DOTNET_ENVIRONMENT?** DbMigrator bir konsol uygulamasıdır ve generic host kullanır. Ortamı bu değişkenle Development yaparız; yalnız ASPNETCORE_ENVIRONMENT ayarlamak konsol host'u için yeterli değildir. Bu değişken ilgili terminal oturumu için geçerlidir.

Bu işlem için DbMigrator'ı Visual Studio'nun başlangıç projesi yapmak gerekmez.

## 4. Başarılı sonucu kontrol et

Konsolda index oluşturma ve seed adımları görünür. Örnek başarı işaretleri:

```text
All indexes applied. Running seed.
Seed complete.
```

Gerçek log satırları zaman damgası ve oluşturulan kayıt bilgilerini de içerir. İşlem bitince terminalin komut satırına dönmesi normaldir; DbMigrator sürekli çalışan bir servis değildir.

Araç şu hazırlıkları yapar:

- Tenant, organizasyon, kullanıcı, rol, refresh token, audit ve Sample index'leri.
- Başlangıç kiracısı ve kök organizasyon birimi.
- Sistem rolleri ve izinleri.
- İlk yönetici hesabı.

MongoDB Compass üzerinden varsayılan `adnoir` veritabanını kontrol edebilirsin. `tenants`, `orgUnits`, `roles`, `users` koleksiyonlarında başlangıç kayıtları bulunur; `refreshTokens`, `auditLogs`, `sampleItems` gibi koleksiyonların ilk kurulumda boş olması normaldir.

### Yönetici hesabı ve parola

`.env` ile değiştirilmediyse varsayılan tenant `adnoir`, yönetici e-postası `admin@adnoir.local` olur.

`AdNoir__Seed__SuperAdminPassword` boş bırakılmışsa ilk hesap oluşturulurken rastgele parola üretilir ve konsolda **bir kez** gösterilir. Bu parolayı güvenli şekilde kaydet; belgeye, Git'e veya paylaşılan ekran görüntülerine ekleme. Veritabanındaki `passwordHash` alanı giriş parolası değildir.

Üretilen parola için hesapta `mustChangePassword` işaretlenir; ilk girişten sonra parola değiştirme akışını kullan. Migrator tekrar çalıştırıldığında mevcut yönetici hesabının parolasını yeniden göstermez veya değiştirmez. `.env` içindeki seed parolasını sonradan değiştirmek mevcut hesabı sıfırlamaz.

## 5. Uygulamaları birlikte başlat

Visual Studio'da **Çözüm Gezgini**nin en üstündeki solution'a sağ tıkla. **Başlangıç Projelerini Yapılandır / Set Startup Projects** seçeneğini aç. Sürüme göre bu bölüm **Özellikler → Başlangıç Projesi** altında olabilir.

**Birden çok başlangıç projesi** seçerek aşağıdaki tabloyu uygula:

| Proje | Eylem | Profil |
|---|---|---|
| AdNoir.IdentityAndOrg.Api | Başlat / Start | http |
| AdNoir.Sample.Api | Başlat / Start | http |
| AdNoir.Gateway | Başlat / Start | http |
| AdNoir.DbMigrator | Yok / None | — |
| Kütüphaneler ve test projeleri | Yok / None | — |

Profili seçebildiğin yerlerde `http` kullan. Mevcut HTTP profilleri `ASPNETCORE_ENVIRONMENT=Development` ayarını içerir. **Uygula → Tamam → F5** ile başlat.

`AdNoir.Shared.Contracts` gibi projeler kütüphanedir; doğrudan başlatılmaz. DbMigrator'ı her F5'te çalıştırmak gerekmez. Index/seed değişikliklerinde yeniden çalıştırılabilir; mevcut kayıtları kontrol edecek şekilde tasarlanmıştır.

Visual Studio proje dizininden başlatsa da Development ortamında ortak yükleyici depo kökündeki `.env` dosyasını bulur. Projenin content root dizininde ayrı bir `.env` varsa o dosya önceliklidir; dosyalar birleştirilmez. Arama en yakın `AdNoir.sln` sınırında durur. Açıkça verilen özel dosya yolları aynen kullanılır. Development dışındaki ortamlarda `.env` yüklenmez.

## 6. Tarayıcıdan kontrol et

Tarayıcı otomatik açılmayabilir; mevcut profillerde otomatik açılış kapalıdır.

| Amaç | Adres |
|---|---|
| Gateway canlılık kontrolü | http://localhost:5070/health/live |
| Gateway hazır olma kontrolü | http://localhost:5070/health/ready |
| Identity API deneme arayüzü | http://localhost:5080/scalar/v1 |
| Sample API deneme arayüzü | http://localhost:5090/scalar/v1 |
| Identity public JWKS | http://localhost:5080/.well-known/jwks.json |

Swagger UI yerine **Scalar** kullanılır. OpenAPI belgesi her API'de `/openapi/v1.json` adresindedir. Bu dokümantasyon uçları Development ortamında açılır. Gateway'de ayrıca Scalar ekranı yoktur ve `/` adresinin 404 dönmesi normaldir.

Identity Scalar ekranından `POST /api/v1/auth/login` çağrısında tenant, yönetici e-postası ve kendi parolanı gönder. Gateway üzerinden giriş adresi `http://localhost:5070/api/v1/auth/login` olur. Dönen access token korumalı çağrılarda `Authorization: Bearer TOKEN` başlığına eklenir.

Ayrıntılı register → login → korumalı endpoint → refresh örneği ve gateway ayarları için [Gateway ve yerel geliştirme](gateway.md) rehberine bak.

## Sık karşılaşılan durumlar

| Durum | Kontrol |
|---|---|
| DLL bulunamıyor | Önce Debug derlemesini yap; terminalin depo kökünde olduğunu kontrol et. |
| MongoDB bağlantı zaman aşımı | MongoDB servisi, bağlantı adresi ve replica set durumunu kontrol et. |
| Transaction replica set hatası | MongoDB standalone çalışıyorsa replica set yapılandırması gerekir. |
| Özel `.env` ayarları uygulanmıyor | Süreç çalışma dizini ve Development ortamını kontrol et. |
| Yönetici parolası tekrar görünmüyor | Hesap zaten varsa migrator parolayı yeniden üretmez; mevcut parolayı kullan. |
| Korumalı endpoint 401 dönüyor | Token eksik, geçersiz veya süresi dolmuş olabilir. Identity yeniden başladıysa geliştirme anahtarı değişmiş olabilir; tekrar giriş yap. |
| Korumalı endpoint 403 dönüyor | Kullanıcının ilgili işlem yetkisi/veri kapsamı uygun olmayabilir. |

## Opsiyonel e-posta servisi

AdNoir.Notification.Api http://localhost:5100 adresinde çalışır. Mevcut servisleri çalıştırmak için bu projeyi başlatmak zorunda değilsin; mail gönderimi varsayılan kapalıdır. Etkinleştirmeden önce DbMigrator'ı yeniden çalıştırıp index ve notification:read izinlerini güncelle. SMTP, RabbitMQ, tenant ayarları ve gönderim geçmişi için [bildirim rehberine](notifications.md) bak.

## gRPC servis kimliği kurulumu

Sample ve Notification servis kimliği olmadan başlamaz. Repo kökünde
`./scripts/development/initialize-grpc-credentials.ps1` çalıştırıp Identity, Sample ve
Notification'ı yeniden başlat. Betik mevcut gRPC ayarlarını değiştirmez.
Ayrıntılar ve üretim farkları: [gRPC güvenliği](grpc-security.md).
