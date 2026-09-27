# Karar günlüğü, düzeltmeler ve açık sorular

Tarih: 27 Eylül 2026 güncellemesiyle. “Yön” tasarım yaklaşımıdır; kullanıcının her alt maddeyi tek tek onayladığı anlamına gelmez. “Taslak” veya “Devir önerisi” kararı kullanıcı onayı olmadan “Kabul edildi” yapma.

## 1. Korunan yön ve öneriler

| ID | Karar / öneri | Durum | Gerekçe ve kaynak |
|---|---|---|---|
| D01 | Projeyi Codex'te geliştirme; burada ayrı MD devir paketi | Kullanıcı talimatı | Güncel istek |
| D02 | Possession + action tabanlı sonuç üretimi | Yön | Match Engine v0.1 konuşması |
| D03 | Saf C# motor, console önce | Yön | Sunumdan bağımsız test |
| D04 | OVR engine'e girmesin | Yön | Rol ve kadro anlamı |
| D05 | Seeded RNG, komutlarla replay | Yön | Hata yeniden üretimi |
| D06 | Kurgusal ilk oyuncu/takımlar | Taslak kapsam tabanı | MVP içerik yaklaşımı |
| D07 | 18 attribute; dört hücum/dört savunma | Taslak kapsam tabanı | İlk dengeyi yönetilebilir tutmak |
| D08 | Stamina, bench, legal substitution | Yön | Canlı menajerlik deneyimi |
| D09 | React/TypeScript + PixiJS + SignalR + ASP.NET | Teknoloji taslağı | Kullanıcının C# odağı ve 2D sunum |
| D10 | PostgreSQL veya MSSQL | Açık | İkisi birden varsayılan olmayacak |
| D11 | Redis | Ertelenmiş/koşullu | Gereksinim varsa |
| D12 | Market/trade/auction/draft | Ertelenmiş | Önce core loop |
| D13 | Rating/değer geçmişi | Vizyon | Gelecekte tarihçe/grafik |
| D14 | 10K/100K Monte Carlo | Yön/hedef | Henüz çalıştırılmadı |
| D15 | 3D/Three.js | Ertelenmiş | Motor sözleşmesini değiştirmeden |

## 2. Bu devirde eklenen öneriler

| ID | Netleştirme | Durum | Etki |
|---|---|---|---|
| H01 | Stamina attribute, Energy state olarak adlandırma | Devir önerisi | İki kavramın karışmasını önler |
| H02 | Integer ms saatler ve sürümlü RNG state | Devir önerisi | Saat/replay doğruluğu |
| H03 | Aynı Advance çekirdeğinden offline ve live runner | Devir önerisi | Canlı komutları sonradan yamamayı önler |
| H04 | Engine/rules/config/RNG/roster sürümleme | Devir önerisi | Seed tek başına yetersiz |
| H05 | Event replay ve simulation replay ayrımı | Devir önerisi | Eski maçı izlemek ile tekrar hesaplamak farklı |
| H06 | Typed payload ve ShotId/TurnoverId korelasyonu | Devir önerisi | Çift istatistik önleme |
| H07 | CommandId + accepted order + apply boundary | Devir önerisi | Retry ve yarış durumları |
| H08 | NBA esinli sade rules profile | Devir önerisi | Tam NBA sadakati iddiası yok |
| H09 | İlk simetri kalibrasyonunda GameForm kapalı | Devir önerisi | Temel bias'ı ayırmak |
| H10 | Modüler monolit; ilk aşamada distributed altyapı yok | Devir önerisi | Mevcut kapsam için basit başlangıç |

## 3. Önceki taslağın teknik düzeltme kaydı

