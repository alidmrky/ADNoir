# DbMigrator

HTTP servisi değildir; kontrollü çalıştırılan bir konsol aracıdır. Mongo index
sağlayıcılarını uygular, sistem rollerini ve ilk tenant/organizasyon/yönetici verisini hazırlar.
Yeni index eklendiğinde yalnız API'yi yeniden başlatmak yeterli değildir; migrator çalışır.

Seed tekrar çalıştırılabilir; bu özellik her veri dönüşümünün otomatik güvenli olduğu
anlamına gelmez. Mevcut veriyi değiştiren migration için veri hacmi, geri dönüş ve
çakışma değerlendirmesi gerekir. API startup'ında index oluşturma ekleme.

İlk yönetici parolası yapılandırılmamışsa bir defa üretilir; güvenli saklanmalıdır.
İlk login sonrası parola değiştirme zorunluluğu iş endpoint'lerini kapatır.
Komutlar ve Visual Studio akışı: [ilk çalıştırma](../development/first-run.md).
