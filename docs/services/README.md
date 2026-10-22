# Servis kataloğu

| Bileşen | Ne zaman kullanılır? | Rehber |
|---|---|---|
| IdentityAndOrg | Hesap, tenant, organizasyon ve yetki yönetimi | [Identity](identity-and-org.md) |
| Gateway | Dış istemcilerin ortak HTTP girişi | [Gateway](gateway.md) |
| Sample | Yeni iş servisi için küçük çalışan örnek | [Sample](sample.md) |
| Notification | Opsiyonel ve izlenebilir e-posta akışı | [Notification](notification.md) |
| DbMigrator | İlk kurulum ve kontrollü index/seed uygulaması | [DbMigrator](db-migrator.md) |
| Shared | Ortak sözleşme ve mekanizmalar; çalıştırılabilir servis değildir | [Ortak bileşenler](../architecture/shared-components.md) |

Bir servisin koleksiyonuna başka servis üzerinden doğrudan bağlanma. Ortak kimlik
ihtiyaçları için Identity'nin güvenli arayüzlerini, iş bildirimi için integration event'leri kullan.
