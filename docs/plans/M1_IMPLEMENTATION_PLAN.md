# M1 — İlk Güvenilir Yapı Taşı: Uygulama Planı

> **Durum:** Kullanıcı tarafından 27 Eylül 2026'da onaylandı ve uygulandı. Aşağıdaki
> kabul kriterleri gerçek koşumla doğrulandı. Kanıt: `docs/11_PROJECT_STATUS.md`.
>
> **Kapsam:** Domain temel modelleri, setup doğrulama, snapshot/version kimliği, deterministik RNG.
>
> **Dışında:** Tam maç simülasyonu, API, DB, SignalR, React, PixiJS, migration, deploy.

## 0. Uygulama sırasındaki sapmalar (kullanıcı onaylı)

| Sapma | Plandaki | Gerçekleşen | Gerekçe |
|---|---|---|---|
| Solution biçimi | `DreamTeam.sln` | `DreamTeam.slnx` | .NET 10 SDK'sının `dotnet new sln` şablonu XML solution formatı üretir. Karar: D28. |
| Doğrulama sözleşmesi | `ValidationException` fırlatır | `MatchSetupValidationResult` döner (kod + alan yolu + mesaj) | 07 belgesi reddetme sebebinin kaydedilmesini, M7 API'sinin alan bazlı hata göstermesini gerektirir. Tüm hatalar toplanır, ilk hatada durulmaz. Karar: D26. |
| Solution/proje yolları | plana uygun | plana uygun | Değişiklik yok. |
| Ek dosyalar | yok | `EngineVersion.cs`, `RngIdentity.cs`, `MatchSetupValidation.cs`, `RosterOrdering.cs`, `TestData.cs` | Sürüm sabitlerinin ve doğrulama tablosunun tek kaynakta tutulması; 08'in istediği "stable ordering" testinin dayanağı. |
| Test sayısı | 16 | 42 | Sınır durumları, kimlik uyumsuzluğu, rating kapsamı, state roundtrip ve aralık kontrolleri eklendi. |

## 1. Keşif Özeti

### Repository durumu

