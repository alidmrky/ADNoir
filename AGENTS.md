# AdNoir — Kök Kurallar

## Güncel geliştirme düzeni

- Yeni public tipler ayrı dosyalardadır; DTO/arayüz/implementasyon/validator birleştirilmez.
  Sınıflar sorumluluğa göre ayrılır; her metot için ayrı dosya oluşturulması gerekmez.
- Önce Shared'daki mevcut mekanizmayı kullan; response, repository, tenant ve güvenlik
  altyapısını servis içinde tekrar yazma. Servise özel iş mantığını Shared'a taşıma.
- Konu kuralları src/Services, src/Shared, src/Gateway ve src/Tools altındaki AGENTS.md
  dosyalarındadır. Dokümantasyon girişi docs/README.md; kurallar ilgili AGENTS.md içinde tutulur.
- Bu dosya backend içindir. Frontend'in kuralları ui/AGENTS.md içindedir ve buradan miras
  almaz; aşağıdaki katman, response, mesajlaşma ve veri bölümleri ui dizinine uygulanmaz.
- Korumalı servis istekleri canlı kullanıcı oturumu doğrulamasından geçer;
  permission cache'i stamp/parola kontrolünü atlatamaz. Anonymous yalnız açık uçlara atanır.

Bu dosya bağlayıcı geliştirme kurallarını tutar. Projenin güncel mimarisi ve kullanım
rehberleri docs/README.md üzerinden erişilebilir.

---

## 1. Dil

- Kod, tip adları, kod yorumları, commit mesajları: **İngilizce**
- `AGENTS.md`, `docs/` altındaki her şey: **Türkçe**

## 2. Geliştirme disiplini

- Kullanıcının belirlediği kapsam içinde çalış; yeni ürün aşamasını kendiliğinden başlatma.
- İşe başlarken docs/README.md ve ilgili mimari/servis rehberlerini oku.
- Davranış veya kurulum değişirse ilgili güncel rehberi aynı değişiklikte güncelle.
- docs altında yalnız proje ve geliştirme bilgileri tutulur; faz, audit değerlendirmesi,
  ilerleme veya oturum tarihçesi oluşturulmaz.
- Değişikliğe uygun kontrolleri çalıştır; sonuç ve sınırlarını teslim açıklamasında belirt.
- Definition of Done: dotnet build uyarısız geçer, gerekli kontroller başarılıdır;
  hiçbir dosyada // TODO veya kısaltılmış gövde bırakılmaz.

## 3. Katman bağımlılıkları (arch testleri zorlar)

| Proje | Referans verebileceği |
|---|---|
| `AdNoir.Shared.Contracts` | **hiçbiri** |
| `AdNoir.Domain.Abstractions` | **hiçbiri** (saf BCL) |
| `AdNoir.BuildingBlocks` | Shared.Contracts, Domain.Abstractions |
| `AdNoir.BuildingBlocks.Mongo` | BuildingBlocks |
| `{Servis}.Domain` | Domain.Abstractions |
| `{Servis}.Application` | Domain, Shared.Contracts, BuildingBlocks |
| `{Servis}.Infrastructure` | Application, Domain, BuildingBlocks.Mongo |
| `{Servis}.Api` | Infrastructure, Application, BuildingBlocks |

- **`BuildingBlocks` MongoDB'ye bağımlı olamaz.** "Application veritabanından bağımsızdır"
  kuralı makine tarafından ancak böyle zorlanabilir.
- `Domain` MongoDB'yi bilmez; `[Bson*]` attribute'ları `Domain`'de kullanılmaz, eşleme
  `Infrastructure`'daki `BsonClassMap` ile yapılır.
- Bir şeyi "şimdilik BuildingBlocks'a koyayım" refleksi geldiğinde: **testi gevşetme, doğru
  projeye koy.**

## 4. Response sözleşmesi

- `IApplicationService` implemente eden **her** public metot `Task<BaseResponse>` veya
  `Task<BaseResponse<T>>` döner, async'tir, son parametresi `CancellationToken`'dır.
- **ProblemDetails kullanılmaz.** 404 ve 500 dahil her yanıt `BaseResponse` zarfıyla döner.
- Servis metotları **domain entity döndürmez**, daima DTO döner.
- **Beklenen sonuçlar `BaseResponse` ile döner, beklenmeyen hatalar exception olarak yükselir.**
  "Kullanıcı bulunamadı" beklenen bir sonuçtur → `NotFound(...)` döndürülür, exception
  fırlatılmaz. Servis metotlarına try/catch serpiştirilmez; tek global handler vardır.
- `CorrelationId`, `TraceId` ve `Message` alanlarını **servis kodu doldurmaz**; sınırda
  merkezî olarak doldurulur.
- `Metadata` sözlüğüne tipli karşılığı olan hiçbir şey konmaz (sayfalama `PagedResponse`'a
  aittir).

## 5. Kullanıcıya gösterilen metin

