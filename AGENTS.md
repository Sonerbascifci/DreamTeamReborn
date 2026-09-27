# Dream Team Reborn — Agent çalışma talimatı

Bu repository, web tabanlı basketbol menajerlik oyunu için hazırlanıyor. Bu dosya mevcut daha üst düzey talimatların yerini almaz. Mevcut repoya eklenirken önceki `AGENTS.md` kurallarını silme.

## İlk oturum

1. `README.md`, `docs/00_CONTEXT_AND_SCOPE.md`, `docs/10_DECISIONS_AND_OPEN_QUESTIONS.md` ve `docs/11_PROJECT_STATUS.md` oku.
2. Seçilen işin teknik belgelerini oku. Match Engine işi için 03–08 arası zorunludur.
3. Repository, solution, SDK, paket, test ve git durumunu gerçek dosyalardan keşfet. Bu paket kodun mevcut olduğunu kanıtlamaz.
4. İlk devir isteği planlamadır. Başlangıç mesajı yalnızca plan istiyorsa kod, migration, paket kurulumu, refactoring veya deploy yapma.
5. Uygulama yetkisi verildiğinde tek milestone'ın kabul kriterlerine kadar ilerle. Yetkilendirilmiş rutin adımlar için tekrar tekrar izin isteme.

## Değişmez tasarım yönü

- Sonuçlar action/possession simülasyonundan çıkmalı; skor önceden seçilip istatistik dağıtılmamalı.
- Motor saf C# olmalı. ASP.NET, EF Core, DB, Redis, SignalR, React ve PixiJS bağımlılığı motora girmemeli.
- OVR kullanıcıya özet gösterim içindir; resolver girdisi değildir.
- Maç başlangıcı veri/config snapshot'ları sabitlenir. Aynı sürümler, seed ve sıralı komutlar aynı simülasyonu üretmelidir.
- Client yalnızca komut gönderir ve event sunar. Skor, para ve ownership değişiklikleri sunucuda doğrulanır.
- İlk içerik kurgusal oyuncu ve takımlardır; gerçek kişi fotoğrafı, lig logosu veya lisanslı veri varsayma.
- Önce konsol motoru ve doğrulama; sonra API/SignalR; sonra React/PixiJS.
- Araştırmada görülen eski oyun özellikleri kendiliğinden backlog taahhüdü değildir.

## Uygulama disiplini

- Belge etiketlerini koru: Yön, Taslak, Devir önerisi, Açık, Ertelenmiş. Sayısal örnekleri kanıtlanmış denge olarak sunma.
- Paket/SDK sürümünü gerçek ortamda doğrula ve kilitle; bu paketten hayalî sürüm çıkarma.
- Tek maçın state'ini tek yürütücü değiştirir. Başlangıçta in-process yeterlidir; mikroservis/actor framework/message broker ekleme.
- Interface yalnızca değişken sınır veya test ihtiyacı olduğunda; her sınıfa otomatik repository/service/interface üretme.
- Gelecekteki veri modellerinin tamamına ilk milestone'da tablo/migration açma.
- Çalışan kullanıcı değişikliklerini koru. Deploy, push/merge ve dış servis kurulumunu ayrı görev kapsamına göre değerlendir.
- RNG çağrı sırası, dictionary sırası, paralel simülasyon ve gerçek saat bağımlılığı determinism açısından incelenir.
- Diagnostic mod, animasyon veya loglama oyun RNG'sini tüketmemeli.
- Motor kuralları için davranış ve invariant testleri yaz. Gerçek kalan riski çözmeyen tekrar testlerinden kaçın.
- Test koşturulmadıysa “geçti” deme. 100.000 maç çalışmadıysa ölçek doğrulandı deme.
- Dış kaynak kodunu kendi kodumuz gibi kopyalama. Özellikle ZenGM referansı yeniden kullanım izni olarak değerlendirilmez.

## Oturum kapanışı

`docs/11_PROJECT_STATUS.md` güncellenir: ne yapıldı, hangi test gerçekten çalıştı, sonucu, açık riskler, bir sonraki tek görev. Tasarım değiştiyse sahibi olan belge ve karar günlüğü güncellenir. Onay verilmemiş karar “kabul edildi” yapılmaz.

Rapor biçimi: değişiklik ve amacı; doğrulama kanıtı; kalan engel veya sonraki görev. Kullanıcıya Türkçe, kod tanımlayıcılarına İngilizce kullan.

## İsteğe bağlı araçlar

Superpowers/Graphify gibi araçların kurulu olduğunu varsayma. Varsa ve işe uygunsa kullan; yoksa aynı keşif, planlama, test ve inceleme adımlarını standart araçlarla yap. Bu dosya subagent veya paralel ajan çalışması zorunluluğu getirmez.
