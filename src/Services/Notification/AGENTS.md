# Notification servis düzeni

Kök AGENTS.md kuralları geçerlidir.

- Her public tip ayrı, kendi adıyla eşleşen dosyada tutulur. DTO, arayüz ve implementasyon aynı dosyada birleştirilmez.
- Application kullanım senaryosuna göre klasörlenir: Emails/Dtos, Emails/Abstractions, servis arayüzü ve implementasyonu ayrı dosyalarda.
- Application servis arayüzü IApplicationService'den türetilir. API somut servis değil arayüz enjekte eder.
- Application DI kayıtları Application/DependencyInjection altında; Infrastructure kayıtları Infrastructure/DependencyInjection altındadır.
- API endpoint, health ve HTTP sınır kodu Program.cs içine yığılmaz; kendi klasörlerindeki genişletmelerle kaydedilir.
- Mongo eşlemeleri ve index sağlayıcıları ayrı dosyalardır; index yalnız DbMigrator ile oluşturulur.
- SMTP kabulü teslimat değildir. Belirsiz sonuçlar otomatik yeniden gönderilmez.
- Gönderim içeriği, alıcı adresi ve SMTP hata metni genel loglara yazılmaz.

## Ortak repository ve kuyruk sınırı

- Liste/detay `IRepository<EmailDelivery>.ListLimitedAsync/FindByIdAsync` ile okunur.
  `IEmailQueries` yalnız sunucu taraflı istatistik sözleşmesidir; genel CRUD eklenmez.
- `MongoEmailRepository` istatistik için ortak `BuildScopeFilterAsync` ve session kullanır.
  `EmailDeliveryMapping` Application'dadır; alıcı maskeleme/saklama kararı burada uygulanır.
- `MongoEmailStore` yalnız kuyruk davranışıdır; `MongoRepository<EmailDelivery>` tabanından
  yararlanır. Enqueue ortak InsertAsync; CAS ortak UpdateWithScopeAsync kullanır.
  ConcurrencyException yalnız worker yarışında false sonucuna çevrilir; diğer hatalar gizlenmez.
- Worker ve consumer tenant içi sistem işi için açık Tenant kapsamı kullanır;
  HTTP okuma repository'sinin kapsam filtresi genişletilmez. Consumer insert aynı outbox
  transaction'ında, SMTP ise transaction dışında kalır.
- API son 100 kaydı döndürür; tam sayfalama endpoint'i değildir. CreatedAtUtc sıralaması
  ortak repository'nin Id eşitlik bozucusuyla kararlıdır. Sayfa API'si eklenirse PagedResponse kullanılır.

Liste sıralamasını değiştirirken DbMigrator indeksleriyle birlikte değerlendir; CreatedAtUtc
ve Id yönleri aynı olmalıdır. İndeks değişikliğinde explain kanıtı, veri dağılımı, sayım
maliyeti ve migrasyon adımı raporlanır. Mevcut isimde indeks anahtarını sessizce değiştirme.
