# Kritik yönetim işlemlerinin audit geçmişi

Audit, entity üzerindeki CreatedAt/UpdatedAt alanlarından ayrıdır: kim, hangi tenant'taki
hangi kaydı, ne zaman ve hangi alanlarla değiştirdi bilgisini auditLogs içinde tutar.

## Mevcut kapsam

| İşlem grubu | Audit kapsamı |
|---|---|
| Kullanıcı yönetimi | Oluşturma, rol atama, aktif/pasif yapma |
| Rol yönetimi | Oluşturma, güncelleme, silme |
| Tenant yönetimi | Oluşturma, güncelleme |
| Organizasyon | Oluşturma, ad/slug değiştirme, taşıma, silme; etkilenen alt düğümler |
| Hesap | Parola değişikliği; gerçek parola/stamp değeri tutulmaz |

Bunlar 13 public kullanım senaryosudur (aktif/pasif tek SetActiveAsync); Application'da
14 RecordAsync çağrı noktası vardır. Rename/Move ortak RepositionAsync yolunu paylaşır;
alt düğümler ayrıca kaydedilir. Login/başarısız login, self-registration, seed, otomatik
refresh/reuse, bütün background işler ve diğer servisler otomatik audit kapsamında değildir.

## Akış ve atomiklik

İş alanlarının eski değerini al → UoW Begin → repository yazımı → gerekiyorsa event publish
→ ITransactionalAuditRecorder.RecordAsync → Commit. Ortak recorder MassTransit'in repository
ile paylaşılan session'ını kullanır. Transaction yoksa hata verir. Audit Insert başarısızsa
istek başarısız olur ve iş transaction'ı geri alınır. Bu davranış kritik yönetim geçmişi
kaybolmasın diye seçildi; audit depolama arızası yönetim yazmalarını da durdurur.

IAuditable uygunluk marker'ıdır; tek başına audit üreten interceptor değildir. Her yeni
kritik kullanım senaryosu açık audit çağrısını içerir. ChannelAuditTrailWriter hâlâ vardır,
fakat DropOldest/arka plan yazımı kritik geçmişin yerine kullanılamaz.

## Alanlar ve gizlilik

- TenantId hedef kaydın tenant'ı; ActorTenantId çağıranın tenant bağlamıdır. Platform
  yöneticisi başka tenant'ı değiştirdiğinde ikisi farklı olabilir.
- UserId kimliği doğrulanmış aktördür. EntityType/EntityId/Action/OccurredAtUtc değişikliği
  tanımlar. CorrelationId HTTP yanıtıyla aynı ortak çözümleyiciden gelir.
- Changes yalnız açık izinli alanları taşır. User için e-posta/display name/parola hash'i
  kaydedilmez. Parola/stamp değişiminde iki değer de *** olur. Varsayılan maskeler boş
  konfigürasyonla kaldırılamaz; özel hassas alanlar listeye eklenebilir.
- IP/user-agent bu recorder tarafından toplanmaz. Rol/tenant/organizasyon adları kişisel
  veya işletmeye özel veri içerebilir; audit koleksiyonu erişim ve saklama politikasına tabidir.
- Aynı rol ataması stamp'i değiştirdiğinde kayıt oluşur. Etkin alan farkı olmayan diğer
  Updated kayıtları atlanabilir. Ret olayları başarı audit'i olarak yazılmaz.

## Kurulum ve doğrulama

Identity Infrastructure AddAdNoirTransactionalAuditTrail kaydını yapar. Index'ler yalnız
DbMigrator'da uygulanır; mevcut audit TTL varsayılanı 365 gündür (AdNoir:Audit:Retention).
Bu süre yasal uyumluluk kararı değildir. Değişen süre için mevcut index'in güncelleme
operasyonu ayrıca planlanmalıdır. Otomatik silme anlık değildir.

Ayrı bir audit listeleme API'si/arayüzü eklenmedi. Mongo erişimi olan operatörler tenant
sınırını ve en az yetkiyi korumalıdır; uygulama garantisi veritabanı yöneticisine karşı
silinemezlik, kriptografik bütünlük veya WORM depolama sağlamaz.

Yerel kontrol (önce izole artifacts derlemesi):

```powershell
./tests/integration/role-security.ps1 -AuditChecks
./artifacts/checks/bin/AdNoir.Repository.IntegrationChecks/debug/AdNoir.Repository.IntegrationChecks.exe
```

İlk betik gerçek Gateway/Identity API'siyle çalışır. Kendi geçici veritabanında audit
validator'ını bilinçli bozarak rol + permission version rollback'ini doğrular; mevcut
adnoir verisine dokunmaz. Gerçek broker teslimat/rollback testi yerine geçmez.
