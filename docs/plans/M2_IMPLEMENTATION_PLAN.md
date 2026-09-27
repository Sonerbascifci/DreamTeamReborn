# M2 — Dar Possession Vertical Slice: Uygulama Planı

> **Durum:** Onay bekliyor. Bu plan 27 Eylül 2026'da hazırlandı. Uygulama kodu yazılmadı.
>
> **Kapsam:** Possession → action → shot/turnover/rebound çekirdeği, saat, event akışı,
> box score ve tek maçlık console çıktısı.
>
> **Dışında:** Faul, serbest atış, uzatma, out-of-bounds, substitution, timeout,
> tactics/pace davranışı, enerji, batch/CLI, API, DB, frontend.

## 0. Bu oturumda kilitlenen kararlar

| ID | Karar | Gerekçe |
|---|---|---|
| D31 | Kural profili: **sade NBA-esinli** — 4 × 12 dakika, hücum saati 24 sn, uzatma 5 dk | 06 belgesindeki H08 devir önerisi. Tam NBA sadakati iddiası yok; bonus ve timeout M3/M5'e kalır. |
| D32 | Periyot açılışı: **seeded RNG 1-bit çekilişi** (`NextUInt64() & 1`). Modulo bias yok | 06 §4 "home/away bias yaratmayacak deterministic/seeded yöntem". 08'in mirror deneyi bunu gerektirir. |
| D33 | Simulator: **tek maç, argümansız**, sabit kurgusal fixture, sonuç + box score yazdırır | 09 M2 "console tek maç". CLI bayrakları/batch/export M6'da; erken tasarlanırsa iki kez yapılır. |
| D34 | M2'de **tactics/pace `MatchSetup`'e eklenmez** — *D30 düzeltmesi* | M4'te tactics'in davranışı olacak. M2'de eklemek sonucu değiştirmeyen ölü veri olurdu ve "taktik etkili" yanılgısı üretirdi. 05, etkisiz mekanizmaların gizlenmesini yasaklıyor. |

### D30 düzeltmesi

M1 sırasında `MatchSetup`'in M2'de `TeamMatchSetup` ile tactics/pace genişletileceği
yazılmıştı. Bu plan onu **geri çekiyor**: M2'de `MatchSetup` **hiç değişmeyecek**.
M1'in doğrulama testleri olduğu gibi yeşil kalır; M2 tamamen yeni dosyalardan oluşur.

## 1. Önceki doğrulamalar (yeniden üretilmedi, yeniden koşuldu)

| Doğrulama | Durum |
|---|---|
| `git status -sb` | Temiz, `main...origin/main` |
| `git log` | `0ef8f1c` ← `b328fee` ← `f6aba1f` |
| ahead / behind | 0 / 0 |
| `dotnet test -c Release` | **42/42** |

M1 kodu duruyor, testleri geçiyor. M2 bu işi **yeniden yapmaz**, üstüne ekler.

## 2. M1'den tüketilen sözleşmeler

