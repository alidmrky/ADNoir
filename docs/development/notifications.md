# E-posta bildirim servisi

## Kapsam ve varsayılan davranış

AdNoir.Notification ayrı bir servistir. İlk kanal SMTP, ilk kullanım hoş geldin e-postasıdır. Varsayılan olarak gönderim ve Identity'nin welcome event üretimi kapalıdır. Notification servisi çalıştırılmadan mevcut kimlik işlemleri kullanılabilir.

API: http://localhost:5100, geliştirme dokümantasyonu: http://localhost:5100/scalar/v1. Gateway yalnız geçmiş sorgularını /api/notifications/emails altında yönlendirir. Genel amaçlı, istemcinin istediği adrese istediği gövdeyi gönderen bir API yoktur.

## Dosya düzeni

- Domain/Emails: EmailDelivery, EmailAttempt, EmailDeliveryStatus ayrı dosyalarda.
- Application/Emails/Dtos: güvenli yanıt modelleri ve sağlayıcı sonucu.
- Application/Emails/Abstractions: IEmailQueries ve INotificationProvider.
- Application/Emails: IEmailQueryService ve EmailQueryService ayrı dosyalarda.
- Application/DependencyInjection: uygulama servis kayıtları.
- Infrastructure/Configuration, Persistence, Messaging, Sending, Templates ve DependencyInjection: ayarlar ve adaptörler.
- Api/Endpoints, Health, Http ve Security: HTTP sınırı. Program.cs yalnız bileşenleri birleştirir.

API somut servis enjekte etmez. MongoDB ve MailKit yalnız Infrastructure'dadır; Domain BSON attribute'ları taşımaz. Servise özel kurallar src/Services/Notification/AGENTS.md içindedir.

## Gönderim akışı

1. Identity'de yeni kullanıcı ve UserRegisteredEvent aynı outbox transaction'ında kaydedilir; yayınlama commit'ten öncedir.
2. RabbitMQ exchange adı identity.user-registered, Notification kuyruğu notification.user-registered. Kuyruk quorum olarak tanımlıdır.
3. Consumer event tenant'ını scope'a yerleştirir. SMTP çağırmaz; Mongo consumer outbox transaction'ında gönderim kaydı oluşturur.
4. Tenant + sourceEventId + kind unique index'i iş düzeyinde tekrar işlemeyi engeller. Kişisel veri temizlendikten sonra da bu kayıt korunur.
5. Worker tenant kapsamıyla due kaydı seçer, version filtresiyle atomik sahiplenir ve denemeyi SMTP çağrısından önce kaydeder.
6. SMTP sonucu, deneme süresi ve güvenli sonuç kodu aynı kayda version kontrolüyle işlenir. Kesin geçici redlerde sınırlı exponential backoff + jitter uygulanır.

Index'ler startup'ta oluşturulmaz. DbMigrator çalıştırılmalıdır; unique index eksikken consumer ve worker işlemeyi reddeder. /health/ready Mongo ve bus durumunu kontrol eder; SMTP teslimatını kanıtlamaz.

## Durumların anlamı

| Durum | Anlam |
|---|---|
| Pending | Henüz denenmedi |
| Processing | Worker kaydı sahiplendi, deneme başladı |
| Accepted | SMTP'nin son kabul yanıtı alındı; alıcının kutusuna teslim edildiği bilinmiyor |
| RetryScheduled | Kesin geçici red veya gönderim başlamadan bağlantı hatası; tekrar planlandı |
| Failed | Kalıcı red, kimlik/TLS hatası veya deneme bütçesi tükendi |
| Skipped | Tenant özelliği kapalı; yeni event için alıcı/içerik saklanmaz |
| OutcomeUnknown | Gönderim başladıktan sonra bağlantı kesildi veya worker lease'i doldu; otomatik tekrar gönderilmez |

SMTP kabulü teslimat değildir. Bounce, spam şikâyeti, açılma ve tıklama bilgisi bu sürümde yoktur. Sağlayıcıya özgü mesaj kimliği SMTP yanıtından tahmin edilmez; gönderideki Message-ID, kayıt kimliğinden üretilir ve sağlayıcı dedup garantisi değildir. OutcomeUnknown kayıtları sağlayıcı kayıtlarıyla elle uzlaştırılmalıdır; kör yeniden gönderim endpoint'i eklenmedi.

## Aktivasyon

