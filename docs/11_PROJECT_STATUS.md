# Proje durumu ve oturum devri

Son güncelleme: 27 Eylül 2026.

## Şu anda

- Aşama: Uygulama. **M1 tamamlandı ve doğrulandı.**
- Aktif milestone: **M1 bitti → sırada M2 (dar possession vertical slice + console tek maç)**.
- Uygulama yetkisi: **M1 için verildi ve kullanıldı.** M2 için yetki **yok**; M2 başlamadan önce ayrıca onay gerekir.
- Git: **`main` branch'i `origin/main` ile eşitlendi.** Uzak: `https://github.com/Sonerbascifci/DreamTeamReborn.git`. İki commit: `f6aba1f` (M0 devir paketi) ve `b328fee` (M1 kodu). Push sonrası doğrulandı: ahead 0 / behind 0.
- Monte Carlo: 0 maç. 10K/100K hedefleri M6'ya ait; henüz çalıştırılmadı.
- DB / runtime / hosting: seçilmedi. M1 bunlara ihtiyaç duymadı.

## M0'da yapılanlar (keşif ve planlama)

- `README.md`, `AGENTS.md`, `CODEX_START.md` ve `docs/00`–`docs/11` ile `docs/plans/M0_PLANNING_HANDOFF.md` okundu.
- Gerçek repo durumu keşfedildi: başlangıçta yalnız doküman vardı, solution/proje/test altyapısı yoktu, `rg` kurulu değildi.
- `.NET 10.0.401` SDK'nın kurulu olduğu doğrulandı.
- `docs/plans/M1_IMPLEMENTATION_PLAN.md` yazıldı, kullanıcı tarafından onaylandı.

## M1'de yapılanlar

**Oluşturulan yapı**

- `DreamTeam.slnx`, `global.json` (SDK 10.0.401 kilidi), `Directory.Build.props` (`net10.0`, nullable, `TreatWarningsAsErrors`).
- `src/DreamTeam.Domain/` — `Position`, `PlayerRatings` (18 `int` attribute), `Player`, `Team`, `Lineup`, `RosterOrdering`.
- `src/DreamTeam.MatchEngine/` — `EngineVersion`, `EngineIdentity`, `MatchSetup`, `MatchSetupValidation`, `MatchSetupValidator`, `IRandomSource`, `RngIdentity`, `SeededRandom`.
- `tests/DreamTeam.MatchEngine.Tests/` — `TestData`, `SetupValidationTests`, `SeededRandomTests`.

**Kilitli sürümler (gerçek ortamdan)**

| Bileşen | Sürüm |
|---|---|
| .NET SDK | 10.0.401 (`global.json`, `rollForward: latestPatch`) |
| Hedef framework | `net10.0` |
| `xunit` | 2.9.3 |
| `xunit.runner.visualstudio` | 3.1.4 |
| `Microsoft.NET.Test.Sdk` | 17.14.1 |
| `coverlet.collector` | 6.0.4 |
| RNG | SplitMix64, `RngIdentity.Algorithm = "SplitMix64"`, `Version = "1"` |
| Motor sürümü | `EngineVersion.Current = "0.1.0"` |

**Doğrulama kanıtı — gerçekten çalıştırılan komutlar**

| Komut | Sonuç |
|---|---|
| `dotnet build DreamTeam.slnx -c Release` | Başarılı — 0 uyarı, 0 hata |
| `dotnet test DreamTeam.slnx -c Release` | Başarılı — 42/42 |
| `dotnet test DreamTeam.slnx -c Debug` | Başarılı — 42/42 |
| `dotnet list package` (Domain) | "Bu çerçeve için paket bulunamadı" — sıfır paket |
| `dotnet list package` (MatchEngine) | "Bu çerçeve için paket bulunamadı" — sıfır paket |
| `dotnet list package` (Tests) | Yukarıdaki dört test paketi, istenen = çözümlenen |
| `grep` `src/` içinde `new Random` / `DateTime` / `Environment.` / `ILogger` / `Thread.` | Yalnızca yorum satırlarında eşleşme; üretim kodunda yok |

