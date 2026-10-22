# Notification

Opsiyonel e-posta servisidir. Identity welcome event yayını ve Notification gönderimi
ayrı ayarlardır; varsayılan kapalıdır. Bir iş servisi SMTP istemcisi açmak yerine uygun
integration event sözleşmesiyle bildirim niyetini iletir.

Domain gönderim/deneme durumlarını ve kurallarını taşır. Application geçmiş/istatistik
sorguları ve DTO'ları sağlar. Infrastructure consumer, Mongo kayıtları, SMTP adaptörü,
worker, lease/retry/saklama mekanizmalarını uygular. API yetkili tenant'ın maskelenmiş
geçmişini ve istatistiklerini açar.

Consumer → kalıcı gönderim niyeti → worker lease → SMTP denemesi → sonuç kaydı.
Aynı event tekrar gelirse unique iş anahtarı ikinci niyet oluşmasını önler. SMTP Accepted
teslim edildi değildir. OutcomeUnknown otomatik tekrar edilmez; operatör incelemesi gerekir.
İçerik/alıcı/SMTP ham hatasını genel loglara yazma.

[Kurulum, durumlar ve sınırlar](../development/notifications.md) ·
[Ayrıntılı mevcut katman anlatısı](../../src/Services/Notification/InfoNotificationSerivs.md) ·
[Kurallar](../../src/Services/Notification/AGENTS.md).

## Ortak altyapıyla ilişki

Liste/detay Application'dan IRepository<EmailDelivery> ile okunur; istatistik IEmailQueries
üzerinden MongoEmailRepository aggregation'ına gider. MongoEmailRepository ve kuyruk
sorumluluğundaki MongoEmailStore, ortak MongoRepository tabanını kullanır. Tenant filtresi,
session ve sürüm kontrolü kopyalanmaz. DTO eşlemesi ve maskeleme Application'dadır.
Ayrıntı: [ortak bileşen seçimi](../architecture/shared-components.md#repository-seçimi-önce-mevcut-mekanizma).