Önce çözümü derle, Mongo replica set'e bağlı DbMigrator'ı Development ortamında yeniden çalıştır. Bu işlem notification index'ini ve notification:read sistem rol iznini ekler. Kullanıcı parolasını sıfırlamaz. İzin cache'lerinin yenilenmesi için geliştirme servislerini yeniden başlatıp tekrar giriş yap.

Notification ve Identity aynı erişilebilir RabbitMQ'ya bağlanmalıdır. İki ayrı in-memory transport süreçler arasında mesaj taşımaz. Identity'de welcome yayınlama açık fakat RabbitMQ adresi boşsa başlangıç reddedilir.

Notification'ı önce başlatıp kuyruğun oluştuğunu kontrol et; sonra Identity yayınlamasını aç. Henüz bağlanmış kuyruk yokken yayınlanan event'ler daha sonra otomatik olarak geçmişe dönük gelmez.

Mevcut .env dosyanı ezmeden aşağıdaki ayarları uygun değerlerle düzenle. Tenant anahtarı slug değil, Mongo tenant kaydının GUID değeridir:

```dotenv
AdNoir__Notification__Enabled=true
AdNoir__Notification__Smtp__Host=smtp.example.com
AdNoir__Notification__Smtp__Port=587
AdNoir__Notification__Smtp__EnableSsl=true
AdNoir__Notification__Smtp__From=notifications@example.com
AdNoir__Notification__Smtp__Username=SMTP_KULLANICISI
AdNoir__Notification__Smtp__Password=SMTP_PAROLASI
AdNoir__Notification__Tenants__GERCEK_TENANT_GUID__Enabled=true
AdNoir__RegistrationNotifications__PublishWelcomeEvent=true
```

SMTP parolası repoya veya gönderim kayıtlarına yazılmaz. İlk adaptör MailKit 4.18.1 ve zorunlu STARTTLS kullanır; implicit TLS/465 ve OAuth ayar yüzeyi bu sürümün kapsamında değildir. Sertifika doğrulaması kapatılmaz. Yalnız Development loopback SMTP alıcısında EnableSsl=false kabul edilir.

