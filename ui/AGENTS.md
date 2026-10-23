# AdNoir UI — Frontend Kuralları

Bu dosya kendi kendine yeter. Kök AGENTS.md backend içindir ve burada geçerli değildir;
katman tablosu, response sözleşmesi, MassTransit, Mongo ve DI kuralları C#'a özgüdür.
Frontend'i bağlayan her şey bu dosyadadır.

Backend'in dışarı verdiği sözleşme burada **veri**dir, kural değil: değiştiğinde bu dosya
da değişir. Sözleşmenin kendisi docs/development/api-policy.md içindedir.

Hedef dizin yapısı ve kararların gerekçeleri docs/development/ui.md içindedir.

---

## 1. Dil ve kapsam

- Kod, tip adları, kod yorumları, commit mesajları: **İngilizce**
- Bu dosya ve docs altındaki her şey: **Türkçe**
- Kapsam: ui dizininin tamamı. src altındaki .NET kodu bu kuralların dışındadır.

## 2. Katman bağımlılıkları (lint zorlar)

| Dizin | Import edebileceği | Edemeyeceği |
|---|---|---|
| `src/contracts/**` | **hiçbiri** | her şey |
| `lib/utils`, `lib/format` | `contracts` | `lib/http`, `lib/auth`, `stores`, `features`, `components`, `app` |
| `lib/http/**` | `contracts`, `lib/utils` | **`lib/auth`**, `stores`, `features`, `components`, `app` |
| `lib/auth/**` | `contracts`, `lib/http`, `lib/utils` | `stores`, `features`, `components`, `app` |
| `lib/query/**` | `contracts`, `lib/http`, `lib/utils` | `stores`, `features`, `components`, `app` |
| `lib/i18n`, `lib/permissions` | `contracts`, `lib/utils` | `lib/http`, `features`, `components`, `app` |
| `stores/**` | `contracts`, `lib/utils` | `lib/http`, `@tanstack/react-query`, `features`, `components`, `app` |
| `components/ui/**` | `lib/utils`, Radix, cva | diğer her şey |
| `components/common/**` | `components/ui`, `lib/{i18n,format,utils}`, `contracts` | `lib/http`, `lib/auth`, `stores`, `features`, `app` |
| `features/*/model/**` | `contracts`, `lib/**` | başka feature, `components`, `app`, `stores` |
| `features/*/api/**` | `contracts`, `lib/{http,query,i18n}`, kendi `model` | başka feature, `components`, `app`, `stores` |
| `features/*/ui`, `features/*/routes` | kendi feature'ı, `components/**`, `lib/**`, `stores` | `lib/http` doğrudan, başka feature'ın iç yolu |
| `app/**` | her şey | — (composition root) |
| `contracts/envelope.ts` | — | `lib/http` dışında **hiçbir yerden** |

- Bu tablo ESLint `import/no-restricted-paths` zone'larına birebir çevrilir. İhlal lint
  hatasıdır, inceleme yorumu değil; zorlanmayan tablo dekordur.
- Bir feature başka feature'ı yalnız `features/<ad>/index.ts` üzerinden görür. Derin yol
  import edilmez.
- **`lib/http`, `lib/auth`'u import etmez.** Interceptor token ister, token modülü refresh
  çağırmak için client ister; bu döngüdür ve refresh isteği kendisini bekleyen
  interceptor'dan geçer. `lib/http` bir `configureHttp({ getAccessToken, onUnauthorized })`
  dışa verir, `main.tsx` bir kez çağırır.
- **`contracts/envelope.ts` yalnız `lib/http`'den erişilebilir.** Böylece zarfı tek yerin
  açtığı kural prose olmaktan çıkar: hiçbir feature `BaseResponse<T>` tipini adıyla göremez.
- `fetch` yalnız `lib/http/client.ts` içinde çağrılır. `localStorage`, `sessionStorage` ve
  `document.cookie` yalnız `lib/auth/token-store.ts` içinde kullanılır.

## 3. Zarf

Backend'in her yanıtı — 404 ve 500 dahil — `BaseResponse` zarfıyla gelir.

- Zarfı yalnız `lib/http/envelope.ts` açar. Dışarıya ya `T` döner ya `ApiError` fırlatır.
  Başka hiçbir yerde `.data`, `.isSuccess`, `.status` veya `.messageCode` okunmaz.
- **HTTP durum koduna göre dallanılmaz**; `status` ve `messageCode` okunur. `conflict` ile
  `concurrencyConflict` ikisi de 409'dur, `noContent` 200'dür, `externalServiceError`
  502'dir. HTTP, backend'in yaptığı ayrımı taşıyamaz. `ApiError` ham HTTP kodunu dışa vermez.
