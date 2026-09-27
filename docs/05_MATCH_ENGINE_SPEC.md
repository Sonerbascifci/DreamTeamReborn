# Match Engine v0.1 tasarımı

## 1. Amaç ve durum

Her sayı, ribaund, asist, top kaybı ve faul simüle olaylardan çıkacak. Saf C# motor, kadro/taktik/stamina etkileşimini gözlenebilir ve yeniden üretilebilir biçimde çözecek.

Bu belge önceki taslağı korur. Formüllerin katsayıları **kalibre edilmemiş önerilerdir**. Gerekli yeni teknik ayrımlar “devir önerisi” olarak işaretlenmiştir. Koddan önce M0/M2 planlarında açık katsayılar, olasılık birimleri ve kural tercihleri tanımlanır.

## 2. Temel attribute'ler

| Grup | Alan | Kullanım |
|---|---|---|
| Athletic | Speed | Transition ve hareket |
| Athletic | Strength | Fiziksel mücadele |
| Athletic | Vertical | Bitiriş, blok, ribaund |
| Athletic | Stamina | Enerji tüketimine dayanıklılık |
| Offense | Inside | Çember çevresi bitiriş |
| Offense | MidRange | Orta mesafe |
| Offense | ThreePoint | Üçlük |
| Offense | FreeThrow | Serbest atış |
| Offense | BallHandling | Top kontrolü |
| Offense | Passing | Pas ve yaratım |
| Offense | OffBall | Topsuz hareket |
| Offense | PostOffense | Post becerisi |
| Defense | PerimeterDefense | Çevre savunması |
| Defense | InteriorDefense | İç savunma |
| Defense | Steal | Top çalma |
| Defense | Block | Blok |
| Defense | Rebounding | Ribaund |
| Mental | BasketballIQ | Karar kalitesi |

Tüm değerler 0–100. `OffensiveIQ` ayrı bir 19. attribute değildir; önceki örnekteki isim `BasketballIQ` olarak netleştirilir. UsagePreference ve Aggression kullanılacaksa attribute değil, ayrı policy/config girdisi olduğu tanımlanır.

## 3. Composite rating taslakları

```text
PickRollBallHandler = .30*BallHandling + .25*Passing
                   + .20*BasketballIQ + .15*MidRange + .10*Speed
PerimeterScoring   = .50*ThreePoint + .20*OffBall
                   + .15*BasketballIQ + .15*BallHandling
ReboundAbility     = .50*Rebounding + .20*Vertical
                   + .20*Strength + .10*BasketballIQ
OnBallDefense      = .45*PerimeterDefense + .20*Speed
                   + .15*Strength + .20*BasketballIQ
```

Ağırlıklar her satırda 1 eder. Bunlar bir kişinin farklı rollerdeki becerisidir; takım gücünü yalnızca beş OVR ortalamasına indirgemez. Spacing, transition defense ve box-out türetimleri henüz sayısallaştırılmadı; M2/M4'te açık formül olmadan gizli bonus eklenmez.

**OVR yalnızca gösterimdir.** OVR 79 olan uygun rol oyuncusu belirli kadroda OVR 84 oyuncudan daha faydalı olabilir.

## 4. Simülasyon birimi

Ana kontrol birimi possession; içinde inbound, pass/drive/post/screen, turnover/foul/shot, flight/result ve rebound action'ları bulunur. Bir action'ın süresi vardır. Rakibin topu kontrol etmesi, basket sonrası normal top devri ve uygun final FT sonrası devir possession'ı sonlandırabilir. Düdük tek başına possession bitişi değildir.

Context: transition, half court, late clock, after timeout, end game. Bir possession aynı anda late clock ve end game olabilir; bütün kavramları birbirini dışlayan tek enum yapmak zorunlu değildir.

Transition; önceki top kaybı/defansif ribaund, pace, hız ve rakibin geri koşusu ile ilişkilidir. Kesin olasılık modeli açık kalibrasyon işidir.

## 5. Aksiyon seçimi

Önceki PickAndRoll başlangıç dağılımı:

| Aksiyon | Ağırlık |
|---|---:|
| PickAndRoll | 0.45 |
| Drive | 0.15 |
| SpotUp | 0.15 |
| Isolation | 0.10 |
| Cut | 0.10 |
| PostUp | 0.05 |

Toplam 1.00. Diğer taktiklerin dağılımları henüz verilmedi. Motion topsuz hareket/cut/spot-up, InsidePost post/inside ağırlığını artırmayı hedefler. Uygun olmayan aksiyon filtrelenirse kalan ağırlıklar yeniden normalize edilir.

Seçim ağırlıklı RNG'dir; “random değil” ifadesi tamamen deterministik oyuncu seçimi anlamına gelmez. Sıralı aday listesi ve maç RNG'si kullanılır. Sıfır toplam ağırlık için açık fallback policy gerekir; sıfıra bölme ve ilk listedeki oyuncuyu sürekli seçme hatası olmamalı.

