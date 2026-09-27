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

| ID | Soru | Önerilen başlangıç / seçenek | Son karar zamanı | Durum (28.09.2026) |
|---|---|---|---|---|
| Q01 | Yeni repo mu mevcut repo mu; hangi SDK/test sistemi? | Gerçek ortamı incele; uyumlu sürümü kilitle | M0, M1 engeli | **Çözüldü → D20, D21** |
| Q02 | RNG ve determinism garanti kapsamı? | Sürümlü algoritma + başlangıçta kilitli runtime; platformlar arası ayrıca test | M0, M1 engeli | **Çözüldü → D22, D23** |
| Q03 | Kimlik, snapshot ve zaman birimi? | Tutarlı Guid/strong ID, immutable giriş, integer ms | M0/M1 | **Çözüldü → D24, D25** |
| Q04 | Sade profil mi tam NBA/FIBA mı? | 06 belgesindeki açıkça sade profil | M0/M2 | **Çözüldü → D31** |
| Q05 | Start possession ve periyot açılışı? | Home/away simetrisini koruyan açık protokol | M2 | **Çözüldü → D32** |
| Q06 | Tüm olasılık katsayıları ve eylem süreleri? | Baseline config v0.1, ölçümle tuning | M2/M4 | **Kısmen çözüldü → D35, D39.** Yapı ve başlangıç değerleri kilitli; **sayısal kalibrasyon M6'ya kaldı** |
| Q07 | Bonus, foul-out, az oyuncu terminal policy? | Sade profil; forfeit/abort farkı açık | M3 | **Çözüldü → D40, D41, D42, D43** |
| Q08 | Defense enum mu scheme+coverage mı? | Dışarıda dört seçenek, içeride policy paketi | M4 | **Çözüldü → D57** |
| Q09 | Energy sıfır endpoint'i, drain/recovery ve FT etkisi? | Her biri config ve testle tanımlanır | M4 | **Çözüldü → D58, D62** |
| Q10 | GameForm dağılımı ve birimi? | Başta kapalı; sonra bounded model | M4/M6 | **Kısmen çözüldü → D60 (M4'te kapalı). M6 kapsamı dışında bırakıldı (D98b); dağılım/birim hâlâ açık** |
| Q11 | Timeout hakkı, legal pencereler, late game? | 06/07 taslağı üzerinden netleştir | M5 | **Çözüldü → D80, D81, D84.** Clutch yok; motor yönetir; 4 tam timeout, son 2'si son 2 dakikada, uzatma +1. **Sapma:** timeout canlı topta uygulanmaz |
| Q12 | Canlı maç kaç gerçek dakika sürmeli? | Simülasyon hızından bağımsız ürün ayarı | M7 | Açık |
| Q13 | PostgreSQL/MSSQL, auth ve hosting? | Ekip/ortam ve gerçek gereksinimle seç | M7 | Açık — M1'i engellemedi |
| Q14 | Oyuncu kopyası/instance ve kadro büyüklüğü? | Engine fixture'ından ürün kuralı çıkarma | M7 | Açık — M1'i engellemedi |
| Q15 | Başlangıç bütçesi, ödül, scout, salary cap? | Basit AI loop, ücretler ayrı karar | M10 | Açık |
| Q16 | PvP MVP şartı mı? | Önce AI rakip; PvP ayrı milestone | M7 öncesi ürün kapsamı | Açık |
| Q17 | Gerçek veri, günlük update ve ekonomi algoritması? | Fictional başlangıç; provider adapter sonra | M11+ | Açık |
| Q18 | Sezon referansı ve sayısal release eşikleri? | Veri seti seç + fixture koşulları + holdout | M6 | **Çözüldü → D98c.** Gerçek veri referansı **yok**; eşikler **iç tutarlılıktan** türetilir (mirror simetri, uç değer güvenliği, dominant strateji yokluğu, kalite farkı yönü, tempo sırası, home/away yer değiştirme). NBA sezonuyla karşılaştırma raporda **bulunmaz** |

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
| D30 | M1'de composite rating, OVR, tactics, pace, stamina ve maç akışı yok | Kullanıcı onayı | 09 roadmap M1 kapsamı. `MatchSetup` bu alanları **M4'te** `TeamMatchSetup` ile ekleyecek — aşağıda D34 bu kaydı düzeltir. |

## 7. M2 kararları — 27 Eylül 2026

Kullanıcı 27 Eylül 2026'da `docs/plans/M2_IMPLEMENTATION_PLAN.md` dosyasını ve D31–D33'ü onayladı; ardından M2 uygulama yetkisini verdi. D34–D39 uygulama sırasında alınan kararlardır.

| ID | Karar | Durum | Gerekçe ve kabul kanıtı |
|---|---|---|---|
| D31 | Kural profili: **sade NBA-esinli** — 4 × 12 dk, 24 sn hücum saati, 5 dk uzatma (tanımlı ama kullanılmıyor) | Kullanıcı onayı | 06 §1'deki H08 devir önerisi. Tam NBA sadakati iddiası yok; bonus/foul-out M3, timeout M5'e kaldı. |
| D32 | Periyot açılışı: seeded RNG **tek bit** (`NextUInt64() & 1`) | Kullanıcı onayı | 06 §4 "home/away bias yaratmayacak seeded yöntem". Modulo bias yok. Test: `FirstPossessionIsDecidedByASeededSingleBitDraw`. |
| D33 | Simulator tek maç, **argümansız**, fixture C# kodu | Kullanıcı onayı | 09 M2 "console tek maç". `ImmutableArray` JSON serileştirme riski M2'de ölçeklenmez. |
| D34 | M2'de tactics/pace `MatchSetup`'e **eklenmedi** — *D30 düzeltmesi* | Uygulama kararı, kullanıcı bilgilendirildi | M4'te tactics'in davranışı olacak. M2'de eklemek sonucu değiştirmeyen ölü veri olurdu. 05 §3 etkisiz mekanizmaların gizlenmesini yasaklıyor. **M2 `MatchSetup`'a hiç dokunmadı.** |
| D35 | Aksiyon → (şut türü, beceri attribute'ü) eşlemesi M2'de kilitlendi; beş oyuncu da her aksiyona uygun, pozisyon dışı ceza **yok** | Uygulama kararı | 05 §5 seçim fonksiyonunun M2 planında kilitlenmesini şart koşar. 02 §5 gizli ceza uydurulmamasını ister. Ağırlık = attribute + 1, toplam sıfır olamaz. |
| D36 | Motor çekirdeği `MatchSimulation` olarak adlandırıldı | Uygulama kararı (zorunlu) | Test namespace'i `DreamTeam.MatchEngine.Tests` iken `MatchEngine` identifier'ı namespace'e çözülüyor (CS0118 derleme hatası). 03'te "runner" ayrı kavram olarak ayrılmış durumda. |
| D37 | Event sözleşmesi: tek zarf + `Type` ayırıcı + türüne özgü payload; `ActionCompleted` türü eklendi | Uygulama kararı | 07 §1'in gerçek zarf yapısı. `ActionCompleted` şıtsız biten aksiyonu temsil eder; onsuz saat ilerlemesi event akışında görünmez kalıyor ve motorun "ilerleme yok" güvenlik ağına takılıyordu. |
| D38 | `Create` doğrulanmış setup ister ve program hatasında `InvalidOperationException` atar; `Simulate` doğrular, `Aborted` sonuç döner | Uygulama kararı | D26 korunur: kullanıcı girdisinin reddi `MatchSetupValidationResult` ile bildirilir, istisnayla değil. Geçersiz setup'tan `TeamMatchState` kurulamayacağı için "Aborted state" yerine bu ayrım seçildi. |
| D39 | `ConfigHash` = sabit sıralı alanlardan üretilen metnin SHA-256'sı (16 hex); JSON canonicalization yok | Uygulama kararı | 03'te istenen setup digest böyle üretilir; 08 §4 byte equality beklemez ama aynı config aynı hash vermelidir. Test: `EveryMeaningfulConfigFieldIsCoveredByTheHash`. |

### M2'de bulunan ve düzeltilen gerçek kod hataları

| Hata | Belirti | Düzeltme | Regresyon testi |
|---|---|---|---|
| `StartPossession` hücum saatini sıfırlamıyordu | İlk hücumu tüketen saat sonraki her hücuma miras kalıyordu: 3 şut denemesi, 349 turnover, 0-5 skor | Yeni hücum 24 s ile başlıyor (06 §6) | `ShotClockIsResetForEveryNewPossession` |
| Takım puanı projector'da iki kez ekleniyordu | Box score 194, gerçek skor 97 | `team.Points` tek yerde artırılıyor | `BoxScoreScoreAgreesWithTheEngineScore` |
| İsabetli şutlarda `3PA` sayılmıyordu | "3P 4-0" gibi imkânsız satırlar | İki sayacın birlikte yürütülmesi | `ShotCountersAreInternallyConsistent` |
| `MatchClock.BeginPeriod` toplam süreyi sıfırliyordu | Rapor 2.880 s yerine 720 s gösteriyordu | `BeginPeriod` örnek metoduna çevrildi, elapsed korunuyor | `EngineGameClockIsMonotonicAndEndsAtZero` |
| Şutla sonuçlanmayan aksiyon event üretmiyordu | Motor "ilerleme yok" güvenlik ağıyla abort oldu | `ActionCompleted` event'i eklendi | `EveryProducedEventIsObservable` |

## 8. M3 kararları — 27 Eylül 2026

Kullanıcı M3'ün kilit kararlarını 27 Eylül 2026'da onayladı. Uygulama kodu yazılmadı.

| ID | Karar | Durum | Gerekçe |
|---|---|---|---|
| D40 | **Bonus: basit profil.** Periyot içinde 5. sayılan savunma faulünden itibaren 2 FT. Kişisel faul sınırı **6**; 6. faulde oyuncu sahadan çıkar | Kullanıcı onayı | 06 §1 devir önerisi. NBA 3 saniye kuralı ve son 2 dakika istisnaları kapsam dışı; tam NBA sadakati iddiası yok. |
| D41 | **Foul-out sonrası otomatik yedekleme**: en uygun yasal yedek sahaya girer; yasal yedek yoksa açık terminal policy | Kullanıcı onayı | 06 §7 devir önerisi. Kullanıcı çevrimdışı kalsa maç sonsuza kadar durmaz. |
| D42 | **Tie-break ve periyot başı**: `releaseTime < expiryTime` geçerli, eşitlikte ihlal. Her uzatmada hücum saati 24 s'e döner, takım faul sayacı sıfırlanır | Kullanıcı onayı | 06 §1 ve §2 devir önerisi. 06 §6'daki belirsiz "periyot başı" satırını kapatır. |
| D43 | Yasal yedek yoksa `MatchAborted` (`NoLegalSubstitute`). Motor **forfeit kazananı uydurmaz** | Kullanıcı onayı | Forfeit basketbol kuralı değil ürün kararıdır; motor kural motorudur. 06 §121 gereği bu maçlar win-rate paydasına katılmaz. M7'de ürün kararına dönüşebilir. |
| D44 | M3'te değişecek beyan edilmiş sözleşmeler: `MatchPhase` +2 faz, `MatchState` +2 nullable alan, `TeamMatchState` +`FoulOutPlayerIds` +`Fouls`, `PossessionEndReason.BonusFreeThrows`, `EventSchemaVersion` 1→2 | Uygulama kararı — uygulandı | 06 §3 ana akışı bu fazları gerektirir; M5 replay'in serileştireceği devam edilebilir durum bunlar. `MatchSetup`, `MatchClock`, `Advance` imzası, `MatchSetupValidator` **dokunulmadı**. `PossessionEndReason.BonusFreeThrows` 06 §4 tablosunda eksikti. |
| D45 | M3 yeni RNG çekilişleri ekler; **M2 sonuçları değişir** | Uygulama kararı | Beklenen. Hiçbir test golden sabit içermez — aynı build içinde iki koşu karşılaştırılır. M6'da golden sequence sabitlenecek. |

## 9. M3 uygulama kararları — 27 Eylül 2026

Uygulama sırasında alınan kararlar. Kullanıcı onayı gerektirmeyen, kural motorunun
kendisine ait kararlar.

| ID | Karar | Durum | Gerekçe |
|---|---|---|---|
| D46 | **Takım faulu her periyotta sıfırlanır**, yalnız uzatmada değil. D42'nin uzatma şartı bunun alt kümesidir | Uygulama kararı — D42'nin sınırlandırılması/ netleştirilmesi | `TeamFoulsThisPeriod` bir periyot sayacıdır. 4. periyottan devreden sayı 5. periyotta erken bonusa yol açıyordu; bu bir uygulama hatasıydı. NBA periyot içi bonus kuralı da böyledir. Plan §5'teki "normal periyotta korunur" ifadesi düzeltildi. |
| D47 | **"En uygun yedek" = BasketballIQ azalan, Stamina azalan, kanonik kadro sırası** | Uygulama kararı | 18 attribute ortalaması ikinci bir attribute tablosu gerektirirdi; bu da M1'in `MatchSetupValidator`'ını değiştirmeyi gerektirirdi. İki attribute deterministik, ucuz ve açıkça yer tutucu. M4'te gerçek rol mantığına bağlanacak. |
| D48 | **FT sayısı bırakma anında değil settlement'ta hesaplanır.** `PendingShot.FreeThrowCount` alanı yok | Uygulama kararı | And-one sonucu şutun isabetine bağlıdır; bırakma anında bilinmez. Yanlış yerde tutmak yanlış FT sayısı üretirdi. `FoulResolver.FreeThrowCountFor` tek doğru yerdir. |
| D49 | **Hücum saati kontrolü iki dalın ortak noktasıdır**: aksiyonun canlı süresi tüketildikten sonra, şuta dönüşmeden önce | Uygulama kararı — gerçek hata düzeltmesi | Yalnız şut dalında kontrol edildiğinde, şuta dönmeyen hücumlarda ihlal hiç kaydedilmiyor ve hücum sıfır saatte devam ediyordu. |
| D50 | **Foul türü çekilişleri `ShotAttempt` yayınlanmadan önce çözülür** | Uygulama kararı — gerçek hata düzeltmesi | Hücum faulü düdük bırakmadan çalar; şut denenmez. Önce `ShotAttempt` yayınlanırsa settlement'sız şut açılır ve tek aksiyon iki kez sayılır (05 §127'nin yasakladığı çift muhasebele). |
| D51 | **`Foul` event'i `actionId` taşır** | Uygulama kararı | Tüketici bir faulu ilgili şuta bağlamak için korelasyon gerekiyor; `PossessionId` tek başına yetersiz (possession içinde birden fazla aksiyon olabilir). 07 §2 bunu ayrıca istemiyor ama payload'sız korelasyon mümkün değil. |
| D52 | **`ApplyFoul` sonrası terminal durum kontrolü zorunludur** | Uygulama kararı — gerçek hata düzeltmesi | Foul-out yasal yedek yoksa `Abort` üretiyor; ardından yazılan `PendingShot`/`Turnover` terminal durumu eziyordu. `NullReferenceException` ve "ilerleme yok" abortu. |
| D53 | **Foul-out yedeklemesi sahadaki beşi yeniden doldurur** (kısma/sınırlama yok) | Uygulama kararı — gerçek hata düzeltmesi | `Take(OnCourt.Length)` her foul-out'ta sahadaki sayıyı azaltıyordu; 5. foul-out'ta `ActionSelector` "sahada oyuncu yok" ile duruyordu. |
| D54 | **`DeadBall` fazı hiçbir adım sınırında kalıcı değildir** | Uygulama kararı | Inbound, onu doğuran event'le aynı adımda atomik çözülür. Faz kalıcı olsaydı event üretmeyen bir adım oluşur ve motorun "ilerleme yok" ağı devreye girerdi. Faz sözlükte tanımlı kalır (04 sözlüğü), akışta kullanılmaz. |
| D55 | M2 testlerinin "ilk beş" invariant'ı "kadro" sınırına genişletildi (`OnlyRosterPlayersAppearInTheBoxScore`, `EveryEventAttributedToAPlayerIsInTheRoster`) | Uygulama kararı | M3'te foul-out yedeklemesi oyuncuyu değiştirir; değişmeyen sınır kadrodur. Test sayısı korundu, yalnız iki testin öncülü netleşti. |
| D56 | `FoulProbabilityPerAction = 0.12` başlangıç değeri | Uygulama kararı — **kalibre değil** | 0.05 ile gözlenen takım başına PF ~9 idi (hedef aralık dışı). 0.12 ile takım başına 10–15 PF. KALİBRE EDİLMEMİŞ; M6 ölçümü. |

## 10. M4 planlama kararları — 27 Eylül 2026

Kullanıcı M4'ün kilit kararlarını 27 Eylül 2026'da onayladı. **Uygulama kodu
yazılmadı**; `docs/plans/M4_IMPLEMENTATION_PLAN.md` hazır ve uygulama yetkisi bekliyor.

| ID | Karar | Durum | Gerekçe |
|---|---|---|---|
| D57 | **Savunma: dört paket policy.** `ManToMan`, `Drop`, `Switch`, `ZonePackPaint`; her biri kendi içinde eşleşme + PnR coverage + paint/closeout tercihini taşır | Kullanıcı onayı | Q08'in devir önerisi (05 §6). 02 §6'daki dört UI seçeneğiyle birebir uyum. İç model netleşir, dış model basit kalır. |
| D58 | **Yorgunluk cezası yalnız isabet logitsine (`z`) girer**: `z = logit(base) + βSkill·skill + βQuality·quality − βFatigue·fatigueLoad` | Kullanıcı onayı | Q09. 05 §7 şablonu. `PlayerRatingCalculator` imzasında `energy` **yoktur**; bu yüzden 05 §12'nin "iki kez cezalandırma" yasağı **yapısal** olarak sağlanır — aynı etkiyi iki kanaldan geçecek kod yolu yoktur. Savunma ve taktik ayrı kanaldan `ShotQuality`'ye girer. |
| D59 | **Tempo iki kanaldan geçer**: aksiyon süresi çarpanı (Slow 1.35 / Normal 1.0 / Fast 0.78) + enerji drain çarpanı (0.88 / 1.0 / 1.15). **Top kaybı etkisi yok** | Kullanıcı onayı | 05 §131 pace'i top kaybı risk faktörü sayıyor ama "her durumda top kaybı yaratmak zorunda değildir" diye uyarıyor. Top kaybı kanalı savunma policy'sine ait (D57); üçüncü kanal çift sayma riskini yükseltirdi. |
| D60 | **GameForm M4'te kapalı.** `PlayerMatchState` form alanı içermez | Kullanıcı onayı | Q10'un devir önerisi (05 §13). D34'teki "etkisiz mekanizmayı gizleme" yasağı: 0 olan bir alan eklemek ölü veridir. Dağılım/birim M6'ya açık kalır. |
| D61 | **`TeamMatchSetup` tipi materialize edilir.** `MatchSetup.Home`/`Away` `Team` → `TeamMatchSetup`; `HomeLineup`/`AwayLineup` kaldırılır | Uygulama kararı — plan onayına bağlı | 04 §25 `TeamMatchSetup`'i "Team snapshot, başlangıç lineup, tactics, pace" olarak tanımlar ve "başlangıçta geçerli" işaretler. Düz alanlarla devam edilirse M5'te `MatchSetup`/`TeamMatchState` alan paralelliği doğar ve hangisinin yetkili olduğu belirsizleşir. **Maliyet: M1–M3 testlerinde mekanik güncelleme, anlamsal değişiklik yok.** |
| D62 | **`Balanced` dağılımı 05 §5'in PickAndRoll örneğini birebir alır** | Uygulama kararı | 05 §5 varsayılan dağılımı PickAndRoll olarak verir. M3'ün düz vektörünü (0.45/0.15/0.15/0.10/0.10/0.05) "tarafsız" bir dağılımla değiştirmek kaynağı olmayan bir tercih olurdu. **M3 davranışı aynen korunur**; taktik etkisi diğer üç dağılımla ölçülür. Yansızlık `NeutralMirror` fixture'ının konusudur, `Balanced` taktiğinin değil. |
| D63 | **Enerji performans çarpanı 05 §12 tablosuna `Energy 0 → 0.65` ankrajı eklenir**; aralar doğrusal interpolasyonla doldurulur, `Energy` her zaman `[0,100]`'e kırpılır | Uygulama kararı | 05 §12 tablosu 10'da bitiyor ve "0 energy endpoint'i açık" diyor. Sıfır bir **nokta** değil bir **taban** olmalı; aksi halde son anda ani ve adaletsiz bir çöküş olur. Tablo `FatigueModel`'de **veri** olarak tutulur, formül kodda değil. |
| D64 | **Enerji yalnız şut kanalına girer.** Asist/pas, ribaund, turnover, blok çekilişlerine yayılmaz | Uygulama kararı | 05 §12 tek bir yorgunluk cezası istiyor. Yaymak kan sayısını çoğaltır ve kalibrasyonu bulanıklaştırır. |
| D65 | **Sıralama ağırlığı**: `weight = max(0.01, 1 + SelectionSpread × NormalizeSkill(composite))`, `SelectionSpread = 0.6` | Uygulama kararı | 05 §76 "ham rating çarpanı aşırı yoğunlaşma üretir" diyor. M3'ün `attribute + 1` kuralı bu formülün `SelectionSpread = 1.0` halidir; 0.6 ile daraltılmıştır. Toplam sıfır olamaz ve hiçbir oyuncu seçilemez olmaz garantileri korunur. |
| D66 | **M4'te olmayan savunma/çözüm mekanizmaları kayda geçirildi**: steal atfedimi, transition aksiyonu, mismatch, takım ribaundu, enerji→asist | Uygulama kararı | Hiçbiri 09'un M4 kabul listesinde sayılmıyor. Sessizce düşürülmüyor — gerekçesiyle listeleniyor. Steal ve transition M5/M6 adayıdır. |
| D67 | **`ShotAttemptPayload` +`ShotQuality` +`ShooterEnergy` alanları** | Uygulama kararı | M4'ün kabul kriteri "policy değişimi beklenen karışımı etkiliyor" — kalite ve enerji event'ten **gözlenebilir** olmazsa bu test yazılamaz. 08 §88'in istediği enerji dağılımı ve M6 kalibrasyonu da bu alanlara bağlı. İki `int`; event bellek maliyeti ihmal edilebilir. |
| D68 | **Birincil savunmacı tek çekilişle seçilir** ve faul, blok, kalite eşleşmesi için **aynı kişi** kullanılır; koşula bağlı savunmacı çekilişleri kaldırıldı | Uygulama kararı — gerçek hata düzeltmesi | M3'te bir aksiyonda iki ayrı savunmacı çekilişi vardı (`PickDefender` faul için, blok kendi ağırlığıyla). Bu 05 §127'nin yasakladığı "aynı olayı iki kez örnekleme" desenidir ve faul ile bloğun farklı kişilere yazılmasına yol açabilir. M4'te çağrı sayısı **sabit +1** olur (her aksiyonda bir savunmacı), koşul bağımlı konum kaymaları biter. |

## 11. M4 uygulama kararları — 27 Eylül 2026

Uygulama sırasında alınan kararlar. Hepsi motor kuralına aittir; ürün kararı
değildir.

| ID | Karar | Durum | Gerekçe |
|---|---|---|---|
| D69 | **Şut denemesinde shooting olmayan savunma faulü de kaydedilir.** Daha önce yalnız `isShooting` doğrulandığında `ApplyFoul` çağrılıyordu; kalan savunma faulleri sessizce kayboluyordu | Uygulama kararı — **M3'ten kalan gerçek hata düzeltmesi** | Kisisel faul sayacı, takım faul sayacı ve bonus hiç tetiklenmiyordu. Gözlenen takım başına PF 2/5 idi; düzeltmeden sonra 16/13. |
| D70 | `PlayerRatingCalculator` imzasında ve `PlayerRatingTables`'da **enerji yoktur**; yalnız statik `PlayerRatings` okunur | Uygulama kararı | 05 §12'nin "iki kez cezalandırma" yasağını **yapısal** kılar: aynı etkiyi iki kanaldan geçirecek kod yolu yoktur. `RatingCalculationNeverSeesEnergy` testi bunu yansımayla da doğrular. |
| D71 | **Savunma kalite cezaları ve IQ terimi 0-100 kalite puanı ölçeğinde** | Uygulama kararı — gerçek hata düzeltmesi | `ShotQualityResolver` kaliteyi tam sayı döndürüyor. 05 §6'nın 0-1 ölçeğindeki cezaları doğrudan taşımak farkı yuvarlamada yok ediyordu: Drop PnR 0.07 ile Switch 0.02 ikisi de 52'ye yuvarlanıyor, yani **dört savunma policy'si arasındaki fark gözlenemiyordu**. Ölçek puana çevrildi: Drop −7, Switch PnR −2, Switch Isolation −6, Zone Drive/Post −5, Zone SpotUp +4, ManToMan PnR −3. Yönler 05 §6'dan, sayılar kalibre edilmemiş. |
| D72 | **`PlayerMatchState.Energy` ve `SecondsOnCourt` `double`** | Uygulama kararı — gerçek hata düzeltmesi | İkisi de hesap sırasında yuvarlanınca kayboluyordu. Enerji: 8 saniyelik dilimlerde `Math.Round(99.98) = 100`, yani enerji **hiç düşmüyordu**. Süre: her 1500 ms'lik şut uçuşu 0.5 s kaybediyordu. |
| D73 | **Periyot arası toparlanma süresi, önceki periyotun tam süresidir** — oyun saatinden okunmaz | Uygulama kararı — sessiz etkisiz mekanizma düzeltmesi | Periyot bittiğinde oyun saati zaten 0'dır; saatten okumak toparlanmayı daima sıfır bırakıyordu. 05 §3'ün "etkisiz mekanizmayı gizleme" yasağı. |
| D74 | **Faul ve turnover olasılığı `[0,1]`'e kırpılır** — keyfî 0.95 tavanı yok | Uygulama kararı | Uydurulmuş bir tavan, `FoulProbabilityPerAction = 1.0` kullanan M3 fixture'larını sessizce zayıflatıyordu ve M4'te gerçek bir hataya yol açtı. |
| D75 | Enerji katsayıları yeniden ölçeklendi: `BaselineDrainPerSecond` 0.0055 → **0.08**, `BaselineRecoveryPerSecond` 0.0042 → **0.012**, `BreakRecoveryPerSecond` 0.02 → **0.04** | Uygulama kararı — **kalibre değil** | İlk değerler 48 dakikada ~3 puan kaybettiriyordu, yani eğri hiç görünmüyordu. Hedef: Stamina 78'de tam maç başına ~50 puan. |
| D76 | **`TeamMatchState.Bench` türetilmiş**; ayrı liste M5'te | Uygulama kararı | İki kaynak (ayrı liste + "sahada olmayan") riskini önler. 04 §28'deki ayrı alan M5'in substitution işidir. |
| D77 | `MatchResult.PlayerEnergy` ve `HomeOverall`/`AwayOverall` eklendi | Uygulama kararı | 08 §88 "oyuncu dakika/enerji dağılımı" ister. OVR motor tarafından **raporlanır ama hiç okunmaz** (T03). |
| D78 | **Uzatma üst sınırı tanımsız bırakıldı.** Puan atılamayan fixture'da 0-0 beraberlik uzatmayı sürdürüyor; motor yalnız eylem guard'ı ile kesiyor | **Açık — karar verilmedi** | 08 T10 "guard → Aborted" dediği için mevcut davranış spec'e uygun. Ama 521 periyot üretmek bir kalite sorunudur ve **ürün kararıdır**: uzatma sayısı sınırlansın mı, sınırdan sonra `Aborted` mi yoksa beraberlik mi kabul mü? M5'e girmeden yanıtlanmalı. Kayda geçti, uydurulmadı. **M5 planlama oturumunda D79 ile kapandı.** |

## 12. M5 planlama kararları — 27 Eylül 2026

Kullanıcıya sorulan dört ürün sorusunun yanıtları ve motor tarafında alınan
kararlar. **Kod yazılmadı**; `docs/plans/M5_IMPLEMENTATION_PLAN.md` yazıldı.

| ID | Karar | Durum | Gerekçe |
|---|---|---|---|
| **D79** | **Uzatma üst sınırı 2** (toplam en fazla 6 periyot). Sınıra gelindiğinde hâlâ eşitse maç **`Aborted`**, kazanan uydurulmaz. `RulesProfile.MaxOvertimePeriods`, `ConfigHash`'e yazılır | Kullanıcı kararı — **D78 kapandı** | 0-0 beraberlikte 521 periyot üretmek kalite ve hız riski. 08 T10 "guard → Aborted" yönüne uygun kalır, ama guard'a kadar 521 periyot beklemek yerine açık bir sınır var. `IsTie = false` kalır (06 §8: yarım kalan maç beraberlik sayılmaz). |
| **D80** | **Clutch yok.** Motor son dakikalarda oyun davranışını değiştirmez | Kullanıcı kararı | 05 §'te böyle bir kural yok; uydurmamak doğru cevap. Clutch bir UI/denge konsepti olarak kalır. Timeout sayısı/penceresi ayrı konu (D84). |
| **D81** | **AI fallback: motor yönetir.** Taktik/tempo/substitution kararlarını motor verir; yönetici istediği anda müdahale eder, "devraldım" modu yok | Kullanıcı kararı | 07 §8 bu politikayı açıkça ürün kararına bırakmıştı. M6 toplu koşu da aynı yolu kullanır. |
| **D82** | **Substitution penceresi: 5+1 nokta.** İsabetli basket, hücum değişimi (turnover/steal), serbest atış serisi sonu, hücum saati ihlali, düdük sonrası faul ve devre arası. **DREB/steal sonrası oyun canlıdır → pencere yok.** | Kullanıcı kararı — 06 §7 kapandı. **Uygulama sırasında netleştirildi (D96)** | Soru etiketi "normal basket sonrası **açılmaz**" derken açıklaması isabetli basketi pencere olarak sayıyordu; belirsizlik oluştu. Kullanıcı açıklamayı onayladı: **pencere AÇILIR.** 06 §7'deki "DREB/steal sonrası oyun canlıdır" kuralı korunur. |
| **D83** | **20 saniyelik timeout'un tipi motorun bilir, süresi M7'nin.** `TimeoutKind.Full` ve `TimeoutKind.Short20` ikisi de sayaçtan düşer; 20 saniyenin duvar saati süresi canlı runner'da yaşar | Kullanıcı kararı | Motor duvar saati tutmaz (03). 20 saniye bir gerçek-zaman kavramıdır; motorda karşılığı yoktur ve uydurulmamalıdır. |
| **D84** | **Timeout: takım başına maçlık 4 tam, son 2'si yalnız düzenleme periyodunun son 2 dakikasında; uzatma başına +1.** Canlı saati tüketmez, hücum saatini başlatmaz. **Uygulama yalnız dead-ball sınırında** | Kullanıcı kararı — **Q11 kapandı** | 06 §18'in "ilk öneri 4" değerini kullanıcı onayladı. **Bilinen sapma:** NBA'da timeout son iki dakikada canlı top anında çağrılabilir; M5'te bu yapılmaz çünkü canlı possession'ı kesmek yeni bir `PossessionEndReason.Timeout` ve possession sayacı kayması demektir. 06 §23 "NBA profili etiketi bu ayrıntıları içermez" diyor. |
| **D85** | Substitution komutu **çıkan ve giren oyuncuyu açıkça adlandırır**; motor kimi çıkaracağını tahmin etmez | Uygulama kararı — 07 §5 | 07 §5 `Substitute` komutunun payload taşıdığını söylüyor. Otomatik çıkarma zaten foul-out yolunda var (M3, `EligibilityPolicy`). |
| **D86** | Bekleyen komut sırası **`AcceptedOrder` ile FIFO**; aynı tip taktik komutlarında last-write-wins doğal olarak çıkar; ikinci substitution ilk uygulanmış lineup'e karşı **yeniden** doğrulanır | Uygulama kararı — 07 §6 | 07 §6 "FIFO veya last-write-wins seçilir ve replay kaydına yansır; sessizce keyfî seçme" diyor. `AcceptedOrder` istemci tarafından belirlenemez (07 §5). |
| **D87** | **M5, `MatchStateFingerprint` yardımcısı ekler ve tüm durum karşılaştırmalarını onunla yapar.** `Assert.Equal(state, restored)` **yazılmayacak** | Uygulama kararı — **ölçülmüş hata** | `ImmutableArray<T>.Equals` **referans eşitliğidir**; bu yüzden `record` üretici eşitliği `Team`, `TeamMatchSetup`, `MatchSetup` ve `MatchState` için bozuktur. Bu oturumda ölçüldü: aynı id + aynı roster içeren iki ayrı `Team` örneğinde `Equals` = **False**; `ImmutableArray` içermeyen `Player`'da = **True**. `Team` JSON round-trip bayt aynı ama `Equals` yine `False`. T16 ("restore sonrası aynı devam") bu yüzden `Assert.Equal` ile test **edilemez**: hem yanlış negatif hem de yanlış pozitif verir. **M7'de kalıcı katmanda çözülmeli.** |
| **D88** | `System.Text.Json` **`net10.0` sınıf kütüphanesinde sıfır NuGet paketiyle** kullanılabilir; `ImmutableArray<T>`, `required`+`init` record round-trip'i çalışır. Enum'lar varsayılan **sayısal** serileştirilir | Ölçüm, karar değil | Bu oturumda geçici bir prob dosyası yazılıp koşturuldu ve silindi. Sonuç: bağımlılık kısıtı bozulmadan replay mümkün. Enum için M5 `JsonStringEnumConverter` (isim tabanlı) kullanacak; aksi halde enum ordering'i bir kez değişse kayıtlı snapshot **sessizce** bozulur. |
| **D89** | `RulesProfile.ShortTimeoutsPerTeam = 3` | **KAPANDI → D98a** (M6 planlaması, 28 Eylül 2026; kullanıcı **5**'i seçti) | D83 yalnız **tipi** tanımladı, sayıyı değil. NBA'da 5'tir ama bu bir sayı uydurmadır. 06 §23'ün "sayılar özel oyun basitleştirmesidir" uyarısının parçası; `ConfigHash`'e girdiği için kolayca değiştirilebilir. |

### Bu oturumda ölçülen (tahmin edilmeyen) bulgular

| Ölçüm | Sonuç |
|---|---|
| `System.Text.Json` sıfır paketle | **Çalışıyor** |
| `ImmutableArray<T>` JSON round-trip | **Çalışıyor** (`byte[]` için hex converter gerekli) |
| `required` + `init` record round-trip | **Çalışıyor** |
| Aynı id + aynı roster, ayrı örnek → `Team.Equals` | **False** (D87) |
| `ImmutableArray` içermeyen `Player.Equals` | **True** (D87'nin kökeni) |
| `TeamMatchSetup.Equals` | **False** (D87) |
| `Team` JSON round-trip | **Bayt aynı**, `Equals` = **False** |
| `IRandomSource.GetState`/`SetState` | **Kesin round-trip**, state = 8 bayt → replay için RNG'de sıfır yeni iş |
| Enum JSON | **Sayısal** (`InsidePost` → `3`) |

### Bu oturumda kapanan açık sorular

- **Q11** (timeout hakkı, legal pencereler, late game) → **D80 + D81 + D84**.
- **06 §7'nin M5'e bıraktığı substitution penceresi kararı** → **D82**.
- **D78** (uzatma üst sınırı) → **D79**.
- **D89** (20 saniyelik timeout sayısı) → **D98a**.

### Hâlâ açık

- **Q10** — GameForm dağılımı/birimi (M6 kapsamı dışında bırakıldı, D98b).
- **Q12** — canlı maç kaç gerçek dakika sürmeli (M7).
- **Q13–Q17, Q19** — DB/auth, oyuncu örneği, kadro büyüklüğü, transfer/draft, PvP MVP, gerçek veri.

## 13. M5 uygulama kararları — 28 Eylül 2026

Motor tarafında alınan kararlar. Hepsi motor kuralıdır; ürün kararı değildir
(D79–D85 kullanıcı kararlarıdır, bölüm 12'ye bakınız).

| ID | Karar | Durum | Gerekçe |
|---|---|---|---|
| **D87** | **`MatchStateFingerprint` yardımcısı eklendi; durum karşılaştırmaları `Assert.Equal` ile YAPILMAZ.** Tüm replay/invariant testleri parmak izi kullanır | Uygulama kararı — **ölçülmüş hata** | `ImmutableArray<T>.Equals` **referans eşitliğidir**; bu yüzden `record` üretici eşitliği `Team`, `TeamMatchSetup`, `MatchSetup` ve `MatchState` için bozuktur. Bu oturumda ölçüldü: aynı id + aynı roster içeren iki ayrı `Team` örneğinde `Equals` = **False**; `ImmutableArray` içermeyen `Player`'da = **True**; `Team` JSON round-trip bayt aynı ama `Equals` yine **False**. T16 (`Assert.Equal(state, restored)`) bu yüzden **yazılamaz**: hem yanlış negatif hem yanlış pozitif verir. **M7'de kalıcı katmanda çözülmeli** (03 §"Match completion idempotent"). |
| **D88** | Enum'lar **isim tabanlı** serileştirilir; `System.Text.Json` **sıfır NuGet paketiyle** kullanılır; `ImmutableArray<byte>` hex olarak yazılır | Ölçüm + uygulama kararı | Bu oturumda ölçüldü: sıfır paketle çalışıyor, `ImmutableArray<T>` ve `required`+`init` record round-trip'i doğru, RNG state 8 bayt. Ama enum varsayılan **sayısal** (`InsidePost` → `3`): ordering'i bir kez değişse kayıtlı snapshot **sessizce** bozulur. |
| **D89** | `RulesProfile.ShortTimeoutsPerTeam = 3` | **KAPANDI → D98a** (M6 planlaması, 28 Eylül 2026; kullanıcı **5**'i, NBA gerçeğini seçti) | D83 yalnız **tipi** tanımladı, sayıyı değil. NBA'da 5'tir ama M5'te bunu bir sayı **uydurmak** olurdu; bu yüzden açık bırakıldı. 06 §23'ün "sayılar özel oyun basitleştirmesidir" uyarısının parçası; `ConfigHash`'e girdiği için tek satır değişir. |
| **D90** | `CommandValidator.ValidateForApplication` taktik/tempo için `null` döner; **throw etmez** | Uygulama kararı — **gerçek hata düzeltmesi** | İlk sürüm `default:` dalında `throw` vardı. ActionDecision sınırı **her aksiyonda** sunulduğu için tüm taktik komutları istisna fırlatıyordu — hiçbir taktik komutu uygulanamıyordu. "Bu türde ek kural yok" demek doğru davranıştır; **tanımsız** tür hata vermeye devam eder. |
| **D91** | `CommandQueue.Settle` boş girdide aynı örneği döner | Uygulama kararı | Gözlemlenebilir "değişiklik yok" durumunu korur. |
| **D92** | `Simulate` kalan komutları **eşleşen komutların kendi indeksleriyle** çıkarır; `RemoveRange(0, n)` **kullanılmaz** | Uygulama kararı — **gerçek hata düzeltmesi** | İlk sürüm filtrelenmiş listeden `n` komut siliyordu; yanlış komutları atıp sonrakileri kaydırıyordu. DeadBall substitution'ı hiç gönderilmiyor, maç sonunda "Expired" ile reddediliyordu. |
| **D93** | `CommandQueue.Settle` verilen komutları kuyruktan **da çıkarır** | Uygulama kararı — **gerçek hata düzeltmesi** | Önceden yalnız `Settled` işaretleniyordu; komut hem "işlendi" sayılıp hem kuyrukta kalıyordu. Doğrudan çağrıldığında aynı komut ikinci kez uygulanabiliyordu. İki kayıt tutarlı olmalı: "işlendi" = "kuyrukta değil". |
| **D94** | **`Advance(state, commands)` çağrısı "şimdi gönderiliyor" demektir.** Kabul edilen komut, sunulan sınır o adımda varsa **AYNI ADIMDA** uygulanır | Uygulama kararı — **gerçek hata düzeltmesi** | Önceden komut bir adım gecikmeli uygulanıyordu. Çağıran "gönderdim" diyordu, etki bir adım sonra oluyordu; artık kuyruğu ayrıca kontrol etmek gerekiyordu. `Simulate` zaten sunulan sınıra göre gönderdiği için bu yolda davranış değişmedi. |
| **D95** | **Süre denetimi kabulden ÖNCE yapılır** (`Advance` girişinde) | Uygulama kararı — **gerçek hata düzeltmesi** | `Simulate` komutu hedef sınıra gelene kadar tutuyor; bu arada `ExpiresAfterSequence` aşılmış olabilir. Denetim kabulde yoksa komut kuyruğa girer ve D94 gereği **hemen** uygulanır — yani "süresi dolmuş" komut yine de etki ederdi. |
| **D96** | **Uygulama sırasında netleşen iki belirsizlik:** (a) isabetli basket sonrası substitution penceresi **AÇILIR** (D82 düzeltmesi); (b) `MatchEventType` **25**'tir, plandaki "24 + 6" **sayım hatasıydı** | Kullanıcı kararı (a) + ölçüm (b) | (a) Soru etiketi ile açıklaması çelişiyordu; kullanıcı açıklamayı onayladı. (b) 7 yeni tür eklendi, 6 değil: `TacticChanged`, `DefenseChanged`, `PaceChanged`, `Substitution`, `Timeout`, `CommandApplied`, `CommandRejected`. Ayrıca `DefenseChanged` **ayrı** event olarak eklendi: 07 §2 "Müdahale" ailesi tek `TacticChanged` ile iki alanı kapsayamaz. |
| **D97** | **`MatchSnapshot` içinde `MatchState` değil, `MatchSnapshotData` DTO'su** vardır; `Restore(config)` config'i dışarıdan alır | Uygulama kararı — **teknik zorunluluk** | `MatchState.Config.ActionProfiles[].Skill` bir `Func<>`'dur ve `System.Text.Json` ile **serileştirilemez**; `Random` bir arayüzdür. Config bir **parametredir**, durum değildir: `ConfigHash` ile doğrulanır (D97'nin uyguladığı gibi). Bu ayrım "hangi alan neden dışarıda kaldı" sorusunu tek yerde yanıtlar. |

### M5 sonrası hâlâ açık

- **D87** — `ImmutableArray<T>` içeren `record`'larda bozuk değer eşitliğinin **M7'de** çözülmesi.
- **Q10** — GameForm dağılımı/birimi (M6 kapsamı dışında bırakıldı, D98b).
- **Q12** — canlı maç süresi (M7).
- **Q13–Q17, Q19** — DB/auth, oyuncu örneği, kadro büyüklüğü, transfer/draft, PvP MVP, gerçek veri.

## 14. M6 planlama kararları — 28 Eylül 2026

Dört soru kullanıcıya soruldu, dördü de cevaplandı. Bunların **tamamı ürün
kararıdır**; motor kuralı veya teknik zorunluluk değildir.

| ID | Karar | Durum | Gerekçe |
|---|---|---|---|
| **D98a** | **`RulesProfile.ShortTimeoutsPerTeam = 5`** (NBA gerçeği). D89 böylece kapandı | Kullanıcı kararı | 06 §23'ün "sayılar özel oyun basitleştirmesidir" uyarısı bu sayıyı bir **kural** olmaktan çıkarıp **kaynaklı bir gerçek** haline getiriyor. M5'te 3 bir yer tutucuydu ve bilinçli olarak açık bırakıldı; kullanıcıya üç seçenek sunuldu (5 / 3 / hiç 20 sn timeout). 5 seçildi. `ConfigHash` değişir (tek satır). |
| **D98b** | **M6 kapsamı = ölçüm + ilk 3 kalibrasyon adımı.** 10K ölçüm → 08 §8 adım 3'e kadar (temel şut/turnover/foul/rebound aileleri **sırayla**) → 100K doğrulama | Kullanıcı kararı | 09 "simulator ve kalibrasyon" diyor ve 08 §8 sekiz adım veriyor. Kullanıcıya üç kapsam sunuldu; orta seçildi. Sonuç: M6 biterken motor **ölçülmüş ve kısmen kalibre edilmiş** olur. 08 §8'in "aynı anda her şeyi değiştirme" kuralı nedeniyle en fazla **iki parametre ailesi** değiştirilir. **Q10 (GameForm) bu kapsamın dışında bırakıldı** ve açık kalıyor. |
| **D98c** | **Q18 KAPANDI: gerçek veri referansı YOK.** 08 §6'nın metrikleri ölçülür, eşikler **iç tutarlılıktan** türetilir: mirror simetri, uç değer güvenliği, dominant strateji yokluğu, kalite farkı yönü, tempo sırası, home/away yer değiştirme. NBA sezonuyla karşılaştırma **raporda bulunmaz** | Kullanıcı kararı | 08 §7: "Şut hedefleri engine spec'tedir; **seçilmemiş NBA sezonunun gerçek ortalaması gibi sunulmaz**." Bu karar eşiği bir sezon verisine değil motorun kendi mantığına bağlar. **Bu "oyun gerçekçi" demek değildir** — sayısal olarak tutarlı ama oyuncuya tuhaf gelen bir motor bu kararla mümkündür. 08 §8 adım 8 (insan playtest'i) M6 dışıdır. Risk kayda geçti, gizlenmedi. |
| **D100** | **T15 (diagnostics) M6'ya dahil.** Diagnostics açık/kapalı → **aynı domain outcome**; diagnostics **RNG tüketmez** ve **`ConfigHash`'e girmez** | Kullanıcı kararı | 08 T15 "M2/M6" diyor ve M2'de yazılamamıştı. M6 motorun **son ölçüm milestone'ı**; kalibrasyon sırasında kural hatalarını sıfırlamak için teşhis aracı gerekiyor. Kapsam sınırı: **yalnız sayaçlar**, yeni karar mekanizması değil — 05 §3'ün "etkisiz mekanizmayı gizleme" yasağını ihlal etmemek için her sayacın raporda **gerçekten gösterildiği** doğrulanacak. |

### M6 planlama sırasında ölçülen gerçek (plan §3)

Plan yazılmadan önce 100K ölçeği **tahmin edilmedi, ölçüldü**: geçici bir prob
testi yazıldı, koşturuldu ve silindi.

- Maç başına **1107 event** (max 1268), maç başına **4.62 ms**, 212.4 possession
- 300 maçta **0 aborted**, 5 uzatma (%1.67)
- 100K = **~462 sn** sıralı (süre sorun değil) ve **110.7 milyon event** (bellek sorun)

**Sonuç:** 08 §119'un "summary mode" zorunluluğu bir optimizasyon değil,
**dayanıklılık şartıdır** — tüm event'ler tutulursa ~21 GB. M6'nın ilk işi
bellek sınırlı akış kipidir.