# Gateway kuralları

Kök kurallar geçerlidir. İş verisi, repository veya domain iş akışı ekleme.
Yollar ve anonymous erişim açıkça tanımlanır; iç gRPC yüzeyini genel route ile açma.
JWT'nin hedef serviste tekrar doğrulanmasını ve tenant/oturum/izin kontrollerini kaldırma.
Servis secret header'larını loglama veya dış istemciden güvenilir servis kimliği sayma.