Handler için BallHandling/Passing/IQ ve gerekiyorsa kullanım tercihi; shooter için seçilen aksiyon ve beceri; defender için matchup rolü kullanılır. Ham rating'leri çarpan eski örnek aşırı yoğunlaşma üretebilir; normalize ağırlık veya bounded composite denenmelidir. Kesin seçim fonksiyonu M2 planında kilitlenir.

## 6. Savunma ve taktik etkileşimi

Yön: otomatik counter yok; savunma, aksiyon kalitesi, shot profile, mismatch ve turnover riskine etki eder.

Önceki örneklerde Drop için midrange +6%, roll +5%, pull-up three -2%, turnover -1%; Switch için ilk aksiyon -5%, isolation +7%, mismatch +5% yazılmıştı. **Bunlar onaylı matris değildir.** Yüzde mi yüzde puan mı olduğu tanımlanmadığı ve Drop etkisi personel/coverage derinliğine bağlı olduğu için doğrudan config'e kopyalanmamalı. Bu sayılar burada yalnızca taslak geçmişi olarak saklanır.

Devir önerisi: ilk dört savunma seçeneğini paket policy olarak tanımla: temel eşleşme + PnR coverage + paint/closeout tercihi. Böylece Drop'ın PnR dışındaki davranışı da açıklanır. Ayrı kullanıcı seçenekleri yaratmadan iç model netleşir.

## 7. Şut kalitesi ve isabet

Önce aksiyon kalitesinden bir ShotQuality oluşur; pas yaratımı, spacing, matchup, IQ ve savunma baskısı etkiler. Sonra shooter becerisi ile birlikte isabet olasılığı hesaplanır.

Devir önerisi, boyutsal olarak tutarlı şablon:

```text
skill = (shotRating - 50) / 50
quality = (shotQuality - 50) / 50
z = logit(baseProbability[shotType])
    + betaSkill[shotType] * skill
    + betaQuality[shotType] * quality
    - betaFatigue[shotType] * fatigueLoad
    + formLogitOffset
pMake = 1 / (1 + exp(-z))
```

Bu, katsayıları verilmiş bitmiş formül değildir. `baseProbability` (0,1), normalize skill/quality [-1,1], fatigueLoad [0,1] aralığındadır. Formun nasıl dönüştürüleceği açık karardır. Savunma ve taktik ShotQuality içine girdiyse z'ye aynı etkiyi tekrar ekleme. Extreme z için sayısal kararlılık sağla.

Önceki tuning hedefleri, **NBA ortalaması iddiası olmadan**:

| Tür | Başlangıç hedef aralığı |
|---|---:|
| AtRim | %60–68 |
| ClosePost | %48–58 |
| MidRange | %38–45 |
| ThreePoint | %33–39 |
| FreeThrow | Oyuncuya bağlı; formülü ayrı |

Bunlar aggregate fixture çıktısı için hipotezlerdir; her oyuncuya clamp olarak uygulanmaz. Konteks ve örneklem belirtilmeden test sabiti yapılmaz.

## 8. Sonuç ağacı

Turnover, foul ve shot için bağımsız üç yazı-tura atıp aynı aksiyonda çelişkili sonuçlar üretme. Devir önerisi: koşullu dallanma veya normalize kategorik seçim.

1. Aksiyon başlatılır; kurala aykırılık/turnover riski çözülür.
2. Devam eden aksiyonda offensive/non-shooting foul dalı olabilir.
3. Şut aksiyonunda shooting foul bayrağı ile make/miss ilişkisi birlikte çözülür; and-one desteklenir.
4. Sayılabilir şut için block/make/miss ayrımı oluşturulur. Blok aynı şuta bağlıdır; ayrıca ikinci FGA yazılmaz.
5. Canlı miss varsa rebound veya out-of-bounds kontrolü; horn/FT arası ölü top için sahte rebound yok.

Dağılımlar ve dal önceliği M2/M3'te tabloya çevrilmeli. Foul olasılığı yüksek diye şut denemelerinin istemeden iki defa örneklendiği bir model kurulmaz.

## 9. Turnover ve steal

Risk faktörleri: baseline, savunma baskısı, tempo, handler/passer becerisi ve IQ. İşaretler açık ve test edilebilir olmalı. Fast pace her durumda top kaybı yaratmak zorunda değildir; koşullu etkidir.

Turnover bir hücum sonucu; steal savunmacıya atfedilebilen alt durumdur. Travel, offensive foul ve shot clock violation steal üretmez. Steal olunca kaybeden oyuncu/ takımın tek turnover kaydı ile ilişkilendirilir. Hangi savunmacının çaldığı ayrı ağırlıklı seçimdir.

## 10. Ribaund

Önce uygun canlı miss belirlenir. OREB/DREB rekabeti ilgili beşler, box-out ve şut türüne göre çözülür. Kazanan tarafta oyuncu ataması yapılır; toplamda bir rebound outcome oluşur. Team rebound/out-of-bounds farklı outcome olabilir.