| Tip | M2'de kullanımı |
|---|---|
| `MatchSetup` | Maç giriş girdisi. **Değiştirilmez.** |
| `MatchSetupValidator.Validate` | `MatchEngine.Create` içinde tek giriş kapısı; reddedilen setup motoru başlatmaz |
| `Team` / `Lineup` | Sahadaki beş, `Lineup` sırasıyla slot sırası |
| `RosterOrdering.Canonical` | Ribaund/yardımcı seçimi aday listesinin stabil sırası |
| `PlayerRatings` (18 `int`) | Şut becerisi girdisi |
| `SeededRandom` / `IRandomSource` | Oyun RNG'si; `IRandomSource` artık gerçek tüketici bulur (risk #4 kapanır) |
| `EngineIdentity` | Event zarfına yazılır (risk #2 kısmen kapanır) |

## 3. Oluşturulacak dosyalar

```
src/DreamTeam.Simulator/                       # YENİ console projesi
  DreamTeam.Simulator.csproj
  Program.cs                                   # argümansız tek maç (D33)
  SingleMatchReport.cs                         # okunabilir çıktı biçimi

src/DreamTeam.MatchEngine/
  Config/EngineConfig.cs                       # düz baseline kayıt (kalibre EDİLMEMİŞ)
  Core/MatchPhase.cs                           # 06 §3 durum makinesinin alt kümesi
  Core/MatchClock.cs                           # üç ayrı integer ms sayacı
  Core/MatchState.cs                           # tek otorite
  Core/PossessionState.cs                      # 06 §4 kimlik muhasebesi
  Core/StepResult.cs                           # state + event'ler + terminal durumu
  Core/MatchResult.cs
  Core/MatchEngine.cs                          # Create / Advance / Simulate
  Actions/ActionContext.cs                     # 05 §4 context bayrakları
  Actions/ActionSelection.cs                   # seçilen action + oyuncular
  Actions/ActionSelector.cs
  Actions/ShotType.cs
  Actions/ShotResolver.cs
  Actions/TurnoverResolver.cs
  Actions/ReboundResolver.cs
  Events/MatchEvent.cs                         # zarf + typed payload
  Events/MatchEventTypes.cs                    # M2'deki somut event kayıtları
  Events/BoxScoreProjector.cs
  Projection/PlayerBoxScore.cs
  Projection/TeamBoxScore.cs

tests/DreamTeam.MatchEngine.Tests/
  M2TestData.cs                                # kurgusal fixture + config
  ClockTests.cs
  PossessionIdentityTests.cs                   # T05
  ShotOutcomeTests.cs                          # T04
  BoxScoreInvariantTests.cs                    # 08 §2
  MatchTerminationTests.cs                     # guard → Aborted
  DeterminismTests.cs                          # T01, T15, roster sırası
  SimulateTests.cs                             # tam maç bütünlüğü
```

`DreamTeam.Simulator` `DreamTeam.MatchEngine`'e referans verir ve solution'a eklenir.

## 4. Sözleşmeler

### 4.1 Saat (H02)

```csharp
namespace DreamTeam.MatchEngine.Core;

/// <summary>Üç ayrı sayaç. Hepsi integer milisaniye; motor duvar saati tutmaz.</summary>
public readonly record struct MatchClock
{
    public int Period { get; init; }              // 1-4 normal
    public long GameClockMs { get; init; }        // bu periyotta kalan
    public long ShotClockMs { get; init; }        // bu hücumda kalan
    public long ElapsedGameTimeMs { get; init; }  // toplam oynanan
    public bool IsGameTimeConsumed => ...         // FT/substitution dışarıda
}
```

`GameClockMs` ve `ShotClockMs` **farklıdır** (04 §"Saat birimi"). `Advance` içinde bir
action canlı süre tükettiğinde ikisi de doğru miktarda azalır. Serbest atış,
substitution ve dead-ball bu sürümde süre tüketmez.

### 4.2 State

```csharp
public enum MatchPhase { NotStarted, LiveBall, DeadBall, PeriodBreak, Completed, Aborted }

public sealed record PossessionState
{
    public required int PossessionId { get; init; }
    public required TeamSide Offense { get; init; }
    public required int ActionCounter { get; init; }
}

public sealed record MatchState
{
    public required MatchSetup Setup { get; init; }
    public required MatchClock Clock { get; init; }
    public required MatchPhase Phase { get; init; }
    public required IReadOnlyDictionary<TeamSide, int> Score { get; init; }
    public required PossessionState? Possession { get; init; }
    public required long NextSequence { get; init; }
    public required ulong RngState { get; init; }     // devam için state
    public required int ActionCount { get; init; }     // guard sayacı
}
```

`RngState` snapshot'ta **devam eden state** olarak tutulur, seed olarak değil (05 §14).
`MatchState` değişmezdir; `Advance` yeni bir `MatchState` döner. Tek yürütücü kuralı:
aynı `MatchState`'i yalnız bir yol değiştirir.

### 4.3 Motor çekirdeği (H03)

```csharp
public sealed class MatchEngine
{
    public MatchEngine(EngineConfig config);

    public MatchState Create(MatchSetup setup);   // validate eder; reddederse Aborted
    public StepResult Advance(MatchState state);  // bir sonraki anlamlı sınır
    public MatchResult Simulate(MatchSetup setup);// Create + Advance döngüsü
}
```

`Simulate` **aynı `Advance`'i** döngüye sokar. İkinci bir simülasyon algoritması yazılmaz.
`Advance` imzası M5'te manager command listesi alacak şekilde genişletilecek; bu
kasıtlı, kayıtlı bir değişikliktir (M2'de `ScheduledManagerCommand` tipi henüz yok).

### 4.4 Event zarfı (07 §1)

```csharp
public abstract record MatchEvent
{
    public required Guid MatchId { get; init; }
    public required long Sequence { get; init; }        // maç içi tekil, 1'den başlar
    public required int SchemaVersion { get; init; }
    public required string EngineVersion { get; init; }
    public required string ConfigHash { get; init; }
    public required int Period { get; init; }
    public required long GameClockMs { get; init; }
    public required long ElapsedGameTimeMs { get; init; }
    public int? PossessionId { get; init; }
    public long? ActionId { get; init; }
    public TeamSide? TeamId { get; init; }
    public Guid? PlayerId { get; init; }
}
```

M2 event seti: `MatchStarted`, `PeriodStarted`, `PossessionStarted`, `ShotAttempt`,
`ShotMade`, `ShotMissed`, `Rebound`, `Turnover`, `PossessionEnded`, `PeriodEnded`,
`MatchEnded`, `MatchAborted`.

**Zorunlu ayrım (07 §3):** `ShotAttempt` şutun kimliğini açar; `CountsAsFieldGoalAttempt`
`ShotMade`/`ShotMissed` payload'ında taşınır ve M2'de her zaman `true`'dur. Block ve
foul M3'e kaldığı için M2'de yalnız bu tek yol vardır — çift muhasebe riski M2'de yapısal
olarak imkânsızdır.

### 4.5 Config

`EngineConfig` düz bir kayıttır; `RulesProfile` (periyot sayısı/dakikası, hücum saati,
uzatma) ve şut/turnover/ribaund başlangıç değerlerini taşır. `Baseline` statik örneği
**kalibre edilmemiş** olarak işaretlenir.

- 05 §7'deki aralıklar (%60–68 AtRim vb.) **aggregate fixture hipotezidir**; oyuncuya
  clamp olarak uygulanmaz.
- Tüm katsayılar config'te; algoritma semantiği kodda (05 §16).
- `ConfigHash`, sabit sıralı alanlardan üretilen bir metnin SHA-256'sıdır. JSON
  canonicalization kullanılmaz — byte equality beklenmez (08 §4), ama aynı config aynı
  hash verir. Bu, "risk #2: hash üreten yok" maddesini kapatır.

## 5. M2'de çözülen sonuçlar

09 dört sonuç ister. **Beşinci bir güvenlik sonucu ekleniyor:**

| # | Sonuç | Sonraki hâl | Not |
|---|---|---|---|
| 1 | Sayılabilir isabet | Rakip inbound | Skor + possession kapanır |
| 2 | Canlı miss + **DREB** | Rakip inbound | **Yeni** possession id |
| 3 | Canlı miss + **OREB** | Aynı devam | **Aynı** possession id (T05) |
| 4 | Turnover | Rakip inbound | Yeni possession id |
| 5 | **ShotClockViolation** | Rakip inbound | *Eklendi* — aşağıya bakınız |

**Neden 5. sonuç eklendi:** 06 §2 "Simülasyon zamanı daima ilerlemeli veya sonlu bir
dead-ball zinciri tükenmelidir." Aksi halde shot clock dolduğunda `Advance` sonsuz
döngüye girer. `ShotClockViolation` zaten 04'ün `TurnoverKind` listesinde var; bu bir
faul/FT özelliği değil, sonlanma güvenliğidir. Döngüyü bitirmek yerine kuralı
uygulamak doğru seçimdir.

## 6. Test senaryoları

| ID | Test | Beklenen |
|---|---|---|
| T01 | `SameSetupProducesIdenticalEventStream` | İki `Simulate` → event dizisi birebir aynı |
| T01b | `ConsoleOutputIsByteIdenticalAcrossRuns` | Simulator çıktısı iki koşuda aynı bayt |
| T02 | `RosterInputOrderDoesNotChangeOutcome` | Roster ters sırada → aynı event dizisi |
| T04 | `MadeShotScoresCorrectPointsAndOneFga` | 2/3 puan, 1 FGA + 1 FGM, 1 possession kapanışı |
| T04b | `MissedShotRecordsOneFgaNoScore` | 1 FGA, 0 FGM, skor değişmez |
| T04c | `EachScoringEventAddsPointsExactlyOnce` | Skor = Σ event puanı; hiçbir event iki kez sayılmaz |
| T05 | `OffensiveReboundKeepsPossessionIdentity` | OREB sonrası `PossessionId` **değişmez** |
| T05b | `DefensiveReboundStartsNewPossession` | DREB sonrası `PossessionId` **değişir** |
| T15 | `DiagnosticsDoNotChangeOutcome` | Diagnostics açık/kapalı → aynı event dizisi |
| — | `ClockAdvancesMonotonically` | Her adımda `ElapsedGameTimeMs` artar, asla azalmaz |
| — | `GameAndShotClocksConsumeTheSameLiveTime` | Action süresi iki sayaca da aynı miktarda yansır |
| — | `FourPeriodsThenCompleted` | 4 periyot sonrası `Completed` |
| — | `GuardProducesAbortedNotFalsifiedScore` | Aşırı küçük guard → `Aborted`; skor uydurulmaz |
| — | `ShotClockViolationEndsPossession` | Hücum saati dolunca possession kapanır, döngü ilerler |
| — | `SequenceIsUniqueAndContiguous` | Sequence 1'den başlar, boşluksuz ve tekil |
| — | `BoxScoreInvariantsHold` | 3PM ≤ 3PA ≤ FGA, FGM ≤ FGA, 3PM ≤ FGM, REB = OREB + DREB |
| — | `BoxScoreMatchesEventStream` | Projector çıktısı == engine'in kendi skor durumu |
| — | `FirstPossessionComesFromSeededSingleBitDraw` | Başlatan taraf 1-bit çekilişle belirlenir; bit terslenince taraf değişir |
| — | `EngineNeverWritesGameDataFiles` | Maç içinde dosya/clock/log bağımlılığı yok |

T03 (OVR etkisiz) M2'de **anlamsızdır**: OVR M2'de hiç yok. M4'te gösterim amaçlı
OVR eklendiğinde gerçek anlamda test edilecek; M2'de sahte bir "geçti" işareti konmayacak.

## 7. Kabul kriterleri

| Kriter | Nasıl doğrulanır |
|---|---|
| Temiz build | `dotnet build DreamTeam.slnx -c Release` → 0 uyarı, 0 hata |
| Tüm testler geçti | `dotnet test DreamTeam.slnx -c Release` → yeşil, M1'in 42 testi dahil |
| T01/T04/T05/T15 dar kapsamda | Yukarıdaki tablo |
| Skor korunumu | `EachScoringEventAddsPointsExactlyOnce` + `BoxScoreInvariantsHold` |
| Saat ilerliyor | `ClockAdvancesMonotonically` |
| Console tek maç üretir | `dotnet run --project src/DreamTeam.Simulator -c Release` |
| Motor saf C# | `dotnet list package` → MatchEngine ve Simulator'da paket yok |
| M1'e dokunulmadı | `git diff --stat f6aba1f..HEAD -- src/DreamTeam.Domain src/DreamTeam.MatchEngine/Randomness src/DreamTeam.MatchEngine/Core/MatchSetup.cs` boş; `SetupValidationTests` değişmedi |
| Eksiklik bildirimi | Console çıktısı ve `11_PROJECT_STATUS` "foul/FT/OT yok" diye açıkça belirtir |

## 8. Uygulama adımları

1. **Config + saat** — `EngineConfig`, `MatchClock`, `ConfigHash`. Test: saat birimleri ve hash kararlılığı.
2. **State çekirdeği** — `MatchPhase`, `PossessionState`, `MatchState`, `StepResult`, `MatchResult`.
3. **Event sözleşmesi** — `MatchEvent` + M2 event kayıtları + sequence üreteci.
4. **Resolver'lar** — `ShotResolver`, `TurnoverResolver`, `ReboundResolver`; hepsi saf, RNG girdisi açık.
5. **`ActionSelector`** — beş oyuncu + basit ağırlıklı seçim; `RosterOrdering` ile stabil sıra.
6. **`MatchEngine`** — `Create` / `Advance` / `Simulate`; guard ve terminal durumlar.
7. **Box score** — `BoxScoreProjector`, `PlayerBoxScore`, `TeamBoxScore`.
8. **Simulator** — `Program.cs` + `SingleMatchReport`.
9. **Testler** — yukarıdaki tablo, T01–T05 + T15 dahil.
10. **Doğrulama ve rapor** — Release + Debug koşu, paket listesi, M1 dokunulmazlık kontrolü.

## 9. Bilinçli olarak yapılmayacaklar

- M1 dosyalarının (`MatchSetup`, `MatchSetupValidator`, `SeededRandom`, `RosterOrdering`,
  `TestData`, `SetupValidationTests`) **hiçbir değişikliği**. Yeni ihtiyaç çıkarsa
  milestone kapsamı genişletilir, M1 sessizce değiştirilmez.
- Faul, FT, and-one, bonus, block, out-of-bounds, substitution, timeout, uzatma, energy.
- Tactics/pace davranışı (D34) — M4.
- Seeded RNG dışında herhangi bir rastgelelik; `System.Random` kullanımı.
- Console dışında kalıcı çıktı: JSON/CSV export, batch, rapor dosyası — M6.
- Fixture'lar JSON dosyası değil, C# kodu (D33). `ImmutableArray` serileştirme riski
  M2'de ölçeklenmez; M6'da erken test edilir.
- OVR, composite rating, türetilmiş rol güçleri — M4.

## 10. Riskler

| Risk | Etki | Azaltma |
|---|---|---|
| M2 sonuçları istatistiksel olarak anlamsız | Yanlış "motor çalışıyor" izlenimi | Console çıktısı ve durum belgesi kalibrasyon yapılmadığını açıkça yazar |
| Dört sonuç + sonlanma = eksik kural seti | M3 kuralları eklenince event şeması değişir | `SchemaVersion` baştan konur; M3'te artış normaldir |
| `Advance` imzası M5'te değişecek | API kırılması | Şimdiden kayıtlı; M5 genişletmesi planlı değişiklik |
| Config katsayıları kalibre değil | Denge iddiası riski | `Baseline` üzerinde "kalibre edilmemiş" etiketi; M6'da ölçüm |
| Action süreleri keyfi | Saat/sonlanma davranışı şüpheli | Yapısal testler (monotonluk, guard) sayısal değeri değil yapıyı doğrular |
| `Sequence` yeniden kullanılırsa box score bozulur | Çift muhasebe | `SequenceIsUniqueAndContiguous` testi |

## 11. Sonraki milestone bağlantısı

M3 kuralları (foul/FT/bonus/horn/uzatma/foul-out) bu çekirdeğin üstüne biner:
`Advance` aynı kalır, `MatchPhase` genişler, event tipleri çoğalır, `SchemaVersion` artar.
M2'nin `MatchState`, `MatchClock` ve `Advance` sözleşmeleri M3 tarafından değiştirilmez.
