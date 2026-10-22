# Ortak bileşen kataloğu

Yeni koddan önce src/Shared içinde aynı yeteneğin bulunup bulunmadığını ara.
Serviste ikinci repository tabanı, response zarfı, tenant çözümleyici veya exception
handler yazma. Ortak bileşeni kullan; eksik yetenek gerçekten genel ise doğru ortak
projeyi genişlet. İş modülüne ait özel kuralı Shared'a taşıma.

| Proje | Amaç | Kullanım |
|---|---|---|
| AdNoir.Shared.Contracts | Saf paylaşılabilir sözleşmeler | BaseResponse, PagedResponse, MessageCodes, PermissionCatalog, integration event, proto |
| AdNoir.Domain.Abstractions | Saf domain/veri erişimi arayüzleri | Entity tabanları, IMultiTenant, IHasVersion, IRepository, IUnitOfWork, DataScope |
| AdNoir.BuildingBlocks | Veritabanından bağımsız uygulama mekanizmaları | IApplicationService, mevcut kullanıcı/tenant bağlamı, doğrulama, izin kontrolü, canlı oturum middleware'i, mesaj çözümleme, hata eşleme, telemetry |
| AdNoir.BuildingBlocks.Mongo | MongoDB implementasyonları | MongoRepository, session/UoW, BSON conventions, index soyutlamaları, audit/persistence mekanizmaları |

## Sık ihtiyaçlar

### Entity tabanı seçimi

| Taban | Sağladığı yapı | Örnek ihtiyaç |
|---|---|---|
| Entity | Kimlik | En küçük kalıcı nesne |
| AuditedEntity | Oluşturma/değiştirme alanları | Kimin ne zaman oluşturduğunu tutmak |
| FullAuditedEntity | Audit alanları + soft-delete + Version | Tenant'a bağlı olmayan sürümlü kayıt |
| TenantEntity | Kimlik + TenantId | Basit tenant kaydı |
| FullAuditedTenantEntity | Tenant + audit alanları + soft-delete + Version | Çoğu tenant iş entity'si |
| HierarchicalTenantEntity | Önceki yapı + hiyerarşi alanları | Organizasyon ağacı benzeri model |

Audit alanı taşıması ile iş değişikliği geçmişine audit kaydı yazılması farklıdır. IAuditable
opt-in'i ve maskeleme politikasını ayrıca değerlendir; altyapı bütün iş akışlarında otomatik
audit üretildiğini kanıtlamaz. IOrgUnitScoped birim kapsamını, IHasVersion optimistic
concurrency'yi belirtir. Yalnız Version alanı eklemek yeterli değildir; update filtresi kullanmalıdır.

### Uygulama servisi ile repository'yi birlikte kullanmak

IApplicationService bir marker/sözleşme kuralıdır; bütün CRUD'u sağlayan sihirli bir base
servis değildir. Kullanım senaryosu arayüzünü bundan türet; somut servis ilgili Application
konu klasöründe repository arayüzünü ve gerekiyorsa IUnitOfWork'ü enjekte eder.

Örnek okuma sırası: FindByIdAsync(id, cancellationToken) → bulunamadıysa NotFound →
bulunduysa entity'den DTO üret → BaseResponse<Dto>.Success(dto). Servis CorrelationId,
TraceId ve çeviri metni doldurmaz; API ToHttpResult ve ortak sınır bileşenleri bunu yapar.

Örnek yazma sırası: iş kuralı/ExpectedVersion kontrolü → UoW BeginAsync → repository
UpdateAsync → kritik event varsa PublishAsync → CommitAsync → DTO yanıtı. Hata yükselirse
transaction davranışını UoW sağlar; her servis için yeni transaction yöneticisi yazılmaz.

IRepository.ListPagedAsync, PagedResult döndürür; Application bunu DTO listesine çevirip
PagedResponse üretir. Sayfalama için kararlı sıralama seç. Repository entity döndürebilir;
HTTP Application servis sözleşmesi entity döndürmez.

- Liste yanıtı: BaseResponse içine metadata sayfalaması ekleme; PagedResponse kullan.
- Beklenen iş sonucu: NotFound/Forbidden/Conflict gibi BaseResponse sonuçları. Beklenmeyen
  hata global handler'a yükselir; her metotta try/catch oluşturma.