Depo kökünden ek terminal:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet src/Services/Notification/AdNoir.Notification.Api/bin/Debug/net9.0/AdNoir.Notification.Api.dll --contentRoot src/Services/Notification/AdNoir.Notification.Api --urls http://localhost:5100
```

Visual Studio çoklu başlangıç listesine isteğe bağlı AdNoir.Notification.Api / http eklenebilir. Enabled=false olduğunda consumer ve gönderim durur; yapılandırılmış tenant'ların saklama bakım işlemleri çalışmaya devam eder. Tenant bazında Enabled=false ise yeni event'ler Skipped olur ve bekleyen kayıtlar SMTP denemesi yapılmadan atlanır. Ayar değişiklikleri yeniden başlatma gerektirir.

## Şablonlar

welcome.en.json ve welcome.tr.json assembly içine WithCulture=false ile gömülür. İçerik plain-text'tir; desteklenen tek yer tutucu {displayName}, konu satırında yer tutucu veya newline yoktur. Tanınmayan dil İngilizceye düşer. Tenant için AdNoir:Notification:Tenants:<GUID>:Templates:tr veya :en altında Version, Subject, Body ile tam override yapılabilir. İçerik değiştiğinde Version artırılmalıdır. Konu/gövde gönderim kaydı oluşturulurken sabitlenir; tekrar denemede değişen şablon sessizce kullanılmaz.

Bu sürümde tenant'ın mail özelliği ayarlanır. Son kullanıcı pazarlama tercihi/abonelikten çıkma modeli henüz yoktur; pazarlama veya kampanya gönderimi için kullanılmamalıdır. Parola sıfırlama/doğrulama token'ı taşıyan şablonlar eklenmedi.

## Geçmiş ve analiz

JWT, notification:read izni ve DataScope.Tenant okuma kapsamı gerekir. Tenant claim'den alınır; query/header ile başka tenant seçilemez. Daha dar kapsam reddedilir.

- GET /api/notifications/emails/: en yeni en fazla 100 kayıt. İsteğe bağlı ?status=Failed filtresi. Tam dışa aktarım veya sayfalama değildir.
- GET /api/notifications/emails/{id}: maskelenmiş alıcı ve deneme geçmişi. Başka tenant'ın kaydı 404 döner.
- GET /api/notifications/emails/statistics: son 30 günde oluşturulan kayıtların mevcut durum dağılımı, toplam deneme sayısı, tekrar denenmiş kayıt sayısı, ilk denemeye kadar ortalama bekleme ve ölçülebilen denemelerin ortalama süresi. Mongo üzerinde aggregate edilir.

İstatistikler oluşturulma tarihine göre bir kohorttur; bugünkü SMTP trafiğiyle aynı ölçüm değildir. Accepted teslimat oranı olarak sunulmaz. Gövde, konu ve tam alıcı API'de dönmez. Sonuç kodları makine kodudur; ham sağlayıcı hata metni gösterilmez.

## Saklama ve işletim sınırları

- RetentionDays varsayılan 30; 1–365 aralığı. Terminal kayıtların alıcı, konu, gövde ve correlation bilgileri süre dolunca worker tarafından temizlenir. API süre dolmuş kişisel alanı bakım gecikse bile göstermez.
- İş kimliği, durum, şablon sürümü ve deneme metadatası dedup/analiz için korunur. Bu metadata için otomatik silme yoktur; tüm kaydı silmek eski event'in tekrar mail oluşturmasına yol açabilir.
- Bekleyen kaydın içeriği işlem bitene kadar gerekir. Global durdurma bekleyenleri silmez. Tenant'ı yapılandırmadan kaldırmadan önce kuyruğunu sonlandırıp retention bakımını tamamla; disabled tenant girişini bu süre boyunca koru. Servis çalışmazsa fiziksel içerik temizliği de çalışmaz.
- Mongo erişimi ve yedekleri kişisel veri içerir; burada uygulama düzeyi içerik şifreleme uygulanmadı. RabbitMQ outbox, error queue ve broker yedeklerinin saklama/erişim politikası ayrıca yönetilmelidir.
- MaxAttempts varsayılan 5, en çok 10. LeaseSeconds varsayılan 120, SMTP deadline 30 saniye. Lease, deadline'dan en az 30 saniye uzun olmalıdır.
- PollSeconds varsayılan 5; her instance her turda tenant başına en fazla bir kayıt alır. Bu dağıtık sağlayıcı kotası değildir; instance sayısı arttıkça toplam hız artabilir.
- Tam olarak bir mail garantisi yoktur. Belirsiz sonuçları otomatik yeniden göndermemek mükerrer mail riskini azaltır; operatör uzlaştırması gerekebilir.

## Yerel doğrulama

Aşağıdaki entegrasyon kontrolü gerçek yerel Mongo replica set, MassTransit'in kendi in-memory test harness'ı ve dışarıya mail iletmeyen loopback SMTP kullanır:

```powershell
dotnet run --project tests/AdNoir.Notification.IntegrationChecks
```

Araç bağlantı ayarlarını ortamdan almaz: yalnız localhost:27017/rs0 üzerinde kendisinin ürettiği adnoir_notification_check_<GUID> veritabanını kullanır ve sonunda temizler. Eşzamanlı redelivery/dedup, retry, kalıcı red, kayıp SMTP onayı, tenant sınırı, istatistikler, optimistic concurrency, retention ve worker çökmesi sonrası belirsiz sonuç senaryolarını doğrular.

Üretim SMTP sağlayıcısında TLS, kimlik doğrulama, gönderici alan adı ve teslimat politikası ayrıca yapılandırılır. Loopback SMTP kontrolü dış alıcıya teslimat garantisi vermez.

## Veri erişimi düzeni

Geçmiş liste/detay ortak IRepository<EmailDelivery> üzerinden okunur. IEmailQueries yalnız
istatistik için kullanılır; MongoEmailRepository ortak scope filtresini aggregation'a ekler.
DTO maskelemesi Application/Emails/EmailDeliveryMapping içindedir ve enjekte edilen saatle
saklama süresini değerlendirir. Kuyruk store'u da ortak MongoRepository üzerinden insert,
filtre/session ve Version kontrolünü kullanır. API hâlâ son 100 kaydı döndürür; tam sayfalama
ve dışa aktarım eklenmedi. Yeni servislerde aynı yaklaşım için [kontrol listesine](new-service.md) bak.

## Liste sorgusu indeksleri

Son 100 kayıt CreatedAtUtc DESC, Id DESC sırasıyla ortak repository'den okunur.
DbMigrator'daki ix_email_tenant_created_id ve ix_email_tenant_status_created_id bu sıralamayı
filtresiz/durum filtreli okumalarda destekler. Güncellemeden sonra DbMigrator çalıştırılır;
API startup indeks oluşturmaz. Eski indeks otomatik silinmez. Son-100 okuması ListLimitedAsync kullanır; toplam sayım yapılmaz.
