# Proje genel anlatısı

AdNoir, .NET 9 ve MongoDB üzerinde çok kiracılı backend geliştirmek için bir temeldir.
Hazır bir CRM, e-ticaret ürünü veya kullanıcı arayüzü değildir. Ortak kimlik, yetki,
veri erişimi ve iletişim mekanizmaları üzerine kendi iş servislerini eklersin.

Bir tenant bir müşteri/şirket sınırıdır. Aynı altyapıdaki iki tenant'ın iş verileri ayrı
filtrelenir. Tenant içindeki organizasyon ağacı ise merkez/bölge/bayi gibi daha dar
erişim kapsamlarını temsil eder. Tenant izolasyonu ile rol izni aynı kontrol değildir;
bir endpoint izni olması bütün tenant'ların verisini okumaya izin vermez.

## Tipik istek

İstemci → Gateway → ilgili servis API'si → Application kullanım senaryosu →
Domain kuralları + Infrastructure adaptörleri → MongoDB.

Servis JWT'yi doğrular; tenant çözer; canlı kullanıcı oturumunu denetler; izin matrisini
kullanarak işlemi yetkilendirir. Repository tenant/veri kapsamını uygular. Yanıt DTO ve
BaseResponse ile dışarı çıkar. Servisler birbirinin koleksiyonlarına erişmez.

## Mevcut temel

- IdentityAndOrg: tenant, organizasyon, hesap, rol ve token yönetimi.
- Gateway: dış HTTP girişi ve yönlendirme.
- Sample: kendi iş servisini geliştirirken incelenebilecek küçük örnek.
- Notification: opsiyonel e-posta gönderim niyeti, worker ve gönderim geçmişi.
- DbMigrator: index ve başlangıç verisi hazırlığı.
- Shared: yukarıdaki servislerin tekrar yazmadan kullandığı ortak sözleşme ve mekanizmalar.

## Kullanım sınırları

Proje aktif geliştirmededir. Üretim kurulumu için TLS, secret yönetimi, yedekleme ve
kapasite gereksinimleri hedef ortama göre değerlendirilmelidir. SMTP Accepted, alıcıya
teslim anlamına gelmez. Audit kaydı yalnız açıkça kapsama alınan işlemlerde üretilir.
