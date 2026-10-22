# Ortak Mongo altyapısı

Kök ve Shared kuralları geçerlidir. Generic repository tenant/soft-delete/kapsam/Version
kontrollerini servis implementasyonlarıyla gevşetme. Servis koleksiyonu/BSON map'i o
servisin Infrastructure katmanına aittir. Index yalnız DbMigrator ile uygulanır.
MassTransit outbox ve repository aynı session'ı kullanır; bağımsız session açma.

Kritik audit ITransactionalAuditRecorder ile aynı aktif session/transaction içinde yazılır.
Transactional recorder kaydı host tarafından AddAdNoirTransactionalAuditTrail ile açıkça
seçilir; genel Mongo kaydı her host'a kullanıcı/audit bağımlılığı yüklemez. Channel yazıcısı
kritik audit garantisi değildir. Varsayılan hassas alan maskeleri konfigürasyonla kaldırılamaz.
CorrelationId mevcut ortak ResponseEnricher üzerinden çözülür; ikinci resolver yazılmaz.
