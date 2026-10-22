# Katmanlar ve bağımlılık yönü

| Katman | Buraya yazılır | Buraya yazılmaz |
|---|---|---|
| Domain | Entity, value object, saf iş kuralı, repository arayüzü | DTO, Mongo/BSON, HTTP, MassTransit |
| Application | Kullanım senaryosu, servis arayüzü/implementasyonu, istek/yanıt DTO'su, validator, uygulama politikası | Mongo sorgusu, SMTP/gRPC istemcisi, endpoint |
| Infrastructure | Repository implementasyonu, BSON map, index sağlayıcı, harici servis adaptörü, consumer, worker, katman DI | HTTP endpoint, yeni response zarfı |
| API | Endpoint, HTTP/gRPC sınırı, authentication ve middleware sırası, composition root | Koleksiyon yazımı, iş akışının kendisi |

Application implementasyonu Application'da bulunur; bir servis arayüzü olması onun her
implementasyonunun Infrastructure'a taşınacağı anlamına gelmez. Dış sistem adaptörünün
implementasyonu Infrastructure'dadır. Örneğin EmailQueryService kullanım senaryosudur;
SMTP gönderici ise dış sistem adaptörüdür.

## İzin verilen proje referansları

Kök [AGENTS.md](../../AGENTS.md) tablosu bağlayıcıdır:
Domain → Domain.Abstractions;
Application → Domain, Shared.Contracts, BuildingBlocks;
Infrastructure → Application, Domain, BuildingBlocks.Mongo;
API → Infrastructure, Application, BuildingBlocks.

Shared.Contracts ve Domain.Abstractions bağımsızdır. BuildingBlocks MongoDB bilmez.
BuildingBlocks.Mongo, BuildingBlocks üzerine Mongo implementasyonlarını ekler.
Servisten başka bir servisin Application/Infrastructure projesine referans verilmez;
ortak event/proto sözleşmesi ve ağ/mesajlaşma sınırı kullanılır.

## Örnek yerleşim

```text
AdNoir.Orders.Domain/Orders/Order.cs
AdNoir.Orders.Domain/Orders/IOrderRepository.cs
AdNoir.Orders.Application/Orders/Abstractions/IOrderService.cs
AdNoir.Orders.Application/Orders/Dtos/CreateOrderRequest.cs
AdNoir.Orders.Application/Orders/Dtos/OrderDto.cs
AdNoir.Orders.Application/Orders/Validators/CreateOrderRequestValidator.cs
AdNoir.Orders.Application/Orders/OrderService.cs
AdNoir.Orders.Infrastructure/Persistence/MongoOrderRepository.cs
AdNoir.Orders.Infrastructure/Persistence/Maps/OrderClassMap.cs
AdNoir.Orders.Infrastructure/Persistence/Indexes/OrderIndexProvider.cs
AdNoir.Orders.Api/Endpoints/OrderEndpoints.cs
```

Orders örnektir; repoda mevcut servis değildir. Servise özel DTO Shared.Contracts'a konmaz.
Servisler arası event ve proto sözleşmesi paylaşılır; entity paylaşılmaz.
