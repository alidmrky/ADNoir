# Mesajlaşma ve veri tutarlılığı

Application yalnız IIntegrationEventPublisher bilir. MassTransit v8.5.10 doğrudan
Infrastructure içinde kullanılır; ikinci bir IEventBus katmanı oluşturulmaz.

Kritik akış: transaction başlat → veri değiştir → integration event publish →
transaction commit. Repository ile MassTransit outbox aynı IClientSessionHandle'ı kullanır.
Session MassTransit MongoDbContext'ten alınır; bağımsız Mongo session açmak atomikliği bozar.
Publish commit'ten sonra yapılmaz. Mongo transaction için replica set gerekir.

Consumer at-least-once teslimatı kabul eder; aynı mesajın tekrarında iş etkisi üretmemelidir.
Inbox dedup kaydı tek başına yeterli değildir. Kuyruk/exchange adları açık yapılandırılır.
Yeni event tipi Shared.Contracts içinde servisler arası sözleşmedir; entity taşınmaz.

Notification bunun somut örneğidir: consumer transaction içinde gönderim niyeti kaydeder.
SMTP'ye worker gider. SMTP kabulü ile Mongo güncellemesi atomik değildir; belirsiz sonuç
otomatik tekrar edilmez. Ayrıntı [Notification](../services/notification.md).

Development süreç içi transport iki ayrı servisi birbirine bağlamaz. Ayrı süreçlerde
mesaj teslimatı için RabbitMQ yapılandırılır. Broker portu AdNoir__RabbitMq__Port ile
ayarlanır; varsayılan 5672'dir. Identity, Sample ve Notification aynı ortak ayarı kullanır.
Üretimde TLS, broker erişim izinleri, kuyruk saklama ve kapasite ayrıca yapılandırılmalıdır.
