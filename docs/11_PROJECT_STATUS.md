# Proje durumu ve oturum devri

Son güncelleme: 27 Eylül 2026.

## Şu anda

- Aşama: Uygulama. **M5 tamamlandı ve doğrulandı.**
- Aktif milestone: **M5 bitti → sırada M6 (CLI batch + 10K/100K deney ve denge adayı).**
- Uygulama yetkisi: **M5 için verildi ve kullanıldı.** M6 için yetki **yok**.
- Git: `main` == `origin/main` (`0fe9069`, M4 uygulaması; push sonrası doğrulandı).
- Monte Carlo: **0 maç.** 324 test içinde 40–400 seed'li smoke koşular var; 10K/100K deneyi **çalıştırılmadı** (M6).
- DB / runtime / hosting: seçilmedi. M1–M5 bunlara ihtiyaç duymadı.

## Son oturumda (M5 uygulaması) yapılanlar

- M5 planı onaylandı; **D79–D89** kilitlendi, **D90–D97** uygulama kararları olarak
  kayda geçti. **D78 ve Q11 kapandı.**
- **Oluşturulan motor dosyaları:** `Commands/ManagerCommand.cs` (3 enum +
  `CommandPayload` + `ScheduledManagerCommand`), `Commands/CommandQueue.cs`,
  `Commands/CommandValidator.cs`, `Commands/CommandResult.cs` (15 reddedilme
  sebep kodu), `Rules/SubstitutionPolicy.cs`, `Rules/TimeoutPolicy.cs`,
  `Rules/OvertimePolicy.cs`, `Replay/MatchSnapshot.cs` (+`MatchSnapshotData`),
  `Replay/MatchStateFingerprint.cs`, `Replay/MatchSnapshotSerializer.cs`,
  `Replay/ImmutableArrayByteConverter.cs`.
- **Değişenler:** `RulesProfile` (+`MaxOvertimePeriods`, +4 timeout alanı,
  +`FinalTwoMinutesMs` türevi), `EngineConfig.ComputeConfigHash` (+5 alan),
  `MatchState` (+`CommandQueue`), `StepResult` (+`CommandResults`),
  `MatchSimulation` (`Advance`/`Simulate` imzaları, komut sınırları, uzatma tavanı),
  `TeamMatchState` (+2 timeout sayacı), `MatchEventType` (18→**25**),
  `MatchEventPayloads` (+7 payload), `EventSchemaVersion` 3→**4**,
  `MatchResult` (+6 timeout alanı), `MatchSetupValidator` (dokunulmadı).
- **Yeni testler:** `M5TestData`, `CommandValidationTests` (22), `SubstitutionTests` (16),
  `TimeoutTests` (18), `SnapshotReplayTests` (18), `CommandDeterminismTests` (9),
  `ApplicationBoundaryTests` (13). **223 → 324 test.**
- **M1–M4 testleri güncellendi:** `MatchTerminationTests`'teki 0-0 testi yeniden
  adlandırıldı ve bölündü (D79 sonrası artık eylem guard'ı değil uzatma tavanı
  kesiyor).
- **6 gerçek hata bulundu ve düzeltildi**, hepsi regresyon testli. En kritik üçü:
  **D90** (tüm taktik komutları istisna fırlatıyordu — hiçbir taktik komutu
  uygulanamıyordu), **D92** (`Simulate` yanlış komutları siliyordu — dead-ball
  substitution'ı hiç gönderilmiyordu) ve **D94** (komut bir adım gecikmeli
  uygulanıyordu).

### Uygulama sırasında kullanıcıya açılan iki soru

1. **D96(a)** — Soru etiketi "normal basket sonrası pencere açılmaz" derken
   açıklaması isabetli basketi pencere sayıyordu. Kullanıcı açıklamayı onayladı:
   **pencere AÇILIR** (5+1 nokta).
2. **D89** — `ShortTimeoutsPerTeam = 3` hâlâ **kaynaksız**; onay bekliyor.

### Doğrulama kanıtı — gerçekten çalıştırılan komutlar

