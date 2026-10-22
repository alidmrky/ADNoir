# AdNoir dokümantasyonu

Bu dizin projenin güncel kullanım ve geliştirme rehberidir. AdNoir aktif geliştirmededir;
yerel kontrollerin geçmesi üretim kabulünün tamamlandığı anlamına gelmez.

## İlk kez gelenler

1. [Proje nedir?](overview.md)
2. [İlk çalıştırma](development/first-run.md) ve [gRPC kimlik kurulumu](development/grpc-security.md)
3. [Katmanlar ve dosya yerleşimi](architecture/layers.md)
4. [Ortak bileşenler: önce burada ara](architecture/shared-components.md)
5. [Servis kataloğu](services/README.md)
6. [Bir özellik geliştirmek](development/feature-workflow.md) veya [yeni servis eklemek](development/new-service.md)

## Mimari ve kurallar

- [Kimlik, tenant ve oturum güvenliği](architecture/security.md)
- [Mesajlaşma, outbox ve transaction](architecture/messaging.md)
- [İşlem audit kayıtları](development/audit-trail.md)
- [Kod düzeni](development/code-layout.md)
- [API değişiklik politikası ve sürümleme](development/api-policy.md)
- [AGENTS kurallarının haritası](rules/README.md)

Kuralların bağlayıcı kaynağı kök ve ilgili kaynak dizinindeki AGENTS.md dosyalarıdır.
Bu rehberler kuralları örneklerle açıklar; farklı bir kural seti oluşturmaz.

## Kurulum ve işletim

[Gateway](development/gateway.md) · [Identity/Sample](development/identity-sample.md) ·
[Notification](development/notifications.md) · [gRPC güvenliği](development/grpc-security.md)

## Proje dosyaları

[Lisans](../LICENSE) · [Güvenlik politikası](../SECURITY.md) ·
[Katkı rehberi](../CONTRIBUTING.md) · [Değişiklik günlüğü](../CHANGELOG.md)