| Öğe | Gerçek durum |
|---|---|
| Git repository | **Yok** — `git status` "not a git repository" hatası verdi |
| Solution (.sln/.slnx) | **Yok** |
| Proje (.csproj) | **Yok** |
| Test altyapısı | **Yok** |
| global.json | **Yok** |
| Directory.Build.props | **Yok** |
| Mevcut dosyalar | `README.md`, `AGENTS.md`, `CODEX_START.md`, `docs/` (13 belge + M0 plan) |
| .NET SDK | **10.0.401** kurulu (ayrıca 2.2, 3.1, 9.0 SDK'ları mevcut) |
| İşletim sistemi | Windows 10.0.26200, win-x64 |

**Sonuç:** Repository, belgeler dışında boş bir başlangıç noktasıdır. M1, sıfırdan solution ve proje yapısı kurar.

### Belgelerdeki çelişkiler ve teknik eksikler

| # | Konu | Durum | M1 etkisi |
|---|---|---|---|
| C01 | `Stamina` attribute ile `Energy` state karışıklığı riski | H01 ile çözüldü: attribute `Stamina`, state `Energy` | M1'de doğru isimlendirme kullanılacak |
| C02 | "Aynı seed aynı maç" yetersizliği | H04 ile çözüldü: engine/rules/config/RNG/roster sürümleme gerekli | M1'de EngineIdentity ve snapshot version tanımlanacak |
| C03 | RNG algoritması belirlenmemiş | Q02 açık; SplitMix64 önerisi var | M1'de SplitMix64 seçilecek, golden vector ile doğrulanacak |
| C04 | Test framework belirlenmemiş | Q01 açık; xUnit en yaygın .NET seçimi | M1'de xUnit seçilecek |
| C05 | Target framework belirlenmemiş | Q01 açık; SDK 10.0.401 kurulu | M1'de net10.0 seçilecek |
| C06 | Kimlik tipi (Guid vs strong ID) | Q03 açık; domain belgesi "güçlü kimlik tipleri veya tutarlı Guid" der | M1'de Guid kullanılacak (basitlik + determinism için yeterli) |
| C07 | Zaman birimi | H02 ile netleşti: integer milisaniye | M1'de `GameClockMs`, `ShotClockMs` int64/ms |
| C08 | OVR formülü seçilmedi | GDD'de açık; OVR gösterim için, engine girdisi değil | M1'de OVR yok; composite rating'ler M4'te |
| C09 | Pozisyon dışı oynatma cezası tanımlı değil | GDD'de açık | M1'de yok; M4'te ele alınacak |
| C10 | Kadro büyüklüğü açık ürün kararı | GDD'de açık; test fixture'ları için 10 kişilik öneri var | M1'de 10 kişilik fixture kabul edilecek (ürün kararı değil) |

**M1'i gerçekten engelleyen kararlar:** C03 (RNG algoritması), C04 (test framework), C05 (target framework). Bunlar M1 başlamadan önce netleşmelidir.

## 2. Karar Önerileri

| ID | Karar | Öneri | Gerekçe |
|---|---|---|---|
| K01 | Target framework | `net10.0` | SDK 10.0.401 kurulu; en güncel LTS öncesi sürüm |
| K02 | Test framework | `xUnit` | .NET ekosisteminde en yaygın; `dotnet test` ile native entegrasyon |
| K03 | RNG algoritması | `SplitMix64` | Basit, hızlı, 64-bit state, platformlar arası deterministik; .NET `Random` yerine özel implementasyon |
| K04 | Kimlik tipi | `Guid` | Tutarlı, basit, determinism için yeterli; strong ID gereksiz karmaşıklık |
| K05 | Zaman birimi | `int64` milisaniye | H02 ile uyumlu; floating point yok |
| K06 | Proje yapısı | `src/` + `tests/` + `fixtures/` + `config/` | Mimari belgesiyle uyumlu |
| K07 | Solution adı | `DreamTeam.sln` | Proje adıyla tutarlı |

## 3. Oluşturulan Dosya Yapısı (gerçek)

```
DreamTeam.slnx                             # D28: .NET 10 SDK'sının ürettiği biçim
global.json                                # SDK 10.0.401 kilidi (D20)
Directory.Build.props                      # net10.0, nullable, TreatWarningsAsErrors
src/
  DreamTeam.Domain/                        # paket referansı yok
    Players/Position.cs
    Players/PlayerRatings.cs               # 18 attribute, int
    Players/Player.cs
    Teams/Team.cs
    Teams/Lineup.cs
    Teams/RosterOrdering.cs                # kanonik sıralama
  DreamTeam.MatchEngine/                   # paket referansı yok
    Core/EngineVersion.cs
    Core/EngineIdentity.cs
    Core/MatchSetup.cs
    Core/MatchSetupValidation.cs           # hata kodu + hata + sonuç
    Core/MatchSetupValidator.cs
    Randomness/IRandomSource.cs
    Randomness/RngIdentity.cs
    Randomness/SeededRandom.cs
tests/
  DreamTeam.MatchEngine.Tests/
    TestData.cs                            # kurgusal fixture üreticileri
    SetupValidationTests.cs
    SeededRandomTests.cs
```

## 4. Gerçekleşen Model ve Interface Sözleşmeleri

### 4.1 Domain — Player

```csharp
namespace DreamTeam.Domain.Players;

public enum Position { PG, SG, SF, PF, C }

public sealed record Player
{
    public required Guid Id { get; init; }
    public required string DisplayName { get; init; }
    public required Position Position { get; init; }
    public required PlayerRatings Ratings { get; init; }
}

public sealed record PlayerRatings
{
    public required int Speed { get; init; }
    public required int Strength { get; init; }
    public required int Vertical { get; init; }
    public required int Stamina { get; init; }
    public required int Inside { get; init; }
    public required int MidRange { get; init; }
    public required int ThreePoint { get; init; }
    public required int FreeThrow { get; init; }
    public required int BallHandling { get; init; }
    public required int Passing { get; init; }
    public required int OffBall { get; init; }
    public required int PostOffense { get; init; }
    public required int PerimeterDefense { get; init; }
    public required int InteriorDefense { get; init; }
    public required int Steal { get; init; }
    public required int Block { get; init; }
    public required int Rebounding { get; init; }
    public required int BasketballIQ { get; init; }
}
```

**Invariant:** Tüm attribute değerleri 0–100 aralığında. Sınır dışı veya NaN kabul edilmez.

### 4.2 Domain — Team ve Lineup

```csharp
namespace DreamTeam.Domain.Teams;

public sealed record Team
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required ImmutableArray<Player> Roster { get; init; }
}

public sealed record Lineup
{
    public required ImmutableArray<Guid> PlayerIds { get; init; }
}

public static class RosterOrdering
{
    public static ImmutableArray<Player> Canonical(ImmutableArray<Player> roster);
}
```

**Invariant:** Lineup beş farklı oyuncu içerir; tüm oyuncular takım kadrosundadır.
`ImmutableArray<T>` seçimi D25: dışarıdan verilen `List` sonradan değiştirilirse
snapshot etkilenmez. `RosterOrdering.Canonical` girdi sırasından bağımsız
kanonik sıra üretir (Id, ardından DisplayName ordinal).

### 4.3 MatchEngine — MatchSetup

```csharp
namespace DreamTeam.MatchEngine.Core;

public sealed record MatchSetup
{
    public required Guid MatchId { get; init; }
    public required Team Home { get; init; }
    public required Team Away { get; init; }
    public required Lineup HomeLineup { get; init; }
    public required Lineup AwayLineup { get; init; }
    public required ulong Seed { get; init; }
    public required EngineIdentity Engine { get; init; }
}
```

### 4.4 MatchEngine — EngineIdentity

```csharp
namespace DreamTeam.MatchEngine.Core;

public sealed record EngineIdentity
{
    public required string EngineVersion { get; init; }
    public required string RulesVersion { get; init; }
    public required string BalanceConfigHash { get; init; }
    public required string RngAlgorithm { get; init; }
    public required string RngVersion { get; init; }
}
```

### 4.5 MatchEngine — IRandomSource

```csharp
namespace DreamTeam.MatchEngine.Randomness;

public interface IRandomSource
{
    ulong NextUInt64();
    double NextDouble();                      // [0.0, 1.0)
    void GetState(Span<byte> destination);     // tam olarak StateSizeInBytes
    void SetState(ReadOnlySpan<byte> state);   // tam olarak StateSizeInBytes
}
```

### 4.6 MatchEngine — SeededRandom ve RngIdentity

```csharp
namespace DreamTeam.MatchEngine.Randomness;

public static class RngIdentity
{
    public const string Algorithm = "SplitMix64";
    public const string Version = "1";
}

public sealed class SeededRandom : IRandomSource
{
    public const int StateSizeInBytes = sizeof(ulong);
    public SeededRandom(ulong seed);
    public ulong NextUInt64();
    public double NextDouble();                      // (u >> 11) * 2^-53
    public void GetState(Span<byte> destination);
    public void SetState(ReadOnlySpan<byte> state);
}

public static class EngineVersion      // DreamTeam.MatchEngine.Core
{
    public const string Current = "0.1.0";
}
```

**Notlar:** State tek `ulong`'dur ve little-endian serileştirilir. `NextDouble`
tüm bitleri `ulong.MaxValue`'a bölmez, üst 53 biti `2^-53` ile çarpar; bu
[0, 1) sınırını garanti eder ve platform davranışına bağlı kalmaz.
`EngineVersion` assembly informational version'ı **kullanmaz**: build metadata'sı
hash içerdiğinden aynı kaynak aynı kimliği üretmezdi.

### 4.7 MatchEngine — Doğrulama sözleşmesi

```csharp
namespace DreamTeam.MatchEngine.Core;

public static class MatchSetupValidator
{
    public const int RequiredLineupSize = 5;
    public const int MinimumRating = 0;
    public const int MaximumRating = 100;

    public static MatchSetupValidationResult Validate(MatchSetup setup);
}

public enum MatchSetupErrorCode { /* 17 kod: None dahil */ }

public sealed record MatchSetupValidationError(
    MatchSetupErrorCode Code, string Field, string Message);

public sealed class MatchSetupValidationResult
{
    public static MatchSetupValidationResult Valid { get; }
    public bool IsValid { get; }
    public IReadOnlyList<MatchSetupValidationError> Errors { get; }
    public bool Contains(MatchSetupErrorCode code);
}
```

## 5. Bağımlılıklar

| Proje | Bağımlılıklar |
|---|---|
| `DreamTeam.Domain` | **Yok** — `dotnet list package`: "Bu çerçeve için paket bulunamadı" |
| `DreamTeam.MatchEngine` | `DreamTeam.Domain` proje referansı; **paket referansı yok** |
| `DreamTeam.MatchEngine.Tests` | `DreamTeam.MatchEngine` + xunit paketleri |

**Kilitlenen paket sürümleri (gerçek ortamdan, `dotnet list package` ile doğrulandı):**

| Paket | İstenen | Çözümlenen |
|---|---|---|
| `xunit` | 2.9.3 | 2.9.3 |
| `xunit.runner.visualstudio` | 3.1.4 | 3.1.4 |
| `Microsoft.NET.Test.Sdk` | 17.14.1 | 17.14.1 |
| `coverlet.collector` | 6.0.4 | 6.0.4 |

**SDK:** 10.0.401 (`global.json` ile kilitli, `rollForward: latestPatch`).

## 6. Test Senaryoları ve Kabul Kriterleri

### 6.1 SetupValidationTests (21 test metodu)

| Test | Senaryo | Beklenen |
|---|---|---|
| `ValidSetupWithFiveDistinctPlayersIsAccepted` | Beş farklı uygun oyuncu | `IsValid` |
| `LineupMayTakeAnyFiveDistinctRosterPlayers` | 9 kişilik kadro, 5'lik alt küme | `IsValid` |
| `SamePlayerInTwoSlotsIsRejected` | Aynı oyuncu iki slotta | `DuplicateLineupPlayerId` |
| `LineupPlayerOutsideRosterIsRejected` | Kadro dışı oyuncu | `LineupPlayerNotInRoster` |
| `FewerThanFivePlayersIsRejected` | 4 oyuncu | `LineupSizeInvalid` |
| `MoreThanFivePlayersIsRejected` | 7 oyuncu | `LineupSizeInvalid` |
| `RatingBoundsZeroAndHundredAreAccepted` | 0 ve 100 sınırları | `IsValid` |
| `RatingOutsideZeroHundredIsRejected` (−1, 101, int.Min/Max) | Sınır dışı rating | `RatingOutOfRange`, alan `.Ratings.ThreePoint` |
| `EveryRatingAttributeIsCoveredByValidation` | 18 attribute'ün tamamı | Kapsam senkron; 18 hata, alan adları eşleşir |
| `SetupIsIsolatedFromExternalCollectionMutation` | Kaynak liste setup sonrası değişiyor | Snapshot değişmez, hâlâ geçerli |
| `EmptyMatchIdIsRejected` | `Guid.Empty` | `MatchIdMissing` |
| `SameTeamOnBothSidesIsRejected` | Home == Away | `SameTeamOnBothSides` |
| `DuplicateRosterPlayerIdIsRejected` | Kadroda yinelenen oyuncu | `DuplicateRosterPlayerId` |
| `EmptyRosterIsReportedWithoutThrowing` | Tanımsız `ImmutableArray` | `RosterMissing`, istisna yok |
| `UninitializedLineupIsReportedWithoutThrowing` | Tanımsız lineup | `LineupMissing`, istisna yok |
| `IncompleteEngineIdentityIsRejected` (5 alan) | Boş alanlar | `EngineIdentityIncomplete` |
| `EngineVersionMismatchIsRejected` | Farklı motor sürümü | `EngineIdentityIncomplete` |
| `RngIdentityMismatchIsRejected` | Setup başka RNG iddia ediyor | `RngIdentityMismatch` |
| `ValidationReportsAllProblemsInsteadOfFailingFast` | Üç hata birlikte | Üçü de raporlanır |
| `CanonicalRosterOrderDoesNotDependOnInputOrder` | Ters sıralı kadro | Aynı kanonik sıra |
| `CanonicalOrderingHandlesUninitializedRoster` | `default` | Boş döner, istisna yok |

### 6.2 SeededRandomTests (16 test metodu)

| Test | Senaryo | Beklenen |
|---|---|---|
| `SeedZeroMatchesGoldenSequence` | seed 0 | 10 sabit `ulong` |
| `NonZeroSeedMatchesGoldenSequence` | seed 12345 | 10 sabit `ulong` |
| `NextDoubleMatchesGoldenSequence` | seed 0 | 5 sabit `double` |
| `StateAfterFiveDrawsMatchesGoldenState` | 5 çekiliş sonrası state | `0x1715609F7C746C69` |
| `RestoringStateContinuesTheSameStream` | 7 çekiliş → state → restore | 32 çekiliş aynı |
| `StateRoundTripDoesNotChangeEncoding` | Serileştirme sırası | Golden dizi aynen tekrarlanır |
| `SameSeedProducesSameSequence` | 1000 çekiliş | Tam eşitlik |
| `DifferentSeedsProduceDifferentSequences` | seed 1 vs 2 | Farklı |
| `ConsecutiveSeedsAreNotSequentiallyCorrelated` | seed 1000 vs 1001 | İlk çıktılar > 1e6 farklı |
| `InterleavedDrawsDoNotAffectEachOther` | Araya `NextDouble` girer | Ham çıktılar ikişer adım kayar |
| `NextDoubleStaysWithinUnitInterval` | 100.000 çekiliş | Hepsi [0, 1-2^-53] içinde; uçlar görüldü |
| `NextDoubleConsumesExactlyOneStateStepPerCall` | 5 çift çekiliş | State tam 5 adım ilerlemiş |
| `StateRejectsWrongLengthBuffer` | 7 ve 9 bayt | `ArgumentException` |
| `RngIdentityIsStableAndMatchesTheImplementation` | Sabitler | `SplitMix64` / `1` / 8 bayt |

### 6.3 Kabul Kriterleri — doğrulandı

| Kriter | Sonuç | Kanıt |
|---|---|---|
| `dotnet build` hatasız | **Geçti** | Release: 0 uyarı, 0 hata |
| `dotnet test` tüm testler geçti | **Geçti** | Release ve Debug: 42/42 başarılı |
| Golden vector: SplitMix64 sıfır çıkış | **Geçti** | `SeedZeroMatchesGoldenSequence`; kaynak https://prng.di.unimi.it/splitmix64.c, çapraz kontrol Python + Node |
| Immutable snapshot: dış mutasyon etkisiz | **Geçti** | `SetupIsIsolatedFromExternalCollectionMutation` |
| Determinism: aynı seed + aynı sıra | **Geçti** | `SameSeedProducesSameSequence`, `RestoringStateContinuesTheSameStream`; iki ayrı süreçte (Release + Debug) aynı golden değerler |
| Motor saf C# | **Geçti** | `dotnet list package`: Domain ve MatchEngine'de paket yok; `src/` içinde `new Random`/`DateTime`/`ILogger` eşleşmesi yalnızca yorum satırlarında |
| "Farklı log ayarı etkisiz" | **Geçti (yapısal)** | Motor projelerinde loglama paketi/çağrısı yok; loglama oyun RNG'sini tüketecek bir yol içermiyor |
| Runtime ve config identity kaydı | **Geçti** | `EngineIdentity` + `EngineVersion.Current` + `RngIdentity`; uyumsuzluk reddi testli |

## 7. Uygulama Adımları — tamamlandı

1. **İskelet:** `global.json`, `Directory.Build.props`, `DreamTeam.slnx`, üç proje, proje referansları.
2. **Domain:** `Position`, `PlayerRatings` (18 int), `Player`, `Team`, `Lineup`, `RosterOrdering`.
3. **Core:** `EngineVersion`, `EngineIdentity`, `MatchSetup`, `MatchSetupValidation`, `MatchSetupValidator`.
4. **RNG:** `IRandomSource`, `RngIdentity`, `SeededRandom`.
5. **Testler:** `TestData`, `SetupValidationTests`, `SeededRandomTests`.
6. **Doğrulama:** `dotnet build` (Release + Debug), `dotnet test` (Release + Debug), `dotnet list package`.

## 8. Varsayımlar

| # | Varsayıma | Gerekçe |
|---|---|---|
| A01 | .NET 10 SDK kalıcı olarak kullanılacak | Ortamda kurulu; global.json ile kilitlenecek |
| A02 | xUnit test framework olarak kullanılacak | .NET standardı; `dotnet test` native destek |
| A03 | SplitMix64 RNG algoritması seçildi | Basit, hızlı, deterministik, 64-bit state |
| A04 | Guid kimlik tipi kullanılacak | Basit, tutarlı, determinism için yeterli |
| A05 | 10 kişilik test fixture kabul edildi | GDD'de öneri var; ürün kadro büyüklüğü M7'de karar verilecek |
| A06 | OVR formülü M1'de yok | GDD'de açık; composite rating'ler M4'te |
| A07 | Pozisyon dışı oynatma cezası M1'de yok | GDD'de açık; M4'te ele alınacak |

## 9. Açık Sorular — yanıtlandı

| ID | Soru | Karar | Durum |
|---|---|---|---|
| Q01 | Target framework / test framework | `net10.0` + xUnit 2.9.3 (SDK 10.0.401 kilitli) | Kullanıcı onayı 27.09.2026 — D20, D21 |
| Q02 | RNG algoritması ve determinism kapsamı | SplitMix64, sürüm `1`; garanti aynı kilitli runtime içinde | Kullanıcı onayı 27.09.2026 — D22, D23 |
| Q03 | Kimlik tipi ve snapshot | `Guid`; `ImmutableArray<T>` ile yapısal değişmezlik | Kullanıcı onayı 27.09.2026 — D24, D25 |

Bu üçü M1'i engelleyen kararlardı; artık açık değildir. M1'i etkilemeyen
sorular (Q04–Q18) kendi milestone'larında karara bağlanmaya devam eder.

## 10. Sonraki Milestone Bağlantısı

M1 tamamlandı. M2'de kullanılacak M1 çıktıları:
- `MatchSetup` + `MatchSetupValidator.Validate` — tek giriş kapısı
- `SeededRandom` / `IRandomSource` — 08'in istediği kontrollü test double sınırı
- `EngineIdentity` / `RngIdentity` / `EngineVersion` — replay kimliği
- `RosterOrdering.Canonical` — aday listelerinin stabil sırası

M2'de eklenecekler: `MatchEngine`, `MatchState`, `PossessionState`, `MatchClock`,
`ActionSelector`, `ShotResolver`, `TurnoverResolver`, `ReboundResolver`,
events, `BoxScoreProjector` ve console giriş noktası.

## 11. Riskler ve durum

| Risk | Durum | Azaltma / kalan iş |
|---|---|---|
| Golden vector yanlış hesaplanırsa yanlış "başarılı" test | **Kapatıldı** | Resmî referans + iki bağımsız uygulama (Python, Node) ile üçlü doğrulama |
| .NET 10 SDK güncellenirse `global.json` kilidi eski kalır | Açık | `rollForward: latestPatch`; SDK yükseltmesinde `global.json` güncellenir |
| `ImmutableArray<T>` JSON serileştirmesi M6/M7'de sürpriz çıkarabilir | Açık | M6'da fixture okuma/yazma ile erken doğrulanacak |
| Setup digest/hash'i hesaplanmıyor | Açık | M1 kapsamı dışı; 03'te istenen digest M2/M6 config ile birlikte eklenmeli |
| Cross-platform bit düzeyi eşitlik kanıtlanmadı | Bilinçli erteleme | D23 kapsamı dışında; M6'da ayrıca ölçülecek |
| `IRandomSource` henüz tüketici yok | Açık | M2'de `MatchEngine` bu arayüzü tüketmeye başlayacak |
| Git repository yok | **Kapatıldı (27.09.2026)** | Kullanıcı uzak repo'yu oluşturdu; `main` → `origin/main` ile eşitlendi (`f6aba1f`, `b328fee`, `0ef8f1c`) |
