# Security Policy · Güvenlik Politikası

[English](#english) · [Türkçe](#türkçe)

---

## English

### Project status

ADNoir is under active development and has **no stable release**. It is a backend
foundation you build your own services on top of — not a hardened, production-ready
product. Local checks passing does not mean a deployment is secure.

Before any production use you are responsible for TLS termination, secret management,
backups, broker and database access policy, and capacity planning for your own target
environment. See [docs/overview.md](docs/overview.md) for the current usage limits.

### Supported versions

| Version | Supported |
|---|---|
| `0.1.x` | ✅ Current development line |
| < `0.1.0` | ❌ Unreleased history, not supported |

Only the latest tag on `main` receives fixes. There are no backports.

### Reporting a vulnerability

**Do not open a public GitHub issue for a security problem.**

Email **`alidmrky@gmail.com`** with:

- what the issue is and which component it affects
  (Gateway, IdentityAndOrg, Notification, Sample, DbMigrator, Shared),
- the version or commit SHA you tested,
- reproduction steps, and the impact you believe it has.

Expect an acknowledgement within **7 days**. Because this is a personal side project
maintained in spare time, a fix timeline is agreed case by case rather than promised
up front. Please give a reasonable window before disclosing publicly.

### Scope

**In scope** — anything that breaks a guarantee the project claims to make:

- tenant isolation bypass (reading or writing another tenant's data),
- authorization bypass: missing permission check, data-scope widening, privilege escalation
  through role assignment,
- authentication flaws: token forgery, refresh token reuse not revoking the family,
  live-session checks (security stamp, permission version, account state) being skipped,
- gRPC service identity bypass, or a service reading a tenant it was not granted,
- secret or personal data exposure in logs, audit records, or API responses,
- loss of outbox atomicity leading to silent data/message divergence.

**Out of scope:**

- missing hardening the project openly documents as not implemented — see
  *Known limitations* below,
- anything requiring MongoDB, RabbitMQ, Redis or SMTP to already be compromised,
- findings against a deployment whose TLS, secrets or network policy you configured,
- the `Sample` service's business logic — it is a demonstration, not a product module,
- dependency CVEs with no demonstrated path through ADNoir (report upstream instead).

### Known limitations

These are documented design trade-offs, not vulnerabilities. Report them only if you can
show a concrete exploit beyond what is described here.

| Area | Limitation | Detail |
|---|---|---|
| Access tokens | No `jti` denylist; a single issued access token cannot be revoked before it expires. Logout revokes the refresh token family. | [security.md](docs/architecture/security.md) |
| Permission cache | Revoking a gRPC service key does not flush existing caches instantly. | [grpc-security.md](docs/development/grpc-security.md) |
| Service identity | Static shared keys. No mTLS, no short-lived tokens, no automatic rotation. | [grpc-security.md](docs/development/grpc-security.md) |
| TLS | Must terminate directly at Identity. Forwarded-header / proxy TLS termination is not wired up. | [grpc-security.md](docs/development/grpc-security.md) |
| Audit trail | No WORM storage, no cryptographic integrity. A database administrator can alter it. | [audit-trail.md](docs/development/audit-trail.md) |
| Gateway | Rate limiting uses the direct network peer. `X-Forwarded-For` is not trusted, so placing it behind another proxy needs explicit design. | [gateway.md](docs/development/gateway.md) |
| Notification | No application-level encryption of e-mail content at rest. | [notifications.md](docs/development/notifications.md) |
| Revocation | Authorization can change between the check and the write. No atomic cross-service revocation guarantee. | [security.md](docs/architecture/security.md) |

### Secrets

Never commit a real `.env`, signing key, or SMTP credential. `.gitignore` excludes `.env`
and `*.env`; only `.env.example` is tracked. If you believe a secret reached the
repository, report it through the channel above rather than opening an issue.

---

## Türkçe

### Projenin durumu

ADNoir aktif geliştirmededir ve **kararlı bir sürümü yoktur**. Kendi servislerini üzerine
geliştireceğin bir backend temelidir; sertleştirilmiş, üretime hazır bir ürün değildir.
Yerel kontrollerin geçmesi bir kurulumun güvenli olduğu anlamına gelmez.

Üretim kullanımından önce TLS sonlandırma, secret yönetimi, yedekleme, broker/veritabanı
erişim politikası ve kapasite planlaması senin sorumluluğundadır. Mevcut kullanım sınırları
için [docs/overview.md](docs/overview.md) dosyasına bak.

### Desteklenen sürümler

| Sürüm | Destek |
|---|---|
| `0.1.x` | ✅ Güncel geliştirme hattı |
| < `0.1.0` | ❌ Yayınlanmamış geçmiş, desteklenmiyor |

Yalnız `main` üzerindeki en son etiket düzeltme alır. Geriye dönük yama yapılmaz.

### Güvenlik açığı bildirimi

**Güvenlik sorunu için herkese açık GitHub issue açma.**

**`alidmrky@gmail.com`** adresine şunları yaz:

- sorunun ne olduğu ve hangi bileşeni etkilediği
  (Gateway, IdentityAndOrg, Notification, Sample, DbMigrator, Shared),
- test ettiğin sürüm veya commit SHA'sı,
- yeniden üretme adımları ve etkisini nasıl değerlendirdiğin.

**7 gün** içinde bir yanıt bekleyebilirsin. Bu, boş zamanda sürdürülen kişisel bir yan proje
olduğu için düzeltme takvimi önceden taahhüt edilmez; durum bazında kararlaştırılır.
Herkese açık duyurudan önce makul bir süre tanı.

### Kapsam

**Kapsam içi** — projenin verdiğini söylediği bir garantiyi bozan her şey:

- tenant izolasyonunun aşılması (başka bir tenant'ın verisini okumak veya yazmak),
- yetkilendirme atlatma: eksik izin kontrolü, veri kapsamının genişlemesi, rol ataması
  yoluyla yetki yükseltme,
- kimlik doğrulama kusurları: token sahteciliği, refresh token yeniden kullanımının aileyi
  iptal etmemesi, canlı oturum denetiminin (security stamp, izin sürümü, hesap durumu)
  atlanması,
- gRPC servis kimliğinin atlatılması veya bir servisin yetkisi olmayan tenant'ı okuması,
- loglarda, audit kayıtlarında veya API yanıtlarında secret/kişisel veri sızması,
- outbox atomikliğinin kaybı sonucu veri ile mesajın sessizce ayrışması.

**Kapsam dışı:**

- projenin açıkça "uygulanmadı" diye belgelediği sertleştirme eksikleri — aşağıdaki
  *Bilinen sınırlar* tablosuna bak,
- MongoDB, RabbitMQ, Redis veya SMTP'nin zaten ele geçirilmiş olmasını gerektiren senaryolar,
- TLS, secret veya ağ politikasını senin yapılandırdığın bir kuruluma dair bulgular,
- `Sample` servisinin iş mantığı — o bir gösterimdir, ürün modülü değildir,
- ADNoir üzerinden somut bir istismar yolu gösterilmeyen bağımlılık CVE'leri (yukarı akışa bildir).

### Bilinen sınırlar

Bunlar belgelenmiş tasarım ödünleridir, açık değildir. Yalnız burada anlatılanın ötesinde
somut bir istismar gösterebiliyorsan bildir.

| Alan | Sınır | Ayrıntı |
|---|---|---|
| Access token | `jti` denylist yok; tek bir access token süresi dolmadan iptal edilemez. Logout refresh token ailesini iptal eder. | [security.md](docs/architecture/security.md) |
| İzin cache'i | gRPC servis anahtarı iptal edildiğinde mevcut cache anında boşalmaz. | [grpc-security.md](docs/development/grpc-security.md) |
| Servis kimliği | Sabit paylaşılan anahtar. mTLS, süreli token ve otomatik yenileme yok. | [grpc-security.md](docs/development/grpc-security.md) |
| TLS | Doğrudan Identity'de sonlanmalı. Forwarded-header / proxy TLS sonlandırması kurulmadı. | [grpc-security.md](docs/development/grpc-security.md) |
| Audit | WORM depolama ve kriptografik bütünlük yok. Veritabanı yöneticisi değiştirebilir. | [audit-trail.md](docs/development/audit-trail.md) |
| Gateway | Kota doğrudan ağ eşinin adresini kullanır. `X-Forwarded-For` güvenilmez; başka proxy arkasına koymak ayrı tasarım ister. | [gateway.md](docs/development/gateway.md) |
| Notification | E-posta içeriği için uygulama düzeyi şifreleme yok. | [notifications.md](docs/development/notifications.md) |
| İptal | Kontrol ile yazma arasında yetki değişebilir. Servisler arası atomik iptal garantisi yok. | [security.md](docs/architecture/security.md) |

### Secret'lar

Gerçek bir `.env`, imzalama anahtarı veya SMTP parolasını asla commit'leme. `.gitignore`
`.env` ve `*.env` dosyalarını dışlar; yalnız `.env.example` takip edilir. Bir secret'ın
repoya girdiğini düşünüyorsan issue açmak yerine yukarıdaki kanaldan bildir.
