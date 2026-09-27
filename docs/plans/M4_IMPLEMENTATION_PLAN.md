# M4 — Oyuncu/Taktik Kararlarının Etkisi: Uygulama Planı

> **Durum:** Uygulandı ve doğrulandı (27 Eylül 2026). Aşağıdaki kabul tablosu
> gerçek koşu sonuçlarıyla dolduruldu. Sapmalar §12'de kayıtlı, bulunan hatalar
> §13'te.
>
> **Kapsam:** Tactics/pace girdisi, 4+4 policy, bounded composite rating'ler,
> savunma çözümü, ShotQuality, enerji/stamina, OVR (gösterim amaçlı).
>
> **Dışında:** Substitution pencereleri, timeout, canlı taktik değişimi, maç içi
> yönetici komutları (hepsi M5); GameForm (Q10, kapalı); top çalma; batch/CLI;
> API, DB, frontend.

## 0. Bu oturumda kilitlenen kararlar

| ID | Karar | Gerekçe |
|---|---|---|
| D62 | `Balanced` dağılımı **05 §5'in PickAndRoll örneğini birebir alır** | 05 §5 varsayılan dağılımı PickAndRoll olarak veriyor. M3'ün düz vektörünü "tarafsız" bir dağılımla değiştirmek kaynağı olmayan bir tercih olurdu. **M3 davranışı korunur**; taktik etkisi diğer üç dağılımla ölçülür |
| D57 | **Savunma: dört paket policy.** `ManToMan`, `Drop`, `Switch`, `ZonePackPaint` — her biri kendi içinde eşleşme + PnR coverage + paint/closeout tercihini taşır | 05 §6'nın devir önerisi. 02 §6'daki dört UI seçeneğiyle birebir uyum. İç model netleşir, dış model basit kalır. Q08 çözüldü |
| D58 | **Yorgunluk cezası yalnız isabet logitsine (`z`) girer**, `betaFatigue * fatigueLoad` terimiyle | 05 §7 şablonu. `PlayerRatingCalculator` enerjiye **bakmaz**; bu yüzden iki kat sayma yapısal olarak imkânsız. Savunma ve taktik ise ayrı kanaldan `ShotQuality`'ye girer. Q09 çözüldü |
| D59 | **Tempo iki kanaldan geçer:** aksiyon süresi çarpanı + enerji drain çarpanı. Top kaybı riski **savunma policy'sine bırakılır** | 05 §131 pace'i top kaybı risk faktörü sayıyor ama zorunluluk koymuyor. İki etki zaten savunma kanalından geleceği için üçüncü kanal çift sayma riskini yükseltirdi. |
| D60 | **GameForm M4'te kapalı.** `PlayerMatchState` form alanı **içermez** | 05 §13'ün devir önerisi. D34'teki "etkisiz mekanizmayı gizleme" yasağı: 0 olan bir alan eklemek ölü veridir. Q10 M6'ya açık kalır. |
| D61 | **Takım taktiği `MatchSetup`'a girer ve `TeamMatchSetup` tipi materialize edilir** | 04'ün `TeamMatchSetup` sözlüğü. M5'te timeout/subs kotaları da takım kapsamına girecek; düz alanlar M5'te çöplenir. M4 doğru yer. |

### M4'te değişmesi gereken M3 sözleşmeleri

Beyan edilmiş ve gerekçelendirilmiştir:

| Değişiklik | Neden | Etki |
|---|---|---|
| `MatchSetup.Home`/`Away`: `Team` → `TeamMatchSetup`; `HomeLineup`/`AwayLineup` **kaldırılır** | D61, 04'ün sözlüğü | **Yüksek**: M1/M2/M3 testlerinin `setup.Home.Roster` → `setup.Home.Team.Roster` gibi mekanik güncellemesi gerekir. Anlamsal değişiklik yok |
| `TeamMatchSetup` (yeni): `Team`, `Lineup`, `OffensiveTactic`, `DefensiveTactic`, `Pace` | 04 sözlüğü + D61 | Yeni |
| `OffensiveTactic`, `DefensiveTactic`, `Pace` enum'ları (yeni) | 02 §6 dört+dört+üç | Yeni |
| `ActionProfile.Weight` **kaldırılır**; ağırlıklar `OffensiveTacticProfile`'e taşınır | 05 §5: dağılım taktiğe bağlıdır, tek düz vektör yeterli değil | `ActionSelector` imzası değişir |
| `ShotResolver`/`ShotMath` `quality` ve `fatigueLoad` alır | 05 §7 şablonu | `PendingShot` yeni iki alan taşır |
| `BlockResolver` sabit `BlockProbability` yerine rating türevi hesaplar | Savunma rating'leri şut kalitesine girmeli (risk 9) | Config alanı kaldırılır |
| `TeamMatchState`'e `PlayerStates` (`PlayerMatchState[]`) | 04 `PlayerMatchState`; enerji + saniye muhasebesi | Yeni |
| `MatchState`'e `NextSequence` dışında sayaç eklenmez | — | — |
| `ShotAttemptPayload` +`ShotQuality`, +`ShooterEnergy` | Politika etkisi event'ten **gözlenebilir** olmalı (M4 kabul kriteri) ve M6 kalibrasyonu neden'i görebilmeli | Payload şekli değişir |
| `EventSchemaVersion` 2 → **3** | Payload şekli değişti | — |
| `Config` +`TacticsModel`, `FatigueModel`, `PaceModel`, `DefenseModel`, `SelectionSpread` | Katsayıların sahibi config (09 §62) | `ConfigHash` değişir |
| `MatchSetupValidator` genişler: taktik/pace enum doğrulaması | Yeni alanlar | **Beyan edildi** — M3'te dokunulmamıştı, M4'te zorunlu |

