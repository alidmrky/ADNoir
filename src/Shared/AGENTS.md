# Ortak kod kuralları

Kök kurallar geçerlidir. Mevcut ortak yetenek servis içinde kopyalanmaz; ortak mekanizma
servis adlarına veya servis koleksiyonlarına bağımlı yapılmaz.
Shared.Contracts: sözleşmeler; Domain.Abstractions: saf soyutlamalar;
BuildingBlocks: DB bağımsız mekanizmalar; BuildingBlocks.Mongo: Mongo implementasyonları.

Yeni ortak tip için birden fazla kullanımın aynı anlamı taşıdığı gösterilir. Servise özel
DTO, iş enum'u ve workflow ortak dizine taşınmaz. Yeni public tip ayrı dosyadadır.
Mevcut sözleşmenin davranışını değiştirdiğinde bütün tüketiciler ve dokümantasyon değerlendirilir.
