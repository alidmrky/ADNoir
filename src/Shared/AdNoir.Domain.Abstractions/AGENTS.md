# Domain soyutlamaları

Kök ve Shared kuralları geçerlidir. Saf BCL; Mongo/HTTP/framework bağımlılığı yok.
Repository/UoW sözleşmeleri ve genel entity yetenekleri burada; servis iş modeli burada değil.
Yeni marker, generic repository filtrelerinde gerçek davranışıyla birlikte değerlendirilir.

Toplam gerektirmeyen son-N okumalarında IRepository.ListLimitedAsync, toplamlı sayfalarda
ListPagedAsync kullanılır. Servis içinde özel repository kopyası oluşturulmaz.