| Komut | Sonuç |
|---|---|
| `dotnet build DreamTeam.slnx -c Release --no-incremental` | Başarılı — **0 uyarı, 0 hata** |
| `dotnet test DreamTeam.slnx -c Release` | Başarılı — **324/324** (M1'in 42, M2'nin 79, M3'ün 32, M4'ün 62 testi dahil) |
| `dotnet test DreamTeam.slnx -c Debug` | Başarılı — **324/324** |
| `dotnet run --project src/DreamTeam.Simulator -c Release` | Completed, **5 periyot** (uzatma), 3180.0 s, **112-118**, 237 possession, `ConfigHash 349032b31595c15b` |
| Aynı komut iki kez → SHA256 karşılaştırma | `82239D3246F795D2…AA002C` — **iki koşuda aynı** |
| `dotnet list package` (MatchEngine, Simulator) | "Bu çerçeve için paket bulunamadı" — **sıfır paket** |
| Yasaklı çağrı taraması (`new Random`/`DateTime`/`Guid.NewGuid`/`Environment`/`Console`/`ILogger`/`File`/`Process`/`Sleep`) | **Temiz** (2 eşleşme `ShotMath.Logit`, yasaklı değil) |
| `git diff` (`MatchClock`, `IRandomSource`, `SeededRandom`, `EngineIdentity`) | **Boş — dokunulmadı** |

### Kritik hash / kimlik

- `EngineVersion.Current = "0.1.0"` (değişmedi), `RngIdentity` SplitMix64 v1 (değişmedi).
- `ConfigHash` M4'te `b043cfa08c4baecf` idi, **M5'te `349032b31595c15b`** —
  D79 (uzatma tavanı) ve D84 (timeout bütçeleri) alanları hash'e girdi.
- `EventSchemaVersion` 1 → 2 → 3 → **4**.
- `MatchEventType` 18 → **25** (planda "24 + 6" yazıyordu; bu bir **sayım hatasıydı**,
  7 yeni tür eklendi — D96).

### Gözlenen tek maç — denge kanıtı DEĞİLDİR

Seed 20260927, tek maç, argümansız simulator. Skor 112-118 ve 5 periyot; savunma
rotasyonu/kapanış modeli hâlâ yok. **10K/100K deneyi çalıştırılmadı; M6.**
Bu argümansız koşuda **hiç komut gönderilmez** (D81: motor yönetir).

## Son oturumda (M4 uygulaması) yapılanlar

- M4 planı onaylandı; **D57–D68** kilitlendi, **D69–D78** uygulama kararları olarak kayda geçti.
- **Oluşturulan motor dosyaları:** `Config/Tactics.cs` (3 enum + `TeamMatchSetup`), `Config/TacticsModel.cs`, `Config/DefenseModel.cs`, `Config/FatigueModel.cs`, `Config/PaceModel.cs`, `Core/PlayerMatchState.cs`, `Ratings/PlayerRatingTables.cs`, `Ratings/PlayerRatingCalculator.cs`, `Ratings/TeamRatingCalculator.cs`, `Tactics/OffensivePolicy.cs`, `Tactics/DefensivePolicy.cs`, `Actions/ShotQualityResolver.cs`, `Fatigue/FatigueCalculator.cs`.
- **Değişenler:** `MatchSetup` (D61 `TeamMatchSetup`), `TeamMatchState` (+taktik/tempo/`PlayerStates`/`Bench`), `MatchState`, `MatchResult` (+`PlayerEnergy`, +OVR), `MatchSimulation` (yeni çağrı sırası, `ConsumeLive`, periyot arası toparlanma), `ShotMath`, `ShotResolver`, `BlockResolver`, `TurnoverResolver`, `FoulResolver`, `ReboundResolver`, `ActionSelector`, `EligibilityPolicy`, `MatchSetupValidator` (+taktik/tempo), `MatchSetupValidation` (+2 hata kodu), `MatchEventPayloads` (`ShotAttemptPayload` +2 alan), `EngineConfig` (+4 model, `QualityScale`, `SelectionSpread`).
- **Yeni testler:** `M4TestData`, `RatingCompositeTests` (14), `TacticDistributionTests` (11), `DefensePolicyTests` (19), `PaceAndEnergyTests` (18). **158 → 222 test.**
- **M1–M3 testleri güncellendi** (mekanik `TeamMatchSetup` geçişi + üç M3 varsayımının düzeltilmesi).
- **10 gerçek hata bulundu ve düzeltildi**, hepsi regresyon testli. En kritik üçü: M3'ten kalan **kayıp savunma faulü** (D69), **enerjinin hiç düşmemesi** (D72) ve **yorgunluk kanalının fiilen ölü olması** (D73/D74 → `PerformanceMultiplier` ve `Interpolate` hataları).

