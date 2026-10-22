# Ortak uygulama mekanizmaları

Kök ve Shared kuralları geçerlidir. MongoDB ve servis projelerine referans verme.
HTTP zarfı, hata eşleme, mesaj çözümleme, context, izin ve oturum politikaları burada ortaktır.
Yeni serviste bunları yeniden yazma. Harici transport generated tiplerini buraya sızdırma;
arayüz ve gerektiğinde küçük transport adaptör noktası kullan.
Canlı oturum doğrulaması başarısız olursa erişim açılmaz; izin matrisi cache'i yerine geçmez.

HTTP bağlama hataları ortak ExceptionResponseMapper üzerinden eşlenir; servis içinde
parser try/catch veya ayrı zarf yazılmaz. Dahili JsonException tek başına 400 gerekçesi
sayılamaz. Sayısal istek enum sözleşmesi değiştirilirse OpenAPI ve tüm tüketiciler incelenir.