**Değişmeyecekler:** `MatchClock` (üç sayacın anlamı), `Advance` imzası,
`IRandomSource`/`SeededRandom`, `EngineIdentity`, `MatchClock`'in `Consume` sözleşmesi.
M5'e bırakılan `Advance` imzası genişletmesi M4'te yapılmaz.

### Bilinen yan etki: RNG çağrı sırası değişir

M3'te iki elle sayım yapılan bir tasarım hatası vardı: bir aksiyonda iki
*ayrı* savunmacı çekilişi yapılıyordu (`PickDefender` faul için, blok kendi
ağırlığıyla). M4 bunları **tek bir çekilişte birleştirir**.

| | M3 | M4 |
|---|---|---|
| Aksiyon | 2 | 2 |
| Birincil savunmacı | 0 veya 1 (koşula bağlı) | **1 (her zaman)** |
| Top kaybı | 1 | 1 |
| Faul | 1 + koşullu 1–2 | 1 + koşullu 1–2 |
| Şuta dönüşme | 1 | 1 |
| Çember teması | 1 | 1 |
| Blok | 1 | 1 |
| İsabet | 1 | 1 |
| Asist | 1 | 1 |
| Ribaund | 2 | 2 |

Değişiklik: **sabit +1 çekiliş ve koşullu çekilişlerin kaldırılması.** Bu, koşul
bağımlı konum kaymalarını (bir dalda 3, diğerinde 5 çekiliş) ortadan kaldırır ve
m3'ün "koşullu çekiliş" riskini azaltır. M2/M3 sonuçları **değişir** — bu kabul
edilmiştir (D45'in devamı). Hiçbir test golden sabit içermez.

## 1. Neden `TeamMatchSetup` (D61)

04_DOMAIN_AND_DATA_MODEL.md §25'ta `TeamMatchSetup` "Team snapshot, başlangıç
lineup, tactics, pace" olarak tanımlıdır ve "başlangıçta geçerli" işaretlidir.
Bugün `MatchSetup` bu alanı dört düz property ile (`Home`, `Away`, `HomeLineup`,
`AwayLineup`) taşıyor. M4 taktik/pace'i eklediğinde bu altı property olur
(`Home`, `Away`, `HomeLineup`, `AwayLineup`, `HomeOffense`, `HomeDefense`,
`HomePace`, +3 ev karşılığı). M5'te buna `TimeoutsRemaining` ve
`SubstitutionsUsed` de gelir.

Düz alanlarla devam etmek, M5'te kaçınılmaz olarak `MatchSetup` ile
`TeamMatchState`'in alan paralelliğine dönüşür ve hangisinin yetkili olduğu
belirsizleşir. 03'ün tek doğruluk kapısı ilkesi bunu reddeder. M4, 04'ün
sözlüğünü fiilen materyalize eden milestone'dır; **şimdi** yapılması doğrudur.

Maliyet: M1'in `SetupValidationTests` ve M2/M3'ün `M2TestData`/`M3TestData`
fixture'larında mekanik güncelleme. Test sayısı ve anlamı değişmez.

## 2. Mevcut durumun dürüst özeti

M3'te **savunma hiçbir şut sonucunu etkilemiyor.** Somut olarak:

- `BlockProbability = 0.06` — sabit sayı, hiçbir rating'e bağlı değil.
- `PickDefender` yalnız **faul atfedilecek** savunmacıyı seçer; blok kendi
  çekilişiyle ayrı ve rating'siz sonuçlanır.
- `ReboundResolver` OREB olasılığını sabit `OffensiveReboundProbability` verir;
  `Rebounding` rating'i yalnız *ribaund alacak kişiyi* seçer, sonucu değil.
- `ShotMath.MakeProbability(base, shotRating, skillScale)` — savunma, taktik,
  enerji, kalite yok. `quality` terimi 05 §7'de tanımlı ama **hiç geçirilmiyor**.
- `ActionSelector` ağırlığı `profile.Skill(ratings) + 1.0`; kompozit yok.
- `PlayerMatchState` hiç yok: enerji, saniye, dakika muhasebesi yok.

Yani M3 "kural bütünlüğü" idi; M4 "kararların sonucu etkilemesi" olacak. M4'ün
kabul kriteri (`controlled policy değişimi beklenen aksiyon karışımını etkiliyor`)
ancak burada anlam kazanır.

## 3. Oluşturulacak dosyalar

```
src/DreamTeam.MatchEngine/
  Config/
    Tactics.cs                     # OffensiveTactic, DefensiveTactic, Pace, TeamMatchSetup
    TacticsModel.cs                # 4 hücum dağılımı + savunma parametreleri
    FatigueModel.cs                # drain/recovery/eğri kancaları
    PaceModel.cs                   # 3 tempo × 2 çarpan
    DefenseModel.cs                # block tabanı, baskı, faul disiplini
  Core/
    PlayerMatchState.cs            # Energy, SecondsOnCourt
  Ratings/
    PlayerRatingCalculator.cs      # 18 -> bounded composite'ler + seçim ağırlığı
    TeamRatingCalculator.cs        # takım OVR (GÖSTERİM AMAÇLI)
  Tactics/
    OffensivePolicy.cs             # taktik -> normalize edilmiş aksiyon dağılımı
    DefensivePolicy.cs             # savunma -> kalite baskısı, block, faul, turnover
  Actions/
    ShotQualityResolver.cs         # aksiyon + eşleşme + IQ -> quality [0,100]
    BlockResolver.cs               # DEĞİŞİR: sabit olasılık -> rating türevi
    TurnoverResolver.cs            # DEĞİŞİR: savunma baskısı girer
    FoulResolver.cs                # DEĞİŞİR: savunma disiplini girer
    ActionSelector.cs              # DEĞİŞİR: taktik dağılımı + composite ağırlık
  Fatigue/
    FatigueCalculator.cs           # drain, recovery, performans çarpanı
tests/DreamTeam.MatchEngine.Tests/
  M4TestData.cs                    # taktik/pace/enerji fixture'ları
  RatingCompositeTests.cs          # sınır, OVR, ağırlık özellikleri
  TacticDistributionTests.cs       # 4 taktik normalize + aksiyon karışımı kayar
  DefensePolicyTests.cs            # 4 policy, quality aralığı, çift sayma yok
  PaceTests.cs                     # possession sayısı + enerji drain'i
  EnergyTests.cs                   # T12: sınırlar + dakika muhasebesi
  M4InvariantTests.cs              # quality [0,100], energy [0,100], tek kanal
```

`ActionSelector`, `ShotMath`, `ShotResolver`, `BlockResolver`, `TurnoverResolver`,
`FoulResolver`, `ReboundResolver`, `MatchSimulation` **genişler/değişir**.
`MatchClock`, `IRandomSource`, `SeededRandom`, `EngineIdentity` **dokunulmaz**.

## 4. Taktik ve tempo girdileri

```csharp
public enum OffensiveTactic { Balanced, PickAndRoll, PerimeterMotion, InsidePost }
public enum DefensiveTactic { ManToMan, Drop, Switch, ZonePackPaint }
public enum Pace { Slow, Normal, Fast }

public sealed record TeamMatchSetup
{
    public required Team Team { get; init; }
    public required Lineup Lineup { get; init; }
    public required OffensiveTactic Offensive { get; init; }
    public required DefensiveTactic Defense { get; init; }
    public required Pace Pace { get; init; }
}
```

### Aksiyon dağılımları

M3'ün tek düz `ActionProfile.Baseline` vektörü, M4'te `Balanced` taktiğinin
dağılımı olur. Diğer üçü 05 §5'in PickAndRoll örneğine göre **yönü**
belirlenir; **sayısal değerler kalibre edilmemiştir** ve M6'da ölçülecektir.

| Aksiyon | Balanced | PickAndRoll | PerimeterMotion | InsidePost |
|---|---:|---:|---:|---:|
| PickAndRoll | **0.45** | **0.45** | 0.12 | 0.10 |
| Drive | **0.15** | **0.15** | 0.18 | 0.15 |
| SpotUp | **0.15** | **0.15** | 0.25 | 0.10 |
| Isolation | **0.10** | **0.10** | 0.10 | 0.20 |
| Cut | **0.10** | **0.10** | 0.22 | 0.12 |
| PostUp | **0.05** | **0.05** | 0.13 | 0.33 |
| **Toplam** | **1.00** | **1.00** | **1.00** | **1.00** |

**Düzeltme — `Balanced` 05 §5'in PickAndRoll örneğini birebir alır.** Aşağıdaki
tabloda `Balanced` sütunu ile `PickAndRoll` sütunu aynıdır, çünkü **05 §5
PickAndRoll'ı örnek/varsayılan dağılım olarak verir.** Dört taktiğin hepsine
keyfî farklı sayı vermek, M3'ün yansız düz vektörünü (0.45/0.15/0.15/0.10/0.10/0.05)
keyfî bir "tarafsız" dağılımla değiştirmek anlamına gelirdi ve bunun kaynağı
olmayan bir tercih olurdu.

Bu, M4'ün tek en dürüst dönüşü: **M3'ün davranışı aynen korunur**, taktik
etkisi diğer üç dağılımla ölçülür. Yansızlık iddiası `NeutralMirror` fixture'ının
konusudur; `Balanced` taktiği yansız değildir ve iddia etmez.

**Kural:** tüm dört vektör her zaman 1.00'a normalize edilir; ağırlığı ≤ 0 olan
aksiyon aday listesine girmez (M3'teki `ActionSelector` filtresi korunur) ve
kalanlar yeniden normalize edilir. `0.00` toplam → `InvalidOperationException`
(mevcut davranış korunur; motor ilerlemez ve sessizce ilk oyuncuyu seçmez).

### Tempo (D59)

```
Pace    SetupActionMs çarpanı    EnergyDrain çarpanı
Slow    ×1.35                    ×0.88
Normal  ×1.00                    ×1.00
Fast    ×0.78                    ×1.15
```

Çarpanlar **kalibre edilmemiştir**. Dikkat: `SetupActionMs` çarpanı hücum
saatini de etkiler — `24000 / (8000 × 1.35) = 2.22` aksiyon, yavaş tempoda
possession başına 2 aksiyona yaklaşır ve ihlal olasılığı artar. Bu gerçek bir
basketbol etkisidir ama M3'ün 8 s aksiyon süresiyle oynayan
`ShotIsNeverReleasedWhenTheShotClockHasAlreadyExpired` testi **Normal** çarpanla
çalıştığı için bozulmaz. Gerekirse çarpanlar M6'da ayarlanır.

**Pace top kaybını ETKİLEMEZ** (D59). Top kaybı kanalı savunma policy'sine aittir.

## 5. Composite rating'ler ve OVR (D58'in yapısal güvencesi)

`PlayerRatingCalculator` 18 attribute'den **sınırlı [0,100]** composite üretir:

| Composite | Girdi |
|---|---|
| `Handle` | BallHandling, Passing, BasketballIQ |
| `PerimeterDefense` | PerimeterDefense, Steal, Speed, BasketballIQ |
| `InteriorDefense` | InteriorDefense, Block, Strength, Rebounding |
| `Rebounding` | Rebounding, Strength, Vertical |
| `Athleticism` | Speed, Vertical, Strength, Stamina |
| `Scoring` | MidRange, ThreePoint, FreeThrow, Inside |

Ağırlıklar config'de; composite'ler **ortalama** değil ağırlıklı toplamdır ve
sonuç `[0,100]` aralığına kırpılır.

**Seçim ağırlığı** (05 §76: "ham rating çarpanı aşırı yoğunlaşma üretir"):

```csharp
weight = max(0.01, 1.0 + SelectionSpread * NormalizeSkill(composite))
```

`SelectionSpread = 0.6` ile aralık `[0.4, 1.6]`. Bu üç özelliği birden garanti
eder: toplam **asla sıfır olmaz** (mevcut `+1.0` garantisi korunur), tek oyuncu
seçilemez hale gelmez ve yoğunlaşma sınırlıdır. M3'ün `attribute + 1`
kuralı bu formülün `SelectionSpread = 1.0` halidir; `0.6` ile daraltılmıştır.

### Enerji kanalı yasağı

`PlayerRatingCalculator` **`PlayerMatchState` alanı görmez ve imzasında
`energy` yoktur.** `Handle`, `PerimeterDefense` vb. statik snapshot'tan hesaplanır.
Yorgunluk **yalnız** `ShotMath` içindeki `- betaFatigue * fatigueLoad` terimiyle
şuta girer. 05 §12'nin "hem rating'i çarpıp hem şutta aynı yorgunluğu tekrar
cezalandırma" yasağı bu yüzden **yapısal** olarak sağlanır: aynı etkiyi iki
kanaldan geçecek bir kod yolu yoktur. Test bunu imzadan da doğrular.

### OVR (T03, D23/D24)

`TeamRatingCalculator` ilk beşin composite'lerinden takım OVR'si üretir. OVR:

- **çözüm girdisi değildir** — hiçbir resolver OVR'ı okumaz.
- `PlayerRatingCalculator`'dan **tamamen ayrı**dır; ağırlık seti config'de ayrı.
- Yalnız `MatchResult` ve rapor çıktısında görünür (kullanıcıya özet).

**T03 testi:** OVR ağırlık seti değiştirilip aynı fixture/seed koşulduğunda
maç sonucu ve event akışı **bit düzeyinde aynı** kalmalıdır. Bu, OVR'ın
girdi değil çıktı olduğunu kanıtlar.

## 6. Savunma çözümü (D57)

Dört paket policy'nin her biri üç ayrı kanaldan etki eder. Kanallar ayrıdır ki
tek etki iki kez sayılmasın:

| Kanal | Ne etkiler | Nasıl |
|---|---|---|
| **Kalite baskısı** | `ShotQuality` | `DefensivePolicy.QualityPenalty(action, shotType, matchup) -> double` |
| **Blok** | blok çekilişi | `base + slope × NormalizeSkill(defenderInteriorDefense)` |
| **Baskı → top kaybı** | turnover çekilişi | `base + slope × NormalizeSkill(defenderPerimeterDefense)` |
| **Disiplin → faul** | faol çekilişi | `base + slope × (1 − discipline)` |

`Matchup`, `OffensiveTactic` ile `DefensiveTactic` çiftidir. Örnek kural:

- `PickAndRoll` + `Drop` → PnR coverage avantajı: kalite **düşer** (roll man'a
  açık, bu yüzden `CoverageRollOpen` uygulanır).
- `Isolation` + `Switch` → en yüksek kalite baskısı (primary defender değişir).
- `ZonePackPaint` → `Drive`/`ClosePost` kalitesi düşer, `SpotUp`/üçlük yükselir.

Bu matris **sayısal olarak kalibre edilmemiştir.** 05 §6'daki eski yüzdeler
("Drop için midrange +6% …") **kopyalanmaz**: 05 onları onaylı matris olmadığını
ve yüzde/yüzde-puan ayrımının tanımsız olduğunu açıkça söyler.

## 7. Enerji ve stamina (D58)

```csharp
public sealed record PlayerMatchState
{
    /// <summary>Kalan enerji, 0-100. `Stamina` DEĞİLDİR (04 §33).</summary>
    public required int Energy { get; init; }
    public required long SecondsOnCourt { get; init; }
}
```

Kişisel fauller `FoulCounters.Personal` içinde **zaten** tutulduğu için
`PlayerMatchState`'e ikinci kez konmaz (çift kaynak yasak).

### Drain ve recovery

```
drainPerSecond    = FatigueModel.BaselineDrainPerSecond * (100 - Stamina) / 100 * paceDrainMultiplier
recoveryPerSecond = FatigueModel.BaselineRecoveryPerSecond * (Stamina / 50)      // 0.5..2.0 çarpan
```

- `BaselineDrainPerSecond` hedefi: bir 12 dakikalık periyotta ortalama oyuncu
  %55–70 enerji kaybetsin → ~0.006/enerji-saniye civarı. **Kalibre değil.**
- `BaselineRecoveryPerSecond` yedekte geçen süreyle orantılıdır. Periyot arası
  toparlanma **ayrı ve daha hızlıdır** (`BreakRecoveryPerSecond`); 05 §149
  "aralarda toparlanır" dediği için kural değil, açık bir tuningleme
  parametresidir.
- `Energy` **her zaman `[0,100]`'e kırpılır.** Sıfır bir nokta değil, bir
  **taban** değerdir: performans çarpanı aşağıda tanımlı çiftte biter.

### 05 §12 performans eğrisi

| Energy | Çarpan |
|---:|---:|
| 100 | 1.00 |
| 80 | 0.99 |
| 60 | 0.97 |
| 40 | 0.92 |
| 25 | 0.84 |
| 10 | 0.72 |
| 0 | **0.65** (M4 kararı — 05 §12 endpoint'i açık bırakmıştı) |

Aralar **doğrusal interpolasyon**la doldurulur (05 §12'nin adayı). 0'ın altına
inilmez. Bu tablo `FatigueModel`'de **veri** olarak tutulur; formül kodda değil.

### Zaman muhasebesi (T12, 05 §164)

Enerji **yalnız aktif oyuncuya değil, on oyuncuya da** güncellenir: her canlı
süre tüketiminde sahadaki 5 oyuncu `drain`, yedek 5 oyuncu `recovery` alır.
`SecondsOnCourt` yalnız sahadakiler için artar.

- **Canlı süre** tüketen adımlar: `SetupActionMs` (tempoya çarpmış), `ShotFlightMs`.
- **Canlı süre tüketmeyenler**: serbest atışlar (06 §2), inbound/devir, ribaund,
  ölü top. Bunlar enerji ve saniye **değiştirmez**.
- Periyot arası: ayrı hızlı toparlanma adımı, canlı süre **sayılmaz**.

`MatchResult`'a maç sonu enerji dağılımı eklenir (08 §88 "oyuncu dakika/enerji
dağılımı" ister). Bu M6'nın kalibrasyon girdisidir.

## 8. Zorluklar ve bilinçli basitleştirmeler

| Konu | Karar | Gerekçe |
|---|---|---|
| **Steal** (05 §133) | **M4'te yok.** Turnover çekilişine defansif baskı girer, çalan oyuncu atfedilmez | 05 §9 ayrı bir alt durum ister; 09'un M4 kabul listesi saymıyor. M5'e bırakılır ve **kayda geçirilir** — sessizce düşürülmez |
| **Takım ribaundu** | M4'te hâlâ yok | Sahada ribaund alacak beş her zaman vardır; out-of-bounds M5+ |
| **Mismodifikasyon** (05 §84 "mismatch +5%") | **M4'te yok** | Pozisyon eşleştirmesi yok (D35: pozisyon dışı oynatma cezası yasak). Mismatch, pozisyon modeli olmadan tanımsız |
| **Transition aksiyonu** (05 §57) | **M4'te yok** | `ActionProfile`'da aksiyon yok; yeni bir mekanizma 05 §3 gereği ayrı milestone ister |
| **Bench yönetimi** (09 §62) | Durum hazır, **karar M5'te** | 04'in `PlayerMatchState`'i ve dakika muhasebesi M4'te gelir; substitution penceresi M5 |
| **Enerji → top çalma/ribaund** | **Yalnız şut** | 05 §12'deki tek kanal yasağı (D58). Diğer sonuçlara yayılmaz |
| **Enerji → asist/pas** | **Yok** | 05 §12'de belirtilmemiş. Uydurulmaz |

## 9. Test senaryoları

| ID | Test | Beklenen |
|---|---|---|
| T12a | `EnergyDrainIsBoundedAndNeverNegative` | `0 ≤ Energy ≤ 100` her adımda |
| T12b | `BenchRecoveryNeverExceedsCap` | Toparlanma 100'ü geçmez |
| T12c | `MinuteAccountingSumsCorrectly` | 5 oyuncu × 2880 s = 14400 s = toplam `SecondsOnCourt` |
| T12d | `FreeThrowsConsumeNoEnergy` | FT adımı enerji/saniye değiştirmez |
| T12e | `EnergyPenaltyAppliesExactlyOnce` | Enerji yalnız `z`'ye girer; composite'ler etkilenmez |
| T12f | `AllTenPlayersUpdateOnEveryLiveInterval` | Sahnede ve yedekte olan **on** oyuncu da güncellenir |
| T03 | `OverallRatingChangeDoesNotAlterMatchOutcome` | OVR ağırlık seti değişir → event akışı bit düzeyinde aynı |
| — | `EveryTacticDistributionSumsToOne` | 4 hücum taktiği × 4 savunma = 16 kombinasyon |
| — | `ZeroWeightActionIsFilteredAndRemainderRenormalized` | 0.00 toplam → açık hata |
| — | `ControlledTacticChangeShiftsActionMix` | PickAndRoll vs InsidePost → `PostUp` payı belirgin artar |
| — | `ControlledTacticChangeShiftsShotTypeMix` | InsidePost → `ClosePost` artar, `ThreePoint` azalır |
| — | `ShotQualityAlwaysWithinRange` | `0 ≤ quality ≤ 100` |
| — | `DefensePolicyShiftsQuality` | ManToMan vs ZonePackPaint → aynı aksiyonda farklı ortalama kalite |
| — | `DropWeakensRollCoverage` | PnR + Drop → PnR + Switch'a göre **düşük** kalite |
| — | `DefenseEntersExactlyOneChannel` | Kalite değiştiğinde blok/top-kaybı/faul çekilişi **değişmez**; `z` yalnız `quality`+`fatigueLoad` üzerinden değişir |
| — | `BlockProbabilityRisesWithInteriorDefense` | Blok oranı savunma rating'iyle artar |
| — | `TurnoverRisesWithDefensivePressure` | Baskı ↑ → top kaybı ↑ |
| — | `PaceChangesPossessionCount` | Fast > Normal > Slow |
| — | `PaceChangesEnergyDrain` | Fast > Normal > Slow (aynı seed, aynı lineup) |
| — | `PaceDoesNotAlterActionOrShotMix` | Tempo **yalnız** süre ve enerji; aksiyon/şut karışımı değişmez (D59) |
| — | `AllTwelveTacticPairsAreSimulable` | 4 hücum × 4 savunma = 16 kombinasyonun tamamı koşulur, hiçbiri hata atmaz |
| — | `EveryZeroTotalWeightCaseThrows` | 1.00'a normalize edilemeyen dağılım açık hata verir |
| — | `SelectionWeightIsAlwaysPositive` | `weight ≥ 0.01`; hiçbir oyuncu seçilemez olmaz |
| — | `CompositeScoresAreBounded` | Tüm composite'ler `[0,100]` |
| — | `NormalPaceMatchesM3BehaviourForPaceIndependentRules` | M3'ün faul/bonus/FT kuralları tempodan bağımsız kalır |
| — | `M3CoreStillHolds` | T04–T11 ve T01/T02/T05 yeniden doğrulanır |

**T11 hâlâ uygulanamaz değil** (M3'te yazıldı, korunur). **T13–T16 M5'te.**
T17/T18 M6'da.

## 10. Kabul kriterleri — gerçek koşu sonuçları

| Kriter | Nasıl doğrulandı | Sonuç |
|---|---|---|
| Temiz build | `dotnet build DreamTeam.slnx -c Release --no-incremental` | **0 uyarı, 0 hata** |
| Tüm testler geçti | `dotnet test -c Release` | **222/222** |
| Tüm testler geçti (Debug) | `dotnet test -c Debug` | **222/222** |
| Motor saf C# | `dotnet list package` (MatchEngine, Simulator) | **Sıfır paket** |
| Determinism | Simulator çıktısının SHA256'ı, iki ayrı koşu | `BAFCDE8D6884B7ECD34E0C697562EB6324D89A8BF8E7C50D2DB19B52FC0DEF9D` — **iki koşuda aynı** |
| Action dağılımları normalize | `EveryTacticDistributionSumsToOne` | 4/4 taktik 1.00 |
| 16 policy kombinasyonu oynanabilir | `AllTwelveTacticPairsAreSimulable` | 16/16 Completed |
| Politika değişimi karışımı etkiler | `ControlledTacticChangeShiftsActionMix`, `ControlledTacticChangeShiftsShotTypeMix` | InsidePost ↑ PostUp/ClosePost, PerimeterMotion ↑ Cut/SpotUp/3P |
| Energy sınırları ve dakika muhasebesi | T12a–T12f | `5 × elapsed` eşitliği 6 basamağa kadar doğru |
| OVR girdi değil | T03 | Farklı ağırlık seti → **bit düzeyinde aynı event akışı** |
| Şans nedeniyle tek maç sonucu yönü **zorunlu test yapılmadı** | 09 §64 | — |
| `MatchClock`/`IRandomSource`/`SeededRandom`/`Advance` dokunulmadı | `git diff` | Doğrulandı |
| Eksiklik bildirimi | Console çıktısı | M4'te kalan boşlukları yazıyor |

### Gözlenen tek maç (seed 20260927, **denge kanıtı değildir**)

`112-118`, **5 periyot (uzatma)**, 3180.0 s, 237 possession, FG 48-93 / 48-89,
3P 7-16 / 7-12, FT 9-10 / 15-16, PF 16 / 13, BLK 3 / 3, TOV 30 / 32.

Enerji: başlayanlar 95 / 93 / 92 / 100 / 100 (sırasıyla 53.0 / 53.0 / 53.0 /
44.1 / 40.3 dk). **Foul-out yedeklemesi gözleniyor**: 1-3 ve 1-1 sahadan çıktı,
yerlerine 1-5 ve 1-6 girdi (12.7 dk / 91 enerji ve 8.9 dk / 90 enerji).

Skor yüksek; savunma rotasyonu/kapanış modeli henüz yok (M5/M6). Denge ölçümü
M6'nın işidir.

### Kritik hash / kimlik

- `EngineVersion.Current = "0.1.0"` (değişmedi), `RngIdentity` SplitMix64 v1.
- `ConfigHash` M3'te `d9f0df73ddf1f89c` idi, **M4'te `b043cfa08c4baecf`**.
- `EventSchemaVersion` 1 → 2 → **3**.
- `rules-v0.2-simple-nba` profili korundu (D31).

## 11. Bilinçli olarak yapılmayacaklar

- GameForm (D60) — Q10 M6'ya açık.
- Substitution pencereleri, kullanıcı değişikliği, timeout, canlı taktik/pace
  değişimi, geç oyun politikası, AI fallback — **M5**.
- Steal atfedimi, transition aksiyonu, mismatch, takım ribaundu, out-of-bounds,
  savunma 3 saniye, technical/flagrant — §8.
- Maçlar arası enerji, sakatlık, para ile toparlanma — 02 §96'da dışarıda.
- Forfeit (motor kazanan seçmez) — M7 adayı.
- Batch CLI, deney manifesti, JSON/CSV export — M6.
- API, DB, SignalR, React, PixiJS.

## 12. Riskler

| Risk | Etki | Azaltma |
|---|---|---|
| `TeamMatchSetup` refactor'ı M1–M3 testlerini kırar | Yüksek test churn | mekanik; `git diff` ile gözden geçirilir, anlamsal değişiklik beklenmez |
| Yeni composite'ler aşırı yoğunlaştırır | Oyuncular aynılaşır | `SelectionSpread` ile bounded ağırlık; `SelectionWeightIsAlwaysPositive`, `CompositeScoresAreBounded` |
| Savunma etkisi iki kanaldan sayılır | Yapay düşük skor | Her etki **tek** kanala bağlı; `DefenseEntersExactlyOneChannel` testi |
| Tempo `SetupActionMs`'i çarptığı için hücum saatini bozar | Çok fazla ihlal | Çarpanlar 1.35/1.0/0.78 ile sınırlı; M6'da ölçüm. `NormalPaceMatchesM3Behaviour…` korur |
| Enerji tüketimi çok hızlı/yavaş | Dakika muhasebesi anlamsız | T12c gerçek toplamı doğrular; katsayı M6 |
| Sıfır toplam ağırlık | Bölme hatası | `InvalidOperationException`; mevcut davranış korunur |
| 4 savunma × 4 hücum matrisi 16 durum | Ayarlanacak çok katsayı | Matriste **tek bir** türetilmiş tablo; 16 elle yazılmış katsayı yok |
| `ShotAttemptPayload` büyüyor | Event bellek boyutu | iki `int`; M6'nın bellek sınırlı özet kipi zaten M6 işi |
| Savunma çözümü sonuçları yüksek tutar | Skor daha da artar | M3'te de savunma yoktu; M4 **iyileştirme** getirir. Etki yönü M6'da ölçülür |

## 13. Sonraki milestone bağlantısı

**M5** (müdahale ve replay): `Advance` imzası manager command listesiyle genişler;
`PlayerMatchState` ve `TeamMatchSetup` serileştirilir; timeout, substitution
pencereleri, canlı taktik/pace değişimi, steal atfedimi, AI fallback.

## 12. Uygulama sapmaları

| # | Sapma | Neden |
|---|---|---|
| 1 | **Kalite cezaları ve IQ terimi 0-1 yerine 0-100 kalite puanı ölçeğinde** | `ShotQualityResolver` kaliteyi tam sayı döndürüyordu. 05 §6'nın 0-1 ölçeğindeki cezaları (Drop PnR 0.07 vs Switch 0.02) doğrudan taşımak farkı yuvarlamada yok ediyordu: ikisi de 52'ye yuvarlanıyordu ve policy'ler arası fark **gözlenemiyordu**. Ölçek doğrudan puana çevrildi (Drop −7, Switch −2, Zone Drive −5, Zone SpotUp +4, ManToMan PnR −3). D71 |
| 2 | **`PlayerMatchState.Energy` ve `SecondsOnCourt` `double`** | İkisi de hesap sırasında yuvarlanınca kayıp oluyordu. Enerji için 8 saniyelik dilimlerde `Math.Round(99.98) = 100` ile **enerji hiç düşmüyordu**; süre için her 1500 ms'lik şut uçuşu 0.5 saniye kaybediyordu ve T12c eşitliği bozuluyordu. D72 |
| 3 | **Periyot arası toparlanma süresi `_state.Clock.GameClockMs`'ten değil önceki periyotun tam süresinden okunur** | Periyot bittiğinde oyun saati zaten 0'dır; saatten okumak toparlanmayı daima sıfır bırakıyordu (sessiz etkisiz mekanizma). D73 |
| 4 | **Savunma disiplini `Math.Clamp` tavanı 1.0** (0.95 değil) | 0.95 gibi keyfî bir tavan `FoulProbabilityPerAction = 1.0` kullanan M3 fixture'larını sessizce zayıflatıyordu ve M4'te gerçek bir hataya yol açtı. Olasılık zaten [0,1] aralığındadır. D74 |
| 5 | **Enerji katsayıları yeniden ölçeklendi** (`BaselineDrainPerSecond` 0.0055 → 0.08) | İlk değer 48 dakikada ~3 puan kaybettiriyordu; yorgunluk kanalı ölüydü. Yeni değer Stamina 78'de tam maç başına ~50 puan. Hâlâ kalibre değil. D75 |
| 6 | **`ActionProfile.SelectionComposite` alanı eklendi**; `Weight` kaldırıldı | §3'te tarif edilmişti; uygulamada iki kanal (ham `Skill` ve composite) ayrı ayrı okunması gerekiyordu, aksi halde seçim ağırlığı ham rating'ten gelirdi. |
| 7 | **`TeamMatchState.Bench` türetilmiş bir erişimci** | 04 §28'de `Bench` ayrı bir alan; ayrı liste M5'in substitution işi. Türetmek iki kaynak riskini önler. |
| 8 | **M2 testlerinin iki öncülü genişletildi** (`M2TestData.Config` yeni config alanları, `TestData.Setup` lineup parametrelerini korur) | D61'in mekanik test maliyeti. Test sayısı ve anlamı değişmedi. |
| 9 | **M3 testlerinin üç varsayımı M3'ün gerçek davranışına göre düzeltildi** | `AndOneAwardsExactlyOneFreeThrow` artık blokların varlığını hesaba katıyor; `TeamFoulCounterResetsEachPeriod` faul oranını 0.15'e çekti (1.0'da mac D43 ile Aborted oluyordu); iki uzatma testi düşük skorlu profile geçti (varsayılan config'de 120 seed'de uzatma gözlenmiyordu). |
| 10 | **`ShotClockExhaustionEndsThePossessionWithoutAKick` iki teste bölündü** | Hiç puan atılamayan bir fixture'da 0-0 beraberlik **sonsuz uzatmaya** yol açıyor. 08 T10'e göre doğru davranış guard → `Aborted`; bu artık ayrı bir test (`DegenerateZeroScoreMatchIsAbortedByTheGuardWithoutFalsifyingTheScore`). Uzatma sayısı üst sınırı **kilitlenmedi** — bkz. risk 3. |

## 13. Bulunan ve düzeltilen motor hataları (regresyon testli)

| Hata | Belirti | Test |
|---|---|---|
| **Şut denemesinde shooting olmayan savunma faulü hiç kaydedilmiyordu** (M3'ten kalan) | `ApplyFoul` yalnız `isShooting` doğrulandığında çağrılıyordu. Kalan savunma faulleri **sessizce kayboluyordu**: kisisel faul sayacı, takım faul sayacı ve bonus hiç tetiklenmiyordu. Gözlenen PF 2/5 (olması gereken ~14/12) | `TeamFoulCounterResetsEachPeriod`, `EveryLiveMissLeadsToReboundOrFreeThrowSeriesOrHorn` |
| **Enerji hiç düşmüyordu** | Her 8 saniyelik dilimde `Math.Round(99.98) = 100`; kesirli drain geri dönüyordu | `EnergyDrainIsBoundedAndNeverNegative`, `PaceChangesEnergyDrain` |
| **Yorgunluk performans eğrisi ölüydü** | `PerformanceMultiplier` 100'ün altındaki **her** enerji için en üst kancayı döndürüyordu. 80 üzeri enerjide `fatigueLoad = 0`, yani yorgunluk şuta hiç girmiyordu | `PerformanceCurveInterpolatesBetweenAnchors` |
| **İnterpolasyon ters yöndeydi** | `ratio = (energy - upper) / span` negatif çıkıyor, sonuç `lower`'ın altına iniyordu: energy 90 → 1.005 (1.00'den **yüksek**) | `PerformanceCurveInterpolatesBetweenAnchors`, `PerformanceCurveIsMonotonicAndBounded` |
| **Enerji katsayısı ~20 kat küçüktü** | 48 dakikada ~3 puan; eğri hiç görünmüyordu | `PaceChangesEnergyDrain` |
| **Periyot arası toparlanma hiç uygulanmıyordu** | Süre, periyot bittikten sonra sıfırlanmış oyun saatinden okunuyordu → daima 0 | `PeriodBreakRecoveryDoesNotCountAsCourtTime` |
| **Dakika muhasebesi eksik tutuluyordu** | `ConsumeLive`, periyot sonunda saatin kırpıp **atladığı** süreyi oyunculara yazıyordu (istenen değil gerçekten oynanan süre kullanılmıyordu). Toplam `5 × elapsed`'dan 90 s sapıyordu | `MinuteAccountingSumsCorrectly` |
| **Her adımda yarım saniye kaybı** | `SecondsOnCourt` uzun tip + `1500/1000 = 1` kesme | `MinuteAccountingSumsCorrectly` |
| **`OffBall` composite tablosunda eksikti** | 18 attribute'un tamamı tabloda geçmiyordu (bütünlük testi buldu) | `EveryRatingAttributeIsInTheTable` |
| **T12f oynanmayan bir durumu doğruluyordu** | Maç başında herkes 100'de; yedek toparlanması kırpılıp hiç değişmiyordu, "on oyuncu güncelleniyor" iddiası boştu | `AllTenPlayersUpdateOnEveryLiveInterval` |

### Hâlâ açık riskler (M4 sonrası)

1. **Katsayılar kalibre değil.** Savunma rotasyonu/kapanış modeli yok; skor 112-118.
   10K/100K deneyi M6'da. M4'ün kabul ettiği "tek maç sonucu yönü" ölçümü
   bilinçli olarak **yapılmadı** (09 §64).
2. **Uzatma üst sınırı tanımsız.** Puan atılamayan bir fixture'da 0-0 beraberlik
   uzatmayı sürdürüyor; motor yalnız eylem guard'ı ile kesiyor ve 521 periyot
   üretebiliyor. 08 T10 "guard → Aborted" dediği için bu **spec'e uygun** ama
   ürün kararı değil. M5'e girmeden karar verilmeli: uzatma sayısı sınırlansın
   mı (ve sınırdan sonra `Aborted` mı, beraberlik mi) ya da bu kabul mü, görünmez
   mi? Kayda geçti, karar verilmedi.
3. **Uzatmada dakika kaydı gerçek değil.** `PeriodsPlayed` uzatmayı içerir ama
   uzatma periyodu için `SecondsOnCourt` daha az olur (5 kişi × daha kısa süre).
   Bu doğru davranıştır; `T12c` testi `elapsed` üzerinden tanımlı olduğu için
   tutarlıdır.
4. **Enerji etkisi ölçülebilir ama zayıf.** Maç sonunda başlayanlar ~92-95
   enerjide; `fatigueLoad` ≈ 0.05. Bu, M3'teki "hiç etki yok" durumundan ilerleme
   ama henüz dönüşümlü bir sistem değil. Regresyon testleri kanalın **açık** olduğunu
   sabitliyor; M6 nicel etkiyi ölçecek.
5. **`MatchResult` tüm event'leri bellekte tutuyor** ve `PlayerEnergy` eklendi.
   08 §8'in bellek sınırlı özet kipi M6'nın işi.
6. **`ImmutableArray<T>` JSON serileştirilmesi doğrulanmadı.** M5 replay
   serileştirmesinde ilk test edilecek yer.
7. **CI yok.** Doğrulama yalnızca yerel.
8. **T03 ve T12 artık uygulanabilir ve yazıldı**; T13–T18 hâlâ M5/M6'da.
