# API değişiklik politikası

Bu belge ADNoir'ın dışarı verdiği yüzeyin hangi koşullarda değişebileceğini tanımlar.
Bağlayıcı geliştirme kuralları ilgili [AGENTS.md](../../AGENTS.md) dosyalarındadır; burası
sürüm numarasının ne vaat ettiğini anlatır.

## Sürümleme

[Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html) kullanılır.

**Proje `0.x` hattındadır: public yüzey kararlı değildir.** `1.0.0`'a kadar kırıcı
değişiklikler herhangi bir minor sürümde gelebilir. Bu, SemVer'in `0.x` için tanımladığı
davranıştır, bir istisna değildir.

| Sürüm | Anlamı |
|---|---|
| `0.x.y` → `0.(x+1).0` | Yeni özellik; **kırıcı değişiklik içerebilir** |
| `0.x.y` → `0.x.(y+1)` | Düzeltme; kırıcı değişiklik hedeflenmez |
| `1.0.0` | Aşağıdaki garantilerin yürürlüğe girdiği nokta |

`1.0.0` için bir tarih taahhüt edilmez. Bu belgede "`1.0.0` sonrası" denilen garantiler
bugün geçerli değildir.

## Yol sürümleme

Bütün HTTP iş uçları `/api/v1/...` altındadır. Sürüm segmenti **servisin kendi route
tanımındadır**, Gateway'in yeniden yazmasıyla değil: sözleşme servise aittir, bu yüzden
doğrudan servise yapılan çağrı da, Gateway üzerinden geçen de aynı sürümü taşır. Böylece
ileride bir servis `v2`'ye geçerken diğerleri `v1`'de kalabilir.

Sürümsüz kalan uçlar: `/.well-known/jwks.json` (standart URI), `/health/*`, `/scalar/*` ve
`/openapi/*`. Bunlar iş sözleşmesi değildir.

Yeni servis eklerken route grubunu `/api/v1/<kaynak>` olarak tanımla; Gateway tarafında da
aynı yolu ekle. Gateway'in istek kotası auth uçlarını **tam yol eşleşmesiyle** seçer, bu
yüzden auth yollarını değiştiriyorsan `GatewayRateLimitPolicy` içindeki karşılaştırmayı da
güncelle — aksi halde login/register/refresh sessizce dar auth kotasından çıkar.

## Kapsam: neyin sözleşme olduğu

Aşağıdakiler **public yüzeydir**; değişimi bu politikaya tabidir.

| Yüzey | Kapsadığı |
|---|---|
| HTTP API | Gateway üzerinden yayımlanan yollar, istek/yanıt gövdeleri, `BaseResponse` zarfı, HTTP durum eşlemesi |
| Mesaj sözleşmeleri | `AdNoir.Shared.Contracts` içindeki integration event'ler; exchange ve kuyruk adları |
| gRPC | `AdNoir.Shared.Contracts/Protos` altındaki `.proto` dosyaları |
| Mesaj kodları | `MessageCodes` sabitleri ve kod hiyerarşisi |
| İzin kodları | `PermissionCatalog` girdileri |
| Yapılandırma | `AdNoir:*` ayar anahtarları ve `AdNoir__*` ortam değişkenleri |
| Kod kitaplıkları | `AdNoir.Shared.Contracts`, `AdNoir.Domain.Abstractions`, `AdNoir.BuildingBlocks`, `AdNoir.BuildingBlocks.Mongo` public tipleri |
| Veri | Koleksiyon adları, saklanan alan adları, index tanımları |

Aşağıdakiler **public yüzey değildir**; haber verilmeden değişebilir:

- `internal` tipler, `private` üyeler ve metot gövdeleri,
- `Sample` servisinin tamamı — gösterim amaçlıdır, ürün modülü değildir,
- test projeleri, `tests/` altındaki betikler ve `artifacts/` çıktı düzeni,
- log mesajlarının metni (kodları değil),
- kod yorumları ve `docs/` içeriği,
- geliştirme ortamına özgü port numaraları ve `launchSettings` profilleri.

