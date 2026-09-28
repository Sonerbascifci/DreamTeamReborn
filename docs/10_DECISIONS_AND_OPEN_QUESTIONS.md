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
| Q12 | Canlı maç kaç gerçek dakika sürmeli? | Simülasyon hızından bağımsız ürün ayarı | M7 | **Çözüldü → D109** (8 gerçek dakika) |
| Q13 | PostgreSQL/MSSQL, auth ve hosting? | Ekip/ortam ve gerçek gereksinimle seç | M7 | **Çözüldü → D110** (PostgreSQL + JWT bearer) |
| Q14 | Oyuncu kopyası/instance ve kadro büyüklüğü? | Engine fixture'ından ürün kuralı çıkarma | M7 | **Çözüldü → D111** (kişi başına oyuncu, kadro serbest) |
| Q15 | Başlangıç bütçesi, ödül, scout, salary cap? | Basit AI loop, ücretler ayrı karar | M10 | Açık |
| Q16 | PvP MVP şartı mı? | Önce AI rakip; PvP ayrı milestone | M7 öncesi ürün kapsamı | **Çözüldü → D112** (M7'de PvP yok) |
| Q17 | Gerçek veri, günlük update ve ekonomi algoritması? | Fictional başlangıç; provider adapter sonra | M11+ | Açık |
| Q18 | Sezon referansı ve sayısal release eşikleri? | Veri seti seç + fixture koşulları + holdout | M6 | **Çözüldü → D98c.** Gerçek veri referansı **yok**; eşikler **iç tutarlılıktan** türetilir (mirror simetri, uç değer güvenliği, dominant strateji yokluğu, kalite farkı yönü, tempo sırası, home/away yer değiştirme). NBA sezonuyla karşılaştırma raporda **bulunmaz** |
| Q19 | API yetkilendirme modeli, reconnect ve restart/abort politikası? | JWT + sunucu tarafı sahiplik; canlı maç; restart sonrası politika açık | M7 | **Çözüldü → D110, D113** |

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
- **Q12 → D109** (8 gerçek dakika). **Q13 → D110**, **Q14 → D111**, **Q16 → D112**, **Q19 → D110/D113** hepsi kapandı.
- Kalan: **Q15** (bütçe/ödül/scout/salary cap — M10), **Q17** (gerçek veri ve ekonomi — M11+).

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
- **Q12 → D109** (8 gerçek dakika). **Q13 → D110**, **Q14 → D111**, **Q16 → D112**, **Q19 → D110/D113** hepsi kapandı.
- Kalan: **Q15** (bütçe/ödül/scout/salary cap — M10), **Q17** (gerçek veri ve ekonomi — M11+).

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
## 15. M6 uygulama kararlari — 28 Eylül 2026

Uygulama sirasinda alinan kararlar. Hepsi **motor kurali, arac yuzeyi veya
olcum kuralidir**; urun karari degildir (D98a–D100 bolum 14'te).

| ID | Karar | Durum | Gerekce |
|---|---|---|---|
| **D101** | **`ShortTimeoutsPerTeam = 5`** `RulesProfile.SimpleNbaInspired` icine yazildi | Uygulama — **D98a'nin uygulanmasi** | M5'te bilincli olarak kaynaksiz bir yer tutucuydu. Deger `ConfigHash`'e girdigi icin degisim `ConfigHash`'i degistirdi: `2a916d545deaa217` → **`91cbb2e60d78fd9a`**. |
| **D102** | **T15 "acik/kapali" anahtari EKLENMEDI.** T15 "bu yuzeyin eklenmesi hicbir domain sonucunu degistirmedi" anlaminda denetleniyor | Uygulama karari — **plandan bilincli sapma** | Bir config anahtari eklemek o anahtari `ConfigHash`'e sokardı ve "ayni config" anlamini bozardi. Kanit dort bacaktan gelir: (a) M5 istenen simulator ciktisi 120 satirin **114'u bayt ayni**, 6 farkli satirin hepsi bilincli M6 bildirimi; (b) dort tohumun event parmak izi **M5 worktree'sinde** uretilen SHA-256 golden degerleriyle ayni; (c) 100 ve 250 adim sonrasi **RNG durumu** ayni; (d) sayaclar **bos degil** (05 §3 "etkisiz mekanizmayi gizleme" yasagi). |
| **D103** | **Kalibrasyon kaynagi JSON belgedir, C# degil.** `config/engine/baseline.v0.1.json` dogrulanabilir girdi; motorun gomulu `EngineConfig.Baseline` **DEGISTIRILMEDI** | Uygulama karari | 08 §119 "her degisimde once/sonra config hash kaydedilir" diyor. C#'da olsaydi "once/sonra" bir commit olurdu. Motorun fabrika varsayilani M2–M5'in golden testlerine bagli; degistirmek onlari gecersiz kılardi. Iki degerin nerede kullanildigi `M6TestData.Config()` ve `FactoryConfig()` ile acikca yazili. |
| **D104** | **KALIBRASYON HEDEFI 05 §7'nin KENDI araliklaridir** (AtRim %60-68, ClosePost %48-58, MidRange %38-45, ThreePoint %33-39) | Uygulama karari — **D98c ile uyumlu** | 05 §7 bu tabloyu acikca "NBA ortalamasi iddiası olmadan" verir. Yani hedef bizim belgemizdir, dis veri degil. Toplu 2P/3P ortalamasi bu araliklara denetlenemedigi icin **sut turu kirilimi** ayri sayac olarak eklendi (08 §6 "shot type dagilimi"). |
| **D105** | **Sadece IKI parametre ailesi ayarlandi** (D98b): once `ShotModel`, sonra `ActionModel` | Uygulama — **D98b'ye uyum** | 08 §8 "ayni anda her seyi degistirme" diyor. Aile 1: `ThreePointBase` 0.36→0.323→0.293, `AtRimBase` 0.64→0.63. Aile 2: `ShotCompletionProbability` 0.65→0.85. Ikisi de belgenin `Notes` alaninda gerekcesiyle kayitli. |
| **D106** | **`--away-tactics` / `--away-pace` bayraklari eklendi** | Uygulama karari | 08 §5'in savunma matrisi (4 hucum x 4 savunma) iki tarafi ayri ayri kurmayi gerektirir. Tek taraflı `--tactics` ile "hangi eslesme kazaniyor" sorusu sorulamazdi. |
| **D107** | **`--tactics` artik `batch` komutunda da gecerli.** Tek bir `SetupFactory` her iki komutun da yoludur | Uygulama karari — **gercek hata duzeltmesi** | Oneri sadece `single` komutunda uygulaniyordu; `batch` dogrudan `source.Build` cagirip bayragi **sessizce yok sayiyordu**. 1.500 maclik uc farkli taktigin raporlari bayt bayt ayni cikti; savunma matrisi hic olculemedi. Tek mac raporunda taktik degismis gorundugu icin hata ancak iki komut karsilastirilirken yakalandi. |
| **D108** | **`ShotTypeTally.Empty` artik deger uretir**, paylasilan singleton degildir | Uygulama karari — **gercek hata duzeltmesi** | Sayaclar `ref` ile mutasyona ugradigi icin iki yerel degisken ayni nesneyi gosteriyor, ev ve depo toplamlari tek yere karisiyor ve `Empty` surec boyunca birikiyordu. **2.000 macta oranlar yakinsadigi icin hata GORUNMEDI**; 10.000 macta ClosePost **%-67**, MidRange **%+70** gibi imkansiz degerler verdi. |

### M6 uygulama sirasinda OLCULEN buyuk degerler

| Olcum | Deger |
|---|---|
| 10K ayar kosusu (seed-start 1) | 26.8 sn, 373 mac/sn, ev kazanma %49.91 |
| **100K holdout kosusu (sirali)** | **162.2 sn, 616 mac/sn, azami bellek 48.9 MB** |
| **100K holdout kosusu (jobs=4)** | **82.6 sn, 1.210 mac/sn, azami bellek 282.7 MB** |
| **T17 kaniti** | Sirali ve paralel 100K ciktisi: **farkli 6 satir**, hepsi manifestin calisma kaydi (yol, is parcacigi, sure, bellek, hiz). 2.000+ metrik satiri bayt bayt ayni. |
| 100K berraklik | Bitmedigi icin hiz kazanci 1.96x (4 is parcaciginda). Tamamlanmamis paralellik kaybi normaldir. |
| Ortalama skor | Ev 128.73 / Dep 128.75 |
| Takim basina possession / Pace48 | 122.8 / **122.5** |
| Sut turu oranlari (100K) | AtRim %67.57, ClosePost %54.80, MidRange %43.57, ThreePoint %36.31 — ** dortu de 05 §7 araliginda** |
| ORtg | 104.83 |
| TOV/poss / FAUL/poss / OREB% | 0.147 / 0.105 / 0.260 |
| Aborted (100K) | **7 mac (%0.007)** — hepsi D79 uzatma tavani, sebebi raporda acikca yazili |
| Uzatma orani | %2.21 |
| Ev kazanma | %49.80, %95 CI [%49.49, %50.11] — 0.5'i **icermiyor**, simetri esigi GECTI |

### M6'da BULUNAN 8 gercek hata

| # | Hata | Belirti | Duzeltme | Regresyon testi |
|---|---|---|---|---|
| 1 | Fixture rating profili duzlestirildi | Argümansız koşu 112-118/5 periyottan **102-110/4 periyota** düştü; M2–M5 golden sonuçları geçersizleşti | M2'nin per-attribute ofsetleri birebir geri kondu | `TheDefaultMatchIsTheRecordedCalibratedMatch` |
| 2 | **`ActionsWithoutShot` sayacı iki yolda artmiyordu** | `ActionsRun != ShotsAttempted + ActionsWithoutShot` — hücre saati tükenen erken dönüş yolu ve suta dönüşürken faul çözülen yol | Yapısal: `_shotAttemptedThisAction` bayrağı; yeni bir çıkış yolu kimliği bozamaz | `AnActionEitherAttemptsAShotOrIsCountedAsNotDoingSo` |
| 3 | **CLI, değersiz bayrak için değer istiyordu** | `--parallel`, `--summary-only`, `--events` kullanılamaz; `--summary-only` bir sonraki bayrağı yutuyordu | Değersiz bayraklar döngünün başında ele alınıyor | `TheContractFromTheRoadmapParses`, `ParallelImpliesAtLeastTwoJobs` |
| 4 | **`\|\|` kısa devre: savunma taktiği hiç tanınmıyordu** | `--tactics Drop` "geçersiz" hatası veriyordu | İki `TryParse` ayrı deniyor | `TacticOverrideAcceptsAnOffenseOrADefenseName` |
| 5 | **Fixture dosyası yolu, dosyanın içindeki adla eşleşmiyordu** | `--fixture ./x.json` → "dosyada şu ad yok" | Kaynak, dosyanın `Name` alanıyla anahtarlanıyor | `AJsonFixturePathIsAccepted` |
| 6 | **`export-fixtures` bayrakları ayrıştırmıyordu** | `--output` yok sayılıyordu, dosyalar yanlış yere yazılıyordu | Fiş ayrıştırma döngüsüne girdi | `ExportFixturesWritesOneFilePerFixture` |
| 7 | **`DerivedMatchId` geçersiz GUID üretiyordu** | 27 ondalık karakter → `FormatException`; fixture dosyası hiç yüklenemiyordu | 32 karakter (8+8+16) | `EveryCatalogFixtureRoundTripsThroughItsFile` |
| 8 | **`--tactics` toplu koşuda sessizce yok sayılıyordu** | Üç farklı taktigin 1.500 maclik raporu bayt bayt ayni; savunma matrisi ölçülemiyordu | Tek `SetupFactory` her iki komutun yolu | `ABatchRunAppliesTheSameTacticOverrideAsASingleRun`, `DifferentTacticsProduceDifferentBatchResults` |

Ek olarak **rapor tarafında iki hesap hatası** bulundu ve düzeltildi: `Pace48`
iki takımın possession'ını toplayıp **iki katına** çıkarıyordu (211.9 →
**122.5**), ve `Skor StdSap` **sabit 0** yazıyordu — ölçülmemiş bir değeri
ölçülmüş gibi göstermek. Artık margin toplamı ve toplam karelerinden gerçekten
hesaplanıyor (100K'da **18.00**).
## 16. M7 planlama kararları — 28 Eylül 2026

Beş ürün kararı kullanıcıya soruldu, beşi de cevaplandı. Bunların **tamamı ürün
kararıdır**; teknik zorunluluk değildir. Q16 ilk soruda cevapsız kaldı ve
ayrıca soruldu.

| ID | Karar | Durum | Gerekçe |
|---|---|---|---|
| **D109** | **Canlı maç 8 gerçek dakika.** Simüle 48 dakika → hız çarpanı **6.0** | Kullanıcı kararı | 03 "canlı modda ilerleme sınırları gerekir" diyor. 8 dakika, 20 saniyelik timeout'un duvar saati karşılığını ölçülebilir bir değere oturtur ve komut pencerelerini kullanılabilir bırakır. 4 dakika pencereleri çok dar, 12 dakika izlemesi kolay yönetmesi sıkıcı. |
| **D110** | **PostgreSQL + JWT bearer.** Motor ve Domain **sıfır paketle kalır** | Kullanıcı kararı | 09 bunu M7'nin ön koşulu koyuyordu. **Sınır önemli:** paket bağımlılığı yalnız yeni `Infrastructure` ve `Api` projelerindedir; `Application` de sıfır paketle kalır. |
| **D111** | **Oyuncu kişi başına kopya, kadro boyutu serbest** | Kullanıcı kararı | 04'ün kalıcı modeli bunu zaten ima ediyor (`Team` + `RosterEntry` + `Player`). **Sınır:** lineup **5 oyuncu** kalır — bu bir motor kuralıdır (D31), ürün kuralı değil. M6'daki 10 kişilik kadro bir **ölçüm aracıdır**, ürün kuralı değildir (08: "engine fixture'ından ürün kuralı çıkarma"). |
| **D112** | **M7'de PvP YOK, sadece AI** | Kullanıcı kararı | 09'un M7 minimum use case'i "AI maç başlatma" diyor. **Eşzamanlılık altyapısı yine kurulur** — T19 ve "aynı maç iki runner'dan ilerletilmez" kabulü iki bağlantı istiyor. Rakip bulma ve çift taraf sahipliği ayrı iş. |
| **D113** | **Sunucu çökerse maç `Aborted`, kurtarma YOK.** Sebep yazılır, ödül verilmez | Kullanıcı kararı | 09 "restart/abort policy **açık**" diyor — açık olması sürme zorunluluğu değil. 03 kurtarma politikasının sahibini "M7/M11 tasarımı" bırakıyor; kullanıcı kurtarma istemedi. Kurtarma, D87'nin kalıcı çözümünü de gerektirirdi; iki belirsizliği tek milestone'a yığmak yerine ikisi de kayda geçirildi. |
| **D114** | **Q19 soru tablosuna eklendi** | Düzeltme | M6 oturumunda iki "hâlâ açık" listesine Q19 atfı yapılmış ama tabloda **hiç yoktu**. Bu bir belge tutarsızlığıydı; aynı oturumda kapatıldı. |

### M7 planlama sırasında ÖLÇÜLEN ortam gerçeği

Bu bölüm planın en değerli kısmı: **bu ortamda paket indirilemiyor.**

| Ölçüm | Sonuç | Sonuç için ne yapmalı |
|---|---|---|
| `nuget.org` erişimi | **YOK** — `NU1101` | Paket sürümleri **zorla** seçilmeli |
| Yerel NuGet önbelleği | **883 paket** | Gerekli her şey var |
| Planlanan paket setiyle restore | **BAŞARILI** (çevrimdışı) | Sürümler sabitlenebilir |
| `Microsoft.Extensions.Hosting` açık referansı | `NU1510` uyarısı | Referans **verilmeyecek** |
| PostgreSQL 18 kurulu | **Evet** | Entegrasyon testi gerçek DB'ye gider |
| `localhost:5432` | **Dinliyor** | Testler çalışma anında bağlanabilir |
| `Testcontainers.PostgreSQL` | **Yok** | Kullanılamaz; yerel PostgreSQL |

Ölçülen ve kullanılacak sürümler: `Npgsql 10.0.2`,
`Microsoft.AspNetCore.Authentication.JwtBearer 10.0.9`,
`System.IdentityModel.Tokens.Jwt 8.22.0`, `Dapper 2.1.79`,
`Microsoft.AspNetCore.Mvc.Testing 10.0.10`, `Microsoft.AspNetCore.TestHost 10.0.10`.

**Dapper, EF Core değil** (uygulama kararı): 03 generic repository'yi yasaklıyor,
04 tekil kısıtların **şemada görünür** olmasını istiyor. Elle yazılmış SQL'de
`UNIQUE (match_id, sequence)` gözle görülür. Bu bir **gerçek kısıttır**: şema
elle yazılacak, migration aracı olmayacak.

Geçici probe dosyaları (paket denemesi, NuGet erişim testi) yazıldı, koşturuldu
ve **silindi**. Bu oturumda kaynak kodu değiştirilmedi.

### M7'de bilinçli olarak yapılmayacaklar (özet)

PvP (D112) · `web/` istemcisi (M8/M9) · ekonomi/ödül/scout/transfer (M10) ·
`PlayerRatingHistory` (M11) · çoklu sunucu/lease/fencing (03) · Redis (03) ·
maç kurtarma (D113) · `record.Equals` kalıcı düzeltmesi (D87 → M11) ·
EF Core ve migration aracı · hız ayarı ve pause/resume (M8+).

### Planlanan test sayısı ve en kritik test

**Hedef 45–55 test.** En kritik olan:

> **`ALiveMatchProducesTheSameEventsAsSimulate`** — aynı seed + aynı komut
> listesi için duvar saati eşlemesiyle yürüyen canlı maç, `Simulate` ile **bayt
> bayt aynı** event akışını üretmelidir.

Bu, pacer'ın domain sonucunu değiştirmediğini **doğrudan** ölçer. Pacer yalnız
`Advance` döndükten **sonra** bekler; motorun girdisini değiştirmez.

## 17. M7 uygulama (2026-09-28)

M7 planı (`docs/plans/M7_IMPLEMENTATION_PLAN.md`) uygulandı. Bu bölüm
**ölçülen** sonucu ve kararları kayda geçirir; tahmin içermez.

### D115 — Denge belgesinin okuyucusu motora taşındı

**Karar.** `BalanceConfigStore` ve `BalanceConfigDocument`
`src/DreamTeam.Simulator/Config/` altından `src/DreamTeam.MatchEngine/Config/`
altına **taşındı**.

**Neden.** M6'da denge belgesini yalnızca CLI okuyordu. M7'de sunucu da aynı
belgeyi okumalı. İki seçenek vardı:

1. `Infrastructure` → `Simulator` proje referansı. 03'ün bağımlılık
   grafiğini ters çevirir (Simulator bir CLI; sunucu bir kütüphane değil).
2. İkinci bir JSON okuyucu yazmak. D103'ün tam olarak yasakladığı şey budur:
   "hangi config ile üretildi" sorusu cevapsız kalır.

Kabul edilen yol 2 değil, 1'in de reddedildiği hali: okuyucu motorun
`Config` katmanına taşındı. `System.Text.Json` BCL'de olduğu için motor
**yine sıfır paket** kaldı (M5 D88). Sonuç: **tek okuyucu, doğru yön.**

**Yan etki.** `FixtureCatalog.RulesVersion` tek kaynağa bağlandı; kural
sürümü artık `RulesIdentity.Current` (yeni dosya). `EngineIdentity`'ye
özellik **eklenmedi** — o dosya M6'da dondurulmuştu.

### D116 — Kimlikler girdiden türetilir (`Guid.NewGuid` yasak)

Oyuncu, takım ve maç kimlikleri SHA-256 girdi'den üretilir
(`DerivedId.From`). `Guid.NewGuid` bu üç yerde kullanılmaz.

**Neden.** Üç somut neden: tekrar üretilebilirlik (aynı girdi aynı macı
kurar), idempotency (aynı istek iki kez gelirse iki satır oluşmaz) ve
izlenebilirlik. **Ölümlü ölçüm:** `DerivedId` ilk yazımda
`CreatePlayer.DeterministicId` olarak `internal` bir yardımcıydı ve
`StartMatch` başka bir use case'ın içine uzanıyordu. Testler de göremiyordu.
Ayrı bir `DerivedId` tipine taşındı.

### D117 — Sıfır paket sınırı Application'a da genişletildi

`DreamTeam.Application` **sıfır paket**. D110 bunu Domain ve Engine için
söylemişti; M7'de use case katmanı da aynı sınırda tutuldu. Gerekçesi somut:
use case katmanının ihtiyacı olan her şey .NET'in kendisidir; PostgreSQL,
JWT ve SignalR **portların arkasında** durur.

**Ölçüm (bu oturum, `dotnet list package`):**

| Proje | Paket |
|---|---|
| `DreamTeam.Domain` | **SIFIR** |
| `DreamTeam.MatchEngine` | **SIFIR** |
| `DreamTeam.Simulator` | **SIFIR** |
| `DreamTeam.Application` | **SIFIR** |
| `DreamTeam.Infrastructure` | Npgsql 10.0.2, Dapper 2.1.79, System.IdentityModel.Tokens.Jwt 8.22.0 |
| `DreamTeam.Api` | Microsoft.AspNetCore.Authentication.JwtBearer 10.0.9, System.IdentityModel.Tokens.Jwt 8.22.0 |

### D118 — Yetkilendirme merkezileştirildi, grup ön eki boşaltıldı

**Karar.** `MapGroup(string.Empty).RequireAuthorization()` + tam yollar.

**Neden.** İlk yazımda her grup kendi ön ekiyle kurulmuştu
(`MapGroup("/api/players")`) ve kök `MapGet("")` deseni
**`/api/players/` üretiyordu** (son slash). ASP.NET bu isteği eşleştirmiyor
ve 404 dönüyordu. Bu, gözle anlaşılmayıp saatlerce yanlış yere bakmaya
yol açtı; `RouteRegistrationTests` yazıldı ve gerçek desen listesi
**ölçüldü**. Boş ön ekli grup hem yetki kuralını tek yerde tutar hem de
desenleri tam yol yapar.

### D119 — `StateResponse.FromSequence` kuralı düzeltildi

**Bulunan gerçek hata.** Snapshot istenmediğinde `FromSequence`,
istemcinin gönderdiği sınır yerine **`CurrentSequence`** dönüyordu.
Yanıt 1..1051 event içerirken `from=1051` diyordu; istemci "bu yanıtta
1'den başladım" bilgisini kaybediyordu.

**Kural (iki dal, tek kaynak).** `SessionCapture` artık
`RequestedFrom` taşır ve API şunu yapar:
- snapshot döndüyse sınır `SnapshotSequence`,
- snapshot dönmediyse sınır `RequestedFrom`.

`CurrentSequence` **kullanılmaz**; o "sunucu şu an burada" demektir ve
ayrı alanda zaten vardır.

### D120 — `MatchSession.Dispose` kilidi dispose etmez

**Bulunan gerçek yarış koşulu.** Canlı maç biterken yürütücü oturumu
depodan çıkarıp dispose ediyordu; aynı anda yeniden bağlanan bir istemci
`CaptureForAsync` içindeydi ve `SemaphoreSlim.Release` çağrısı
`ObjectDisposedException` atıyordu (kullanıcıya 500). M7 API testleri 4
koşudan 1'inde bu yüzden kırılıyordu.

**Çözüm.** `_gate.Dispose()` **kaldırıldı**. `SemaphoreSlim` yalnız
`AvailableWaitHandle` okunduğunda yönetilemez kaynak tutar; bu kod o
özelliği kullanmaz, dolayısıyla dispose etmek hiçbir şeyi serbest
bırakmaz, yalnızca yarışma yaratır. Kapatma, oturumu depodan çıkarmak
ve `_disposed` işaretini set etmektir; yarım kalmış bir çağrı normal
şekilde tamamlanır.

**Kanıt.** Düzeltmeden sonra API testleri **8 ardışık koşuda** 26/26.

### D121 — Tel sözleşmesi: payload camelCase

**Bulunan gerçek tutarsızlık.** Event zarfı camelCase, payload'ı
PascalCase idi. İstemci bir alanı `sequence`, diğerini `HomeScore`
olarak görmek zorundaydı. Payload da camelCase yapıldı.

**Not.** Veritabanındaki JSONB **farklı ve PascalCase kalır**; o iç
bir biçimdir ve C# tipini yansıtır. İkisinin neden farklı olduğu
`MatchEventDtoFactory` içinde yazılıdır.

### D122 — Migration 7 tablo, planda 6 yazıyordu

`migrations/001_initial_schema.sql` yedi tablo açar: `users`, `players`,
`teams`, `roster_entries`, `matches`, `match_events`,
`manager_commands`. Planda altı yazıyordu. Fark, komut günlüğünün
ayrı tablo olmasıdır; 07 §5'in "aynı CommandId yeniden gelirse aynı
sonuç döner" kuralı `UNIQUE (match_id, command_id)` ile **veritabanında**
garanti edilir ve bunun bir yeri olmalıdır.

Elde edilmeyen tablo yok: ekonomi, rating geçmişi, scout, transfer, lig.

### M7'de **yapılmayan** ve açık kalanlar

| Konu | Durum |
|---|---|
| **Migration gerçek PostgreSQL'de çalıştırılmadı** | `dreamteam_test` rolü yok. `localhost:5432` dinliyor, `pg_hba.conf` tamamı `scram-sha-256`; superuser parolası bilinmiyor. **Bu oturumda migration ÇALIŞTIRILMADI ve "geçti" denemez.** |
| `UNIQUE (match_id, sequence)` gerçek DB'de kanıtlanmadı | Bellek içi sahte ile test edildi; sahte `IgnoreUniqueConstraint` moduyla karşılaştırıldı. |
| Kimlik anahtarı rotasyonu | Yok. İki anahtar aynı anda doğrulanamaz. |
| Hız ayarı ve pause/resume | Yok (M8+). |
| Maç kurtarma (D113) | Yok, ve **olması istenmiyor**. |
| `record.Equals` kalıcı düzeltmesi (D87) | M11'e ertelendi. M7'de `SetupDigest` ve `MatchLifecycle` ile aşıldı. |

### Ölçülen sonuç (bu oturum)

| Ölçüm | Sonuç |
|---|---|
| `dotnet build -c Release --no-incremental` | 0 uyarı, 0 hata |
| `dotnet build -c Debug` | 0 uyarı, 0 hata |
| `dotnet test -c Release` | 528/528 (340 motor + 103 simulator + 59 application + 26 api) |
| `dotnet test -c Debug` | 528/528 |
| API testleri flake kontrolü | 8 ardışık koşu, 26/26 |
| Dondurulmuş 8 sözleşme | `git diff` boş (değişmedi) |
| Motor yasaklı API taraması | Temiz (2 isabet XML yorumu) |