- **Hiçbir kullanıcı metni koda yazılmaz.** Sıra: önce `MessageCodes`'a sabit, sonra
  **desteklenen her kültür** dosyasına karşılık (`messages.en.json`, `messages.tr.json`),
  ancak ondan sonra kodda kullanım.
- Kod formatı hiyerarşik: `{modül}.{varlık}.{durum}` — `identity.user.not_found`.
  Sayısal kod kullanılmaz.
- Yer tutucular **isimlidir** (`{userId}`), sıra numarası kullanılmaz. Aynı kodun her
  kültürdeki metni **aynı yer tutucu setini** kullanmalıdır.
- FluentValidation: `WithErrorCode(MessageCodes...)` kullanılır, `WithMessage` ile düz metin
  **yazılmaz**.
- Aşırıya kaçma: neredeyse aynı 800 giriş, 80 iyi girişten kötüdür. Yeni kod ancak kullanıcı
  farklı bir eylem alacaksa açılır.

## 6. Exception kuralları

- Guard clause'larda standart tipler: `ArgumentNullException.ThrowIfNull(...)`,
  `ArgumentException.ThrowIfNullOrWhiteSpace(...)`,
  `ArgumentOutOfRangeException.ThrowIfNegativeOrZero(...)`.
- **`NullReferenceException`, `IndexOutOfRangeException`, `AccessViolationException`,
  `StackOverflowException`, `OutOfMemoryException` asla bilinçli olarak fırlatılmaz.**
  Bunlar runtime'a aittir; fırlatılırsa gerçek bug ile kasıtlı sinyal ayırt edilemez hale
  gelir. IL tabanlı arch testi bunu tarar.
- İş kuralları için `AdNoirException` hiyerarşisi kullanılır. Bu tipler `ResponseStatus`
  **taşımaz**; tip→durum eşlemesi `ExceptionResponseMapper` içindedir (tek yer).

## 7. Çok kiracılık ve kapsam

- Tenant filtresini unutmak imkânsız olmalıdır: `IRepository<T>` filtreyi otomatik ekler.
- Kiracı sınırını aşmak yalnızca `IUnsafeCrossTenantQuery<T>` ile mümkündür ve çağıran her
  metot **`[AllowsCrossTenantAccess("gerekçe")]`** taşımak zorundadır. IL testi attribute'suz
  kullanımı reddeder.
- `HierarchyPath` üzerinde regex kullanılacaksa **anchored (`^`) ve case-sensitive** olacak;
  `i` opsiyonu index kullanımını bozar.
- Alt ağaç sorguları `Ancestors` alanıyla yapılır, `HierarchyPath` prefix'iyle değil
  (`/bayi-1` ile `/bayi-10` karışır).
- Bir düğümün **yeniden adlandırılması** da alt ağaç güncellemesi tetikler — taşıma kadar
  kolay gözden kaçar, testi zorunludur.

## 8. Mesajlaşma sınırı

- **`Application` katmanı MassTransit'i görmez**; yalnızca `IIntegrationEventPublisher`
  enjekte eder. `Infrastructure` MassTransit'i **doğrudan** kullanır (`IConsumer<T>`, retry
  filtreleri, test harness). **MassTransit'i baştan sarmalamak yasaktır.**
- `IEventBus` adı kullanılmaz; `IADEventBus` gibi kısaltmalı önekler kullanılmaz.
- **MassTransit v8.x kullanılır (8.5.10 pinli).** İnternetteki örnekler v9'a ait olabilir;
  sürüm uyumu doğrulanmadan kod yazılmaz. Emin olunmayan yapılandırma **uydurulmaz, sorulur.**

### 8.1 — Outbox session paylaşımı (en sinsi hata modu)

MassTransit'in outbox'ı ile repository'ler **aynı `IClientSessionHandle`**'ı paylaşmak
zorundadır. Session **biz açmayız, MassTransit'ten alırız** (`MongoDbContext.StartSession()`).
Kendi `IMongoClient.StartSession()` çağrımızla açılan session **outbox'ı kapsamaz**: veri ve
mesaj ayrı transaction'lara düşer, atomiklik kaybolur ve **bu hiçbir hata vermeden sessizce
olur.** `IPublishEndpoint.Publish` çağrısı **`CommitTransactionAsync`'ten önce** yapılmalıdır.

Outbox kullanılan servislerde MassTransit session accessor kullanılır. Bağımsız
session yalnız outbox gerektirmeyen araç/işlemler içindir; atomiklik rollback ile doğrulanır.

- Teslim garantisi **at-least-once**'tır: **her consumer idempotent yazılır** ve her yeni
  consumer için idempotency testi eklenir. Yalnızca `InboxState` deduplication'ına güvenilmez.
- Kuyruk/exchange adlandırması açıkça yapılandırılır, MassTransit'in tip-adı tabanlı
  varsayılanına bırakılmaz (event tipini yeniden adlandırmak üretimde kuyruk değiştirir).

## 9. Altyapı ve ortam

- Altyapı kayıtları **yapılandırmaya göre koşulludur** ve bu dallanma tek bir yerdedir
  (`InfrastructureMode`). Servis kodu hangi modda çalıştığını bilmez.
