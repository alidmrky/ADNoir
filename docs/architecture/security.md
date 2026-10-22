# Kimlik, yetki, tenant ve oturum

JWT imzası/issuer/audience/lifetime doğrulaması kimliği doğrular. Tenant çözümleme imzalı
tenant_id claim'ini kullanır. Canlı oturum denetimi tenant aktifliği, kullanıcı aktifliği/kilidi,
security stamp ve permission version'ı kontrol eder. Ardından rol–izin matrisi işlemin
yapılabilirliğini, repository ise görülebilir veri kapsamını belirler.

## İstek işleme sırası

Authentication → TenantResolution → UserSessionMiddleware → Authorization → endpoint.

Identity veritabanına doğrudan bakar. Sample ve Notification, servis kimliğiyle korunan
ValidateUserSession gRPC çağrısını kullanır. İzin matrisi cache'lenebilir; canlı oturum
sonucu cache'lenmez. Bu nedenle dolu izin cache'i eski token'ın geçmesine yetmez.
Kimlik doğrulanamayan/yetki verilmeyen iç çağrı veya Identity kesintisi erişim açmaz.

| Durum | Sonuç |
|---|---|
| Stamp veya izin sürümü eski, hesap/tenant geçersiz | 401; yeniden login veya uygun refresh gerekir |
| Parola değişimi zorunlu | İş endpoint'leri 403 identity.password.change_required |
| Güncel oturum + parola değiştirme endpoint'i | Zorunluluk varken de parola değiştirilebilir |
| Identity iç çağrısı başarısız | 502; izin kontrolü başarısız olarak kapanır |
| Normal kullanıcıda ilgili izin yok | 403 |

Login/register/refresh/logout anonim sözleşmelerini korur. Refresh yeni claim'ler üretebilir
ancak MustChangePassword durumunu aşamaz. Logout refresh token ailesini iptal eder; tek
bir access token için jti denylist yoktur. Parola değişimi bütün refresh oturumlarını iptal
eder ve stamp döndürür. Rol değişimi yeni claim'lerle refresh'e izin verir; eski access
token canlı kontrolü geçmez.

## Maliyet ve sınır

Her korumalı uzak servis isteğinde ek bir gRPC çağrısı ve Identity okuması vardır.
Kapasite planlamasında bu çağrının gecikmesi ve Identity yükü hesaba katılmalıdır. Kontrol ile iş yazımı arasında eşzamanlı yetki değişimi gerçekleşebilir;
bütün servisleri kapsayan atomik iptal garantisi verilmez.

Servis kimlikleri için [gRPC rehberi](../development/grpc-security.md). İç RPC'de
CanValidateUserSession ve tenant izin listesi gerekir. JWT, servis credential'ı yerine geçmez.