- Wire camelCase'dir ve `status` camelCase **string**'dir (`"validationFailed"`), sayı
  değildir. C# property adlarına veya sayısal enum'a göre yazılan kod sessizce hiçbir
  şeyle eşleşmez.
- Null alanlar JSON'da **yoktur**, `null` değildir. Tipler `?:` kullanır; `=== null`
  kontrolü yazılmaz.
- `PagedResponse` aynı modülde açılır; sayfalama `metadata` sözlüğünden okunmaz.
- Her başarısızlık tek bir `ApiError` olur: `status`, `messageCode`, `message`, `errors`,
  `correlationId`. Zarf ayrıştırılamıyorsa (ağ hatası, CORS, timeout) UI'a ait bir kodla
  sentetik `ApiError` üretilir; uygulamanın geri kalanı ikisini ayırt etmek zorunda kalmaz.
- `correlationId` her hata ekranında görünür. Kullanıcının verebileceği tek tutamak odur ve
  gateway `X-Correlation-Id` başlığını bunun için dışa açar.

## 4. Mesaj ve metin

- Mantık `messageCode` okur, ekran `message` gösterir. Backend metni çağıranın kültürüne
  göre zaten çözdü; UI'ın yeniden çevirmesi kataloğu ikiye böler ve ikisi ayrışır.
- UI kendi kataloğunda bir backend `messageCode`'unun çevirisini tutmaz. İstisna, UI'ın
  **farklı bir eylem** aldığı kısa listedir; o kodlar `contracts/message-codes.ts` içinde
  sabittir, çağrı yerinde literal yazılmaz.
- `errors[].field` sunucu property yoludur, form alanı adı değildir. Yalnız ilk harfi
  küçültülür: `Assignments[0].RoleId` → `assignments[0].RoleId`. Eşleme feature içinde
  açıkça yapılır ve fixture ile kapsanır.
- `warnings` başarı yanıtlarında da gelir ve gösterilir; atlanırsa kısmen başarılı bir
  işlem temiz görünür.

## 5. Token, 401 ve 403

- Access token yalnız `lib/auth/token-store.ts` içindeki modül değişkenindedir.
  localStorage, sessionStorage, cookie, Zustand store veya React state'e **konmaz**.
  Bellekte tutmak bu şemanın tek amacıdır; saklamak kapatmak için seçildiği pencereyi
  geri açar.
- Refresh token localStorage'da tek anahtardadır ve yalnız aynı modül okur/yazar.
- **Aynı anda tek refresh çalışır**; eşzamanlı 401'ler aynı promise'i bekler. Backend
  refresh token'ı rotate eder ve tekrarı hırsızlık sayıp **tüm aileyi iptal eder** —
  ikinci eşzamanlı refresh kullanıcıyı her sekmeden atar. Ayrıca gateway'in auth kotası
  60 saniyede 10 istektir.
- Bir istek başarılı refresh'ten sonra **en fazla bir kez** tekrarlanır. İkinci 401 zorunlu
  çıkıştır; sınır olmazsa sunucu tarafı iptal sonsuz döngü olur.
- `login`, `register` ve `refresh` refresh yoluna girmez. Login'den gelen 401 yanlış parola
  demektir; onda refresh denemek döngü üretir.
- 401 `auth.token_invalid` yalnız süre dolması değil, **iptal edilmiş oturum** da olabilir.
  Refresh yine bir kez denenir, başarısızlığı kesindir.
- **403 + `identity.password.change_required` yetki hatası değildir.** Parola değiştirme
  akışına yönlendirir ve oturumu kapatmaz. Parola değişene kadar her iş endpoint'inden
  döner; yetki hatası sayılırsa kullanıcı çıkamayacağı bir ekrana düşer.
- `mustChangePassword` iki kaynaktan gelir — login yanıtı ve sonraki 403 kodu — ve ikisi
  aynı tek bayrağı yazar. İki bayrak ayrışır ve guard'ın okuduğu eski olan olur.
- Zorunlu çıkış tek fonksiyondur: bellekteki token temizlenir, localStorage temizlenir,
  `queryClient.clear()` çağrılır, yönlendirme yapılır. Query cache bırakılırsa önceki
  kullanıcının verisi aynı sekmedeki sonraki girişe sızar.
- `identity.refresh_token.invalid` ve `.reuse_detected` anında zorunlu çıkıştır, tekrar
  denenmez; aile zaten yok.
- Çıkışta `POST /api/v1/auth/logout` yerel durum temizlenmeden **önce** çağrılır ve o çağrı
  başarısız olsa da yerel durum temizlenir. Endpoint anonimdir ve refresh token ile
  çalışır; atlanırsa 7 gün geçerli bir kimlik canlı kalır.
- JWT yalnız görüntüleme ve kozmetik gating için çözülür, doğrulanmaz; hiçbir erişim kararı
  ona dayanmaz. Token imzalıdır, şifreli değildir.

