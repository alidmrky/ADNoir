# Bir özellik geliştirirken

1. Kök ve değişecek dizinin AGENTS.md dosyalarını ve ilgili mimari/servis rehberlerini oku.
   İstenen değişikliğin kapsamını belirle.
2. Kullanım senaryosunu yaz: çağıran kim, tenant sınırı ne, hangi izin ve veri kapsamı gerekir?
   Beklenen başarı/ret sonuçlarını belirle.
3. Shared kataloğunda mevcut response, repository, validation, context, mesajlaşma ve
   güvenlik mekanizmasını bul. Aynısını servis içinde yeniden yazma.
4. DTO ve sözleşmeyi belirle. Yeni kullanıcı mesajını önce MessageCodes'a, sonra en/tr'ye ekle.
   Başka servise entity gönderme.
5. Saf kuralları Domain'e; akışı Application'a; Mongo/SMTP/gRPC adaptörünü Infrastructure'a;
   HTTP/gRPC sınırını API'ye yerleştir. Tipleri ayrı dosyalarda tut.
6. Yeni entity'nin tenant, soft-delete, Version, kapsam, audit ve index ihtiyaçlarını değerlendir.
   Index sağlayıcısını migrator'a kaydet; startup'ta index oluşturma.
7. Kritik event varsa veri/publish/commit sırasını ve ortak session'ı doğrula. Consumer için
   tekrar teslimat iş etkisini tasarla.
8. DI ve ayarları bağla. Framework varsayılanını değiştirmek için Replace kullan; TryAdd'ın
   mevcut kaydı değiştirmediğini unutma. Secret'ı .env.example'a yazma.
9. Değişikliğe uygun kontrolleri çalıştır: derleme, mimari kurallar, gerçek sınır/başarısızlık
   kontrolleri. Çalıştırılmayan kontrolleri başarılı olarak bildirme.
10. İlgili kullanım ve mimari rehberini güncelle; doğrulama sonuçlarını değişiklik açıklamasına yaz.

Tenant A/B erişimi, yanlış izin, stale stamp, zorunlu parola, concurrency ve tekrarlanan
event gibi olumsuz senaryoları yalnız başarılı yanıtla geçiştirme. Üretim ortamına erişimin
yoksa yerel sonucu üretim kanıtı olarak yazma.

## Veri erişimini değiştirmeden önce

IRepository'nin mevcut metotlarını ve MongoRepository'nin protected genişletme noktalarını
oku. Genel okuma/yazma için onları kullan; özel sorguda ortak filtre/session'ı koru.
Bir query arayüzü eklemek ortak repository'yi devre dışı bırakmaz. Kullanılan ortak
metotları ve yalnız servise özgü kalan davranışı değişiklik raporuna yaz.
[Seçim tablosu](../architecture/shared-components.md#repository-seçimi-önce-mevcut-mekanizma)
ve [yeni servis kapısı](new-service.md#veri-erişimi-teslim-kontrolü) bu incelemenin parçasıdır.
