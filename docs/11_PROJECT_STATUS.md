# Proje durumu ve oturum devri

Son güncelleme: 27 Eylül 2026.

## Şu anda

- Aşama: Uygulama. **M3 tamamlandı ve doğrulandı.**
- Aktif milestone: **M3 bitti → sırada M4 (oyuncu/taktik kararlarının etkisi).**
- Uygulama yetkisi: **M3 için verildi ve kullanıldı** (plan onaylandı, D43 teyit edildi). M4 için yetki **yok**.
- Git: `main` == `origin/main` (M3 commit'i ile eşitlendi; push sonrası doğrulandı).
- Monte Carlo: **0 maç.** 158 test içinde 40–400 seed'li smoke koşular var; 10K/100K deneyi **çalıştırılmadı** (M6).
- DB / runtime / hosting: seçilmedi. M1–M3 bunlara ihtiyaç duymadı.

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
- D40–D45 (M3 kilit kararları) ve D46–D56 (M3 uygulama kararları): aynı dosya bölüm 8 ve 9.
- M3 plan sapmaları ve bulunan 9 hata: `docs/plans/M3_IMPLEMENTATION_PLAN.md` §12.
- Kapanan açık sorular: Q04, Q05, Q06'nın yapısal kısmi, **Q07** (D40–D43).
- Açık kalan: Q08–Q11, Q13–Q18. **Q10** (`GameForm`) M4 kapsamına girdi.

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

## Açık riskler

1. **Cross-platform bit düzeyi eşitlik kanıtlanmadı** (D23 kapsamı dışı, M6'da ölçülecek).
2. **Katsayılar kalibre değil.** Savunma çözümü olmadığı için puan yüksek (104-100). Faul oranı gözle ayarlandı (D56) ama ölçülmedi. 10K/100K deneyi M6'da.
3. **T03 (OVR) ve T15 (diagnostics) testleri yazılamadı** — yüzeyleri yok. Sahte "geçti" konmadı. M6'da eklenince test edilebilir.
4. **`MatchResult` tüm event'leri bellekte tutuyor.** 08 §8 100K deneylerde event biriktirilmemesini ister; bellek sınırlı özet kipi M6'nın işi. M3'te event sayısı da arttı (FT ve blok event'leri).
5. **`ImmutableArray<T>` JSON serileştirmesi doğrulanmadı** — M5 replay serileştirmesinde erken test edilmeli. `PendingShot`/`PendingFrees` M5'in ihtiyacı olan devam edilebilir durum.
6. **`Advance` imzası M5'te genişleyecek** — planlı değişiklik, kayıtlı. Adım sınırı M3'te değişti: bir `Advance` artık bir aksiyonu bitirmek zorunda değil.
7. **CI tanımlı değil.** Testler yalnız yerelde koştu.
8. **`.cs` dosyalarında kod yorumları ASCII'ye çevrildi.** PowerShell'in `Get-Content`/`Set-Content` çift kodlaması Türkçe karakterleri bozdu; 8 dosya kurtarıldı, biri (`Program.cs`) yeniden yazıldı. Kaynak dosyalarda bundan sonra ASCII yorum kullanılacak.
9. **Çember teması şut kalitesine girmiyor.** `RimContactResolver` şu an yalnız saat politikasını besliyor. M4'te kaliteye de girmesi gerekecek; iki yerde ayrı etki yazılırsa 05 §3'teki "iki kat sayma" hatasına dönüşür. Tek yerde toplandı, ikinci tüketici M4'te bağlanmalı.

## Sonraki tek uygulanabilir görev

**M4 planını yazmak** — kod yazmadan `docs/plans/M4_IMPLEMENTATION_PLAN.md`. Kapsam:

- Tactics/pace `MatchSetup` alanları (D34'te M4'e bırakıldı; inert veri olmasın diye aynı milestone'da).
- Savunma çözümü: blok şu an rastgele; savunma rating'leri şut kalitesine girmeli.
- Composite rating'ler ve OVR'nin **girdi** tarafı (D23/D24: OVR çözüm girdisi değildir; T03 ancak burada uygulanabilir).
- Enerji/stamina ve `GameForm` (Q10).
- `EligibilityPolicy`'nin yedek seçimi gerçek rol mantığına bağlanır (D47).
- `RimContactResolver`'ın ikinci tüketicisi: çember teması şut kalitesine girer (risk 9).
- `MatchSetup`, `MatchClock`, `Advance` imzası, `MatchSetupValidator` **korunur** — tactics/pace alanları hariç.

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
