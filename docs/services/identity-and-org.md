# IdentityAndOrg

Tenant ve organizasyon ağacı, kullanıcı, rol, login/refresh/logout/parola değişimi bu
servisin sorumluluğudur. Bir iş servisi hesap tablosunu ve token üretimini tekrar yazmaz.

API HTTP endpoint'lerini ve iç gRPC sınırını taşır. Application kullanım senaryolarını ve
DTO'ları tutar. Domain kullanıcı/rol/tenant/ağaç kuralları ile repository arayüzlerini
tanımlar. Infrastructure Mongo, parola hash'i, JWT imzası, seed ve mesajlaşmayı uygular.

İş servisleri public JWKS ile JWT imzasını doğrular; servis credential'ıyla izin matrisi ve
canlı kullanıcı oturumunu sorgular. İzin matrisi son kullanıcıya doğrudan açılmaz.
Global sistem rolleri okunabilir; tenant'a ait özel roller diğer tenant'tan okunamaz.
TenantAdmin, SuperAdmin atayamaz; sahip olunmayan izin veya daha geniş kapsam devredilemez.

İlk kullanım: Mongo replica set → DbMigrator → Identity → Gateway/iş servisleri.
[Kurulum](../development/first-run.md), [güvenlik](../architecture/security.md),
[servis kuralları](../../src/Services/IdentityAndOrg/AGENTS.md).

## Veri erişimi ve istisnalar

Tenant, OrgUnit, User, Role ve RefreshToken repository'leri ortak
MongoRepository tabanını kullanır.

| Özel işlem | Neden genel CRUD yetmiyor? | Korunan sınır |
|---|---|---|
| Tenant sorguları | Platform kaydı başka tenant'a ait değildir | Ortak soft-delete; API platform yetkisi |
| Role sorguları | Nullable TenantId ile global sistem rolleri birlikte görünür | Tenant özel rolü veya global sistem rolü; ortak soft-delete/session |
| User authentication okumaları | Login sırasında kullanıcı kapsamı henüz yok | Ortak Tenant scope; normal kullanıcı CRUD'u dar kapsamı korur |
| OrgUnit toplu path/ancestor güncellemesi | Birden çok düğümün farklı değerleri tek transaction'da yazılır | Ortak tenant/soft-delete/yazma kapsamı + her düğümde ExpectedVersion; aktif transaction |
| Refresh hash araması | Opaque token sahibinin tenant'ı ilk okumadan önce bilinmez | IUnsafeCrossTenantQuery + gerekçe attribute'u; unique hash; iptal edilmiş kayıt da görünür |
| Refresh toplu iptal | Aile/kullanıcı token'ları koşullu ve idempotent UpdateMany ile iptal edilir | Ortak Tenant filtresi ve session; RevokedAtUtc null koşulu |
| OrgUnitSubtreeResolver | Repository scope hesaplaması resolver'a bağımlı; ters enjeksiyon döngüdür | Açık tenant/soft-delete filtresi, yalnız ID projection, ortak session |

RefreshToken IHasVersion uygulamaz; bulk iptal koşulu RevokedAtUtc üzerindedir.
Resolver ortak transaction session’ını kullanır.