## Kırıcı sayılan değişiklikler

İstemciyi veya üzerine servis geliştireni **kodunu değiştirmeye ya da veri taşımaya**
zorlayan her şey kırıcıdır:

- Bir endpoint'in kaldırılması, yolunun değişmesi veya HTTP metodunun değişmesi.
- İstek gövdesine zorunlu bir alan eklenmesi; var olan bir alanın adının, tipinin veya
  anlamının değişmesi.
- Yanıt gövdesinden bir alanın kaldırılması; tipinin daralması.
- Bir `ResponseStatus` değerinin HTTP karşılığının değişmesi.
- Bir `MessageCodes` sabitinin kaldırılması veya kodunun değişmesi.
- Bir izin kodunun kaldırılması, yeniden adlandırılması veya bir endpoint'in daha geniş bir
  izin istemeye başlaması.
- Integration event'ten alan kaldırılması, exchange/kuyruk adının değişmesi.
- `.proto` içinde alan numarasının yeniden kullanılması, alan kaldırılması, RPC adının değişmesi.
- Bir yapılandırma anahtarının kaldırılması veya varsayılanının davranışı değiştirecek
  şekilde değişmesi.
- Koleksiyon adının veya saklanan alan adının değişmesi — mevcut veriyi taşıma gerektirir.
- Public bir tipin kaldırılması; metot imzasının değişmesi.

**Kırıcı sayılmayanlar:** yeni endpoint eklemek; yanıta yeni alan eklemek; isteğe bağlı bir
istek alanı eklemek; yeni `MessageCodes` sabiti, yeni izin kodu, yeni event tipi veya yeni
`.proto` alanı (yeni numarayla) eklemek; varsayılanı olan yeni bir yapılandırma anahtarı
eklemek; yeni index eklemek.

Bir izni **daraltmak** kırıcıdır, **genişletmek** değildir: daha önce reddedilen bir çağrının
kabul edilmeye başlaması mevcut istemciyi bozmaz.

## `1.0.0` sonrası yürürlüğe girecek kurallar

Bunlar **bugün geçerli değildir**; `1.0.0` ile başlayacak hedeftir:

- Kırıcı değişiklik yalnız major sürümde yapılır.
- Kaldırılacak bir yüzey önce `[Obsolete]` ile işaretlenir, değişiklik günlüğünde duyurulur
  ve en az bir minor sürüm boyunca çalışmaya devam eder.
- Veri taşıma gerektiren her değişiklik için `DbMigrator` adımı ve geri dönüş notu yazılır.
- HTTP yolu değişimlerinde eski yol bir major sürüm boyunca yönlendirilir.

## Değişikliği nasıl teslim edersin

1. Kırıcı olup olmadığını yukarıdaki listeye göre belirle.
2. `CHANGELOG.md` içinde `## [Unreleased]` altına yaz. Kırıcıysa satırı
   **`BREAKING:`** ile başlat ve geçiş adımını yaz.
3. Davranış veya kurulum değiştiyse `docs/` altındaki ilgili rehberi aynı değişiklikte güncelle.
4. Veri taşıma gerekiyorsa `DbMigrator` adımını ekle. Yeni index'i orada kaydet;
   API startup'ında index oluşturma.
5. Yeni bir kullanıcı mesajı varsa önce `MessageCodes`, sonra `en` ve `tr` kataloglarını güncelle.
6. Çalıştırdığın ve çalıştırmadığın kontrolleri teslim açıklamasında belirt.

Yeni servis eklerken [yeni servis kontrol listesi](new-service.md), mevcut bir yüzeyi
değiştirirken [geliştirme iş akışı](feature-workflow.md) geçerlidir.

## Desteklenen sürümler

Yalnız `main` üzerindeki en son etiket düzeltme alır. Geriye dönük yama yapılmaz.
Güvenlik bildirimi için [SECURITY.md](../../SECURITY.md).
