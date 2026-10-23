# Frontend (React) yapısı

Bağlayıcı kurallar [ui/AGENTS.md](../../ui/AGENTS.md) içindedir. UI backend'in bir
tüketicisidir; [API sözleşmesini](api-policy.md) yeniden tanımlamaz.

Uygulama `ui/` altında çalışır. Giriş/kayıt, parola değişimi, tenant, organizasyon,
kullanıcı/rol yönetimi, bildirim geçmişi ve örnek kayıt ekranları Gateway'e bağlıdır.
Genel bakış navigasyon ve oturum bilgilerini gösterir; sunucuda olmayan toplamlar veya
etkinlik verileri üretmez.

## Çalıştırma ve kontroller

Node sürümü `ui/.nvmrc`, pnpm sürümü `ui/package.json` içinde sabittir. Repo kökünden:

```powershell
cd ui
pnpm install --frozen-lockfile
Copy-Item .env.example .env
pnpm dev
```

Arayüz `http://localhost:5173`, varsayılan Gateway `http://localhost:5070` adresindedir.
`VITE_GATEWAY_URL` dışında istemci ortam değişkeni yoktur. API işlemleri için backend
[ilk çalıştırma](first-run.md) rehberine göre ayrıca başlatılır. UI çalışma zamanında
mock veri kullanmaz; MSW yalnız testlerde devrededir.

```powershell
pnpm typecheck
pnpm lint
pnpm format:check
pnpm test
pnpm test:mutations
pnpm build
```

Statik çıktı `ui/dist` altındadır. Hash tabanlı yollar statik sunucuda alt yol rewrite'ı
gerektirmez. `.github/workflows/ui.yml` bu kontrolleri kilitli bağımlılıklarla çalıştırır.
Testler backend, MongoDB veya Docker gerektirmez. `test:mutations` seçili güvenlik
korumalarını geçici olarak bozup ilgili testin başarısızlığını doğrular; her denemede
kaynağı `finally` içinde geri yükler. Bu komut sırasında geliştirme sunucusu veya başka
kaynak düzenleyicisi çalıştırılmamalıdır.

`pnpm format` uygulama kaynaklarını Prettier ile düzenler. Üretilen tipler ve OpenAPI
snapshot'ları bu komuttan hariçtir; biçimlerinin sahibi tip üreticisidir.

## Teknoloji ve dizinler

| Alan | Seçim |
|---|---|
| Derleme | Vite + TypeScript, strict ve exactOptionalPropertyTypes |
| Bileşen | shadcn düzenindeki Button/Input primitive'leri, Radix Slot, semantik CSS |
| Sunucu durumu | TanStack Query |
| Oturum durumu | Zustand; yalnız durum, çözülmüş claim'ler, parola zorunluluğu |
| Form | react-hook-form + zod; format ve zorunluluk |
| Tipler | Commit edilen OpenAPI snapshot'larından üretim |
| Dil | tr ve en; backend metinleri yeniden çevrilmez |
| Test | Vitest, Testing Library, MSW, ESLint ihlal örnekleri |

```text
ui/
├─ AGENTS.md
├─ package.json · pnpm-lock.yaml · pnpm-workspace.yaml · .nvmrc
├─ tsconfig.json · vite.config.ts · eslint.config.js · .env.example
├─ scripts/                  Tip üretimi ve kontrollü regresyon kontrolü
├─ openapi/                  Üç gerçek Development OpenAPI snapshot'ı
├─ mocks/                    Yalnız test zarf fixture'ları ve MSW sunucusu
├─ tests/                    HTTP/oturum, sözleşme, mimari ve ekran testleri
└─ src/
   ├─ main.tsx               Bootstrap ve React root
   ├─ app/                   Composition, router, layout, genel bakış, hata sınırı
   ├─ contracts/             generated, zarf, durum, kodlar, izinler, UI veri tipleri
   ├─ lib/                   http, auth, query, i18n, format, utils
   ├─ stores/                session-store
   ├─ components/            ui primitive'leri ve common bileşikleri
   ├─ features/              auth, tenants, org-units, users, roles,
   │                         sample-items, notifications
   └─ styles/
```

Feature'lar `api/`, `model/`, `ui/` ve `index.ts` üzerinden ayrılır. Auth oturum
işlemlerini tek sahibi olan `lib/auth` üzerinden kullanır; ikinci API katmanı kopyalamaz.
Yönlendirme `app/router.ts` ve composition root içindedir. İhtiyaç doğmadan boş
`routes/` dizinleri oluşturulmaz. Feature dışına erişim yalnız `index.ts` üzerindendir.

