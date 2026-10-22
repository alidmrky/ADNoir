# Frontend (React) yapısı

Bağlayıcı kurallar [ui/AGENTS.md](../../ui/AGENTS.md) içindedir. Bu rehber yapıyı ve
kararların gerekçelerini açıklar; farklı bir kural seti oluşturmaz.

UI, backend'in bir tüketicisidir. Sözleşme [API değişiklik politikasında](api-policy.md)
tanımlıdır ve UI o sözleşmeyi kabul eder, yeniden tanımlamaz.

**Henüz kurulmadı.** Bu belge hedef yapıyı tarif eder; proje iskeleti sonraki adımda
oluşturulacaktır.

## Teknoloji kararları

| Alan | Seçim | Gerekçe |
|---|---|---|
| Derleme | Vite + TypeScript | Gateway CORS örneği zaten 5173'ü kullanıyor |
| Bileşen | shadcn/ui (Radix üzerine) | Bileşenler repoya kopyalanır, bağımlılık değildir |
| Sunucu durumu | TanStack Query | Cache, invalidation, dedup ve yeniden deneme hazır gelir |
| İstemci durumu | Zustand | Oturum, tercih ve geçici etkileşim durumu için yeterli |
| Form | react-hook-form + zod | shadcn `Form` bunu varsayar; zod yalnız format doğrular |
| Tipler | OpenAPI'den üretim | Sözleşme sapması derleme zamanında yakalanır |
| i18n | `tr` + `en` | Backend mesaj kataloğuyla aynı diller |

Access token bellekte, refresh token localStorage'da tutulur. Ayrıntı ve gerekçe
[ui/AGENTS.md](../../ui/AGENTS.md) bölüm 5'tedir.

## Dizin yapısı

```text
ui/
├─ AGENTS.md                  Bağlayıcı frontend kuralları
├─ package.json  pnpm-lock.yaml  .nvmrc
├─ tsconfig*.json  vite.config.ts  eslint.config.js  .prettierrc.json
├─ .env.example
├─ mocks/                     MSW handler'ları ve zarf fixture'ları
└─ src/
   ├─ main.tsx                Composition root; modülleri birbirine bağlayan tek dosya
   ├─ app/                    providers · router · layouts · errors
   ├─ contracts/              generated/ · envelope · response-status ·
   │                          message-codes · permissions
   ├─ lib/                    http/ · auth/ · query/ · i18n/ · permissions/ ·
   │                          format/ · utils/
   ├─ stores/                 session-store · ui-store
   ├─ components/             ui/ (shadcn) · common/ (uygulama bileşikleri)
   ├─ features/               auth · tenants · org-units · users · roles ·
   │                          sample-items · notifications
   └─ styles/
```

Her feature klasörü `api/`, `model/`, `ui/`, `routes/` ve bir `index.ts` içerir.
`index.ts` feature'ın dışarıya açık tek yüzeyidir; başka feature derin yol import etmez.

**Feature klasörleri gateway'in route gruplarını birebir yansıtır.** Gateway'e yeni bir
route grubu eklendiğinde UI'da karşılığı yeni bir feature klasörüdür, başka bir şey değil.

## Katmanların backend karşılığı

Yapı rastgele değil; backend'in katman ayrımının frontend'deki karşılığıdır.

| Backend | UI | Taşınan kural |
|---|---|---|
| `Shared.Contracts`, `Domain.Abstractions` | `src/contracts` | Hiçbir şeye referans vermez |
| `BuildingBlocks` | `src/lib` | Hiçbir feature'a bağımlı olamaz |
| `{Servis}.Infrastructure` | `lib/http`, `lib/auth` | Transport ve depolamaya dokunan tek yer |
| `{Servis}.Application` | `features/*/api`, `features/*/model` | Kullanım senaryosu; transport ve render yok |
| `{Servis}.Api` | `src/app`, `features/*/ui`, `features/*/routes` | Sınır ve composition root |
| "Servis başka servisin Application'ını referanslamaz" | feature'lar arası import | Yalnız `index.ts` üzerinden |

Tam bağımlılık tablosu [ui/AGENTS.md](../../ui/AGENTS.md) bölüm 2'dedir ve ESLint
`import/no-restricted-paths` zone'larına çevrilir. Backend'de katman kuralları IL
testleriyle zorlanıyor; burada lint aynı işi yapar.

## İki yapısal karar