## Son oturumda (M3 uygulaması) yapılanlar

- M3 planı onaylandı, **D43** (`NoLegalSubstitute` → `Aborted`) teyit edildi, kod yazıldı.
- **Oluşturulan motor dosyaları:** `Core/PendingShot.cs`, `Core/PendingFreeThrowSeries.cs`, `Core/FoulCounters.cs`, `Actions/FoulResolver.cs`, `Actions/BlockResolver.cs`, `Actions/FreeThrowResolver.cs`, `Actions/RimContactResolver.cs`, `Rules/ClockResetPolicy.cs`, `Rules/PeriodController.cs`, `Rules/EligibilityPolicy.cs`.
- **Genişletilenler:** `EngineConfig` (+`FoulModel`, `FreeThrowModel`, `FoulType`, `BlockProbability`, `RimContactProbability`, `OffensiveReboundShotClockMs`, `ComputeConfigHash`), `MatchPhase` (+`ShotPending`, `FreeThrows`), `MatchState` (+`PendingShot`, `PendingFrees`, +`NextFoulId`, +`NextFTSeriesId`), `TeamMatchState` (+`Fouls`, +`FoulOutPlayerIds`), `PossessionEndReason` (+`BonusFreeThrows`), `MatchEventType` (12→17), `MatchEventPayloads` (+5), `PlayerBoxScore`/`TeamBoxScore`/`BoxScoreProjector` (+PF, +FTA/FTM, +BLK), `MatchSimulation` (yeniden yazılan adım mantığı).
- **Yeni testler:** `M3TestData`, `M3RuleTests` (25 test), `M3OvertimeAndEdgeTests` (7 test).
- **M2 testleri güncellendi** (öncül netleşmesi, §12 sapma 10): `EveryProducedEventIsObservable`, `MissedShotAwardsNoFieldGoalPoints`, `NoOvertimeIsInventedForATiedMatch` → `TieIsResolvedByOvertimeNotByARandomWinner`, `EveryLiveMissLeadsToReboundOrFreeThrowSeriesOrHorn`, `ScoreEqualsTwoPointersPlusThreePointersPlusFreeThrows`, `OnlyRosterPlayersAppearInTheBoxScore`, `EveryEventAttributedToAPlayerIsInTheRoster`.
- **9 gerçek motor hatası bulundu ve düzeltildi**, her biri regresyon testli (bkz. aşağıdaki tablo ve plan §12).
- `SingleMatchReport` genişletildi: FT/PF/BLK satırları, abort yolunda olay akışı dökümü, güncel eksiklik bildirimi.
- `MatchSetup`, `MatchClock`, `MatchSetupValidator`, `Advance` imzası, `IRandomSource` **dokunulmadı** — `git diff` ile doğrulandı.

## M0 ve M1 özeti

- **M0:** Devir belgeleri okundu, repo keşfedildi (başlangıçta yalnız doküman vardı), `M1_IMPLEMENTATION_PLAN.md` yazıldı ve onaylandı.
- **M1:** Domain modelleri, setup doğrulama, `EngineIdentity`, SplitMix64 RNG. 42 test. `f6aba1f`, `b328fee`, `0ef8f1c`.
- M1'in dosyalarına M2 ve M3 sırasında **hiç dokunulmadı**; `MatchSetup`, `MatchSetupValidator`, `SeededRandom`, `RosterOrdering`, `SetupValidationTests` olduğu gibi duruyor.

## M3'te yapılanlar

**Uygulanan kural bütünlüğü**

