# Contributing · Katkı Rehberi

[English](#english) · [Türkçe](#türkçe)

---

## English

Thanks for looking at ADNoir. It is a multi-tenant .NET 9 / MongoDB backend foundation,
maintained as a personal side project and still under active development.

### Before you write code

**Open an issue first** for anything beyond a typo or an obvious one-line fix. Architecture
here is enforced by tests, not by review alone, and a change that fights the layer rules will
not be mergeable no matter how good the code is. Agreeing on scope first saves you the work.

Then read, in this order:

1. [docs/README.md](docs/README.md) — documentation entry point
2. [docs/overview.md](docs/overview.md) — what the project is and is not
3. [docs/architecture/layers.md](docs/architecture/layers.md) — layer responsibilities and
   the allowed project references
4. [docs/architecture/shared-components.md](docs/architecture/shared-components.md) — what
   already exists in `src/Shared`
5. [AGENTS.md](AGENTS.md) — the binding rules, plus the `AGENTS.md` in the directory you
   are changing

The `AGENTS.md` files are the authority. The guides under `docs/` explain them with
examples; they do not define a second, different rule set.

### Language convention

This project is deliberately bilingual, and the split is not negotiable:

| What | Language |
|---|---|
| Code, type names, code comments, commit messages, PR titles | **English** |
| `AGENTS.md`, everything under `docs/` | **Turkish** |

Issues and pull request discussion may be in either language.

### Rules that will fail your build

These are enforced by the architecture tests in `tests/AdNoir.ArchitectureTests`, so you
will find out quickly — but knowing them up front is cheaper:

- **Layer direction.** `Shared.Contracts` and `Domain.Abstractions` reference nothing.
  `BuildingBlocks` must not depend on MongoDB. `Domain` must not know MongoDB — no `[Bson*]`
  attributes, mapping lives in `BsonClassMap` under Infrastructure. A service never
  references another service's Application or Infrastructure project.
- **Response contract.** Every public method on an `IApplicationService` returns
  `Task<BaseResponse>` or `Task<BaseResponse<T>>`, is async, and takes a `CancellationToken`
  last. `ProblemDetails` is not used — 404 and 500 come back in the same envelope.
- **No entities across the boundary.** Service methods return DTOs, never domain entities.
- **No hardcoded user-facing text.** Add the code to `MessageCodes` first, then the `en` and
  `tr` catalogs. `WithMessage` with a literal is forbidden in validators.
- **Reuse before writing.** Use the existing repository, response, tenant and security
  mechanisms in `src/Shared`. Do not write a second one inside a service, and do not move
  service-specific business logic into `Shared`.
- **Tenant and scope filters come from the repository.** Do not hand-roll them, and do not
  drop them by trusting a previously-read ID list.
- **Indexes belong in `DbMigrator`**, never created at application startup.
- **Transaction order.** Begin → write → publish the integration event → commit. The
  repository and the MassTransit outbox must share one `IClientSessionHandle`.

New entities need a conscious decision about tenant, soft-delete, `Version`, scope, audit
and index requirements. See [feature-workflow.md](docs/development/feature-workflow.md).

### Definition of done

- `dotnet build AdNoir.sln` succeeds with **zero warnings** (warnings are errors here).
- No `// TODO` and no abbreviated method bodies left behind.
- The relevant guide under `docs/` is updated in the same change if behaviour or setup moved.
- Your PR description states which checks you ran **and which you did not**. Do not report a
  check you did not run as passing. If you have no production environment, do not present a
  local result as production evidence.

### Testing

Unit and architecture tests run without external services:

```powershell
dotnet build AdNoir.sln -c Release
./tests/AdNoir.UnitTests/bin/Release/net9.0/AdNoir.UnitTests.exe
./tests/AdNoir.ArchitectureTests/bin/Release/net9.0/AdNoir.ArchitectureTests.exe
```

The test projects are run as executables rather than with `dotnet test`. xunit.v3 builds on
Microsoft.Testing.Platform, and the .NET 10 SDK removed the VSTest bridge `dotnet test` used
to reach it — so `dotnet test` fails on any machine that resolves a .NET 10 SDK, which
`global.json` allows through `rollForward: latestMajor`. Running the compiled test host
directly works on every SDK. Add `-result-trx <file>` for a report; pass `--help` for the
other options.

CI runs exactly these two on every push and pull request, in `Release`, after a build with
warnings treated as errors — so a Roslyn analyzer finding or a style violation fails the run
just as a failing test does. See [.github/workflows/ci.yml](.github/workflows/ci.yml).

Integration scripts under `tests/integration` and the projects under `tests/*.IntegrationChecks`
need a real local MongoDB replica set, PowerShell 7.4+ and free ports; each one creates and
cleans up its own database. **CI does not run them.** They are not required for every change —
run the ones your change can actually break, and say so.

Negative paths matter more than happy paths here: tenant A reading tenant B, a stale security
stamp, a missing permission, a forced password change, concurrent version conflicts, and a
redelivered event. A green success-path test is not evidence that isolation holds.

### Adding a new service

Follow [docs/development/new-service.md](docs/development/new-service.md). Do not copy the
`Sample` service and rename it — that carries over permission names, collection names, queue
names and endpoints that will quietly collide.

### Pull requests

- Branch off `main`. Keep one logical change per PR.
- Commit messages in English, imperative mood (`Add tenant scope filter to ...`).
- Do not commit a real `.env`, a signing key, or any credential. Only `.env.example` is tracked.
- Public API changes follow [docs/development/api-policy.md](docs/development/api-policy.md).
- Add a `CHANGELOG.md` entry under `## [Unreleased]` for anything user-visible.

### Security issues

Do not open a public issue. See [SECURITY.md](SECURITY.md).

### Licence

By contributing you agree that your contribution is licensed under the
[MIT License](LICENSE).

---

## Türkçe

ADNoir'a baktığın için teşekkürler. Çok kiracılı bir .NET 9 / MongoDB backend temelidir;
kişisel bir yan proje olarak sürdürülüyor ve hâlâ aktif geliştirmede.

### Kod yazmadan önce

Yazım hatası veya bariz tek satırlık düzeltme dışındaki her şey için **önce bir issue aç**.
Buradaki mimari yalnız incelemeyle değil testlerle zorlanıyor; katman kurallarına ters düşen
bir değişiklik kod ne kadar iyi olursa olsun birleştirilemez. Kapsamı önce konuşmak sana emek
kazandırır.

Ardından şu sırayla oku:

1. [docs/README.md](docs/README.md) — dokümantasyon giriş noktası
2. [docs/overview.md](docs/overview.md) — projenin ne olduğu ve ne olmadığı
3. [docs/architecture/layers.md](docs/architecture/layers.md) — katman sorumlulukları ve
   izin verilen proje referansları
4. [docs/architecture/shared-components.md](docs/architecture/shared-components.md) —
   `src/Shared` altında hâlihazırda ne var
5. [AGENTS.md](AGENTS.md) — bağlayıcı kurallar ve değiştirdiğin dizindeki `AGENTS.md`

Bağlayıcı kaynak `AGENTS.md` dosyalarıdır. `docs/` altındaki rehberler bunları örneklerle
açıklar; ikinci ve farklı bir kural seti tanımlamaz.

### Dil kuralı

Proje bilinçli olarak iki dillidir ve bu ayrım tartışmaya kapalıdır:

| Ne | Dil |
|---|---|
| Kod, tip adları, kod yorumları, commit mesajları, PR başlıkları | **İngilizce** |
| `AGENTS.md`, `docs/` altındaki her şey | **Türkçe** |

Issue ve PR tartışmaları iki dilde de olabilir.

### Derlemeni bozacak kurallar

Bunlar `tests/AdNoir.ArchitectureTests` içindeki mimari testlerle zorlanır, yani hızlıca
öğrenirsin — ama önceden bilmek daha ucuz:

- **Katman yönü.** `Shared.Contracts` ve `Domain.Abstractions` hiçbir şeye referans vermez.
  `BuildingBlocks` MongoDB'ye bağımlı olamaz. `Domain` MongoDB'yi bilmez — `[Bson*]`
  attribute'u kullanılmaz, eşleme Infrastructure'daki `BsonClassMap`'tedir. Bir servis başka
  bir servisin Application/Infrastructure projesine referans vermez.
- **Response sözleşmesi.** `IApplicationService` implemente eden her public metot
  `Task<BaseResponse>` veya `Task<BaseResponse<T>>` döner, async'tir, son parametresi
  `CancellationToken`'dır. `ProblemDetails` kullanılmaz — 404 ve 500 dahil her yanıt aynı
  zarfla döner.
- **Sınırdan entity geçmez.** Servis metotları DTO döner, domain entity değil.
- **Koda gömülü kullanıcı metni yok.** Önce `MessageCodes`'a kodu, sonra `en` ve `tr`
  kataloglarına metni ekle. Validator'larda literal `WithMessage` yasaktır.
- **Yazmadan önce tekrar kullan.** `src/Shared` içindeki mevcut repository, response, tenant
  ve güvenlik mekanizmalarını kullan. Servis içinde ikincisini yazma; servise özel iş
  mantığını da `Shared`'a taşıma.
- **Tenant ve kapsam filtresi repository'den gelir.** Elle üretme; önceden okunmuş ID
  listesine güvenerek filtreyi kaldırma.
- **Index'ler `DbMigrator`'a aittir**, uygulama başlangıcında oluşturulmaz.
- **Transaction sırası.** Begin → veri yaz → integration event publish → commit. Repository
  ile MassTransit outbox aynı `IClientSessionHandle`'ı paylaşmalıdır.

Yeni entity'ler için tenant, soft-delete, `Version`, kapsam, audit ve index ihtiyaçları
bilinçli olarak kararlaştırılmalıdır. Bkz.
[feature-workflow.md](docs/development/feature-workflow.md).

### Tamamlanmış sayılma ölçütü

- `dotnet build AdNoir.sln` **sıfır uyarıyla** geçer (burada uyarılar hatadır).
- Geride `// TODO` veya kısaltılmış metot gövdesi kalmaz.
- Davranış veya kurulum değiştiyse `docs/` altındaki ilgili rehber aynı değişiklikte güncellenir.
- PR açıklaman hangi kontrolleri çalıştırdığını **ve hangilerini çalıştırmadığını** söyler.
  Çalıştırmadığın bir kontrolü başarılı diye bildirme. Üretim ortamına erişimin yoksa yerel
  sonucu üretim kanıtı olarak sunma.

### Test

Unit ve mimari testler dış servis olmadan çalışır:

```powershell
dotnet build AdNoir.sln -c Release
./tests/AdNoir.UnitTests/bin/Release/net9.0/AdNoir.UnitTests.exe
./tests/AdNoir.ArchitectureTests/bin/Release/net9.0/AdNoir.ArchitectureTests.exe
```

Test projeleri `dotnet test` ile değil, doğrudan çalıştırılabilir olarak koşturulur.
xunit.v3 Microsoft.Testing.Platform üzerine kuruludur ve .NET 10 SDK, `dotnet test`'in ona
ulaşmak için kullandığı VSTest köprüsünü kaldırdı — bu yüzden `global.json`'daki
`rollForward: latestMajor` nedeniyle .NET 10 SDK'ya düşen her makinede `dotnet test`
başarısız olur. Derlenmiş test host'unu doğrudan çalıştırmak her SDK'da çalışır. Rapor için
`-result-trx <dosya>` ekle; diğer seçenekler için `--help`.

CI her push ve pull request'te tam olarak bu ikisini `Release` yapılandırmasında, uyarıların
hata sayıldığı bir derlemenin ardından çalıştırır — yani bir Roslyn analizör bulgusu veya
stil ihlali de, başarısız bir test gibi, koşuyu kırar. Bkz.
[.github/workflows/ci.yml](.github/workflows/ci.yml).

`tests/integration` altındaki betikler ve `tests/*.IntegrationChecks` projeleri gerçek bir
yerel MongoDB replica set'i, PowerShell 7.4+ ve boş portlar ister; her biri kendi
veritabanını oluşturup sonunda temizler. **CI bunları çalıştırmaz.** Her değişiklik için
gerekli değildir — değişikliğinin gerçekten bozabileceğini çalıştır ve bunu belirt.

Burada olumsuz senaryolar başarılı senaryolardan daha önemlidir: tenant A'nın tenant B'yi
okuması, eski security stamp, eksik izin, zorunlu parola değişimi, eşzamanlı sürüm çakışması
ve tekrar teslim edilen event. Yeşil bir başarı testi izolasyonun korunduğunun kanıtı değildir.

### Yeni servis eklemek

[docs/development/new-service.md](docs/development/new-service.md) dosyasını izle. `Sample`
servisini kopyalayıp adını değiştirme — bu, izin adlarını, koleksiyon adlarını, kuyruk
adlarını ve endpoint'leri sessizce taşır ve çakışmaya yol açar.

### Pull request

- `main` üzerinden dallan. Bir PR'da tek bir mantıksal değişiklik tut.
- Commit mesajları İngilizce ve emir kipinde (`Add tenant scope filter to ...`).
- Gerçek bir `.env`, imzalama anahtarı veya herhangi bir credential commit'leme. Yalnız
  `.env.example` takip edilir.
- Public API değişiklikleri [docs/development/api-policy.md](docs/development/api-policy.md)
  politikasına uyar.
- Kullanıcıya görünen her değişiklik için `CHANGELOG.md` içindeki `## [Unreleased]` başlığına
  bir satır ekle.

### Güvenlik sorunları

Herkese açık issue açma. Bkz. [SECURITY.md](SECURITY.md).

### Lisans

Katkı yaparak katkının [MIT Lisansı](LICENSE) altında lisanslandığını kabul etmiş olursun.
