# Proje durumu ve oturum devri

Son güncelleme: 27 Eylül 2026.

## Şu anda

- Aşama: Uygulama. **M2 tamamlandı ve doğrulandı.**
- Aktif milestone: **M2 bitti → sırada M3 (kural bütünlüğü: foul/FT/clock edge case/OT)**.
- Uygulama yetkisi: **M2 için verildi ve kullanıldı.** M3 için yetki **yok**; M3 başlamadan önce ayrıca onay gerekir.
- Git: `main` == `origin/main` (M2 commit'i ile eşitlendi; push sonrası doğrulandı).
- Monte Carlo: **0 maç.** M2'de 121 test içinde çok seed'li smoke koşular var (60–120 seed), ama 10K/100K deneyi **çalıştırılmadı**; bu M6'nın işidir.
- DB / runtime / hosting: seçilmedi. M1 ve M2 bunlara ihtiyaç duymadı.

## M0 ve M1 özeti

- **M0:** Devir belgeleri okundu, repo keşfedildi (başlangıçta yalnız doküman vardı), `M1_IMPLEMENTATION_PLAN.md` yazıldı ve onaylandı.
- **M1:** Domain modelleri, setup doğrulama, `EngineIdentity`, SplitMix64 RNG. 42 test. `f6aba1f`, `b328fee`, `0ef8f1c`.
- M1'in dosyalarına M2 sırasında **hiç dokunulmadı**; `MatchSetup`, `MatchSetupValidator`, `SeededRandom`, `RosterOrdering`, `SetupValidationTests` olduğu gibi duruyor.

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

- D31–D39 ve bulunan 5 gerçek kod hatasının kaydı: `docs/10_DECISIONS_AND_OPEN_QUESTIONS.md` bölüm 7.
- Kapanan açık sorular: Q04, Q05, Q06'nın yapısal kısmı.
- Açık kalan: Q07 (bonus/foul-out/az oyuncu terminal policy — M3), Q08–Q11, Q13–Q18.

## Kapsam dışı bırakılanlar (M2'de bilinçli olarak yok)

- Faul, serbest atış, and-one, bonus, blok, top çalma, out-of-bounds.
- Substitution, timeout, uzatma (OT).
- Savunma taktik çözümü, tempo, enerji/stamina, rol uyumu, composite rating, OVR.
- Tactics/pace `MatchSetup`'te **yok** (D34) — M4.
- Batch CLI, JSON/CSV export, fixture dosyaları, deney manifesti — M6.
- API, DB, SignalR, React, PixiJS.

## Bulunan ve düzeltilen gerçek hatalar

| Hata | Belirti | Durum |
|---|---|---|
| `StartPossession` hücum saatini sıfırlamıyordu | 3 şut denemesi, 349 turnover, 0-5 skor | Düzeltildi + regresyon testi |
| Takım puanı iki kez ekleniyordu | Box score 194, gerçek skor 97 | Düzeltildi + regresyon testi |
| İsabetli şutlarda `3PA` sayılmıyordu | "3P 4-0" imkânsız satırlar | Düzeltildi + regresyon testi |
| `BeginPeriod` toplam süreyi sıfırlıyordu | Rapor 720 s yerine 2880 s olmalıydı | Düzeltildi + regresyon testi |
| Şıtsız aksiyon event üretmiyordu | Motor "ilerleme yok" ağıyla abort | `ActionCompleted` eklendi + test |

## Açık riskler

1. **Cross-platform bit düzeyi eşitlik kanıtlanmadı** (D23 kapsamı dışı, M6'da ölçülecek).
2. **Katsayılar kalibre değil.** Savunma çözümü olmadığı için puan yüksek (97-85). 10K/100K deneyi M6'da.
3. **T15 (diagnostics) testi yazılamadı** — M2'de diagnostics yüzeyi yok. Sahte "geçti" konmadı. M6'da eklenince test edilebilir.
4. **`MatchResult` tüm event'leri bellekte tutuyor.** 08 §8 100K deneylerde event biriktirilmemesini ister; bellek sınırlı özet kipi M6'nın işi.
5. **`ImmutableArray<T>` JSON serileştirmesi doğrulanmadı** — M6 fixture okuma/yazmasında erken test edilmeli.
6. **`Advance` imzası M5'te genişleyecek** — planlı değişiklik, kayıtlı.
7. **CI tanımlı değil.** Testler yalnız yerelde koştu.
8. **`.cs` dosyalarında kod yorumları ASCII'ye çevrildi.** PowerShell'in `Get-Content`/`Set-Content` çift kodlaması Türkçe karakterleri bozdu; 8 dosya kurtarıldı, biri (`Program.cs`) yeniden yazıldı. Kaynak dosyalarda bundan sonra ASCII yorum kullanılacak.

## Sonraki tek uygulanabilir görev

**M3 planını hazırlamak** — kod yazmadan `docs/plans/M3_IMPLEMENTATION_PLAN.md`. Kapsam:

- `Rules/RulesProfile.cs` genişlemesi, `FoulResolver`, `FreeThrowResolver`, `ClockResetPolicy`, `PeriodController`, `EligibilityPolicy`.
- `MatchPhase`'e `ShotPending` ve `FreeThrows` eklenmesi; `EventSchemaVersion` 1 → 2.
- Q07 kararı: bonus, foul-out, beşten az uygun oyuncu için terminal policy.
- 06 §6'da kalan shot-clock reset satırları, horn/release tie-break.
- M2'nin `MatchSetup`, `MatchClock`, `MatchState`, `Advance` sözleşmeleri **değiştirilmemeli**; üstüne binmeli.

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