| Önceki ifade / risk | Bu pakette ele alınışı |
|---|---|
| “Aynı seed aynı maç” | Bütün veri/algoritma/config/komut sırası sürümleri eşit olmalı |
| “Oyuncu seçimi random olmamalı” | Kör eşit random yerine ağırlıklı seeded seçim |
| Raw 0–100 rating'leri sigmoid içine toplama | Merkezlenmiş/ölçeklenmiş girdiler; katsayılar açık |
| Tactic ve defender etkisini iki kat yazma | ShotQuality ile final logit arasında tek attribution |
| Drop için sabit yüzdeler | Tarihsel taslak olarak saklandı; doğru counter matrisi diye uygulanmaz |
| Steal'i turnover nedenleriyle aynı düzeye koyma | Cause + attribution ayrıldı |
| Foul/shot bağımsız terminal dallar | And-one ve FT sequence ayrı çözüldü |
| Her ShotAttempt event'ini FGA sayma | Fiziksel attempt ile sayılabilir attempt ayrıldı |
| Her miss'e rebound atama | Dead-ball/horn/nonfinal FT dışarıda |
| Her possession sonunu dead-ball sanma | DREB/steal canlı devam eder |
| “Normal dağılım [-5,+5]” | Normal sınırsızdır; truncation/ölçek kararı gerekli |
| State'te Momentum, kapsamda momentum yok | Momentum v0.1'de davranış üretmez |
| “%8 efficiency → 56/44 win rate” | Ayrı ölçülen hipotezler; sabit dönüşüm yok |
| “63/37 ise engine kesin bozuk” | Kontrol edilen deneyde bias alarmı; örneklem/fixture incelenir |
| Config değiştir, deployment gerekmez | Algoritma değişimi yine kod gerektirir; aktif maç config'i değişmez |

## 4. Açık sorular — doğru aşamada yanıtlanacak

| ID | Soru | Önerilen başlangıç / seçenek | Son karar zamanı | Durum (27.09.2026) |
|---|---|---|---|---|
| Q01 | Yeni repo mu mevcut repo mu; hangi SDK/test sistemi? | Gerçek ortamı incele; uyumlu sürümü kilitle | M0, M1 engeli | **Çözüldü → D20, D21** |
| Q02 | RNG ve determinism garanti kapsamı? | Sürümlü algoritma + başlangıçta kilitli runtime; platformlar arası ayrıca test | M0, M1 engeli | **Çözüldü → D22, D23** |
| Q03 | Kimlik, snapshot ve zaman birimi? | Tutarlı Guid/strong ID, immutable giriş, integer ms | M0/M1 | **Çözüldü → D24, D25** |
| Q04 | Sade profil mi tam NBA/FIBA mı? | 06 belgesindeki açıkça sade profil | M0/M2 | Açık — M2'de karar verilecek |
| Q05 | Start possession ve periyot açılışı? | Home/away simetrisini koruyan açık protokol | M2 | Açık |
| Q06 | Tüm olasılık katsayıları ve eylem süreleri? | Baseline config v0.1, ölçümle tuning | M2/M4 | Açık |
| Q07 | Bonus, foul-out, az oyuncu terminal policy? | Sade profil; forfeit/abort farkı açık | M3 | Açık |
| Q08 | Defense enum mu scheme+coverage mı? | Dışarıda dört seçenek, içeride policy paketi | M4 | Açık |
| Q09 | Energy sıfır endpoint'i, drain/recovery ve FT etkisi? | Her biri config ve testle tanımlanır | M4 | Açık |
| Q10 | GameForm dağılımı ve birimi? | Başta kapalı; sonra bounded model | M4/M6 | Açık |
| Q11 | Timeout hakkı, legal pencereler, late game? | 06/07 taslağı üzerinden netleştir | M5 | Açık |
| Q12 | Canlı maç kaç gerçek dakika sürmeli? | Simülasyon hızından bağımsız ürün ayarı | M7 | Açık |
| Q13 | PostgreSQL/MSSQL, auth ve hosting? | Ekip/ortam ve gerçek gereksinimle seç | M7 | Açık — M1'i engellemedi |
| Q14 | Oyuncu kopyası/instance ve kadro büyüklüğü? | Engine fixture'ından ürün kuralı çıkarma | M7 | Açık — M1'i engellemedi |
| Q15 | Başlangıç bütçesi, ödül, scout, salary cap? | Basit AI loop, ücretler ayrı karar | M10 | Açık |
| Q16 | PvP MVP şartı mı? | Önce AI rakip; PvP ayrı milestone | M7 öncesi ürün kapsamı | Açık |
| Q17 | Gerçek veri, günlük update ve ekonomi algoritması? | Fictional başlangıç; provider adapter sonra | M11+ | Açık |
| Q18 | Sezon referansı ve sayısal release eşikleri? | Veri seti seç + fixture koşulları + holdout | M6 | Açık |

Q04–Q18'in çoğu M1 için bekleme sebebi değildir. Codex her soruyu bir kerede kullanıcıya yöneltmek yerine aktif milestone'ı etkileyenleri ayırmalı.

## 5. Karar güncelleme biçimi

