# Kuralların haritası

Kurallar ilgili kaynak dizinindeki AGENTS.md dosyalarında tutulur. Bu dosyalar
bağlayıcı kaynak, aşağıdaki rehberler açıklayıcı kaynaktır.

| Konu | Kural kaynağı |
|---|---|
| Dil, geliştirme, bağımlılık, response, tenant, test, paket | [Kök](../../AGENTS.md) |
| Servis katmanları, DTO ve dosya düzeni | [Services](../../src/Services/AGENTS.md) |
| Identity özel kuralları | [IdentityAndOrg](../../src/Services/IdentityAndOrg/AGENTS.md) |
| Notification özel kuralları | [Notification](../../src/Services/Notification/AGENTS.md) |
| Ortak kod ve tekrar kullanımı | [Shared](../../src/Shared/AGENTS.md) |
| Veritabanından bağımsız altyapı | [BuildingBlocks](../../src/Shared/AdNoir.BuildingBlocks/AGENTS.md) |
| Mongo altyapısı | [BuildingBlocks.Mongo](../../src/Shared/AdNoir.BuildingBlocks.Mongo/AGENTS.md) |
| Saf sözleşmeler | [Contracts](../../src/Shared/AdNoir.Shared.Contracts/AGENTS.md) |
| Saf domain soyutlamaları | [Domain.Abstractions](../../src/Shared/AdNoir.Domain.Abstractions/AGENTS.md) |
| Gateway sınırı | [Gateway](../../src/Gateway/AGENTS.md) |
| Index/seed araçları | [Tools](../../src/Tools/AGENTS.md) |
| Dokümantasyon bakımı | [Docs](../AGENTS.md) |

Yeni servis veya klasör kuralı genel kurala ters düşemez. Tarihsel kodun kurala uymayan
örneklerini yeni geliştirmeye kopyalama. Kullanıcının açık talebiyle değişen kararları
güncel kuralda ve ADR'de birlikte kaydet.

Sample'a özgü kurallar [Sample AGENTS](../../src/Services/Sample/AGENTS.md) içindedir.
Identity'nin özel veri erişim gerekçeleri [servis envanterinde](../services/identity-and-org.md),
bağlayıcı sınırları Identity AGENTS dosyasındadır.
