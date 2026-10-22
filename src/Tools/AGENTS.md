# Araç kuralları

Kök kurallar geçerlidir. Index/seed uygulaması DbMigrator'dadır; API startup'a taşınmaz.
Tekrarlanabilir seed ile yıkıcı veri dönüşümünü aynı şey sayma.
Parola/anahtarları kaynak kodda veya raporda saklama. Yeni servis index kayıtları ve
migrator DI kapsamı birlikte güncellenir.
