# Kod ve dosya düzeni

Her yeni public tip kendi adıyla eşleşen dosyada bulunur. DTO, servis arayüzü, servis
implementasyonu ve validator aynı dosyaya toplanmaz. Aynı sorumluluğun metotları bir
sınıfta kalabilir; her metot için ayrı dosya açmak bir hedef değildir.

Sınıf farklı kullanım senaryolarını veya altyapı işlerini toplamaya başladıysa
sorumluluklarını ayır. Sadece satır sayısını düşürmek için partial sınıflarla karmaşıklığı
gizleme. Bir protokol servisinin konu bazlı partial dosyaları aynı tipin organizasyonu
olabilir; ayrı public tipleri tek dosyada toplamakla karıştırma.

Application altında konu klasörü; Dtos, Abstractions, Validators alt klasörleri kullanılır.
Program.cs composition root'tur; endpoint, politika, consumer veya repository gövdesi değildir.
Katman DI kayıtları DependencyInjection altında ayrı genişletme sınıflarındadır.

IApplicationService public metotları async Task<BaseResponse> veya Task<BaseResponse<T>>,
son parametresi CancellationToken olacak şekilde yazılır. DTO döndürülür, entity değil.
İç repository/policy/transport arayüzleri sırf async oldukları için IApplicationService yapılmaz.

Mevcut IdentityDtos.cs gibi çok tipli tarihsel dosyalar henüz bütünüyle ayrıştırılmadı.
Bu örnekler yeni kod için izin değildir; dokümantasyon düzenlemesi bütün repo refactor'ı değildir.
