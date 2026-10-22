# Changelog · Değişiklik Günlüğü

All notable changes to this project are documented here.
Bu projedeki önemli değişiklikler burada kayıt altına alınır.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> **`0.x` means the public surface is not stable.** Breaking changes may land in any minor
> release until `1.0.0`. See [docs/development/api-policy.md](docs/development/api-policy.md).
>
> **`0.x`, public yüzeyin kararlı olmadığı anlamına gelir.** `1.0.0`'a kadar her minor
> sürümde kırıcı değişiklik gelebilir.

---

## [Unreleased]

### Added — Eklenenler

- GitHub Actions CI: build in `Release` with warnings as errors, plus the unit and
  architecture test suites, on every push and pull request. Integration checks that need
  MongoDB are deliberately excluded.

### Changed — Değişenler

- `NuGet.config` now declares `nuget.org` as the only package source. The second source was
  unused — no package matched its mapping pattern — and package source mapping is no longer
  needed with a single source.

### Removed — Kaldırılanlar

- `PendingIntegrationEventPublisher`: a scaffold that logged integration events instead of
  publishing them, used before the message bus was wired up. Its registration was replaced
  by `MassTransitIntegrationEventPublisher`, but the file was never deleted. Nothing
  referenced it.

---

## [0.1.0] — 2026-10-22

First public release. The project has existed for some time; this is the point at which it
was licensed, documented and published.

İlk herkese açık sürüm. Proje bir süredir geliştiriliyordu; bu, lisanslandığı, belgelendiği
ve yayınlandığı noktadır.

### Added — Eklenenler

**Identity and organization · Kimlik ve organizasyon**

- Tenant, organization tree, user and role management.
- RS256 JWT issuing with a public JWKS endpoint; refresh token rotation with family
  revocation on reuse detection.
- Role-to-permission matrix with `Self` / `Unit` / `Subtree` / `Tenant` data scopes.
- Live session validation: tenant state, account state, lockout, security stamp and
  permission version are checked per protected request.
- Account lockout, forced password change, and an audit trail for critical administrative
  operations.

**Shared infrastructure · Ortak altyapı**

- `BaseResponse` / `PagedResponse` envelope for every response, including 404 and 500.
- Generic MongoDB repository applying tenant, soft-delete and data-scope filters
  automatically, with optimistic concurrency through a version field in the update filter.
- Centralized message catalog with `en` and `tr` resources; no user-facing text in code.
- MassTransit with the MongoDB transactional outbox, sharing one session with the
  repositories so data changes and messages commit atomically.
- Serilog, OpenTelemetry, correlation and trace propagation, health endpoints.
- Architecture tests enforcing layer direction, the response contract, the DTO rule and
  cross-tenant access marking.

**Gateway**

- YARP routing with explicit routes, JWT/JWKS validation, CORS allow-list, centralized error
  envelope and health endpoints.
- Redis-backed distributed rate limiting with separate quotas for auth, anonymous and
  authenticated traffic.

**Services · Servisler**

- `Sample`: a thin reference service demonstrating the four layers, tenant data access, the
  gRPC permission client and the outbox flow.
- `Notification`: optional SMTP e-mail with per-tenant templates, delivery and attempt
  history, worker lease, retry with backoff, and retention. Disabled by default.
- `DbMigrator`: console tool applying indexes and the idempotent seed.

**gRPC service identity · gRPC servis kimliği**

- Per-service credentials for the permission matrix and live session RPCs, with explicit
  per-tenant authorization. A user JWT does not grant service access.

### Added in this release — Bu sürümde eklenenler

- `LICENSE` (MIT).
- `SECURITY.md` — reporting channel, scope, and the documented known limitations.
- `CONTRIBUTING.md` — layer rules, language convention, definition of done.
- `CHANGELOG.md` — this file.
- `.github/CODEOWNERS`.
- `docs/development/api-policy.md` — what counts as a breaking change before `1.0.0`.

### Changed — Değişenler

- Documentation restructured under `docs/` into project overview, architecture, services and
  development guides. Phase reports, audit results and progress logs were removed.
- Source comments no longer cite the internal planning document, its phase numbering or its
  decision identifiers. The referenced files did not exist in the repository.
- The integration-check build output directory was renamed from `artifacts/phase6-check` to
  `artifacts/checks`.

### Removed — Kaldırılanlar

- `adnoirplan.md`, the internal phased implementation plan. It described a `.NET 10` target
  that did not match the actual `net9.0` build, and referenced documents that were never in
  the repository.

### Known limitations — Bilinen sınırlar

This release is **not production-hardened**. TLS termination, secret management, backups and
capacity planning are left to the deploying environment. See the limitations table in
[SECURITY.md](SECURITY.md) for the specific trade-offs, including access-token revocation,
permission cache invalidation, service key rotation and audit immutability.

Bu sürüm **üretim için sertleştirilmemiştir**. TLS, secret yönetimi, yedekleme ve kapasite
planlaması hedef ortama bırakılmıştır. Somut ödünler için [SECURITY.md](SECURITY.md)
içindeki sınırlar tablosuna bak.

[Unreleased]: https://github.com/alidmrky/ADNoir/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/alidmrky/ADNoir/releases/tag/v0.1.0