- Faul çözümü: aksiyon başına faul, hücum/savunma/shooting ayrımı, tek kanonik karar ağacı (§3).
- Bonus (D40): periyot içinde 5. sayılan savunma faulünden itibaren 2 FT.
- Serbest atış serileri: and-one (1), kaçan shooting foul (2, üçlükte 3), bonuslu non-shooting (2). **FT arası canlı rebound yok**; yalnız and-one'un tek atışı canlıdır.
- **Kaçan shooting foul'da FGA sayılmaz** (06 §87); `CountsAsFieldGoalAttempt=false`. And-one'da FGA/FGM yazılır.
- Blok (T04): ayrı çekiliş, isabet çekilişi blok gerçekleşirse **tüketilmez** (05 §127). Blok ikinci FGA yazmaz, `ShotId`'yi paylaşır.
- Hücum saati reset tablosu (06 §6) `ClockResetPolicy`'de tek yerde. Çembere değen miss → 14 s; havada kalan → reset yok; savunma non-shooting faul → `min(kalan, 14 s)` (**saat asla artmaz**).
- `releaseTime < expiryTime` geçerli, eşitlikte ihlal (D42). Kontrol iki dalın ortak noktasında (D49).
- Uzatma: 5 dk, çoklu uzatma, eşitlik bozulana kadar. Kişisel fauller korunur, takım faulu **her periyotta** sıfırlanır (D46). Rastgele kazanan seçilmez.
- Foul-out + otomatik yedekleme (D41/D53). Yasal yedek yoksa `Aborted` (`NoLegalSubstitute`), motor kazanan uydurmaz (D43).
- Düdük sonrası geçerli bekleyen şut tamamlanır; isabetse puan yazılır (06 §2).

**Doğrulama kanıtı — gerçekten çalıştırılan komutlar**

| Komut | Sonuç |
|---|---|
| `dotnet build DreamTeam.slnx -c Release --no-incremental` | Başarılı — **0 uyarı, 0 hata** |
| `dotnet test DreamTeam.slnx -c Release` | Başarılı — **158/158** (M1'in 42, M2'nin 79 testi dahil) |
| `dotnet test DreamTeam.slnx -c Debug` | Başarılı — **158/158** |
| `dotnet run --project src/DreamTeam.Simulator -c Release` | Completed, 4 periyot, 2880.0 s, 104-100, FT 13-17 / 6-6, PF 8 / 12, BLK 4 / 6 |
| Aynı komut iki kez → SHA256 karşılaştırma | `BC7DF62F7406A57E…C037A34` — **iki koşuda aynı** |
| `dotnet list package` (MatchEngine, Simulator) | "Bu çerçeve için paket bulunamadı" — **sıfır paket** |

**Kritik hash / kimlik**

- `EngineVersion.Current = "0.1.0"` (değişmedi), `RngIdentity` SplitMix64 v1 (değişmedi).
- `ConfigHash` M2'de `e865f91b9824f616` idi, **M3'te `b87d9443ec56fb3b`** — yeni config alanları eklendi (D39 usulü korunur: SHA-256, sabit sıralı alan dizesi, 16 hex).
- `EventSchemaVersion` 1 → **2**.
- `rules-v0.2-simple-nba` profili korundu (D31): 4×12 dk, 24 s, 5 dk OT.

**Gözlenen tek maç — denge kanıtı DEĞİLDİR**

Seed 20260927, tek maç, argümansız simulator. Skor yüksek çünkü savunma çözümü
henüz yok (M4). 10K/100K deneyi çalıştırılmadı; M6.

## M2'de yapılanlar

**Oluşturulan yapı**

