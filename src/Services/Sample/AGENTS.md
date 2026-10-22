# Sample servis kuralları

Kök ve Services AGENTS.md geçerlidir. Sample ortak altyapının kullanım örneğidir.

- ISampleItemRepository ortak IRepository<SampleItem> sözleşmesini genişletir;
  MongoSampleItemRepository ortak MongoRepository'den gelir. Standart CRUD burada tekrar yazılmaz.
- Application DTO üretir; Mongo sürücüsü Application'a taşınmaz. Yeni özel sorgu gerekiyorsa
  ortak scope filtresi ve session korunur; kopya tenant/Version mekanizması eklenmez.
- Create akışında Insert → Publish → Commit sırası ve aynı outbox session'ı korunur.
- Örneği yeni servise taşırken permission, event, koleksiyon ve kuyruk adları açıkça
  tanımlanır. Mevcut sınıf adını değiştirmek tek başına servis tasarımı değildir.