- Kullanıcı mesajı: önce MessageCodes, sonra en/tr katalogları; endpoint/servise metin gömme.
- Veri erişimi: domain repository arayüzünü Infrastructure'da MongoRepository üzerinden
  uygula. Tenant filtresi ve Version kontrolünü elle kopyalama.
- Transaction + event: IUnitOfWork ve IIntegrationEventPublisher; gerçek session paylaşımı
  [mesajlaşma rehberindeki](messaging.md) kurala tabidir.
- Yetkilendirme: RequirePermission, ICurrentUser, ITenantContext; header'dan tenant alıp
  güvenme. UserSessionMiddleware'i ve gerçek IUserSessionValidator kaydını kur.
- Yeni dış sistem: servise özgü adaptörü Infrastructure'a koy; SMTP gibi sürücüleri
  bütün Application projelerine yayma.

## Bir şeyin ortak olup olmadığı

En az iki kullanımın aynı anlamı taşıdığını göster. Sadece aynı alan isimleri yeterli
değildir: OrderDto ile EmailDto aynı base entity üzerinden tek DTO'ya dönüşmez.
Ortak mekanizma servis adı, iş enum'u veya servisin koleksiyon adına bağımlıysa sınırı yeniden değerlendir.

Mevcut gRPC istemcileri servis bazında proto'dan üretilir; transport bağlantısı Infrastructure'dadır.
Oturum kararları ve HTTP ret davranışı ortak bileşendedir. Benzer adaptörlerin varlığı
yeni serviste güvenlik politikasını kopyalayıp değiştirmek için gerekçe değildir.

Kaynaklar: [Shared](../../src/Shared/AGENTS.md), [BuildingBlocks](../../src/Shared/AdNoir.BuildingBlocks/AGENTS.md).

## Repository seçimi: önce mevcut mekanizma

| İhtiyaç | Kullanılacak yol | Servisin eklediği sorumluluk |
|---|---|---|
| ID ile okuma | IRepository.FindByIdAsync | Yetki kontrolü, DTO eşlemesi |
| Toplam gerektirmeyen sınırlı liste | IRepository.ListLimitedAsync | Pozitif limit, predicate ve sıralama |
| Toplamlı sayfa | IRepository.ListPagedAsync | Sayfa ve toplam |
| Ekleme/güncelleme/silme | IRepository standart metotları | İş kuralı, transaction ve event akışı |
| Sunucu taraflı istatistik | MongoRepository türevinde aggregation | İşe özgü pipeline; ortak scope filtresi + session zorunlu |
| Worker lease/CAS | MongoRepository türevinde özel iş metodu | İş durumu geçişi; ortak UpdateWithScopeAsync |

`I...Queries` veritabanından bağımsız özel okuma sözleşmesi olabilir; generic CRUD'un
kopyası değildir. Basit okuma için DTO ihtiyacı yeni bir veri erişim yolu gerektirmez:
entity ortak repository'den gelir, Application DTO üretir. Aggregation sonucu olan
servise özel okuma DTO'su ise Application sözleşmesinde kalabilir.

Notification somut örneği: EmailQueryService liste/detay için IRepository<EmailDelivery>,
istatistik için IEmailQueries enjekte eder. İkisi aynı scoped MongoEmailRepository'ye
bağlanır. MongoEmailStore ayrı kuyruk sorumluluğudur; o da ortak MongoRepository'den türer.
Store kendi tenant veya Version filtresini üretmez. Worker kullanıcı oturumu taşımadığı
için çözülmüş tenant içinde açık Tenant kapsamı kullanır; HTTP sorguları bu ayrıcalığı almaz.

Liste endpoint'i son 100 sonucu ListLimitedAsync ile toplam saymadan getirir.
Toplamlı sayfalama ListPagedAsync/PagedResponse kullanır. İki yol ortak sıralama üretir;
eşit anahtarlar Id ile kararlılaştırılır. Snapshot/keyset garantisi verilmez.

Ham koleksiyon kullanımı için 'performans' demek yeterli değildir. Generic mekanizmanın
neden yetmediği, scope/session/Version garantilerinin nasıl korunduğu ve ölçüm yazılmalıdır.
Index sağlayıcıları ve index hazırlık kontrolleri veri CRUD'u değildir; kendi altyapı
sorumluluklarında kalır. Bağlayıcı kaynak [kök kural 14](../../AGENTS.md) ve
[servis kurallarıdır](../../src/Services/AGENTS.md).