- `src/DreamTeam.Simulator/` — **yeni console projesi** (D33: argümansız tek maç), `Program.cs` + `SingleMatchReport.cs`.
- `src/DreamTeam.MatchEngine/Config/EngineConfig.cs` — `EngineConfig`, `RulesProfile`, `ShotModel`, `ActionModel`, `ActionProfile`, `ShotType`, `OffensiveAction`, `TurnoverKind`, `ConfigHash` (D39).
- `src/DreamTeam.MatchEngine/Core/` — `MatchSimulation` (D36), `MatchState`, `MatchClock`, `TeamMatchState`, `PossessionState`, `MatchPhase`, `StepResult`, `MatchResult`, `TeamSide`.
- `src/DreamTeam.MatchEngine/Actions/` — `ActionSelector`, `ShotResolver`, `TurnoverResolver`, `ReboundResolver`, `ShotMath`.
- `src/DreamTeam.MatchEngine/Randomness/WeightedSelector.cs` — saf ağırlıklı seçim.
- `src/DreamTeam.MatchEngine/Events/` — `MatchEvent` (zarf), `MatchEventType`, `MatchEventPayloads` (12 tür).
- `src/DreamTeam.MatchEngine/Projection/` — `PlayerBoxScore`, `TeamBoxScore`, `BoxScoreProjector`.
- `tests/` — `M2TestData`, `ClockTests`, `PureFunctionTests`, `PossessionIdentityTests`, `ShotOutcomeTests`, `BoxScoreInvariantTests`, `MatchTerminationTests`, `DeterminismTests`.

**Kilitli sürümler** — değişmedi: SDK 10.0.401, `net10.0`, xunit 2.9.3, `Microsoft.NET.Test.Sdk` 17.14.1, `xunit.runner.visualstudio` 3.1.4, SplitMix64 v1, `EngineVersion.Current = "0.1.0"`, `ConfigHash` örneği `e865f91b9824f616`.

**Doğrulama kanıtı — gerçekten çalıştırılan komutlar**

