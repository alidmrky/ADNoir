# Gateway

YARP ile dış HTTP yollarını doğru servise yönlendirir; JWT/JWKS, CORS, hata zarfı,
health ve istek kotası girişte uygulanır. İş verisi veya repository içermez.

İstemci token'ı hedef servise aktarılır. Gateway'in doğrulaması hedef servisin
authentication/authorization/tenant/oturum denetimini kaldırmaz. İç gRPC servisleri
Gateway'de genel bir catch-all yol ile yayımlanmaz.

Yeni servis eklerken yalnız gereken HTTP prefix'i için route/cluster ekle; anonim
uçları açıkça belirle. Production Redis/RabbitMQ kuralları ve gerçek dağıtık kota
doğrulaması için [Gateway işletim rehberi](../development/gateway.md).
