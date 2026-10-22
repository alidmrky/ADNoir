# AdNoir.IdentityAndOrg — Servise Özel Kurallar

Kök `AGENTS.md` mirasa alınır; buradaki kurallar ona **ek**tir, onu geçersiz kılmaz.

## Rol izolasyonu ve yetki devri

- İzin matrisi gRPC çağrıları repository erişiminden önce servis kimliği, okuma izni ve tenant
  izin listesiyle denetlenir. Boş yapılandırma anonim erişim açmaz. Servis anahtarları loglanmaz.

- Nullable `Role.TenantId` generic tenant filtresine güvenemez. ID tabanlı repository işlemleri
  mevcut tenant'ın özel rolleri ile global sistem rollerini açıkça filtreler.
- `SuperAdmin` ve `TenantAdmin` kodları özel rol oluşturmak için kullanılamaz; `tenant:manage`
  özel tenant rollerine eklenemez. Sistem rolü tanımları API'den değiştirilemez.
- Rol tanımı/ataması `IRoleGrantPolicy` üzerinden geçer. Aktörün güncel aktiflik, kilit,
  security stamp, rol izinleri ve veri kapsamları kontrol edilir. Aktör sahip olmadığı izin
  veya daha geniş kapsam veremez; mevcut hedef atamalarını kaldırmak da aynı denetime tabidir.
- Tenant genelindeki rol tanımlarını değiştirmek Tenant yazma kapsamı gerektirir.
- Tenant parametreli dahili rol sorguları login ve yetki matrisi için kullanılır; istemciden
  tenant kimliği alıp bunları doğrudan açan yeni API eklenmez.

Bu servis iki sorumluluk taşır: **kiracı yönetimi + organizasyon hiyerarşisi** (Faz 2a) ve
**kimlik + yetkilendirme** (Faz 2b).

---

## 1. Hiyerarşi kuralları

**Alt ağaç güncellemesini tetikleyen iki durum vardır ve ikisi de aynı yoldan geçer:**
1. Bir düğümün **taşınması** (`MoveAsync`)
2. Bir düğümün **slug'ının değişmesi** (`RenameAsync`, yalnızca yeni slug verildiğinde)

İkincisi kolayca gözden kaçar. Bu yüzden her ikisi de `OrgUnitService.RepositionAsync`
üzerinden yürür — ayrı bir yol açılırsa alt ağaç güncellemesi bir tarafta unutulur.

- **Sadece isim değişikliği alt ağacı etkilemez.** `RenameOrgUnitRequest.Slug` null ise path
  dokunulmaz. Slug değişimi bilinçli bir istektir, isim düzeltmesinin yan etkisi değildir.
- Alt ağaç güncellemesi **tek transaction** içinde, tek bulk write ile yapılır. Kısmen
  güncellenmiş bir ağaç bırakılamaz.
- Bulk write'ta **her düğümün `Version` alanı filtrede kalır.** "Sadece path güncellemesi"
  diye concurrency kontrolünü düşürmek, alt ağaçtaki eşzamanlı bir düzenlemenin sessizce
  ezilmesi demektir.
- **Döngü koruması:** bir düğüm kendi alt ağacına taşınamaz. Kontrol
  `OrgUnitTree.WouldCreateCycle` içindedir ve ek sorgu gerektirmez — hedef ebeveynin
  `Ancestors` dizisine bakar.
- **Alt ağaç sorguları `Ancestors` ile yapılır**, `HierarchyPath` prefix'iyle değil.
  `/bayi-1` ile `/bayi-10` karışır. Regex kullanılacaksa anchored (`^`) ve case-sensitive
  olacak; `i` opsiyonu index'i devre dışı bırakır.
- Yeni bir path hesaplandığında **yazmadan önce** uzunluk kontrolü yapılır
  (`HierarchyPaths.MaxLength`). Reddedilen bir taşıma ağacı hiç değiştirmemelidir.

## 2. Slug kuralları

- Üretim `Slug.From(...)` ile yapılır: Türkçe karakterler ASCII'ye, küçük harf, alfanümerik
  olmayanlar tek `-`, maks. 64 karakter.
- **Slug türetilmişse** (isimden) çakışmada `-2`, `-3` eki eklenir — kullanıcı o dizeyi
  istemedi.
- **Slug açıkça verilmişse** çakışma hatadır (`org.unit.slug_conflict`) — kullanıcının verdiği
  değeri sessizce değiştirmek yanlış olur.
- Slug kardeşler arasında benzersizdir, global değil. Benzersizlik veritabanında da
  `ux_orgunit_parent_slug` partial unique index'iyle zorlanır.

## 3. Tenant koleksiyonunun özel durumu

`Tenant` entity'si **`IMultiTenant` implemente etmez** — bir kiracı bir kiracıya ait değildir.
Sonuçları:
- Generic repository tenant filtresi eklemez (arayüz kontrolüne bakar).
- `MongoTenantRepository`, `BuildScopeFilterAsync`'i override ederek **veri kapsamı
  daraltmasını da devre dışı bırakır.** Aksi halde `Self` kapsamı kiracı listesini "kimin
  oluşturduğuna" göre filtreler; bu, "bazı kiracılar görünmüyor" şeklinde ortaya çıkan bir
  hata olurdu.
