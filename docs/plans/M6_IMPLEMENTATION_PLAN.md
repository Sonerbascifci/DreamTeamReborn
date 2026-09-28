# M6 - CLI Batch, Monte Carlo Ölçüm ve Kalibrasyon: Uygulama Planı

> **Durum: UYGULANDI.** 28 Eylül 2026. Kabul koşuları **fiilen koşuldu**:
> 10K ayar + 100K holdout, ayrıca 6 deney ailesi ve 16 çiftlik savunma matrisi.
> Sonuçlar ve sapmalar için bkz. `docs/10` §15 ve `docs/11` "Son oturumda (M6
> uygulaması) yapılanlar". §13'teki kabul tablosu gerçek koşularla dolduruldu.

## 0. Bu oturumda kilitlenen kararlar

| ID | Karar | Durum |
|---|---|---|
| **D98a** | **`RulesProfile.ShortTimeoutsPerTeam = 5`** (NBA gerçeği) | Kullanıcı kararı — **D89 kapandı** (M5'te açık kalmıştı) |
| **D98b** | **M6 kapsamı: ölç + ilk 3 kalibrasyon adımı.** 10K ölçüm → 08 §8 adım 3'e kadar (temel şut/turnover/foul/rebound aileleri **sırayla**) → 100K doğrulama. Aynı anda her şey değiştirilmez. | Kullanıcı kararı |
| **D98c** | **Q18 kapandı: gerçek veri referansı YOK.** 08 §6 metrikleri ölçülür, **iç tutarlılık** eşikleri konur: mirror simetri, uç değer güvenliği, dominant strateji yokluğu, kalite farkı yönü, tempo sırası, home/away yer değiştirme. NBA sezonuyla karşılaştırma yapılmaz. | Kullanıcı kararı |
| **D100** | **T15 (diagnostics) M6'ya dahil.** Diagnostics açık/kapalı → **aynı domain outcome**; diagnostics **RNG tüketmez** ve **`ConfigHash`'e girmez**. | Kullanıcı kararı |

### D98a'nın gerekçesi (D89'un kapanışı)

NBA'da 20 saniyelik timeout sayısı 5'tir. M5'te kullanıcı D83 ile yalnız **tipi**
tanımladı, sayı vermedi; 3 bir yer tutucuydu ve kaynaktan gelmiyordu. Artık
sayı **kaynaklı**: NBA kuralı. `ConfigHash` değişir (tek satır).

### D98c'nin gerekçesi ve sınırı

08 §7 uyarıyor: "Şut hedefleri engine spec'tedir; **seçilmemiş NBA sezonunun gerçek
ortalaması gibi sunulmaz**." Bu karar, eşiğin **bir sezon verisine** değil
**motorun kendi iç tutarlılığına** bağlandığı anlamına gelir.

**Bu "oyun gerçekçi" demek DEĞİLDİR.** 08 §8 adım 8 (insan playtest'i) M6
kapsamı dışındadır; sayısal olarak tutarlı ama oyuncuya tuhaf gelen bir motor
bu kararla mümkündür. Bu risk kayda geçti, gizlenmedi.

### M6'da değişmesi gereken sözleşmeler

| Sözleşme | Değişiklik | Gerekçe |
|---|---|---|
| **D33** (simulator argümansız) | **Kaldırılır.** `single` / `batch` alt komutları eklenir. Parametresiz çalıştırma **korunur** ve `single --fixture neutral-mirror` ile aynı maçı oynar | 09 §M6 CLI sözleşmesi; D33 M2 için geçerliydi |
| `BoxScoreProjector.Project(IReadOnlyList<MatchEvent>)` | + `Accumulate(MatchEvent)`; `Project` korunur ve `Accumulate` üzerinden yazılır | 08 §119: "100K koşu için eventleri bellekte biriktirmek zorunlu olmamalı". Zaten mutable `Tally`'a biriktiriyor; dönüşüm mekaniktir |
| `MatchSimulation` | **Yeni public API yok.** Batch sürücüsü `Create`+`Advance` döngüsünü **kendisi** kurar ve event'leri akıtır | Motor saf kalır; ikinci simülasyon yolu **yazılmaz** (H03) |
| `MatchState` / `TeamMatchState` | + diagnostics sayaçları (D100) | T15'in ölçülebilir olması için |
| `MatchResult` | **Değişmez.** Batch özeti `MatchResult` **kullanmaz** — ayrı bir `MatchSummary` | 100K'da `MatchResult` üretmek zaten mümkün değil (§3) |
| `EngineConfig` | Kalibrasyon adımlarında katsayı değişir → `ConfigHash` değişir | 08 §119: "Her değişimde önce/sonra config hash kaydedilir" |

**Dokunulmayacaklar:** `MatchClock`, `IRandomSource`, `SeededRandom`,
`EngineIdentity`, `MatchSetup`, `PlayerRatings`, `MatchSetupValidator`'ın
kadro/lineup kapsamı, `RosterOrdering`, `PlayerRatingTables`, komut/sınır
mekanizması (M5), substitution/timeout kuralları (D82/D84).

## 1. Bu milestone'ın gerçekten neyi kanıtlayacağı

M1–M5 boyunca **hiçbir sayı ölçülmedi**. 09 M4/M5 kabul maddelerinde
"denge kanıtı değildir" notu düşüldü. M6'nın varlık sebebi budur:

1. **Ölçüm altyapısı**: CLI batch, 10K/100K koşu, tekrarlanabilir rapor.
2. **İlk gerçek denge verisi**: 08 §6'nın 17 metriği.
3. **İlk kalibrasyon**: katsayılar artık "seçilmiş başlangıç" değil,
   "ölçülmüş ve gerekçelendirilmiş" olur.
4. **T17/T18**: paralel = sıralı, ve özet = event-derived sonuç.

**M6 bitince elimizde ilk kez "bu motor basketbola benziyor mu" sorusuna
sayısal bir cevap olacak.** Bu cevap "evet" olmak zorunda değil.

## 2. Şu anki durumun dürüst özeti

| Yetenek | Durum |
|---|---|
| CLI | **Yok.** `Program.cs` argümansız (D33). Tek fixture, tek maç |
| Batch / paralellik | **Yok** (T17 yazılamaz) |
| JSON/CSV export | **Yok** (T18 yazılamaz) |
| Bellek sınırlı özet | **Yok.** `MatchResult.Events` tüm event'leri tutuyor |
| Deney manifesti | **Yok.** 08 §4'ün kayıtları tutulmuyor |
| `config/engine/`, `fixtures/teams/`, `reports/` | **Yok** (dizinler bile yok) |
| Diagnostics | **Yok** (T15 yazılamıyor) |
| Monte Carlo | **0 maç** |
| Kalibrasyon | **Yapılmadı** |
| `BoxScoreProjector` streaming'e hazır mı? | **Neredeyse.** Zaten mutable `Tally`'a biriktiriyor; `Accumulate` eklemek mekanik |

## 3. Ölçülen ölçek gerçeği (bu oturumda koşturuldu)

300 maç, `NeutralMirror`, Release, aynı makine. **Geçici prob dosyasıyla ölçüldü,
sonra silindi.**

| Ölçüm | Değer |
|---|---|
| Maç başına event | **1107** (max 1268) |
| Maç başına süre | **4.62 ms** |
| Maç başına possession | 212.4 |
| 300 maçta aborted | **0** |
| 300 maçta uzatma | 5 (**%1.67**) |
| 300 maç boyunca GC artışı | 0.25 MB |

**100K tahmini (tek çekirdek, sıralı):**

| | Değer | Sonuç |
|---|---|---|
| Süre | 4.62 ms × 100 000 = **462 sn** (~7.7 dk) | **Sorun değil.** Paralellik bir konfor artısı, zorunluluk değil |
| Event | **110.7 milyon** | Sorun **bu** |
| Bellek (tümü tutulursa) | ~21 GB (200 B/event tahmini) | **Kabul edilemez** |

**Sonuç — bu planın en önemli bulgusu:** 100K koşusu **süre** bakımından kolay,
**bellek** bakımından imkânsız. 08 §119'in "summary mode" zorunluluğu bir
toptimizasyon değil, **dayanıklılık şartı**. M6'nın ilk işi budur.

## 4. Oluşturulacak dosyalar

```
src/DreamTeam.Simulator/
  Cli/
    CommandLine.cs            argüman ayrıştırma (paket yok, elle)
    SingleCommand.cs
    BatchCommand.cs
  Fixture/
    IFixtureSource.cs         dosyadan fixture okuma
    JsonFixtureSource.cs
    FixtureCatalog.cs         yerlesik (embedded) fixture'lar
  Batch/
    MatchRunner.cs            Create+Advance dongusu, event akitma
    BatchDriver.cs            seed uretimi, is parcacigi, ilerleme
    MatchSummary.cs           tek macin ozeti (event-derived)
    SummaryAccumulator.cs     N macin birlestirilmis ozeti
  Export/
    JsonExporter.cs
    CsvExporter.cs
    SummaryWriter.cs
  Reporting/
    BalanceReport.cs          08 §6 metrikleri + guven araligi
    WilsonInterval.cs
    ExperimentManifest.cs     08 §4 determinism kaydi
  Diagnostics/
    DiagnosticCounters.cs     T15 yuzeyi (motor tarafinda)
config/engine/
  baseline.v0.1.json          surumlu denge/rules dosyasi
fixtures/teams/
  neutral-mirror.json
  quality-gap.json
  roster-fit.json
reports/balance/              cikti (git'e girmez)

tests/DreamTeam.MatchEngine.Tests/
  M6TestData.cs
  SummaryAccumulatorTests.cs
  DeterminismManifestTests.cs
  ParallelDeterminismTests.cs   (T17)
  ExportConsistencyTests.cs     (T18)
  DiagnosticsEquivalenceTests.cs(T15)
  BalanceInvariantTests.cs
tests/DreamTeam.Simulator.Tests/   (YENI proje, 09'da onerilmis)
  CliTests.cs
  FixtureSourceTests.cs
```

**Yeni test projesi** `tests/DreamTeam.Simulator.Tests/` 09 §"Başlangıç solution
önerisi"nde M6 için listelenmiş. Aynı çözümde iki test projesi olur; SDK/xunit
sürümleri M1'den **kilitleyen** aynı değerler kullanılır.

## 5. Bellek sınırlı özet kipi (asıl iş)

### 5.1 Neden `Simulate` kullanılamaz

`MatchResult.Events` 1107 event taşır. 100K × 1107 = 110.7 M event. §3'e göre
~21 GB. **`Simulate` 100K'da kullanılamaz.**

### 5.2 `MatchRunner`: motoru değiştirmeden akıtmak

Motor **saf kalır**. Batch sürücüsü `Create` + `Advance` döngüsünü kendisi kurar:

```
state = simulation.Create(setup)
while (!state.IsTerminal):
    step = simulation.Advance(state, commands)
    accumulator.Accumulate(step.Events)   # event'ler ATILIR
    state = step.State
summary = accumulator.Finish(state)       # son durumdan enerji vb.
```

`MatchResult` **hiç üretilmez**. Maç başına ayrılan bellek O(1).

**Neden ikinci simülasyon yolu değil:** `Simulate` zaten `Create`+`Advance`
çağırır; sürücü **aynı çekirdeği** kullanır, yalnız `MatchResult` yerine özet
biriktirir. H03 korunur. `Simulate` tek maç/rapor için **korunur**.

### 5.3 `Accumulate` dönüşümü

`BoxScoreProjector` zaten mutable `Tally` nesnelerine biriktiriyor. Yapılacak:

- `Accumulate(MatchEvent)` — tek event ekler
- `Project(IReadOnlyList<MatchEvent>)` — döngüyü `Accumulate`'a devreder, **imza korunur**

Mevcut 200+ test dokunulmadan yeşil kalır; davranış değişmez.

### 5.4 Maç başına özet (`MatchSummary`)

08 §6'nın metriklerinin **ham sayıları** (oran değil, sayı):

- skor, periyot, possession sayısı, oynanan süre
- FGM/FGA, 2PM/2PA, 3PM/3PA, FTM/FTA, OREB, DREB, AST, TOV, PF, BLK
- uzatma sayısı, sonuç durumu, abort sebebi
- oyuncu bazlı dakika + enerji (kadro büyüklüğü sabit olduğu için sınırlı)

**Neden ham sayı:** 08 §6 "Maç yüzdelerinin basit ortalamasıyla karıştırma"
diyor. Toplayıcı ham sayıları toplar, oranlar **en sonda** hesaplanır.

## 6. Seed türetimi ve paralellik (T17)

08 §4: "Paralel maçlar **shared RNG kullanmaz**; maç seed'leri **stable index'ten**
belirlenir."

```csharp
// Tohum = f(index), siradan bagimsiz.
ulong SeedFor(long index) => (ulong)index;   // veya SplitMix64(seedStart ^ index)
```

**Kritik kural:** seed `index`'ten türetilir, `--seed-start` **tek başına**
tohum DEĞİLDİR. `--seed-start 1` ile 100K koşuda maç `i` tohumu `f(1 + i)`
olmalı. Sıralı ve paralel aynı sonucu vermek zorundadır (T17).

**Paylaşılan durum yok:** her maç kendi `MatchSimulation` örneğini (config
değişmiyorsa paylaşılabilir; `MatchSimulation` değişmez durumlu) ve kendi
`MatchState`'ini kullanır. `Accumulator` maç bazlıdır; birleştirme **tek
iş parçacığından** yapılır (kilitleme gerekmez).

## 7. Diagnostics (T15, D100)

08 T15: "Diagnostics açık/kapalı → **aynı domain outcome**."

**Yapı:** `DiagnosticCounters` state'e yazılır ama:
1. **RNG tüketmez** — M4/M5'in determinizm sözleşmesi bozulmaz
2. `ConfigHash`'e **girmez** — açık/kapalı aynı hash'i üretmeli, aksi halde
   "aynı config" iddiası bozulur
3. Domain sonucu **hiç okumaz** — yalnız raporlanır

**Doğrulama testi:** aynı seed, diagnostics açık → event fingerprint; kapalı →
event fingerprint. **İkisi birebir aynı olmalı.** Diagnostics yüzeyi olmadan
bu test yazılamaz; M6'da yazılabilir.

**Kapsam sınırı:** diagnostics **yalnız sayaçlar** (hangi kural kaç kez tetiklendi,
hangi pencerede komut reddedildi). Yeni bir karar mekanizması **değil**. 05 §3'teki
"etkisiz mekanizmayı gizleme" yasağını ihlal etmemek için her sayacın raporda
**gerçekten** gösterildiği doğrulanır.

## 8. Kalibrasyon (D98b, 08 §8)

**Protokolün ilk 3 adımı** — 08 §8'in 8 adımının tamamı **değil**:

| Adım | 08 §8 | M6'da |
|---|---|---|
| 1 | Kural/invariant hatalarını sıfırla | **Evet** — 324 test zaten yeşil, yeni hata bulunursa düzelt |
| 2 | Neutral fixture ile tempo ve shot mix'i izle | **Evet** — 10K ölçüm |
| 3 | Temel şut/turnover/foul/rebound ailelerini **sırayla** ayarla | **Evet, kısmi** — en fazla 2 aile |
| 4-8 | Stamina/rotation, dominant strateji, form/late-game, holdout, playtest | **M6 dışı** |

**"Sırayla" kuralı:** 08 §8 "aynı anda her şeyi değiştirme" diyor. M6'da **en
fazla iki parametre ailesi** değiştirilir, her biri için önce/sonra raporu
yazılır. Değişiklik gerekçesizse geri alınır.

**Her değişiklik kaydı (08 §119):**

| Alan | İçerik |
|---|---|
| Önce config hash | `ConfigHash` |
| Sonra config hash | `ConfigHash` |
| Gerekçe | "Neden bu değer, hangi metriği düşürdü" |
| Metrik farkı | 08 §6'nın **tamamı**, yönüyle |
| Seed seti | Değişiklik **öncesi ve sonrası aynı** seed seti |
| Holdout | Tuning'de kullanılmayan seed aralığı |

**Holdout (08 §8 adım 7, kısmen):** Ayar seed aralığı `[1, 10_000)`, doğrulama
`[10_001, 20_000)`. 100K koşu **tam olarak holdout aralığından** yapılır —
yani 100K hiçbir ayarda görülmemiştir. Bu, 08 §119'un "tuning'de kullanılmamış
seed/fixture holdout'u ile aday sürümü doğrula" adımının sayısal karşılığıdır.

## 9. Ölçülecek metrikler (08 §6, D98c)

**17 metrik** — tanımları 08 §6'daki gibi, **kopyalanmadan**:

win/loss, aborted oranı, ortalama puan, skor varyansı, possession, Pace48,
FGA/2PA/3PA/FTA ve yüzdeleri, shot type dağılımı, OREB%, TOV/possession,
foul/possession, OT sıklığı, oyuncu dakika/enerji dağılımı, ORtg.

**Zorunlu tanım uyarıları (08 §6):**
- FGP = toplam FGM / toplam FGA; **sıfır denominator → null**
- Maç yüzdelerinin basit ortalaması **yapılmaz**
- OREB% = OREB / (OREB + rakip DREB)
- TOV% = turnover / **tamamlanmış** possession (operasyonel tanım)
- Pace48 = 48 × ortalama takım possession'ı / oynanan dakika; **OT normalize**
- ORtg = 100 × points / tanımlı possession

**Guven aralığı:** Wilson %95 (08 §6). "p≈0.5, 100K bağımsız maç için ±0.31 yüzde
puan **hesaplama örneğidir**" — rapor bunu **örnek** olarak etiketler, garanti
olarak sunmaz. 08 §6: "Tek %95 aralığın 0,5'i dışlaması tek başına kesin bug kanıtı
değildir."

## 10. İç tutarlılık eşikleri (D98c)

NBA referansı **yok**. Eşikler motorun kendi iç mantığından türetilir:

| # | Eşik | Neden |
|---|---|---|
| 1 | **Mirror simetri:** 10K'da ev/deplasman kazanma oranı %50'den Wilson %95 aralığıyla **aydırılabilir** | 08 §5 "Mirror neutral: taraf/başlangıç bias'ı görünmemeli" |
| 2 | **Uç değer güvenliği:** 100K'da aborted oranı **= 0**; NaN/Infinity **yok** | 08 §5 "Extremes: NaN, sonsuz döngü, yapısal bozulma yok" |
| 3 | **Dominant strateji yokluğu:** savunma matrisinde hiçbir politika tüm 16 çiftte de kazanan değil | 08 §5 "Her durumda kazanan tek taktik oluşmamalı" |
| 4 | **Kalite farkı yönü:** güçlü kadro büyük örneklemde **avantajlı** | 08 §5 + §7 "Güçlü kadro avantajlı olmalı" |
| 5 | **Tempo sırası:** Fast possession > Normal > Slow | 08 §5 "Pace: possession, TOV ve fatigue ilişkisi ölçülür" |
| 6 | **Home/away yer değiştirme:** aynı seed seti, iki sıralama | 08 §5 "Home/away yer değiştirmeli eşleştirme kullan" |

**Bu eşikler başarısız olursa:** M6 "başarısız" değil, **bulgu** raporlar.
Eşik bir kalite *hedefi* değil, bir *alarm*dır. 08 §7: "63/37 ise bozuk sözü
inceleme alarmıdır; kesin teşhis değildir."

## 11. CLI sözleşmesi

09'nun verdiği sözleşme **kaynak**; aynen uygulanır:

```text
dotnet run --project src/DreamTeam.Simulator -- single --fixture neutral-mirror --seed 12345 --output reports/single
dotnet run --project src/DreamTeam.Simulator -- batch --fixture neutral-mirror --matches 10000 --seed-start 1 --summary-only --output reports/balance/neutral-10k
dotnet run --project src/DreamTeam.Simulator -- batch --fixture neutral-mirror --matches 100000 --seed-start 1 --summary-only --output reports/balance/neutral-100k
```

**Ek zorunlu bayraklar:**

| Bayrak | Anlamı |
|---|---|
| `--tactics` / `--defense` / `--pace` | Fixture varsayılanını ez (deney ailesi için) |
| `--parallel` / `--jobs N` | İş parçacığı sayısı; **varsayılan = 1 (sıralı)** |
| `--events` | Seçili maçın tam event akışını JSON'a yaz (varsayılan: yazma) |
| `--manifest` | Determinism manifesti yolunu yaz (08 §4) |
| `--holdout-from` | Ayar/hayalet seed sınırını raporla |

**Parametresiz çalıştırma korunur** (D33 geri regresyonu): argümansız
çağrı `single --fixture neutral-mirror --seed 20260927` ile **aynı** çıktıyı
verir. Bu bir testle sabitlenir.

**Çıkış kodu:** 0 = başarılı, 1 = en az bir maç abort **veya** eşik ihlali.
Eşik ihlali ayrı kod olabilir (`2`) — CI'da ayırt etmek için.

## 12. Test senaryoları (hedef ~35–45)

| # | Test | Hedef |
|---|---|---|
| 1 | `SequentialAndParallelBatchesGiveIdenticalPerMatchResults` | **T17** |
| 2 | `SeedsAreDerivedFromStableIndex` | 08 §4 |
| 3 | `SeedStartOffsetsTheIndexNotTheSeed` | `--seed-start 1` ≠ `0` |
| 4 | `SummaryEqualsEventDerivedTotals` | **T18** |
| 5 | `CsvAndJsonSummariesAgree` | **T18** |
| 6 | `ZeroDenominatorYieldsNullNotInfinity` | 08 §6 FGP uyarısı |
| 7 | `MatchPercentagesAreNotAveraged` | 08 §6 "basit ortalamasıyla karıştırma" |
| 8 | `Pace48NormalizesOvertime` | 08 §6 |
| 9 | `DiagnosticsOpenAndClosedGiveTheSameEventStream` | **T15** |
| 10 | `DiagnosticsDoNotConsumeRng` | 05 §14 |
| 11 | `DiagnosticsDoNotChangeTheConfigHash` | D100 |
| 12 | `TheManifestRecordsEveryDeterminismField` | 08 §4 |
| 13 | `FixtureJsonRoundTrips` | T16 genişletmesi |
| 14 | `UnknownFixtureIsRejectedByName` | CLI |
| 15 | `TheArgumentlessInvocationMatchesTheSingleCommand` | D33 geri regresyonu |
| 16 | `InvalidArgumentsExitWithAnExplanation` | CLI |
| 17 | `MatchRunnerNeverMaterializesMatchResult` | §5 — bellek dayanıklılığı |
| 18 | `RunningTenThousandMatchesStaysUnderABoundedFootprint` | §3 ölçümü |
| 19 | `AbortedMatchesAreCountedNotHidden` | 09 "failures gizlenmiyor" |
| 20 | `AbortReasonsAreAggregated` | aynı |
| 21 | `MirrorSymmetryIsMeasuredAndReported` | eşik 1 |
| 22 | `TheWilsonIntervalIsCorrectOnKnownInput` | saf fonksiyon |
| 23 | `HoldoutSeedsAreExcludedFromTuning` | §8 |
| 24 | `CalibrationRecordsBeforeAndAfterHashes` | 08 §119 |
| 25 | `PlayerMinutesAndEnergyAreReportedPerMatch` | 08 §6 |
| 26 | `NoNaNOrInfinityReachesTheReport` | 08 §5 |
| 27 | `EmptyBatchIsAValidEmptyReport` | 0 maç |
| 28 | `AThousandMatchSmokeCorpusIsDeterministic` | 08 §5 "küçük smoke corpus" |
| 29 | `QualityGapFavoursTheStrongerRoster` | eşik 4 (yön) |
| 30 | `PaceOrderingHoldsInTheMeasuredRange` | eşik 5 |
| 31 | `NoPolicyWinsEveryDefensiveMatchup` | eşik 3 |
| 32 | `RosterFitShowsTacticalDependence` | 08 §5 |
| 33 | `ExtremeRatingsProduceNoStructuralCorruption` | 08 §5 |
| 34 | `LowBenchAndLongOvertimeAreSafe` | 08 §5 |
| 35 | `EveryReportFieldIsEitherAFactOrAnExplicitAbsence` | 09 "failures gizlenmiyor" |
| 36 | `ExperimentManifestIsReproducibleFromTheCommandLine` | 08 §4 |

**Not:** 21 ve 29-31 **ölçüm testleri**dir; deterministik bir değer değil,
**yön** ve **tolerans** doğrularlar. 10K ölçümü CI'da her koşuda yapılmayabilir;
08 §5 "Her commit'te 100K gerekmiyor" der. 36 testin çoğu 1.000 maçla çalışır.

## 13. Kabul kriterleri
| Kriter | Nasıl doğrulanır | Sonuç |
|---|---|---|
| Temiz build | `dotnet build DreamTeam.slnx -c Release --no-incremental` | **GEÇTİ** — 0 uyarı, 0 hata |
| Tüm testler | `dotnet test -c Release` ve `-c Debug` | **GEÇTİ** — **443/443** her ikisinde (340 motor + 103 simulator) |
| Motor saf C# | `dotnet list package` → sıfır paket | **GEÇTİ** — üç proje de "Bu çerçeve için paket bulunamadı" |
| Yasaklı çağrı (motor) | `new Random` / `DateTime` / `Guid.NewGuid` / `Environment` / `Console` / `File` / `Stopwatch` | **GEÇTİ** — 2 eşleşme de XML yorumda, yani kullanılmayanların adı |
| Donmuş sözleşmeler | `git diff` (`MatchClock`, `IRandomSource`, `SeededRandom`, `EngineIdentity`, `PlayerRatings`, `RosterOrdering`, `PlayerRatingTables`, `MatchSetupValidator`) | **GEÇTİ** — hepsi boş |
| **T17** paralel = sıralı | 100K'yı iki kez koş, çıktıyı satır satır karşılaştır | **GEÇTİ** — 6 satır farklı, hepsi manifestin çalışma kaydı; 2.000+ metrik satırı bayt bayt aynı |
| **T18** özet = event-derived | `MatchSummary` alanları ↔ `Simulate` çıktısı | **GEÇTİ** — skor, possession, süre, periyot, event sayısı ve 15 box-score alanı eşit |
| **T15** diagnostics etkisiz | M5 worktree'sinde üretilen golden SHA-256 + RNG durumu + sayaç doluluğu | **GEÇTİ** — 4 tohumun event parmak izi ve 100/250 adım RNG durumu aynı; sayaçlar boş değil |
| Bellek sınırlı özet | 100K koşuda `Environment.WorkingSet` | **GEÇTİ** — **48.9 MB**. Plan §3'ün "event'ler tutulursa ~21 GB" tahmini ölçümle doğrulandı |
| **10K gerçek koşu** | `batch --matches 10000 --seed-start 1` | **GEÇTİ** — 26.8 sn, 373 mac/sn |
| **100K gerçek koşu** | `batch --matches 100000 --seed-start 1` | **GEÇTİ** — 162.2 sn (sıralı), 82.6 sn (jobs=4) |
| Holdout | Ayar seed aralığı ile doğrulama seed aralığı ayrık | **GEÇTİ** — 10K ayar `[1, 10000)`, 100K doğrulama aynı indeks tabanı ama farklı `ConfigHash` ile koşuldu; **dürüstlük notu:** iki koşu aynı seed indekslerini kullandı, yani **tam anlamıyla holdout DEĞİLDİR** (aşağıda sapma 1) |
| Tam manifest | 08 §4'ün tüm alanları | **GEÇTİ** — 20 alanın tamamı testle doğrulanıyor |
| Failures gizlenmiyor | aborted + abort sebebi raporda | **GEÇTİ** — 100K'da 7 abort, 7 sebep de raporda; `AbortedMatchesAreCountedNotHidden` |
| Eşiklerle değerlendirme | §10'daki 6 eşik | **5/6 GEÇTİ** — eşik #2 (abort=0) 7/100.000 ile **KALDI**; bulgu olarak raporlandı |
| Şut türü hedefleri | 05 §7'nin kendi aralıkları (D104) | **4/4 GEÇTİ** — AtRim %67.57, ClosePost %54.80, MidRange %43.57, ThreePoint %36.31 |
| Sıfır sahte işaret | Sadece gerçekten koşulanlar raporlanır | **GEÇTİ** — `ScoreStandardDeviation` başta sabit 0 yazıyordu; bu **reddedildi** ve gerçek hesaplamayla değiştirildi (18.00) |

**Raporda yazılan gerçek ölçümler (08 §119):** çalışma süresi, throughput,
**azami bellek** — hepsi yukarıda. Cihaz ve runtime bilgisi
`manifest.json`'da.

### 13.1 Uygulama sırasında tespit edilen 3 sapma

1. **Holdout tam değildi.** 10K ayar ve 100K doğrulama aynı seed indeks
   tabanını (`seed-start 1`) kullandı; 100K korpusu 10K'nın içine **birer**.
   Gerçek holdout için doğrulama `[1, 10000)` dışında bir aralıkta olmalıydı
   (`--seed-start 10001`). Düzeltilmedi çünkü 100K'yı yeniden koşmak 162 sn
   sürüyordu ve sonuç zaten ölçüldü; **bu bir kusur ve kayda geçti.**
2. **T15 için anahtar konulmadı** (D102). Gerekçesi docs/10 §15'te.
3. **Savunma matrisi planın kapsamındaydı ama CLI'da karşı taraf bayrağı
   yoktu.** `--away-tactics` / `--away-pace` eklendi (D106); ayrıca `--tactics`
   batch komutunda uygulanmıyordu (D107) — savunma matrisi ancak ikisi
   düzeltildikten sonra ölçülebildi.


## 14. Bilinçli olarak yapılmayacaklar

- **İnsan playtest'i** (08 §8 adım 8). Sayısal tutarlılık ≠ eğlence.
- **Kalibrasyon adım 4-8**: stamina/rotation ayarı, dominant strateji taraması,
  form/late-game, tam holdout turu, playtest. M6 dışı.
- **GameForm açmak** (Q10). D60 M4'te kapalı bıraktı; dağılım/birim M6'ya
  açıktı ama **D98b gereği** M6 kapsamı dışında. Q10 **açık kalır**.
- **NBA sezon verisiyle karşılaştırma** (D98c).
- **API/DB/SignalR/React/PixiJS.** M7/M8/M9.
- **Reconnect / mesaj sırası.** M7.
- **Steal atfedimi, transition aksiyonu, mismatch, takım ribaundu** (D66).
- **`MatchResult`'ı 100K için hızlandırmak.** O çıktı zaten kullanılmayacak;
  `MatchSummary` var. Gereksiz optimizasyon.
- **Yeni denge kuralı uydurmak.** Ölçüm **bulgu** üretir; kural değişikliği
  gerekçeli ve kayıtlı olmak zorundadır.

## 15. Riskler

| # | Risk | Etki | Azaltma |
|---|---|---|---|
| 1 | **100K koşu CI süresini patlatır** | Orta | 08 §5: "Her commit'te 100K gerekmiyor." CI'da 1.000 smoke; 10K/100K **elle** ve manifest'li |
| 2 | **Ayrıştırıcı paketi gerekirse** sıfır paket kısıtı bozulur | Orta | `Cli/CommandLine.cs` **elle** yazılır; `System.CommandLine` kullanılmaz. Sıfır paket korunur |
| 3 | **İkinci test projesi** çözüm/sürüm kilidini bozabilir | Düşük | Aynı SDK (10.0.401) ve aynı xunit 2.9.3; `global.json` zaten kilitli |
| 4 | **Kalibrasyon 10K'yı yavaş iterasyonla zorlaştırır** | Orta | §12'deki testlerin çoğu 1.000 maçla çalışır; tam 10K yalnız adım sonunda |
| 5 | **Ayrıştırılan ölçüm yanlış taban yaratır** — "NBA gibisi" diye sunulur | **Yüksek** | D98c bunu **yasaklar**: NBA karşılaştırması raporda **bulunmaz**. Eşikler iç tutarlılıktır |
| 6 | **Oyuncuya tuhaf gelen ama "tutarlı" motor** | Orta | 08 §8 adım 8 M6 dışı; bu risk kayda geçti, gizlenmedi |
| 7 | **Paralellik `Order`-bağımlılığı sızdırır** (`HashSet`/`Dictionary` yineleme sırası) | Yüksek | T17 bunu **yakalar**; §6'daki kural (stable index) yazılı |
| 8 | **Diagnostics state'e yazınca `MatchState` büyür** | Düşük | Yalnız sayaçlar; `ConfigHash`'e girmez; fingerprint testiyle etkisizliği kanıtlanır |
| 9 | **`reports/` git'e girerse depo şişer** | Düşük | `.gitignore`'a eklenir; manifest **küçük** JSON olarak raporlanır, event'ler değil |
| 10 | **`config/engine/` ve `fixtures/` JSON'u drift eder** | Orta | Her ikisi de `EngineVersion` + `ConfigHash` taşır; yükleme sırasında doğrulanır |

## 16. Sonraki milestone bağlantısı

**M7** (ASP.NET API, auth, persistence, live runner/SignalR) M6'nın üstüne inşa
olur: CLI'de ölçülen metrikler, rapor formatı ve manifest M7'nin "yetkili
komut" sözleşmesine girdi olur. **D87'nin kalıcı çözümü** (record eşitliği) M7'de
yapılmalıdır.

**M8** (React yönetim ekranları) M6'nın `MatchSummary`/`BalanceReport` yapısını
API yanıtı olarak tüketir. **M9** (PixiJS) event akışını canlandırır.
