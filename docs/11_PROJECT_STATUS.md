# Proje durumu ve oturum devri

Son güncelleme: 28 Eylül 2026.

## Şu anda

- Aşama: **Planlama.** M6 uygulandı ve ölçüldü; **M7 planı yazıldı, onay bekliyor.**
- Aktif milestone: **M7 (API, auth, persistence, canlı runner, reconnect).**
- Uygulama yetkisi: **M7 için YOK.** Kullanıcının ayrı bir "planı uygula" mesajı gerekiyor.
- Git: `main` == `origin/main` == `bc21816` (M6 uygulaması). Bu oturumun
  değişiklikleri henüz commit edilmedi.
- Monte Carlo: **10K ayar + 100K holdout GERÇEKTEN koşuldu.** Artık "0 maç"
  değil. Raporlar `reports/balance/` altında.
- Denge: **İki parametre ailesi kalibre edildi** (D98b). Dört şut türü de
  05 §7'nin kendi hedef aralığında. NBA sezon verisiyle karşılaştırma
  **yapılmadı** (D98c).
- DB / runtime / hosting: seçilmedi. M1–M6 bunlara ihtiyaç duymadı.
- Test sayısı: **443** (324 → 443; +119). İki test projesi.

## Son oturumda (M6 planlaması) yapılanlar

- **`docs/plans/M6_IMPLEMENTATION_PLAN.md` yazıldı** (443 satır, 16 bölüm).
  **Kod, migration, paket kurulumu, refactoring yapılmadı.**
