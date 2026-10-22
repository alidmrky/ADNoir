# Sample

SampleItem üzerinden dört katmanın, ortak response sözleşmesinin, tenant veri erişiminin,
gRPC izin istemcisinin ve integration event akışının kullanımını gösterir.
Kendi iş servisini tasarlarken okunacak örnektir; üretim iş modelinin yerine geçmez.

Application endpoint detayını bilmez; Infrastructure Mongo ve mesajlaşmayı uygular.
API JWT, tenant, canlı oturum ve izin denetiminden sonra Application'a geçer.
Yeni servis oluştururken Sample'ı bütünüyle kopyalayıp isim değiştirmek; eski izin,
koleksiyon, kuyruk ve endpoint adlarını taşımaya yol açabilir. Bunun yerine
[yeni servis kontrol listesini](../development/new-service.md) izle.

[Çalıştırma ve bağlantı](../development/identity-sample.md).

## Ortak repository kullanımı

MongoSampleItemRepository standart CRUD işlemlerini MongoRepository<SampleItem>
tabanından alır. SampleItemService InsertAsync ve FindByIdAsync kullanır;
DTO eşlemesi Application katmanındadır.
[Servis kuralları](../../src/Services/Sample/AGENTS.md).