## 6. Tenant ve yetki

- `tenantSlug` yalnız login ve register formunda kullanıcı girdisidir. Girişten sonra
  tenant, token'daki `tenant_id`'dir: tenant seçici yoktur, isteğin okuduğu bir store'da
  tenant tutulmaz, tenant header'ı gönderilmez. Backend yalnız imzalı claim'e güvenir.
- Tenant değiştirmek çıkış ve yeniden giriştir.
- **İzin gating'i kozmetiktir.** Butonu gizlemek kolaylıktır; her korumalı işlem yine
  çalışma zamanında 403 `auth.permission_denied` karşılar. İzinler token'da değildir ve
  canlı oturum altında değişebilir.
- `roles` claim'inden izin türeten sabit tablo yazılmaz. İzin matrisi tenant'a özgü veridir,
  sabit değildir; kendi rollerini düzenleyen her tenant için yanlış olur.
- `auth.permission_matrix_unavailable` (502) **geçicidir, ret değildir**: tekrar dene
  bandı gösterilir, gizleme veya çıkış yapılmaz. Backend bilinçli olarak kapalı başarısız
  olur; bunu "yetkiniz yok" diye göstermek kullanıcıya gerçek retleri de yok saymayı öğretir.
- Token'daki `read_scope`, `write_scope` ve `hierarchy_path` yalnız görüntüleme ve sorgu
  varsayılanıdır; sunucunun kapsam filtresinin yerine geçen istemci filtresi yazılmaz.

## 7. Zustand ve TanStack Query sınırı

- HTTP yanıtından gelen hiçbir veri Zustand store'una kopyalanmaz. İki kopya demek bir
  kopyanın bayat olması demektir ve invalidate sonrası render edilen bayat olandır.
- Store yalnız şunları tutar: oturum durumu, çözülmüş claim'ler ve `mustChangePassword`;
  tema, dil, kenar çubuğu gibi tercihler; açık dialog veya çok adımlı taslak gibi geçici
  etkileşim durumu. Başka hiçbir şey.
- Query içinden store yazılmaz; `queryFn` anahtarın parçası olmayan bir store değerini
  okumaz. Gizli okuma anahtarın veriyi tarif etmesini bozar.
- Query anahtarları feature başına tek factory'den gelir (`features/*/api/keys.ts`). Çağrı
  yerinde satır içi anahtar dizisi yazılmaz; bir eleman kaçıran invalidate sessizce ve
  kalıcı olarak başarısız olur.
- Anahtarın bağlı olduğu her değer hook'un parametresidir.
- `staleTime`, `retry` ve `refetchOnWindowFocus` tek yerde, `lib/query` içinde ayarlanır.
  **`retry` 4xx `ApiError` için tekrar denemez.** Kütüphane varsayılanı üç denemedir; 401'de
  bu üç refresh girişimi, 429'da kotayı daha da bozan trafik demektir.

## 8. Eşzamanlılık ve sayfalama

- `expectedVersion` alan mutation, sürümü aynı ekranda sunucudan okunan değerden gönderir.
  `concurrencyConflict` yeniden okuyup kullanıcıya sorar; sürüm artırılıp **tekrar
  denenmez**. Kör tekrar, backend'in Mongo update filtresine koyduğu optimistic concurrency
  korumasını etkisiz bırakır. HTTP bunu `conflict`'ten ayıramaz, yalnız `status` ayırır.
- Listeler `pageNumber` ve `pageSize` adlarını birebir kullanır, 1 tabanlıdır; toplamlar
  sayfalı zarftan okunur. Bu adlar api-policy.md'de public yüzey ilan edilmiştir.

## 9. i18n

- `.tsx` içinde kullanıcıya görünen literal yazılmaz. Sıra: anahtarı **hem** `tr` **hem**
  `en` kataloğuna ekle, sonra kullan. Tek katalogda kalan anahtar diğer dilde ham anahtar
  olarak görünür.
- Yer tutucular isimlidir ve iki katalogda aynı sete sahiptir.
- Desteklenen diller tam olarak `tr` ve `en`'dir; backend kataloğu da budur. Backend'de
  karşılığı olmayan üçüncü bir dil yarı çevrilmiş ekran üretir.
- `Accept-Language` her istekte gönderilir ve sayfanın render edildiği dille aynıdır. Aksi
  halde zarfın `message` alanı, etrafındaki metinden farklı dilde gelir.
- Aşırıya kaçma: neredeyse aynı 800 anahtar, 80 iyi anahtardan kötüdür. Yeni anahtar ancak
  kullanıcı farklı bir şey görecekse açılır.

## 10. shadcn/ui