- **Dört ürün kararı kullanıcıdan alındı ve kilitlendi:**
  - **D98a** — `ShortTimeoutsPerTeam = 5` (NBA). **D89 böylece KAPANDI** (M5'ten kalan borç).
  - **D98b** — M6 kapsamı = **ölçüm + ilk 3 kalibrasyon adımı** (08 §8 adım 3'e kadar).
    **Q10 (GameForm) kapsam dışında bırakıldı** ve açık kaldı.
  - **D98c** — **Q18 KAPANDI:** gerçek veri referansı **yok**; eşikler **iç
    tutarlılıktan** türetilir. NBA sezonuyla karşılaştırma raporda bulunmaz.
  - **D100** — **T15 (diagnostics) M6'ya dahil.** Açık/kapalı → aynı domain
    outcome; RNG tüketmez, `ConfigHash`'e girmez.
- **100K ölçeği tahmin edilmedi, ölçüldü.** Geçici bir prob testi yazıldı,
  koşturuldu ve **silindi**: 300 maçta maç başına **1107 event**, **4.62 ms**,
  212.4 possession, **0 aborted**, %1.67 uzatma. → 100K ≈ **462 sn sıralı**
  (süre sorun değil) ve **110.7 milyon event** (≈21 GB bellek — **sorun bu**).
- **D33 gevşetilecek** (simulator argümansız kuralı) — plan kararı, henüz uygulanmadı.
- Plan §14'te **bilinçli kapsam dışı** listesi ve §15'te **10 risk** kayıtlı.

### Bu oturumda gerçekten çalıştırılan komutlar

| Komut | Sonuç |
|---|---|
| Geçici `TempScaleProbeTests` (300 maç, Release) | **Beklenen şekilde FAIL** (`Assert.Fail` ile prob raporu yazdı) — ölçüm: 1107 event/maç, 4.62 ms/maç |
| Prob dosyası silindi → `dotnet build -c Release` | Başarılı — **0 hata** |
| `dotnet test DreamTeam.slnx -c Release` | Başarılı — **324/324** |
| `git status --short` (prob kalıntısı) | **Temiz** |
| docs/10 ve docs/11 bayt kontrolü | BOM yok, CR yok, U+FFFD yok (her ikisi) |

**Not:** Bu oturumda `dotnet test -c Debug`, `dotnet list package`, yasaklı çağrı
taraması ve `git diff` **koşturulmadı** — M6 kodu yazılmadığı için gerek yoktu.
Son tam doğrulama M5 kapanışındadır (aşağıda).

## Son oturumda (M7 planlaması) yapılanlar

- **`docs/plans/M7_IMPLEMENTATION_PLAN.md` yazıldı** (542 satır, 16 bölüm).
  **Kod yazılmadı; kaynak dosya değiştirilmedi.**
- **Beş ürün kararı alındı** ve `docs/10` §16'ya kaydedildi:

| ID | Karar |
|---|---|
| **D109** | Canlı maç **8 gerçek dakika** (hız çarpanı 6.0) — Q12 kapandı |
| **D110** | **PostgreSQL + JWT bearer** — Q13 kapandı |
| **D111** | **Kişi başına oyuncu kopyası, kadro serbest** (lineup 5 kalır) — Q14 kapandı |
| **D112** | **M7'de PvP yok, sadece AI** — Q16 kapandı |
| **D113** | **Sunucu çökerse maç `Aborted`, kurtarma yok** — Q19 kapandı |
| **D114** | **Q19 soru tablosuna eklendi** — M6'da atıf yapılmış ama hiç yoktu |

- **Q16 iki kez soruldu.** İlk toplu soruda cevapsız kaldı; uydurmak yerine
  ayrı soruldu ve "M7'de PvP yok" cevabı alındı.

### Bu oturumda GERÇEKTEN ölçülen şey (tahmin yok)

| Ölçüm | Sonuç |
|---|---|
| `nuget.org` erişimi | **YOK** — `NU1101: paket bulunamıyor` |
| Yerel NuGet önbelleği | **883 paket** — gerekli her şey var |
| Planlanan paket setiyle `dotnet restore` | **BAŞARILI**, 331 ms, tamamen çevrimdışı |
| `Microsoft.Extensions.Hosting` açık referansı | `NU1510` uyarısı → referans **verilmeyecek** |
| PostgreSQL 18 kurulu mu | **Evet** — `C:\Program Files\PostgreSQL\18\bin` |
| `localhost:5432` dinliyor mu | **Evet** |
| `Testcontainers.PostgreSQL` | **Yok** → yerel PostgreSQL kullanılacak |
| ASP.NET Core runtime | 10.0.12 mevcut |

**Geçici probe dosyaları yazıldı, koşturuldu ve silindi.** Çalışma ağacında
kalıntı yok.

### Planın en kritik tasarım kararı

Pacer "lag-behind" olacak: motor öndeyse **bekler**, gerideyse **beklemez**.
Eğer sabit bekleme kullanılsaydı 8 dakikalık hedef aşılır ya da motoru
yavaşlatmak gerekirdi — yavaşlatmak **domain sonucunu değiştirir**.

Bunu doğrulayan test: **`ALiveMatchProducesTheSameEventsAsSimulate`** — aynı
seed + aynı komutlarla canlı koşu, `Simulate` ile bayt bayt aynı event akışını
üretmeli.

## Önceki oturumda (M6 uygulaması) yapılanlar

## Son oturumda (M6 uygulaması) yapılanlar

- M6 planı **onaylandı** ve uygulandı. D98a 3→5, D98b iki aile, D98c iç tutarlılık
  eşikleri, D100 diagnostics — dördü de uygulandı.
- **Bellek sınırlı özet kipi (plan §5, asıl iş).** `MatchRunner`
  `Create`+`Advance` döngüsünü kurar, event'leri `BoxScoreProjector.Accumulate`
  ile akıtır ve **atır**. `MatchResult` 100K'da hiç üretilmez.
  **Kanıt:** 100K maçta **azami bellek 48.9 MB** (sıralı). Plan §3'ün tahmini
  "event'ler tutulursa ~21 GB" idi; ölçüm bunu doğruladı.
- **Oluşturulan dosyalar (motor):** `Diagnostics/DiagnosticCounters.cs`
  (23 sayaç + `DiagnosticTally` + `Merge` + `ToReport`), `Replay`'e diagnostics
  alanı, `BoxScoreProjector.Accumulate`, `MatchResult.Diagnostics`.
- **Oluşturulan dosyalar (simulator):** `Cli/CommandLine.cs`, `SimulatorRunner.cs`,
  `Fixture/FixtureCatalog.cs`, `Fixture/JsonFixtureSource.cs`,
  `Config/BalanceConfigStore.cs`, `Batch/{MatchSummary,MatchRunner,SummaryAccumulator,BatchDriver}.cs`,
  `Export/SummaryWriter.cs`, `Reporting/{BalanceReport,BalanceReportBuilder,WilsonInterval,ExperimentManifest}.cs`.
- **Oluşturulan dosyalar (veri):** `config/engine/baseline.v0.1.json` (258 satır),
  `fixtures/teams/*.json` (7 fixture).
- **Oluşturulan test projesi:** `tests/DreamTeam.Simulator.Tests/`
  (`M6TestData`, `CliTests`, `ParallelDeterminismTests`, `ExportConsistencyTests`,
  `FixtureSourceTests`, `BalanceInvariantTests`, `ArgumentlessInvocationTests`).
- **Yeni testler:** 119. **324 → 443.**
- **8 gercek hata bulundu ve duzeltildi** (D101–D108 tablosu, docs/10 §15).
  En kritik ikisi: **`--tactics` toplu koşuda sessizce yok sayılıyordu**
  (savunma matrisi hiç ölçülemiyordu) ve **`ShotTypeTally.Empty` paylaşılan
  değişken** idi (10K'da ClosePost %-67, MidRange %+70).
- **D87 yeni bir yere bulaştı:** `FixtureTeamFile` da `ImmutableArray` içeriyor,
  `Assert.Equal` her zaman False dönüyor. Testler alan alan karşılaştırıyor.

### M6'da GERÇEKTEN koşulan ölçümler

| Koşu | Sonuç |
|---|---|
| **10K ayar** (seed-start 1) | 26.8 sn, 373 mac/sn |
| **100K holdout, sıralı** | 162.2 sn, 616 mac/sn, **azami bellek 48.9 MB** |
| **100K holdout, jobs=4** | 82.6 sn, 1.210 mac/sn, azami bellek 282.7 MB |
| **T17** | İki koşunun 6 satırı farklı; hepsi manifestin çalışma kaydı. 2.000+ metrik satırı **bayt bayt aynı**. |
| **100K denge** | Ev kazanma %49.80 [49.49, 50.11]; ortalama skor 128.73/128.75; Pace48 122.5; ORtg 104.83; TOV/poss 0.147; aborted 7/100.000 |
| **Şut türleri (100K)** | AtRim %67.57, ClosePost %54.80, MidRange %43.57, ThreePoint %36.31 — **dördü 05 §7 aralığında** |
| **Deney aileleri (5K)** | quality-gap ev kazanma %65.94; roster-fit %66.18; pace Slow 93.9 < Normal 122.5 < Fast 150.0; mirror-swapped neutral ile aynı |
| **Savunma matrisi (16 çift × 2K)** | Drop PnR'ı kesiyor (PickAndRoll %45.6); ZonePackPaint perim hareketine yeniliyor (%50.8). **Hiçbir savunma her eşleşmeyi kazanmıyor.** |

### Doğrulama kanıtı — gerçekten çalıştırılan komutlar

| Komut | Sonuç |
|---|---|
| `dotnet build DreamTeam.slnx -c Release --no-incremental` | Başarılı — **0 uyarı, 0 hata** |
| `dotnet test DreamTeam.slnx -c Release` | Başarılı — **443/443** (340 motor + 103 simulator) |
| `dotnet test DreamTeam.slnx -c Debug` | Başarılı — **443/443** |
| `dotnet run --project src/DreamTeam.Simulator -c Release` (iki kez) | SHA256 `243C1BCFC327AA0…D3E236C7` — **iki koşuda aynı** |
| `dotnet list package` (3 proje) | "Bu çerçeve için paket bulunamadı" — **sıfır paket** |
| Yasaklı çağrı taraması (motor + domain) | **Temiz** — 2 eşleşme de XML yorum (`<c>DateTime.Now</c>`, `<c>new Random()</c>`), yani kullanılmayanların adı |
| `git diff` (7 donmuş sözleşme + `MatchSetupValidator`) | **Boş — dokunulmadı** |
| `dotnet run -- ... batch --matches 100000` (×2) | Yukarıdaki tablo; **fiilen koşuldu** |

### Kritik hash / kimlik

- `EngineVersion.Current = "0.1.0"` (değişmedi), `RngIdentity` SplitMix64 v1 (değişmedi).
- `ConfigHash` M5'te `349032b31595c15b` idi; M6'da **önce** D98a sonra
  D98a+D105 ile **`91cbb2e60d78fd9a`**. Ara değer: `2a916d545deaa217`.
- `EventSchemaVersion` **4** (değişmedi — diagnostics event üretmiyor, 07 §1).
- `MatchEventType` **25** (değişmedi).

### GÖZLENEN SAYILAR — denge kanıtı, ama yeterli değil

100K holdout, 2.000+ metrik satırı, dört şut türü hedef aralığında, altı iç
tutarlılık eşiğinden beşi geçti. **Ama:**

- **NBA sezonuyla karşılaştırma yapılmadı** (D98c). "Gerçekçi" denemez.
- **İnsan playtest'i yapılmadı** (08 §8 adım 8, M6 dışı). Sayısal tutarlılık
  oyuncu deneyimi değildir.
- `AST/poss = 0.455` **yapay bir değerdir**: motor her isabetli basket için bir
  asist oyuncusu seçer, gerçek bir asist kavramı yok. Bu bir katsayı değil,
  **eksik bir mekanizmadır**; D98b kapsamında düzeltilmedi ve M7 adayıdır.
- `Pace48 = 122.5` hızlıdır. Uzatma ihlalleri yok edildiği için possession
  sayısı arttı; bu bir tutarsızlık değil, kalibrasyonun sonucu.
- 100K'da **7 maç** uzatma tavanında Abort oldu (D79). Gerekçeleri raporda.
## Önceki oturumda (M5 uygulaması) yapılanlar

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

### M5 uygulaması sırasında kullanıcıya açılan iki soru

1. **D96(a)** — Soru etiketi "normal basket sonrası pencere açılmaz" derken
   açıklaması isabetli basketi pencere sayıyordu. Kullanıcı açıklamayı onayladı:
   **pencere AÇILIR** (5+1 nokta).
2. **D89** — `ShortTimeoutsPerTeam = 3` kaynaksızdı; **M6 planlamasında
   kapandı → 5 (NBA), D98a.**

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

## M4 uygulamasında yapılanlar

- M4 planı onaylandı; **D57–D68** kilitlendi, **D69–D78** uygulama kararları olarak kayda geçti.
- **Oluşturulan motor dosyaları:** `Config/Tactics.cs` (3 enum + `TeamMatchSetup`), `Config/TacticsModel.cs`, `Config/DefenseModel.cs`, `Config/FatigueModel.cs`, `Config/PaceModel.cs`, `Core/PlayerMatchState.cs`, `Ratings/PlayerRatingTables.cs`, `Ratings/PlayerRatingCalculator.cs`, `Ratings/TeamRatingCalculator.cs`, `Tactics/OffensivePolicy.cs`, `Tactics/DefensivePolicy.cs`, `Actions/ShotQualityResolver.cs`, `Fatigue/FatigueCalculator.cs`.
- **Değişenler:** `MatchSetup` (D61 `TeamMatchSetup`), `TeamMatchState` (+taktik/tempo/`PlayerStates`/`Bench`), `MatchState`, `MatchResult` (+`PlayerEnergy`, +OVR), `MatchSimulation` (yeni çağrı sırası, `ConsumeLive`, periyot arası toparlanma), `ShotMath`, `ShotResolver`, `BlockResolver`, `TurnoverResolver`, `FoulResolver`, `ReboundResolver`, `ActionSelector`, `EligibilityPolicy`, `MatchSetupValidator` (+taktik/tempo), `MatchSetupValidation` (+2 hata kodu), `MatchEventPayloads` (`ShotAttemptPayload` +2 alan), `EngineConfig` (+4 model, `QualityScale`, `SelectionSpread`).
- **Yeni testler:** `M4TestData`, `RatingCompositeTests` (14), `TacticDistributionTests` (11), `DefensePolicyTests` (19), `PaceAndEnergyTests` (18). **158 → 222 test.**
- **M1–M3 testleri güncellendi** (mekanik `TeamMatchSetup` geçişi + üç M3 varsayımının düzeltilmesi).
- **10 gerçek hata bulundu ve düzeltildi**, hepsi regresyon testli. En kritik üçü: M3'ten kalan **kayıp savunma faulü** (D69), **enerjinin hiç düşmemesi** (D72) ve **yorgunluk kanalının fiilen ölü olması** (D73/D74 → `PerformanceMultiplier` ve `Interpolate` hataları).

## M3 uygulamasında yapılanlar

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

- **D57–D68** (M4 kilit kararları): bölüm 10.
- **D69–D78** (M4 uygulama kararları): bölüm 11. D78 **artık açık değil** → D79.
- **D79–D89** (M5 kilit kararları): bölüm 12. **D89 artık açık değil → D98a.**
- **D90–D97** (M5 uygulama kararları): bölüm 13.
- **D101–D108** (M6 uygulama kararları): bölüm 15. 8 gerçek hata ve 2 rapor
  hesap hatası burada.
- M6 sapmaları (T15 anahtarı yerine altı kanıt) ve 8 hata:
  `docs/10_DECISIONS_AND_OPEN_QUESTIONS.md` §15.
- **D98a–D98c, D100** (M6 planlama kararları): bölüm 14. **D89 ve Q18 kapandı.**
- M3 sapmaları ve 9 hata: `docs/plans/M3_IMPLEMENTATION_PLAN.md` §12.
- M4 sapmaları (10) ve 10 hata: `docs/plans/M4_IMPLEMENTATION_PLAN.md` §12 ve §13.
- M5 sapmaları (12) ve 6 hata: `docs/plans/M5_IMPLEMENTATION_PLAN.md` §16 ve §17.
- Kapanan açık sorular: Q04, Q05, Q06'nın yapısal kısmi, Q07, Q08, Q09,
  Q10'un "M4'te kapalı" yarısı, **Q11**, **Q18**.
- Açık kalan: **D87** (M7'de çözülecek), **Q10** (GameForm dağılımı/birimi),
  **Q12**, Q13–Q17, Q19.

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

1. **"Gerçekçi" denemez.** D98c gereği NBA sezon verisiyle karşılaştırma
   yapılmadı. 100K koşu dört şut türünü 05 §7'nin kendi aralıklarına oturttu
   ve beş iç tutarlılık eşiğini geçti; bu **sayısal tutarlılıktır**, oyun
   gerçekçiliği değildir.
2. **İnsan playtest'i yapılmadı** (08 §8 adım 8, D98b gereği M6 dışı).
   Sayısal tutarlı ama oyuncuya tuhaf gelen bir motor bu aşamadan geçebilir.
3. **`AST/poss = 0.455` yapay.** Motor her isabetli basket için bir asist
   oyuncusu seçer; gerçek asist kavramı (pas zinciri, yarı saha) yok. Bu bir
   katsayı değil **eksik mekanizma**; D98b kapsamında düzeltilmedi. M7 adayı.
4. **`Pace48 = 122.5` hızlı.** Uzatma ihlalleri yok edildiği için possession
   arttı. Bu bir tutarsızlık değil, kalibrasyonun sonucu — ama tempo başka bir
   aileden ayrı ayrı ayarlanmadı (D98b iki aileyle sınırlıydı).
5. **Yalnız iki parametre ailesi ayarlandı** (D105). 08 §8'in adım 4–8'i
   (stamina/rotation, dominant strateji taraması, form/late-game, tam holdout
   turu, playtest) **M6 dışında kaldı**. `GameForm` (Q10) açık.
6. **`QualityBonus` / `ShotBias` hâlâ kalibre değil.** `TacticsModel`'in üç
   taktiği kendi dağılımlarıyla ölçülebilir etki gösteriyor (3PA payı 0.099 /
   0.148 / 0.248) ama `QualityBonus` büyüklükleri 05 §5'ten gelen yöndeler,
   sayısal değerlerden değil. M7+ adayı.
7. **D87 yeni bir yere bulaştı.** `FixtureTeamFile` da `ImmutableArray` içeriyor;
   `Assert.Equal` her zaman False dönüyor. Testler alan-alan karşılaştırıyor
   ama **kalıcı çözüm hâlâ M7'de bekliyor.**
8. **`EventSchemaVersion = 4`** — diagnostics sayaçları **event üretmiyor**
   (07 §1). Doğru ama şu an sürüm artmadı; ileride event eklenirse artmalı.
9. **CI tanımlı değil.** Testler yalnız yerelde koştu. 100K koşu 162 sn sürüyor;
   CI'da 1.000 maçlık smoke yeterli (08 §5: "Her commit'te 100K gerekmiyor").
10. **Cross-platform bit düzeyi eşitlik kanıtlanmadı** (D23 kapsamı dışı).
11. **Argümansız koşunun değerleri kalibrasyonla değişti** (137-149, 4 periyot).
    Test bunu sabitliyor ama M2–M5'in golden değerleri artık **fabrika config'i**
    için geçerli, kalibre belge için değil. İki değerin nerede kullanıldığı
    `M6TestData.Config()` / `FactoryConfig()` ile yazılı.
12. **`reports/` git'e girmemeli.** Şu an `.gitignore` kontrolü yapılmadı.
13. **Kaynak dosya yorumlarında ASCII kuralı.** Yeni kaynak dosyalarda ASCII
    yorum kullanıldı; PowerShell `Get-Content`/`Set-Content` ile düzenleme
    yapılmadı (çift kodlama riski). Belgeler `[IO.File]::ReadAllText` +
    `UTF8Encoding($false)` ile yazıldı ve bayt seviyesinde doğrulandı.


## Sonraki tek uygulanabilir görev

**M7 planını onaylamak ve uygulama yetkisi istemek.** Kapsam (03, 09 §M7):

1. **D87'nin kalıcı çözümü.** `ImmutableArray<T>` içeren `record`'larda değer
   eşitliği bozuk; M6'da `FixtureTeamFile`'a da bulaştı. 03 §"Match completion
   idempotent" bir kalıcı katman eşitliği gerektiriyor. M5/M6'da parmak izi
   ve alan-alan karşılaştırma ile aşıldı; bu M7'nin ilk işi.
2. **API + auth + persistence.** Q13 (DB/auth) ve Q19 kararları burada.
3. **Canlı runner / SignalR.** Q12 (canlı maç süresi) burada; D83'teki 20
   saniyelik timeout'un duvar saati süresi de M7'de yaşar.
4. **Reconnect ve mesaj sırası** (T19).
5. **Assist kavramı.** M6'da `AST/poss = 0.455` yapay: her isabetli basket için
   bir asist oyuncusu seçiliyor, gerçek asist kavramı yok. Kalibrasyon
   katsayısı değil mekanizma eksikliği; M7'ye bir aday.
6. **GameForm (Q10)** — hâlâ açık, D98b gereği M6 dışında bırakıldı.

**Q12–Q19 hâlâ açık.** M7 için ayrı bir plan ve ayrı uygulama yetkisi gerekir.

- Çalıştırılan komut/test, sonuç ve varsa başarısızlık
- Sürümler, config/fixture hash ve seed seti (simülasyon varsa)
- Yapılan ve açık kalan kararlar
- Çözülmemiş risk/engel
- Sonraki tek uygulanabilir görev
- Geçerli uygulama yetkisinin kapsamı

## Doküman bakım kuralı

Özellik tamamlandığında yalnız burada işaretlemek yeterli değildir; gerçek davranış ilgili spec'ten farklıysa spec ve karar günlüğü de güncellenir. Planlarda checkbox işaretleri gerçek doğrulamadan sonra tamamlanır. Raporlanmamış test başarısı varsayılmaz.
