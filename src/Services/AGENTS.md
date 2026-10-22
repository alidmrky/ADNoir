# Servis geliştirme kuralları

Kök AGENTS.md geçerlidir. Her yeni public tip ayrı, tipiyle aynı adlı dosyada bulunur.
DTO, servis arayüzü, servis implementasyonu ve validator aynı dosyada birleştirilmez.
Metotlar sorumluluğa göre sınıflanır; her metot için dosya açılmaz. Farklı iş akışlarını
tek büyük servise yığma; partial ile sorumluluk sorununu gizleme.

- Domain: entity/value object/saf kural/repository arayüzü; Mongo/BSON/HTTP yok.
- Application: kullanım senaryosu, servis implementasyonu/arayüzü, Dtos, Validators.
- Infrastructure: repository implementasyonu, BSON map, index, transport, worker, consumer.
- API: endpoint/protokol sınırı/composition root. Program.cs'ye iş mantığı veya tip yığma.
- Katman DI kayıtları ayrı DependencyInjection dosyalarındadır.
- Önce Shared içinde mevcut yeteneği ara ve kullan. Servise özel ikinci response,
  repository tabanı, tenant middleware veya güvenlik politikası yazma.
- Servise özel DTO/iş kuralı Shared'a taşınmaz; başka servisin Infrastructure'ı referanslanmaz.
- JWT → tenant → canlı oturum → izin kontrol sırası korunur. Yeni servis gerçek
  IUserSessionValidator kaydı olmadan güvenli kabul edilmez; anonim fallback yok.
- Yeni assembly mimari test envanterine eklenir; servis rehberi ve ayarlar güncellenir.

## Veri erişimi inceleme kapısı

- Standart CRUD için Application'da `IRepository<TEntity>` veya onu genişleten Domain
  repository sözleşmesini enjekte et. Servise özel ikinci generic repository yazma.
- Özel sorgu arayüzü yalnız özel ihtiyacı içerir. Aggregation gerekmesi basit liste/detay
  okumalarını da ham koleksiyona taşımak için gerekçe değildir.
- Infrastructure özel sorguları `MongoRepository<TEntity>` tabanının scope filtresi ve
  session accessor'ını kullanır. Güncellemede ortak `UpdateAsync`/`UpdateWithScopeAsync`
  kullanılabiliyorsa elle Version filtresi/artırımı yazılmaz.
- Yeni servis tesliminde kullanılan Shared tip/metotlarını, özel sorgunun gerekçesini,
  tenant A/B, eksik tenant, dar kapsam ve concurrency doğrulamasını raporla.
- Notification'daki örnek: `EmailQueryService` → `IRepository<EmailDelivery>`;
  `IEmailQueries` yalnız istatistik; `MongoEmailRepository` ve kuyruk store'u ortak tabanı kullanır.

## Otomatik mimari kapı

src altındaki yeni csproj, mimari test referanslarına ve AdNoirAssemblies envanterine eklenir;
Application katmanı ayrıca ApplicationLayers'a girer. Eksik/fazla/tekrar kayıt test hatasıdır.
Servislerin repository dışı Mongo CRUD/aggregation çağrıları IL kuralıyla reddedilir;
Store/Query adı istisna değildir. İstisna için ADR ve dar kapsamlı denetim değişikliği gerekir.
Repository türevinde filtre/session doğruluğu ayrıca gerçek Mongo kontrolleriyle kanıtlanır;
IL kuralının geçmesi bu garantilerin yerine geçmez.