Tam bağımlılık tablosu AGENTS.md'dedir ve `import/no-restricted-paths` zone'larıyla
zorlanır. Testler yasak import, depolama ve fetch örneklerini ESLint'e vererek gerçekten
reddedildiklerini doğrular. `contracts/envelope.ts` yalnız `lib/http` tarafından okunur;
`fetch` yalnız `lib/http/client.ts`, tarayıcı depolaması yalnız `lib/auth/token-store.ts`
içindedir. Üretim/denetim script'leri tarayıcı uygulama katmanının dışındadır.

`lib/http`, `lib/auth` import etmez. `main.tsx` içinden çağrılan `app/bootstrap.ts`,
`configureHttp` ile token, refresh, çıkış, dil, parola zorunluluğu ve uyarı callback'lerini
bağlar. Query ayarlarının tek sahibi `lib/query/client.ts` dosyasıdır.

## Ekranların API kapsamı

| Ekran | İşlemler |
|---|---|
| Oturum | Login, mevcut tenant'a kayıt, refresh, tek/tüm oturumlardan çıkış, parola değişimi |
| Tenant | Sayfalı liste, oluşturma, güncel detay üzerinden ad/saat dilimi/durum düzenleme |
| Organizasyon | Sayfalı liste ve hiyerarşi yolu; oluşturma, yeniden adlandırma, taşıma, silme |
| Kullanıcı | Sayfalı liste, oluşturma, kapsamlı rol atamalarını değiştirme, etkinleştirme/devre dışı bırakma |
| Rol | Liste, özel rol oluşturma/düzenleme/silme; sistem rollerini salt okunur gösterme |
| Bildirim | Durum filtresi, maskelenmiş geçmiş, detay/denemeler, tarih aralıklı istatistikler |
| Örnek kayıt | Oluşturma ve kimlikle bulma; backend'de olmayan liste endpoint'i çağrılmaz |

Tenant/organizasyon/kullanıcı listeleri 1 tabanlı `pageNumber` ve `pageSize` gönderir.
Rol ve bildirim listeleri backend'de sayfalı değildir. İlişkili organizasyon/rol kimlikleri
formlarda açıkça girilir; istemci tenant kapsamını yeniden filtrelemez ve role göre
izin türetmez. Tenant seçimi yalnız login/register'da yapılır.

Düzenleme açılırken güncel detay okunur. Form bu sürümü korur; `concurrencyConflict`
durumunda kaydetme durur ve kullanıcı güncel kaydı yeniden açar. Kullanıcı etkinlik
işleminde `expectedVersion` query parametresidir, diğer düzenlemelerde request
gövdesindedir. Taşıma formunda boş üst birim köke taşımayı ifade eder.

Masaüstünde kenar çubuğu, dar ekranlarda açılır navigasyon kullanılır. Dil değişimi
açık formu kapatır ve sorguları yeniler; eski dildeki sunucu mesajını çevirmeye çalışmaz.
Native `dialog` odağı sınırlar ve Escape ile kapanır. DM Sans ve Manrope Google Fonts
üzerinden yüklenir; erişilemezse sistem sans fontu kullanılır.

## Zarf ve tip üretimi

Wire alanları camelCase, zarf `status` değeri camelCase string'dir. Null alanlar yoktur;
isteğe bağlı alanlar `?:` olarak yazılır. `exactOptionalPropertyTypes` atamaları sınırlar;
TypeScript'in tek başına `=== null` ifadesini yasaklamadığı unutulmamalıdır.

Zarfı `lib/http/envelope.ts` açar: sonuç payload veya `Page<T>` olur; başarısızlık tek
`ApiError` olarak çıkar. HTTP koduna göre dal seçilmez. Ağ/bozuk JSON hataları sentetik
UI kodlarıyla aynı modele girer. Başarı uyarıları gösterilir. Hata ekranında server veya
istek correlation kimliği görünür; query retry beklenen istemci hatalarında kapalıdır.

| Servis | Development belgesi |
|---|---|
| Identity | http://localhost:5080/openapi/v1.json |
| Sample | http://localhost:5090/openapi/v1.json |
| Notification | http://localhost:5100/openapi/v1.json |