- `components/ui/**` üretilmiş koddur. Düzenlemek serbesttir — shadcn'in amacı budur — ama
  her düzenleme tek satırlık gerekçe yorumu taşır, böylece sonraki üretim tahmin edilmek
  yerine diff'lenebilir.
- İçinde uygulama bilgisi bulunmaz: API çağrısı, store okuması, i18n anahtarı, feature
  import'u yok. Bir primitive oturumu bilmeye başladığı an, yukarı akış erişilebilirlik
  düzeltmesi için üreticiyi yeniden çalıştırmak onu yok eder.
- Mevcut bir bileşen için `shadcn add` yeniden çalıştırıldığında sonuç diff olarak incelenir,
  olduğu gibi kabul edilmez.
- Uygulamaya özgü bileşikler `components/common` altına yazılır; üretilebilir küme küçük kalır.
- Radix doğrudan import edilmez, `components/ui` üzerinden kullanılır; aksi halde iki ayrı
  buton davranışı oluşur.

## 11. Üretilen tipler

- `contracts/generated/**` elle düzenlenmez. Farklı bir şekil, backend değişikliği ve
  yeniden üretim demektir; ikisi aynı commit'tedir.
- Üç ayrı OpenAPI dokümanı vardır (Identity, Sample, Notification). Gateway `/openapi/*`
  yönlendirmez ve dokümanlar yalnız Development'ta açıktır; birleşik bir kaynak yoktur.
- Üretilen çıktı commit edilir, kurulum sırasında üretilmez. CI'da çalışan bir backend yoktur.
- Üretilenler yalnız **payload** şekilleridir. Zarf, hata eşlemesi ve durum işleme
  `lib/http` içinde elle yazılır; OpenAPI dokümanı 415, 429, 502, 503 ve 504'ü bildirmez,
  üretilen durum birleşimi gerçek durumları imkânsız gösterir.
- Mevcut backend'de request enumları sayısal, response enumları camelCase string'dir.
  Üretici `DataScopeInput` ve `TenantStatusInput` tiplerini ayrı tutar. Notification
  OpenAPI yanıt şemaları eksik olduğundan payload'ları gerçek C# record tanımlarından
  üretilir; sınırlar ve yenileme komutları `docs/development/ui.md` içinde açıklanır.
- `contracts/message-codes.ts` ve `contracts/permissions.ts` backend'den elle yansıtılır ve
  **testle** doğrulanır, gözle değil. Sözleşmenin OpenAPI'de adı geçmeyen tek parçası budur.

## 12. Form ve doğrulama

- Formlar react-hook-form kullanır; shadcn'in `Form` bileşeni bunu varsayar.
- zod şeması yalnız **format ve zorunluluk** doğrular. İş kuralı sunucunundur ve `errors`
  dizisiyle gösterilir. Backend'in FluentValidation kuralı zod'a kopyalanmaz; kopyalanan
  kural zamanla ayrışır ve kullanıcıya sunucunun kabul edeceği bir girdiyi reddeder.
- Sunucu doğrulama hatası her formda gösterilebilir olmalıdır; istemci şeması geçse bile
  `validationFailed` yanıtı ele alınır.

## 13. Ortam ve yapılandırma

- Base URL **gateway**'dir ve tek tipli bir accessor'dan gelir. Servis portları uygulama
  kodunda kullanılmaz; CORS, kota ve route listesini yalnız gateway uygular.
- `VITE_*` değerleri bundle'a girer ve istemcide okunabilir. Hiçbir secret bu öneki almaz.
- `ui/.env` commit edilmez; `ui/.env.example` edilir ve her değişkeni listeler.
- Geliştirme portu 5173'tür. Değişirse `AdNoir__Gateway__AllowedOrigins__0` aynı commit'te
  güncellenir; aksi halde ortaya çıkan CORS hatası backend hatası gibi okunur.

## 14. Test ve Definition of Done

- Definition of Done: `tsc --noEmit` temiz, `eslint --max-warnings=0` temiz, testler geçer,
  `vite build` başarılı; hiçbir dosyada `TODO` veya kısaltılmış gövde kalmaz.
- `eslint-disable` tek satırlık gerekçe yorumu olmadan yazılmaz.
- 3., 5. ve 6. bölümdeki kurallardan bozulabilir olanların her biri için, bilinçli bir
  ihlalde **gerçekten kırmızı olduğu görülmüş** bir test bulunur. Hiç kırmızı olduğu
  görülmemiş bir kural, çalışmayan bir kuraldan ayırt edilemez.
- Vazgeçilmez olan: beş eşzamanlı 401, tam olarak bir `POST /api/v1/auth/refresh` üretir.
- API testleri gerçek zarfı döndüren mock'lar kullanır; 404 ve 500 için de başarısızlık
  zarfı döner. Çıplak DTO döndüren bir mock, zarf kuralını test edilemez yapar.