Üçlük miss'inde guard lehine, inside miss'inde uzunlar lehine konum ağırlığı önerildi; katsayılar henüz yoktur. Fizik simülasyonu veya tam koordinat gerekmez. Offensive crash tercihi varsa transition defense maliyetiyle birlikte tanımlanmalı; ücretsiz güçlendirme olmamalı.

## 11. Faul ve serbest atış

Shooting, non-shooting ve offensive foul temel kapsamdır. Drive/inside yoğunluğu, matchup ve savunma disiplini etkileyebilir. Aggression/Discipline 18 attribute içinde yok; policy veya türetilmiş değer olarak tanımlanmadan doğrudan property eklenmez.

Takım faul sayacı, bonus, kişisel faulden çıkma, and-one, FT dizisi ve son atış sonrası canlı top `06_RULES_AND_STATE_MACHINE.md` tarafından belirlenir.

## 12. Enerji ve stamina

Maç başında Energy 100 taslağı. Sahada oyun zamanı ve aksiyon yüküyle azalır; bench/izin verilen mola ve aralarda toparlanır. Speed/press/sprint/post battle gibi yükler stamina attribute'üyle ilişkilidir.

Önceki performans eğrisi:

| Energy | Çarpan |
|---:|---:|
| 100 | 1.00 |
| 80 | 0.99 |
| 60 | 0.97 |
| 40 | 0.92 |
| 25 | 0.84 |
| 10 | 0.72 |

0 energy endpoint'i, interpolation ve hangi becerilerin etkilendiği açık. Linear interpolation ara değerler için adaydır; tamamı motor tarafından tanımlanmalı. Hem rating'i çarpıp hem şutta aynı yorgunluğu tekrar cezalandırma. Saat bazlı drain + aksiyon bazlı ek yük kullanılacaksa baseline'ın neyi içerdiği açık olsun.

Enerji güncellemesi yalnızca aktif oyuncuya yapılmaz: sahadaki/yedek bütün oyuncular için elapsed time bir kez hesaba katılır. Toplam oynama süresi her canlı zaman aralığında on oyuncuya dağıtılır.

## 13. GameForm

Önceki taslak: maç başında küçük, çoğunlukla merkeze yakın, [-%5,+%5] sınırlı form. Normal dağılım kendiliğinden sınırlı değildir; truncated normal/clamp/başka bounded dağılım seçimi açık. Standart sapma ve etki birimi tanımlanmadan kullanılmaz.

Devir önerisi: ilk simetri ve temel kalibrasyonda GameForm kapalı; ana kurallar doğrulandıktan sonra sürümlü config'le ekle. Aynı iki takımın farklı seed ile farklı sonuç vermesi zaten RNG ile mümkündür; GameForm zorunlu çeşitlilik mekanizması değildir.

## 14. RNG ve determinism

Tek seeded oyun RNG'si veya açıkça sürümlenmiş alt akışlar kullanılır. Arbitrary `new Random()` çağrıları yok. Algoritma adı/sürümü ve state serialization sabitlenir; runtime'ın default Random davranışına platformlar arası garanti yüklenmez.

Aynı setup + engine/rules/balance/RNG sürümleri + aynı sıralı komutlar aynı domain eventlerini üretmelidir. DateTime, thread schedule, logging, UI frame hızı ve unordered collection sonucu değiştirmez.

RNG state snapshot'ta yalnız başlangıç seed'i olarak değil, devam için mevcut state olarak tutulur. Stable candidate order gerekir. Cross-runtime bit düzeyi eşitlik gerekiyorsa floating point/math fonksiyonları ayrıca sınanır; v0.1 garanti kapsamı M0'da tanımlanır.

## 15. Canlı müdahale ve son bölüm davranışı

ChangeOffense, ChangeDefense, ChangePace, Substitution, Timeout. İstek önce doğrulanır ve queue'ya alınır; uygun kural sınırında uygulanır. Timeout zamanı ve substitution penceresi kesin kuralla belirlenir.

Late-game policy: CatchUp, ProtectLead, HoldForLastShot, IntentionalFoul, TwoForOne taslak davranışları. Auto/FoulImmediately/NoIntentionalFoul kullanıcı seçenekleri önerildi. M5 kapsamında skor farkı/saat/bonus/timeout bağlamı netleştirilir. Kullanıcının açık “faul yapma” tercihini auto policy sessizce geçersiz kılamaz.

## 16. Config ve diagnostics

Değişebilen katsayılar balance config'te; algoritma semantiği kodda olur. Config değişimi yeni maça uygulanır; mevcut maç frozen snapshot kullanır. Config sürümü/hash kaydedilir. Geçersiz ağırlık, NaN, negatif süre veya uyumsuz parametre başlangıçta reddedilir.

Diagnostic kayıt: possession/action id, seçilen oyuncular, context, normalize girdiler, logit katkıları, final probability, RNG roll ve outcome. Olasılık katkısını yüzde puan gibi yazma; final p'ye dönüşümü ayrı göster. Diagnostic kapalı/açık sonuç aynı olmalı. Online client'a gizli RNG/gelecek sonucu gönderme.