- **`Production` ortamında Redis veya RabbitMQ bağlantı dizesi eksikse uygulama başlangıçta
  hata verip durur.** Asla sessizce yedek moda düşmez. Bu davranış testle sabitlenmiştir,
  gevşetilemez.
- **Bu makinede Docker kurulu değildir ve kurulmayacaktır.** Testcontainers gerektiren
  testler `[Trait("RequiresDocker","true")]` ile işaretlenir; yerelde filtrelenir, CI'da
  filtresiz koşar.
- Ayarlar `.env` üzerinden gelir; Docker olmadığı için `DotEnvConfigurationProvider`
  yalnızca `Development`'ta kaydedilir. **`.env` asla commit edilmez**, `.env.example` edilir.

## 10. Veri kuralları

- Entity Id: `Guid`, üretimi `Guid.CreateVersion7()`. MongoDB'de **binary**, string değil.
- Tek `ConventionPack`: camelCase element adları, enum'lar string, `DateTimeKind.Utc`,
  extra element'leri yok say, null yazma, `GuidRepresentation.Standard`.
- **Optimistic concurrency:** `IHasVersion` implemente eden entity'lerde `Version`
  **filtreye dahil edilir** ve aynı işlemde artırılır. Sadece alanı taşımak yeterli değildir;
  filtreye konmazsa alan dekoratiftir.
- Index'ler startup'ta değil, `AdNoir.DbMigrator` içinde oluşturulur.
- Audit trail **opt-in**'dir (`IAuditable`) ve hassas alanlar için maskeleme listesi zorunludur.

## 11. Test kuralları

- Saf domain kuralları (hiyerarşi taşıma, döngü koruması) **unit test** ile, Mongo
  gerektirmeden sınanır. Her şeyi integration test yapmak yavaşlatır.
- Bir arch kuralı yazıldığında, kuralın **gerçekten bir ihlali yakaladığı** da test edilir
  (`tests/AdNoir.ArchitectureTests/Samples/RuleProbes.cs` kalıbı). Hiç kırmızı olduğu
  görülmemiş bir kural, çalışmayan bir kuraldan ayırt edilemez.
- MassTransit testlerinde kütüphanenin **kendi test harness'ı** kullanılır, elle mock yazılmaz.

## 12. DI kayıt kuralı

Framework'ün kendi varsayılanını sağladığı bir servisi geçersiz kılarken **`TryAdd`
kullanılmaz, `Replace` kullanılır.** `AddAuthorization()` gibi çağrılar varsayılanlarını
zaten `TryAdd` ile kaydeder; sonrasında yapılan bir `TryAdd` **sessizce hiçbir şey yapmaz**.
Yanlış kayıt, permission policy çözümlemesini ve 401/403 yanıt zarfını bozabilir.

Kendi tipimiz için varsayılan sağlıyorsak (servisin kendi implementasyonuyla değiştirebileceği
bir taban) `TryAdd` doğrudur.

## 13. Paketler

- Tüm sürümler `Directory.Packages.props`'tadır (Central Package Management). Proje
  dosyalarına `Version` yazılmaz.
- Yeni paket eklenmeden önce **net9.0 uyumu ve tam sürümü doğrulanır**; tahmin edilmez.

## 14. Ortak veri erişimini kullanma zorunluluğu

- Standart entity okuma/yazma için `IRepository<TEntity>` kullanılır. DTO döndürmek,
  ayrı bir `I...Queries` arayüzü açmak veya worker yazmak ortak altyapıyı atlama gerekçesi değildir.
- Mongo'ya özel aggregation/claim/bulk işlemleri `MongoRepository<TEntity>` türevi içinde,
  `Collection`, `BuildScopeFilterAsync` ve `SessionAccessor` kullanılarak uygulanır.
  Tenant/soft-delete/Version filtreleri servis içinde yeniden yazılmaz.
- Özel sorgu sözleşmesi yalnız generic repository'nin ifade edemediği ihtiyacı taşır;
  `FindByIdAsync`, `ListPagedAsync`, `InsertAsync`, `UpdateAsync` kopyaları oluşturulmaz.
- Worker/consumer kullanıcı kapsamını tahmin etmez: güvenilir kaynaktan tenant çözülür,
  tenant içi sistem işi için açık `DataScope.Tenant` kullanılır; genel okuma filtresi
  override edilerek HTTP erişimi genişletilmez. Tenant çözülemiyorsa işlem durur.
- İstisna gerekiyorsa önce teknik zorunluluk, korunan güvenlik/session garantileri ve
  doğrulama kanıtı ilgili mimari rehberde açıklanır; yalnızca sınıfı Store/Query diye adlandırmak istisna değildir.
- Yeni servis incelemesinde kullanılan ortak bileşenler dosya/metot düzeyinde gösterilir.
  Uygulama DTO eşlemesi ve maskelemesi Application'dadır; veri sürücüsü Infrastructure'da kalır.