| Komut | Sonuç |
|---|---|
| `dotnet build DreamTeam.slnx -c Release` | Başarılı — 0 uyarı, 0 hata |
| `dotnet test DreamTeam.slnx -c Release` | Başarılı — **121/121** (M1'in 42 testi dahil) |
| `dotnet test DreamTeam.slnx -c Debug` | Başarılı — **121/121** |
| `dotnet run --project src/DreamTeam.Simulator -c Release` | Completed, 4 periyot, 2880.0 s, 1040 event, 97-85 |
| Aynı komut iki kez → SHA256 karşılaştırma | `714F344F…403D3` — **bayt bayt aynı** (T01b) |
| `dotnet list package` (MatchEngine, Simulator) | "Bu çerçeve için paket bulunamadı" — **sıfır paket** |
| `src/` içinde `new Random` / `DateTime` / `Guid.NewGuid` / `Environment` / `Console` / `ILogger` / `File` | Üretim kodunda **yok** |

## Kararlar

- D31–D39 ve M2'de bulunan 5 gerçek kod hatası: `docs/10_DECISIONS_AND_OPEN_QUESTIONS.md` bölüm 7.
- D40–D45 (M3 kilit), D46–D56 (M3 uygulama): aynı dosya bölüm 8 ve 9.
- **D57–D68** (M4 kilit kararları): bölüm 10.
- **D69–D78** (M4 uygulama kararları): bölüm 11. D78 **artık açık değil** → D79.
- **D79–D89** (M5 planlama kararları): bölüm 12. **D89 açık** (kaynaksız sayı).
- M3 sapmaları ve 9 hata: `docs/plans/M3_IMPLEMENTATION_PLAN.md` §12.
- M4 sapmaları (10) ve 10 hata: `docs/plans/M4_IMPLEMENTATION_PLAN.md` §12 ve §13.
- Kapanan açık sorular: Q04, Q05, Q06'nın yapısal kısmi, Q07, Q08, Q09,
  Q10'un "M4'te kapalı" yarısı, **Q11**.
- Açık kalan: **D89** (`ShortTimeoutsPerTeam` sayısı — kaynaksız yer tutucu),
  **Q10** (GameForm dağılımı/birimi — M6), Q12, Q13–Q18.

- **D79–D89** (M5 kilit kararları): `docs/10` bölüm 12. **D82 uygulama sırasında
  netleştirildi (D96)**; **D89 açık** (kaynaksız sayı).
- **D90–D97** (M5 uygulama kararları): `docs/10` bölüm 13.
- M5 sapmaları (12) ve 6 hata: `docs/plans/M5_IMPLEMENTATION_PLAN.md` §16 ve §17.
- Kapanan açık sorular: Q04, Q05, Q06'nın yapısal kısmi, Q07, Q08, Q09,
  Q10'un "M4'te kapalı" yarısı, **Q11**.
- Açık kalan: **D89** (timeout sayısı), **D87** (M7'de çözülecek), **Q10**,
  **Q12**, Q13–Q18.

## Kapsam dışı bırakılanlar (M3'te bilinçli olarak yok)

- Substitution pencereleri ve kullanıcı değişikliği — M5. M3 yalnız foul-out zorunlu değişikliğini yapar.
- Timeout hakkı ve pencereleri — M5.
- Out-of-bounds, ball out of play, jump ball, savunma 3 saniye, technical/flagrant faul.
- Frontcourt/backcourt FT reset ayrıntısı — kayıt altına alındı, uygulanmadı.
- Forfeit kavramı (motor kazanan seçmez) — ürün kararı, M7 adayı.
- Savunma taktik çözümü, tempo, enerji/stamina, rol uyumu, composite rating, OVR — M4.
- Tactics/pace `MatchSetup`'te **yok** (D34) — M4.
- Batch CLI, JSON/CSV export, fixture dosyaları, deney manifesti — M6.
- API, DB, SignalR, React, PixiJS.

## Bulunan ve düzeltilen gerçek hatalar

**M2'den kalan (M3'te de düzeltildiği doğrulananlar dahil)**

| Hata | Belirti | Durum |
|---|---|---|
| `StartPossession` hücum saatini sıfırlamıyordu | 3 şut denemesi, 349 turnover, 0-5 skor | Düzeltildi + regresyon testi |
| Takım puanı iki kez ekleniyordu | Box score 194, gerçek skor 97 | Düzeltildi + regresyon testi |
| İsabetli şutlarda `3PA` sayılmıyordu | "3P 4-0" imkânsız satırlar | Düzeltildi + regresyon testi |
| `BeginPeriod` toplam süreyi sıfırlıyordu | Rapor 720 s yerine 2880 s olmalıydı | Düzeltildi + regresyon testi |
| Şıtsız aksiyon event üretmiyordu | Motor "ilerleme yok" ağıyla abort | `ActionCompleted` eklendi + test |

**M3 sırasında bulunanlar**

| Hata | Belirti | Düzeltme | Regresyon testi |
|---|---|---|---|
| Faz `ShotPending`'te kalıyordu | `PendingShot` temizleniyor, faz dönmüyor; "ilerleme yok" abortu | Faz `LiveBall`'a döner | `ManualAdvanceLoopMatchesSimulate` |
| Serbest atış puanı box score'a yazılmıyordu | Skor 74, box score PTS 64 | `CountFreeThrowMade` puanı da artırır | `ScoreEqualsTwoPointersPlusThreePointersPlusFreeThrows` |
| Hücum faulü `ShotAttempt` yayınlıyordu | Settlement'sız şut; tek aksiyon iki kez sayılıyordu | Foul türü `ShotAttempt`'ten önce çözülür (D50) | `EveryShotAttemptIsSettledByExactlyOneMadeOrMissed` |
| `!shotAttempted` dalında hücum saati kontrolü yoktu | Şuta dönmeyen hücumlarda ihlal hiç kaydedilmiyordu | Kontrol iki dalın ortak noktasına taşındı (D49) | `ShotClockExhaustionEndsThePossessionWithoutAKick` |
| Şıtsız aksiyonda faul `ActionCompleted`'tan sonra çözülüyordu | Hücum faulü iki olay üretiyordu | Faul önce çözülür | `EveryProducedEventIsObservable` |
| Yedekleme beşi doldurmuyordu | Foul-out birikince "sahada oyuncu yok" | Kesme/sınırlama kaldırıldı (D53) | `OnCourtAlwaysHasExactlyFiveLegalPlayers` |
| Terminal durum eziliyordu | `NullReferenceException` / "ilerleme yok" | `ApplyFoul` sonrası terminal kontrolü (D52) | `NoLegalSubstituteAbortsMatchWithExplicitReason` |
| Takım faulu periyotlar arası taşınıyordu | 5. periyotta erken bonus | Her periyotta sıfırla (D46) | `TeamFoulCounterResetsAtEveryPeriodStart` |
| Skor eşitliği testi FT'yi hesaba katmıyordu | Yanlış negatif | `+ FTM` eklendi | `ScoreEqualsTwoPointersPlusThreePointersPlusFreeThrows` |

**M5 sırasında bulunanlar (6)**

| Hata | Belirti | Düzeltme | Regresyon testi |
|---|---|---|---|
| Tüm taktik komutları istisna fırlatıyordu | `ValidateForApplication` `default:` → `throw`; ActionDecision her aksiyonda sunulduğu için hiçbir taktik komutu uygulanamıyordu | Taktik/tempo için `null` dön; tanımsız tür hata vermeye devam eder (D90) | `TwoPendingTacticCommandsApplyInAcceptedOrderLastWriteWins` |
| `Simulate` yanlış komutları siliyordu | `RemoveRange(0, n)` filtrelenmemiş listeden siliyordu; dead-ball substitution'ı hiç gönderilmiyor, maç sonunda "Expired" ile reddediliyordu | Eşleşen komutların kendi indeksleriyle çıkar (D92) | `PendingCommandsAreExpiredWhenTheMatchEnds` |
| Komut bir adım gecikmeli uygulanıyordu | `Advance(state, commands)` çağrısı "şimdi gönderiliyor" demekti ama komut bir sonraki adımda boşaltılıyordu | Sunulan sınır o adımda varsa aynı adımda uygula (D94) | `ACommandIsAppliedAtTheBoundaryOfTheStepThatReceivesIt` |
| Süresi dolmuş komut yine de etki ediyordu | `Simulate` komutu tutarken `ExpiresAfterSequence` aşılabiliyor; kabulde denetim yoksa komut kuyruğa girip hemen uygulanıyordu | Süre denetimini kabulden önce yap (D95) | `PendingCommandsAreExpiredWhenTheMatchEnds` |
| Uygulanan komut kuyrukta kalıyordu | `Settle` yalnız `Settled` işaretliyordu; aynı komut ikinci kez uygulanabiliyordu | İşlenen komutları kuyruktan da çıkar (D93) | `SettledCommandsAreRemembered` |
| 0-0 maçı hâlâ 521 periyot üretiyordu | D79 tavanı bağlandıktan sonra 6 periyotta duruyor; test adı "guard" derdi ve yanlıştı | Test yeniden adlandırıldı ve bölündü | `DegenerateZeroScoreMatchIsAbortedAtTheOvertimeLimitWithoutFalsifyingTheScore`, `TheOvertimeLimitPreventsTheActionGuardFromFiring` |

## Açık riskler

1. **Cross-platform bit düzeyi eşitlik kanıtlanmadı** (D23 kapsamı dışı, M6'da ölçülecek).
2. **Katsayılar kalibre değil.** M5 sonrası gözlenen tek maç 112-118, 5 periyot.
   Savunma rotasyonu/kapanış modeli yok. 10K/100K deneyi **M6'da ve
   çalıştırılmadı.**
3. **T15 (diagnostics) hâlâ yazılamaz** — diagnostics yüzeyi yok. M5'te de
   yazılmadı; **sahte "geçti" konulmadı.** M6'da eklenince test edilebilir.
4. **Serileştirme ölçüldü ve çalışıyor.** `System.Text.Json` sıfır paketle
   çalışıyor, `ImmutableArray<T>` ve `required`+`init` record round-trip'i doğru,
   RNG state 8 bayt ve kesin. **Kalan:** enum'lar varsayılan sayısal olurdu —
   M5 isim tabanlıya çevirdi (D88). `MatchState` doğrudan serileştirilemez
   (`Func<>` alanları); `MatchSnapshotData` DTO'su kullanılıyor (D97).
5. **`Advance`/`Simulate` imzaları M5'te genişledi** — kayıtlı planlı değişiklik.
   Parametresiz aşırı yüklemeler **korundu** ve `CommandList.Empty` ile aynıdır;
   ikinci bir motor yolu **yazılmadı** (H03). `MatchPhase.DeadBall` hâlâ
   **kalıcı yazılmıyor** (D54 korunuyor) — komutlar aynı adımın içinde
   `HandleAfterPossession` ve `RunAction` sınırlarından boşaltılıyor.
6. **CI tanımlı değil.** Testler yalnız yerelde koştu.
7. **`.cs` dosyalarında kod yorumları ASCII'ye çevrildi.** PowerShell'in
   `Get-Content`/`Set-Content` çift kodlaması Türkçe karakterleri bozdu; 8 dosya
   kurtarıldı. Kaynak dosyalarda bundan sonra ASCII yorum kullanılacak; belge
   düzenlemesinde `[IO.File]::ReadAllText/WriteAllText` + `UTF8Encoding($false)`
   kullanıldı ve bayt seviyesinde doğrulandı.
8. **Çember teması şut kalitesine girmiyor.** `RimContactResolver` yalnız saat
   politikasını besliyor. İkinci tüketici hâlâ bağlanmadı; iki yerde ayrı etki
   yazılırsa 05 §3'teki "iki kat sayma" hatasına döner. M6 adayı.
9. **`ImmutableArray<T>` içeren `record`'larda değer eşitliği bozuk** (D87,
   **ölçüldü**). `Team`, `TeamMatchSetup`, `MatchSetup`, `MatchState` etkilenir.
   M5'te `MatchStateFingerprint` ile aşıldı; **M7'de kalıcı katmanda çözülmeli**
   (03 §"Match completion idempotent").
10. **`ShortTimeoutsPerTeam = 3` kaynaktan gelmiyor** (D89). Yer tutucu; kullanıcı
    onayı bekliyor.
11. **Timeout canlı topta uygulanmıyor** (D84 sapması). Üründe hissedilir; 06 §23'e
    dayanıyor ve simulator raporunda açıkça yazılıyor.
12. **AI fallback = motor yönetir** (D81). Yönetici hiç komut göndermezse maç motor
    tarafından oynanır. "Devraldım" modu ürün kararı olarak M7'ye bırakıldı.
13. **`MatchResult` tüm event'leri bellekte tutuyor** ve M5'te 6 timeout alanı
    eklendi. 08 §8'in bellek sınırlı özet kipi M6'nın işi.
14. **Snapshot boyutu**: `MatchState` tüm kadroyu taşıyor; 100K deneyde snapshot
    maliyeti ölçülmedi. M6.

## Sonraki tek uygulanabilir görev

**M6 planını yazmak** — kod yazmadan `docs/plans/M6_IMPLEMENTATION_PLAN.md`. Kapsam:

- **CLI batch** (`DreamTeam.Simulator` genişlemesi): argümansız D33 kuralı
  gevşer; seed aralığı, maç sayısı ve çıktı biçimi.
- **10K/100K deney**: ilk gerçek Monte Carlo ölçümü. `MatchResult` hâlâ tüm
  event'leri bellekte tutuyor; **bellek sınırlı özet kipi** (08 §8) M6'nın işi.
- **Denge adayı raporu**: ortalama skor, possession sayısı, FT oranı, tempo
  dağılımı, enerji dağılımı; tekrarlanabilirlik.
- **T15** (diagnostics açık/kapalı → aynı domain outcome) burada yazılabilir:
  diagnostics yüzeyi M6'da eklenir.
- **Q10** — GameForm dağılımı/birimi burada çözülür.
- **Q18** — sezon referansı ve sayısal release eşikleri.
- Katsayı kalibrasyonu: 10K ölçümü → ayar → 100K doğrulama. **M5'te hiç
  ölçülmedi.**

## Her oturum sonunda doldurulacak kayıt


- Tarih ve aktif milestone
- Tamamlanan iş ve ilgili commit/dosyalar
- Çalıştırılan komut/test, sonuç ve varsa başarısızlık
- Sürümler, config/fixture hash ve seed seti (simülasyon varsa)
- Yapılan ve açık kalan kararlar
- Çözülmemiş risk/engel
- Sonraki tek uygulanabilir görev
- Geçerli uygulama yetkisinin kapsamı

## Doküman bakım kuralı

Özellik tamamlandığında yalnız burada işaretlemek yeterli değildir; gerçek davranış ilgili spec'ten farklıysa spec ve karar günlüğü de güncellenir. Planlarda checkbox işaretleri gerçek doğrulamadan sonra tamamlanır. Raporlanmamış test başarısı varsayılmaz.
