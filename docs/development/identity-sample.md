# Yerel Identity ve Sample ayarları

Identity Development Kestrel ayarları REST/JWKS için http://localhost:5080, gRPC için yalnız HTTP/2 kullanan http://localhost:5082 adresini tanımlar. Bu ayarlar launchSettings içindeki URL'lerden önceliklidir. HTTPS geliştirme yapılacaksa Kestrel endpoint'leri sertifika ile açıkça düzenlenmelidir.

Sample Development ayarları bu adresleri kullanır. Ortam değişkeni veya .env karşılıkları:

- AdNoir__Identity__GrpcAddress=http://localhost:5082
- AdNoir__Jwt__Issuer=http://localhost:5080
- AdNoir__Jwt__Audience=adnoir
- AdNoir__Jwt__JwksUri=http://localhost:5080/.well-known/jwks.json

Development dışındaki ortamlarda gRPC ve JWKS HTTPS olmalıdır. Issuer ve audience boş bırakılamaz. .env Git'e eklenmez.

Sample, cache boşken Identity'den tam yetki matrisini ister. Kiracı veya sürüm uyuşmazsa erişim verilmez. Rol değişikliği nedeniyle token sürümü eski kaldıysa yeni token gerekir. Cache L1 ömrü 30 saniye, L2 ömrü 5 dakikadır; izin matrisi cache isabeti canlı oturum kontrolünü atlatmaz. Her korumalı istekte stamp/aktiflik/parola zorunluluğu ayrıca Identity üzerinden doğrulanır. Ayrıntı: [oturum güvenliği](../architecture/security.md).

Sample ve Notification, Identity'nin gRPC uçlarına ayrı bir servis kimliğiyle bağlanır; kullanıcı JWT'si bu yetkiyi vermez. Kimlik yapılandırılmadan bu servisler başlamaz. Kurulum ve üretim farkları için [gRPC güvenliği](grpc-security.md).
