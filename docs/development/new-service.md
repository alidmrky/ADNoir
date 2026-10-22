# Yeni servis ekleme kontrol listesi

Aşağıdaki Orders adları örnektir; bu servis repoda yoktur.

## Sınırı belirle

Servisin sahip olduğu iş kavramını ve veriyi tanımla. Kimlik, tenant çözümleme,
response veya notification taşımasını tekrar geliştirme. Diğer servisin repository'sini
referanslama. Yeni servis gerçekten ayrı bir veri/iş sınırı mı, mevcut modül mü karar ver.

## Projeler ve kod

- Domain/Application/Infrastructure/Api projelerini solution'a ekle; [bağımlılık tablosunu](../architecture/layers.md) koru.
- Hedef framework ve paketleri merkezi dosyalardan al; projeye Version yazma.
- Domain entity/repository arayüzü; Application konu klasörü, DTO, servis ve validator;
  Infrastructure repository/BSON/index/adaptör; API endpoint ve composition root.
- Shared'daki servis marker'ı, BaseResponse, MongoRepository, IUnitOfWork ve event publisher'ı kullan.
- Katman DI genişletmelerini ekle. MassTransit outbox ile repository aynı session'ı kullanmalı.

## Çalıştırma ve erişim

- Ayrı port, launch profile, appsettings ve secretsız .env.example açıklaması ekle.
- JWT/JWKS, tenant çözümleme, UserSessionMiddleware ve gerçek IUserSessionValidator'ı kaydet.
- İç Identity istemcisi için ayrı servis credential'ı oluştur; sunucuda CanReadPermissionMatrix,
  CanValidateUserSession ve gerekli tenant kapsamını açıkça tanımla.
- PermissionCatalog ve endpoint RequirePermission kaydını ekle; anonymous yolları tek tek seç.
- Route grubunu `/api/v1/<kaynak>` olarak tanımla. Sürüm segmenti servisin kendi tanımındadır,
  Gateway yeniden yazmaz; ayrıntı [API değişiklik politikası](api-policy.md#yol-sürümleme).
- Gateway route/cluster, CORS gereksinimi, health ve telemetry servis adını ayarla.
- Production bağlantıları eksikse sessiz fallback açma. Kuyruk/exchange adlarını sabitle.
- Index sağlayıcılarını DbMigrator'da kaydet; yeni servisin API startup'ında index oluşturma.

## Kontrol ve teslim

- Mimari testlerde yeni assembly'leri ve ilgili katman listelerini kaydet. Projeyi solution'a
  eklemek otomatik olarak bütün mimari test kapsamına sokmaz.
- Yeni mimari kural için ihlali gerçekten yakalayan probe ekle; mevcut kuralları gevşetme.
- Gerçek tenant sınırı, eski oturum, yanlış izin ve altyapı hata durumlarını doğrula.
- Event consumer varsa iş düzeyi idempotency ve transaction rollback kanıtını üret.
- Servis rehberini docs/services altına ekle, kataloğa bağla; servis AGENTS'ına yalnız özgül
  kuralları yaz. Genel kuralları kopyalayıp farklılaştırma.
- Build/test sonuçları ve çalıştırılmayan kontrolleri değişiklik açıklamasında belirt.

## Veri erişimi teslim kontrolü

Kod incelemesinde aşağıdaki soruların her biri dosya/metot gösterilerek yanıtlanır:

1. Hangi işlemler IRepository standart metotlarını kullanıyor? Liste/detay için ayrı ham
   Mongo sorgusu varsa neden ortak metot yetmiyor?
2. Özel aggregation/claim hangi MongoRepository türevinde? BuildScopeFilterAsync bütün
   veri sorgularına ekleniyor mu? Ortak SessionAccessor ve outbox transaction'ı korunuyor mu?
3. Version güncellemesi ortak tabandan mı geliyor? Yeni CAS gereksinimi varsa önce mevcut
   UpdateWithScopeAsync değerlendirildi mi?
4. Worker tenant'ı nereden alıyor? Tenant yokken reddediliyor mu? HTTP sorgusu yanlışlıkla
   Tenant kapsamına genişletilmiş mi?
5. DTO/maskeleme Application'da mı? Servise özel model yanlışlıkla Shared'a taşınmış mı?
6. Gerçek tenant A/B, eksik tenant, dar kapsam, eski Version ve liste sınırı kontrollerinin
   sonucu nedir? İstisna varsa teknik gerekçe ve korunan garantiler ilgili mimari rehberde açıklanıyor mu?

[Ortak veri erişimi örneği](../architecture/shared-components.md#repository-seçimi-önce-mevcut-mekanizma)
Notification üzerinden anlatılır. Ortak mekanizmayı kopyalamak veya Store adıyla atlamak
bu kapıyı geçmez; genel kurallar [Services AGENTS](../../src/Services/AGENTS.md) içindedir.

Toplu yazmalarda her operation ortak yazma kapsamı filtresini taşımalıdır. Önceden okunmuş
ID listesine veya GUID'in tekilliğine güvenerek tenant filtresini kaldırma. Kısmi güncelleme
kabul edilmiyorsa aktif transaction zorunlu olsun; olumsuz senaryoda rollback doğrulansın.
Scope resolver gibi döngü nedeniyle repository kullanamayan altyapı istisnası ayrıca
belgelenir; ortak session ve tenant/soft-delete koruması yine zorunludur.

## Mimari envanter ve repository denetimi

Yeni projenin test ProjectReference kaydını ve AdNoirAssemblies.All() girdisini ekle;
Application projesini ApplicationLayers'a da ekle. Kaynak envanteri eksik veya tekrar
kaydı reddeder. Mimari kontroller kaynak checkout içinden çalıştırılır.
Servis dışı ham Mongo CRUD için adlandırma muafiyeti yoktur; ortak MongoRepository
kullanılmalıdır. Özel sorgunun scope/session/Version doğruluğu ayrıca incelenir.

Toplam gerektirmeyen son-N okumaları IRepository.ListLimitedAsync kullanır. Dışarıdan gelen
limit için validator üst sınır belirler; ortak metot pozitif limit zorunlu tutar.