Gateway bu belgeleri yönlendirmez. Üç servisi çalıştırdıktan sonra:

```powershell
pnpm generate:contracts --refresh
```

`ui/openapi/` snapshot'ları ve `src/contracts/generated/` birlikte commit edilir.
`pnpm generate:contracts` mevcut snapshot'lardan çevrimdışı üretir;
`pnpm generate:contracts --check` dosya yazmadan çıktıyı karşılaştırır. Üretim kurulumda
çalışmaz; CI'da backend yoktur. Üretilen tipler yalnız payload'dur, zarf değildir.

Mevcut OpenAPI metadatasının iki sınırı vardır:

- Notification uçları response şeması bildirmez. Üç e-posta DTO'su gerçek C# record
  tanımlarından deterministik olarak üretilir; script desteklemediği sözdiziminde durur.
- OpenAPI, `ToHttpResult` özel response serializer'ını görmez. Response enum üyeleri
  C# kaynaklarından camelCase string'e düzeltilir. Request binding ise varsayılan
  ASP.NET ayarlarıyla sayısal enum alır. `TenantStatusInput` ve `DataScopeInput` ayrı
  sayısal tiplerdir; UI bu eşlemeyi açıkça yapar. JWT scope claim'leri ayrıca PascalCase'dir.

Bu metadata farkları üreticide açıklanır; backend metadata'sı tamamlandığında
istisnalar kaldırılabilir. Üretilen dosyalar elle düzenlenmez. `message-codes.ts` ve
`permissions.ts` kaynak C# kataloglarıyla test edilir. İki dilin anahtarları ve named
placeholder setleri karşılaştırılır; backend mesaj kataloğu istemciye kopyalanmaz.

## Gateway ve CORS

UI yalnız Gateway'e istek gönderir. Backend `.env` içinde:

```dotenv
AdNoir__Gateway__AllowedOrigins__0=http://localhost:5173
```

Vite host'u `localhost` olarak ayarlanmıştır. Tarayıcıda `127.0.0.1` kullanmak yukarıdaki
CORS izniyle aynı origin değildir. İzinli başlıklar Authorization, Content-Type,
Accept-Language ve X-Correlation-Id; dışa açılanlar Retry-After ve X-Correlation-Id'dir.
Tenant header'ı gönderilmez. Auth kotası 60 saniyede 10 istektir; refresh'in tek uçuşlu
olması bu nedenle de gereklidir.

## Oturum ve doğrulama sınırları

Access token modül belleğinde, refresh token tek localStorage anahtarındadır. Aynı
sekmede refresh promise'i paylaşılır. Web Locks destekleyen tarayıcılarda sekmeler arası
rotasyon da sıraya alınır; refresh token lock alındıktan sonra okunur. Uygulama HTTPS
veya localhost üzerinde çalıştırılmalıdır. Storage değişimi kendiliğinden refresh
başlatmaz; aksi halde sekmeler birbirini sürekli döndürür.

Çıkış cache'i temizler. Geciken eski oturum yanıtı yeni kullanıcı adına mutation
tekrarlayamaz. Login ve sonraki parola zorunluluğu yanıtı aynı bayrağı yazar. Parola
değişimi backend'de bütün oturumları sonlandırır; UI yeniden girişe döner.

**Etkin izin endpoint'i yoktur.** GET roles ayrıca role:read ister; sıradan kullanıcının
izinlerini öğrenme yolu değildir. UI işlem düğmelerini rol claim'inden gizlemez; 403'ü
karşılar. `auth.permission_matrix_unavailable` geçici hata olarak tekrar dene bandıyla
gösterilir, oturum kapatılmaz.

MSW testleri gerçek camelCase zarflarla 404/500, bozuk yanıt, başarı uyarıları, sayfalama,
beş eşzamanlı 401, refresh rotasyonu, iptal, parola zorunluluğu, 403 ve geçici izin
matrisi hatasını sınar. Ekran testleri sürüm çakışmasında otomatik tekrar yapılmadığını,
formdaki sunucu hatalarını ve bildirim filtresini doğrular.

Bu kontroller çalışan backend ile uçtan uca kabul testi yerine geçmez. Canlı kabulde
seed edilmiş kullanıcıyla login, tenant kapsamı, rol değişimi sonrası refresh, parola
değişimi ve Mongo transaction davranışı ayrıca doğrulanmalıdır.
