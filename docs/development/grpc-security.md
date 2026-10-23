# gRPC servis kimliği ve tenant erişimi

Identity izin matrisi uç noktası kullanıcı JWT'sinden ayrı servis kimliği ister. Sample ve
Notification ayrı rastgele anahtar kullanır. Kimliği bilinen servis ayrıca izin matrisi okuma
yetkisine ve istenen tenant için erişime sahip olmalıdır. Denetim repository çağrısından önce
yapılır; kullanıcı JWT'si tek başına bu yetkiyi vermez.

## İlk yerel kurulum

Repo kökünde PowerShell 7 ile bir defa çalıştır:

```powershell
./scripts/development/initialize-grpc-credentials.ps1
```

Betik 32 baytlık iki rastgele anahtar üretip ignored `.env` içine istemci/sunucu eşleşmelerini
ekler. Anahtarları ekrana yazmaz. Mevcut gRPC ayarı varsa değiştirmeden durur. Ardından
Identity, Sample ve Notification'ı yeniden başlat. Gateway için servis anahtarı gerekmez.
Yerel betik bu iki ortak servise açıkça `AllTenants=true` verir.

Sample/Notification kimliği eksikse başlangıç başarısızdır. Identity Development'ta boş
izin listesiyle başlayabilir fakat bütün gRPC çağrılarını reddeder. Diğer ortamlarda boş
liste başlangıç hatasıdır. Bu, sessizce anonim erişime dönüşmez.

## Ayarlar

İstemci: `AdNoir:Identity:ServiceCredentials:sample` veya `:notification` altında `ServiceId`
ve `Key`. Sunucu: `AdNoir:GrpcSecurity:Callers:0` (sonraki kayıtlar 1, 2...) altında:

| Alan | Anlamı |
|---|---|
| `Credential:ServiceId` | İstemcideki kimlikle birebir eşleşir |
| `Credential:Key` | İstemcideki Base64 anahtarla birebir eşleşir |
| `CanReadPermissionMatrix` | Varsayılan false; kimlik tek başına izin vermez |
| `CanValidateUserSession` | Varsayılan false; canlı kullanıcı oturumu doğrulama RPC izni |
| `AllTenants` | Varsayılan false; ortak servis için bilinçli geniş yetki |
| `TenantIds:0`, `TenantIds:1` | AllTenants=false olduğunda izin verilen tenant GUID'leri |

AllTenants ve TenantIds birlikte yapılandırılamaz. TenantIds boş ve AllTenants=false ise
hiçbir tenant'a erişilemez. Bilinmeyen servis/yanlış anahtar gRPC Unauthenticated (16),
yetkisiz servis/tenant PermissionDenied (7) döner. Yinelenen kimlik header'ı reddedilir.
Geçerli servis yetkili olduğu tenant'ın bütün rol matrisini okuyabilir; bu yetki son kullanıcı
rolü değildir. AllTenants kullanan servis ele geçirilirse bütün tenant matrisleri risk altındadır.

## Üretim ve anahtar yenileme

- İstemci HTTPS kullanır. Sunucu HTTPS olmayan çağrıyı reddeder; yalnız Development ve
  loopback istemci adresinde HTTP istisnası vardır. Sertifika doğrulaması kapatılmaz.
- TLS doğrudan Identity'de sonlanmalıdır. Güvenilir forwarded-header/proxy TLS sonlandırması
  bu dilimde kurulmadı; kendiliğinden X-Forwarded-Proto başlığına güvenilmez.
- Anahtarlar üretimde secret manager/ortam değişkenleriyle servis başına verilmeli. Ortak
  yerel `.env` düzenini dağıtımlarda tüm servislerle paylaşma. Header'ları proxy loglarına alma.
- Yenileme için yeni ServiceId ve anahtarla ikinci sunucu kaydı ekle; aynı tenant/izin sınırlarını
  koru, Identity'yi yeniden başlat. İstemciyi yeni kimliğe geçir ve yeniden başlat. Doğruladıktan
  sonra eski kaydı kaldırıp Identity'yi yeniden başlat. Süreli token/mTLS ve otomatik yenileme yok.
- İzin matrisi cache'leri anahtar iptal edildiğinde anında boşalmaz; mevcut cache süresi boyunca
  kullanılabilir. Acil iptalde ilgili istemcileri durdurma/cache temizleme operasyonu gerekir.

## Doğrulama

Sample ve Notification kayıtlarında CanValidateUserSession=true de gerekir.
Mevcut kurulumda bu alanı ilgili sunucu kayıtlarına ekleyip Identity'yi yeniden başlat.
Yeni kurulum betiği alanı üretir. ValidateUserSession aynı tenant izin listesini kullanır;
yanıtı cache'lenmez. [Oturum davranışı](../architecture/security.md).

```powershell
dotnet build AdNoir.sln --no-restore --artifacts-path artifacts/checks --verbosity quiet
./tests/integration/grpc-security.ps1
./tests/integration/grpc-security.ps1 -SessionChecks
```

Gerçek yerel Mongo `rs0`, mongosh, PowerShell 7.4+ ve boş 16170/16180/16182/16190/16200
portları gerekir. Betik ayrı veritabanı ve kendi süreçlerini açıp sonunda temizler. 17 kontrol
login/setup dahil sayıdır: HTTP/2 kimlik/tenant reddi, yetkili çağrılar, eksik istemci anahtarıyla
başlangıç hatası ve iki gerçek servisin boş cache üzerinden başarılı yetkilendirmesi.
Üretim TLS/secret dağıtımı ve canlı ağ erişimi ayrıca doğrulanmalıdır; unit test kullanılmaz.

Development ortamında Visual Studio proje dizininden başlatsa da varsayılan .env dosyası content root içinde yoksa en yakın üst AdNoir.sln dosyasının yanından yüklenir. Projeye özel .env önceliklidir; dosyalar birleştirilmez. Ayar değişikliklerinden sonra süreçleri yeniden başlatın.