**`lib/http`, `lib/auth`'u import etmez.** Doğal refleks, interceptor'ın token modülünü
çağırmasıdır. Ama token modülü `/api/v1/auth/refresh` çağırmak için client'a ihtiyaç duyar:
döngü oluşur ve refresh isteği, kendisini bekleyen interceptor'dan geçer. Çözüm bağımlılığı
ters çevirmek — `lib/http` bir `configureHttp({ getAccessToken, onUnauthorized })` dışa
verir, `main.tsx` bir kez çağırır. Backend'deki "Application `IIntegrationEventPublisher`
enjekte eder, Infrastructure MassTransit'i doğrudan kullanır" kalıbının aynısı.

**`contracts/envelope.ts` yalnız `lib/http`'den erişilebilir.** Böylece "zarfı tek yer
açar" kuralı metin olmaktan çıkar: lint, hiçbir feature'ın `BaseResponse<T>` tipini adıyla
bile görmesine izin vermez. `lib/http` dışarıya ya `T` döner ya `ApiError` fırlatır.

## Zarf hakkında bilinmesi gerekenler

Backend'in her yanıtı — 404 ve 500 dahil — `BaseResponse` zarfıyla gelir. Üç ayrıntı UI
kodunu sessizce bozabilir:

| Beklenen | Gerçek |
|---|---|
| `{ Status, Data }` PascalCase | **camelCase**: `status`, `data`, `messageCode` |
| `status: 4` sayısal enum | **camelCase string**: `"validationFailed"` |
| `message: null` | Alan JSON'dan **tamamen çıkar** |

Üçüncüsü için TypeScript tarafında `exactOptionalPropertyTypes` açılır; "yok" ile "null"
ayrımı tip sistemine taşınır ve `=== null` kontrolü derleme hatası verir.

HTTP durum kodu, `status`'ün taşıdığı ayrımı taşıyamaz: `conflict` ile
`concurrencyConflict` ikisi de 409, `noContent` 200, `externalServiceError` 502 döner.
Dallanma her zaman `status` veya `messageCode` üzerindendir.

## Tip üretimi

Her servis kendi OpenAPI dokümanını yayınlar; gateway bunları yönlendirmez ve dokümanlar
yalnız Development ortamında açıktır.

| Servis | Doküman |
|---|---|
| Identity | `http://localhost:5080/openapi/v1.json` |
| Sample | `http://localhost:5090/openapi/v1.json` |
| Notification | `http://localhost:5100/openapi/v1.json` |

Akış: üç servisi yerelde çalıştır → üretim komutunu çalıştır → çıktıyı commit'le.
Üretim elle tetiklenir; CI'da çalışan bir backend yoktur. Bir DTO değiştiğinde yeniden
üretim aynı commit'in parçasıdır.

Üretilenler yalnız payload şekilleridir. Zarf, hata eşlemesi ve durum işleme `lib/http`
içinde elle yazılır — OpenAPI dokümanı 415, 429, 502, 503 ve 504'ü bildirmez, üretilen bir
durum birleşimi bu gerçek durumları imkânsız gösterirdi.

`message-codes.ts` ve `permissions.ts` backend'den elle yansıtılır; OpenAPI'de adı geçmeyen
tek sözleşme parçası budur, bu yüzden testle doğrulanır.

## Gateway ve CORS

UI yalnız gateway ile konuşur (`http://localhost:5070`). Servis portları uygulama kodunda
kullanılmaz; CORS, istek kotası ve route listesini yalnız gateway uygular.

Gateway varsayılanında hiçbir origin izinli değildir. Geliştirme için `.env` dosyasında:

```dotenv
AdNoir__Gateway__AllowedOrigins__0=http://localhost:5173
```

İzin verilen istek başlıkları: `Authorization`, `Content-Type`, `Accept-Language`,
`X-Correlation-Id`. Dışarı açılan başlıklar: `Retry-After`, `X-Correlation-Id`. Tenant
başlığı göndermek mekanik olarak mümkün değildir — zaten gönderilmemelidir, tenant
token'daki imzalı claim'dir.

Kota sınırları: auth uçları 60 saniyede 10 istek, diğer anonim istekler 30, kimliği
doğrulanmış istekler 300. Eşzamanlı refresh fırtınası bu kotayı doldurur; tek uçuşlu
refresh kuralının ikinci gerekçesi budur.

## Henüz eksik olan

**İzin uçları.** İzinler token'da taşınmaz (bilinçli karar) ve kullanıcının kendi etkin
izinlerini döndüren bir uç henüz yoktur. `GET /api/v1/roles` `role:read` ister, sıradan
kullanıcıda bu izin yoktur. Bu uç eklenene kadar UI izin bazlı gizleme yapamaz; her işlem
denenir ve 403 karşılanır.

Bu durum [ui/AGENTS.md](../../ui/AGENTS.md) bölüm 6'daki "gating kozmetiktir" kuralını
geçersiz kılmaz, yalnız bugün gating'in hiç olmadığı anlamına gelir.