Her yeni karar: ID, tarih, karar metni, kullanıcı/teknik karar sahibi, durum, gerekçe, etkilenen belge ve kabul testi. Öneri reddedilirse silmek yerine “Yerine geçen: …” bağlantısı ekle. Böylece geçmiş bir taslak yeni oturumda yeniden kesin karar olarak sunulmaz.

## 6. M0 ve M1 kararları — 27 Eylül 2026

Kullanıcı 27 Eylül 2026'da `docs/plans/M1_IMPLEMENTATION_PLAN.md` dosyasını ve aşağıdaki kararları onayladı; ardından M1 uygulama yetkisini verdi.

| ID | Karar | Durum | Gerekçe ve kabul kanıtı |
|---|---|---|---|
| D20 | Target framework `net10.0`; `global.json` ile SDK 10.0.401 kilitlendi | Kullanıcı onayı | Ortamda kurulu tek güncel SDK. Kanıt: `dotnet build` temiz. `rollForward: latestPatch`. |
| D21 | Test framework xUnit 2.9.3 + `Microsoft.NET.Test.Sdk` 17.14.1 + `xunit.runner.visualstudio` 3.1.4 | Kullanıcı onayı | Sürümler `dotnet new xunit` şablonundan geldi ve `dotnet list package` ile doğrulandı. |
| D22 | Oyun RNG'si SplitMix64; algoritma adı/sürümü `RngIdentity` sabitlerinde | Kullanıcı onayı | Referans: Vigna, `splitmix64.c` (public domain, https://prng.di.unimi.it/splitmix64.c). Kabul testi: `SeedZeroMatchesGoldenSequence`. |
| D23 | Deterministik garanti kapsamı: aynı SDK/runtime içinde, aynı seed + aynı çağrı sırası = aynı çıktı. Cross-platform eşitlik M6'da ayrıca ölçülür | Kullanıcı onayı | Platformlar arası floating point eşitliği garanti altına alınmadı; 08 belgesinin "başlangıç garantisi aynı kilitli runtime" yaklaşımı. |
| D24 | Kimlik tipi `Guid`; testlerde 32-hex deterministik üretim | Kullanıcı onayı | Strong ID'nin M1'de getireceği karmaşıklık yok; Guid yeterli. |
| D25 | Snapshot koleksiyonları `ImmutableArray<T>`; dışarıdan gelen listelerin kopyası alınır | Kullanıcı onayı (uygulama kararı) | `IReadOnlyList` yalnız dışarıdan `List` ile beslenirse değiştirilebilirdi. Kabul testi: `SetupIsIsolatedFromExternalCollectionMutation`. |
| D26 | Setup doğrulaması istisna fırlatmaz; `MatchSetupValidationResult` (kod + alan yolu + mesaj) döner | Kullanıcı onayı (uygulama kararı) | 07 belgesi reddedilen komutların sebebinin kaydedilmesini ve M7 API'sinin alan bazlı hata göstermesini gerektirir. Plan taslağı istisna öngörmüştü; bu kasıtlı bir sapmadır. |
| D27 | `EngineIdentity` boş alanları ve motorun yerleşik sürüm/RNG kimliğiyle uyumsuzluğu reddeder | Kullanıcı onayı (uygulama kararı) | Seed tek başına maçı tanımlamaz. Kabul testleri: `IncompleteEngineIdentityIsRejected`, `EngineVersionMismatchIsRejected`, `RngIdentityMismatchIsRejected`. |
| D28 | Solution biçimi `DreamTeam.slnx` (yeni XML solution formatı) | Kullanıcı onayı (uygulama kararı) | .NET 10 SDK'sının `dotnet new sln` şablonu `.slnx` üretir. 03 belgesi her iki biçimi de kabul eder. |
| D29 | Rating aralık doğrulaması modelde değil `MatchSetupValidator` içinde; `IRandomSource` arayüzü kontrollü test double sınırı olarak durur | Kullanıcı onayı (uygulama kararı) | Tek kapı, makine tarafından okunabilir hata kodu üretir. 08 belgesi dallanan kurallar için test double şart koşar; M1'de henüz tüketici yok. |
| D30 | M1'de composite rating, OVR, tactics, pace, stamina ve maç akışı yok | Kullanıcı onayı | 09 roadmap M1 kapsamı. `MatchSetup` bu alanları M2'de `TeamMatchSetup` ile ekleyecek. |