- Bu koleksiyonu kimin okuyabileceği bir **yetkilendirme** sorusudur ve Faz 2b'de platform
  düzeyinde bir izinle cevaplanır.

## 4. Katman sınırları (bu serviste)

- `Domain` yalnızca `Domain.Abstractions`'ı görür. Hiyerarşi mantığı (`OrgUnitTree`,
  `HierarchyPaths`, `Slug`) **saf**tır: async yok, veritabanı yok, altyapı yok.
- `Application` MongoDB'yi göremez. `BsonClassMap` konfigürasyonları `Infrastructure`'dadır;
  `Domain` entity'lerinde `[Bson*]` attribute'u **kullanılmaz**.
- `OrgUnitSubtreeResolver` repository yerine koleksiyona doğrudan gider. Bu bilinçlidir:
  generic repository kapsam filtresini kurarken bu resolver'ı kullanır, dolayısıyla
  resolver'ın repository'yi kullanması bağımlılık döngüsü olurdu.

## 5. Faz 2b'de yapılacaklar

- `User`, `Role`, `Permission` entity'leri; kayıt, login, refresh ve `stamp` akışı
- **Hesap kilitleme** — Identity'nin store yüzeyini kullanmadığımız için bu
  bizim sorumluluğumuzdadır ve atlanamaz
- **Refresh token aile/rotasyon mekanizması**: token düz metin saklanmaz, yalnızca
  SHA-256 hash'i; yeniden kullanım tespitinde **tüm aile** iptal edilir
- RS256 anahtar yönetimi ve `/.well-known/jwks.json` endpoint'i
- `GetPermissionMatrix` gRPC servisi — **bu olmadan yetkilendirme ilk istekte çöker**
- `DbMigrator` içinde idempotent seed: izin katalogu, sistem rolleri, kök tenant,
  kök OrgUnit, SuperAdmin kullanıcısı

**Faz 2a'da JWT doğrulaması bilinçli olarak eksiktir.** `Program.cs` authentication
pipeline'ını kurar ama imza anahtarı Identity'nin JWKS endpoint'inden gelecektir ve o endpoint
Faz 2b'de yazılacaktır. O zamana kadar korumalı endpoint'ler 401 döner — bu doğru davranıştır
(fail-closed), eksiklik olarak "geçici olarak açalım" şeklinde düzeltilmez.

## Ortak repository ve özel erişim sınırları

- OrgUnit toplu yeniden yazımında her operation filtresi ortak
  `BuildScopeFilterAsync(CurrentUser.WriteScope)` sonucunu ve ExpectedVersion'ı içerir.
  İşlem aktif transaction olmadan çalışmaz. ID listesinin önceden filtrelenmiş olması
  yazma sınırında tenant/soft-delete/kapsam filtresini kaldırma gerekçesi değildir.
- Refresh token aile/kullanıcı toplu iptali ortak Tenant scope filtresinden geçer.
  FamilyId/UserId GUID olması tenant filtresinin yerine geçmez.
- Opaque token'dan tenant bulma yalnız FindByHashAsync içindeki gerekçeli
  `IUnsafeCrossTenantQuery` çağrısıyla yapılır. İptal edilmiş token replay tespiti için
  okunabilir kalır; tenant çözüldükten sonraki yazmalar tenant sınırına tabidir.
- OrgUnitSubtreeResolver repository filtresinin bağımlılığıdır; kendi repository'sini
  enjekte etmek döngü yaratır. Bu açık istisnada tenant/soft-delete filtresi ve ortak
  IMongoSessionAccessor zorunludur. Session dışı okuma transaction içindeki ağacı kaçırır.
- Tenant platform kaydı, nullable tenant'lı global sistem rolleri ve anonim authentication
  sorgularının dar kapsam istisnaları korunur; diğer servisler bunları genel CRUD örneği
  olarak kopyalamaz. İstisna envanteri docs/services/identity-and-org.md içindedir.

## Kritik yönetim audit'i

- Kullanıcı oluşturma/rol atama/aktiflik, rol CRUD, tenant oluşturma/güncelleme,
  organizasyon oluşturma/yeniden adlandırma/taşıma/silme ve parola değişimi
  ITransactionalAuditRecorder ile iş transaction'ı içinde, commit'ten önce kaydedilir.
- Organizasyon yapısı değiştiğinde etkilenen alt düğümlerin path/ancestor farkları da
  aynı transaction'da yazılır. Audit yazımı hatası yakalanıp başarılı sonuç üretilmez.
- Entity/request bütünü serialize edilmez; açık alan listesi kullanılır. Parola/stamp
  değişiminde gerçek değer yerine sabit işlem işaretleri verilir; ortak masker bunları maskeler.
- Aynı rol/aktiflik isteği stamp döndürüyorsa audit atlanmaz. TenantId hedef kaydın,
  ActorTenantId işlemi yapan bağlamın tenant'ıdır; platform işlemlerinde farklı olabilirler.
- IAuditable yalnız uygunluk marker'ıdır, otomatik bütün metotlara audit ekleyen interceptor
  değildir. Yeni yönetim metodu için audit kapsamı ve rollback kontrolü birlikte eklenir.