**Golden vector kaynağı**

- Birincil kaynak: Sebastiano Vigna, `splitmix64.c` (public domain) — https://prng.di.unimi.it/splitmix64.c
- Çapraz kontrol: Python 3.14.2 (keyfi büyüklükte tamsayı) ve Node 22 (BigInt) ile ayrı ayrı hesaplandı; üçü de aynı diziyi verdi.
- Testlerde seed 0 ve seed 12345 için 10'ar ham çıktı, 5 `double` çıktı ve 5 çekiliş sonrası state sabitlendi.

## Kararlar

- Yapılan ve onaylanan: D20–D30 → `docs/10_DECISIONS_AND_OPEN_QUESTIONS.md` bölüm 6.
- Kapanan açık sorular: Q01 (SDK/test), Q02 (RNG ve determinism kapsamı), Q03 (kimlik ve snapshot).
- Açık kalan ve M1'i engellemeyen: Q04–Q18. Q04/Q05/Q06 M2'de karara bağlanacak.

## Kapsam dışı bırakılanlar (M1'de bilinçli olarak yok)

- Tam maç simülasyonu, `MatchState`, `PossessionState`, saat, foul/FT, uzatma.
- Tactics, pace, composite rating, OVR, stamina/enerji hesabı.
- Event üretimi, `BoxScoreProjector`, console/simulator, fixtures, config dosyaları.
- API, DB, SignalR, React, PixiJS. Motor bunların hiçbirine bağımlı değil.

## Açık riskler

1. **Cross-platform bit düzeyi eşitlik kanıtlanmadı.** Garanti kapsamı bilinçli olarak aynı kilitli runtime ile sınırlı (D23). M6'da ayrıca ölçülmeli.
2. **Setup digest/hash'i henüz hesaplanmıyor.** `EngineIdentity.BalanceConfigHash` şimdilik çağıran tarafdan gelen bir alan; içerik hash'i üreten yok. M2/M6 config ile birlikte eklenmeli.
3. **`ImmutableArray<T>` JSON serileştirmesi doğrulanmadı.** M6 fixture okuma/yazmasında erken test edilmeli.
4. **`IRandomSource` henüz tüketici değil.** Arayüz 08'in test double gereksinimi için var; M2'de `MatchEngine` tüketecek.
5. **Oyun RNG'si henüz hiçbir şeyi beslemiyor.** Motor henüz maç üretmiyor; determinism kanıtı RNG çekirdeği düzeyinde geçerli, maç düzeyinde değil.
6. **CI tanımlı değil.** Testler yalnızca yerelde koştu. GitHub Actions ile otomatik `dotnet test` eklenmedi; istenirse ayrı iş.

## Sonraki tek uygulanabilir görev

**M2 planını hazırlamak** — kod yazmadan önce `docs/plans/M2_IMPLEMENTATION_PLAN.md`. M2 kapsamı:

- `MatchState`, `PossessionState`, `MatchClock` (integer ms), aynı `Advance` çekirdeği.
- Dört sonuç: sayılabilir isabet, canlı miss + DREB, canlı miss + OREB, turnover.
- `ActionSelector`, `ShotResolver`, `TurnoverResolver`, `ReboundResolver`.
- `MatchEvent` + `BoxScoreProjector` + console çıktısı.
- M2'de kilitlenmesi gereken kararlar: Q04 (sade kural profili), Q05 (periyot açılış protokolü), Q06 (baseline katsayılar ve aksiyon süreleri), shot-clock reset davranışı.
- Foul/FT/OT yok; bu eksiklik raporda açıkça bildirilecek.

## Her oturum sonunda doldurulacak kayıt

Aşağıdaki başlıklara gerçek bilgi yaz; boş bilgiyi uydurma:

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
