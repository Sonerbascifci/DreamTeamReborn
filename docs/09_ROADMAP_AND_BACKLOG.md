# Roadmap ve aşamalı backlog

Bu bir milestone planıdır; bütün gelecek özellikler için henüz yazılmamış kodu varsayan uygulama planı değildir. Her milestone başladığında gerçek repo yollarıyla kendi küçük adımlı planı hazırlanır. M0 ilk planı pakette bulunur.

## Bağımlılık sırası

M0 → M1 → M2 → M3 → M4 → M5 → M6 → M7 → M8 → M9 → M10. M11+ ayrı ürün kapsamıdır. M1–M6 birlikte doğrulanmış Engine v0.1'i oluşturur.

| ID | Teslim | Bağımlılık | Çıkış kapısı |
|---|---|---|---|
| M0 | Repo envanteri, karar kayıtları, M1 uygulama planı | Belgeler | M1 engelleri net; kod yazılmadı |
| M1 | Domain/setup validation ve seeded RNG çekirdeği | M0 | T01–T03 temeli ve sınır testleri |
| M2 | Dar possession vertical slice + console tek maç | M1 | Shot/TOV/rebound/skor; ilk event akışı |
| M3 | Kurallar, foul/FT, clock edge case, OT | M2 | T04–T11; tamamlanan maç invariant'ları |
| M4 | Dört + dört taktik, tempo, enerji | M3 | Rol/taktik/rotasyon davranışları gözlenebilir |
| M5 | Scripted manager commands ve replay devamı | M4 | T13–T16; canlı adımlama sözleşmesi |
| M6 | CLI batch, 10K/100K deney ve balance adayı | M5 | Tekrarlanabilir ölçüm raporu |
| M7 | ASP.NET API, auth, persistence, live runner/SignalR | M6 | Yetkili komut, reconnect, sonuç saklama |
| M8 | React yönetim ekranları + text live viewer | M7 | İlk uçtan uca yönetim/maç akışı |
| M9 | PixiJS 2D match presentation | M8 | Eventler doğru ve sıralı canlandırılıyor |
| M10 | Basit ödül/scout/imzalama döngüsü | M8/M9 | Bir defa ödül/işlem ve playtest |
| M11+ | PvP, market, draft, lig, gerçek veri | Ürün kararı | Ayrı spec ve kabul kriterleri |

## M0 — Keşif ve ilk uygulama planı

Dosya: [M0_PLANNING_HANDOFF.md](plans/M0_PLANNING_HANDOFF.md). Repository var mı, runtime/test altyapısı ne, kullanıcı değişiklikleri var mı? Yalnız ilgili kararları ele al. DB/hosting kararı M1'i engellemesin. Çıktı `docs/plans/M1_IMPLEMENTATION_PLAN.md`; plan onayı gelmeden uygulama yok.

## M1 — İlk güvenilir yapı taşı

Önerilen dosyalar:

- `src/DreamTeam.Domain/Players/Player.cs`, `PlayerRatings.cs`, `Position.cs`.
- `src/DreamTeam.Domain/Teams/Team.cs`, `Lineup.cs`.
- `src/DreamTeam.MatchEngine/Core/MatchSetup.cs`, `MatchSetupValidator.cs`, `EngineIdentity.cs`.
- `src/DreamTeam.MatchEngine/Randomness/IRandomSource.cs`, `SeededRandom.cs`.
- `tests/DreamTeam.MatchEngine.Tests/SetupValidationTests.cs`, `SeededRandomTests.cs`.

Somut işler: SDK/test framework'ünü kilitle; en küçük solution'ı kur; rating aralığı ve duplicate/foreign lineup üyelerini reddet; input snapshot'ını dış mutasyondan ayır; RNG algoritması ve state roundtrip'ini seçip golden vector ile doğrula.

Kabul: temiz build/test; beş farklı uygun oyuncu; 0/100 sınırları; geçersiz sayı; aynı RNG seed/state aynı dizi; farklı log ayarı etkisiz; runtime ve config identity kaydı. Henüz “maç motoru tamamlandı” denmez.

## M2 — Dar uçtan uca simülasyon

Önerilen dosyalar: `Core/MatchEngine.cs`, `MatchState.cs`, `PossessionState.cs`, `MatchClock.cs`; `Actions/ActionSelector.cs`, `ShotResolver.cs`, `TurnoverResolver.cs`, `ReboundResolver.cs`; `Events/MatchEvent.cs`, `BoxScoreProjector.cs`; simulator `Program.cs`, fixtures ve temel config.

Dört durumla başla: sayılabilir isabet, canlı miss+DREB, canlı miss+OREB, turnover. Context ve action süresi açık olsun. Aynı Advance çekirdeğini kullanan full-loop wrapper üret. Eventlerden console sonucu ve box score çıkar.

Kabul: T01–T05 ve T15 dar kapsamda; score conservation; clock ilerliyor; possession kimliği doğru. Foul/OT yokken bu eksikliği CLI/raporda bildir. İncomplete M2 koşuları final NBA benzeri ürün diye sunulmaz.

## M3 — Kural bütünlüğü

Önerilen dosyalar: `Rules/RulesProfile.cs`, `FoulResolver.cs`, `FreeThrowResolver.cs`, `ClockResetPolicy.cs`, `PeriodController.cs`, `EligibilityPolicy.cs`; karşılık gelen scenario testleri.

İşler: onaylı reset tablosu; foul/bonus; and-one/2/3 FT; horn ve release; foul-out replacement; period starter; OT; terminal forfeit/abort. ShotAttempt ile box-score attempt ayrımını tamamlama.

Kabul: T04–T11; FT arası rebound yok; offensive foul tek turnover; geçersiz beşliyle oyun sürmüyor; tie çözümü doğru; hiç NaN/sonsuz döngü yok. Eşitlik, guard sonucu uydurma galibiyetle çözülmüyor.

## M4 — Oyuncu/taktik kararlarının etkisi

Önerilen dosyalar: `Ratings/PlayerRatingCalculator.cs`, `TeamRatingCalculator.cs`; `Tactics/OffensivePolicy.cs`, `DefensivePolicy.cs`; `Fatigue/FatigueCalculator.cs`; config'e açık ağırlıklar/eğri ekleri.

İşler: 18 attribute composite'leri; 4+4 policy; pace; handler/shooter/defender weighting; enerji tüketimi/toparlanma; bench yönetimine hazır state. Katsayıların sahibi config; aynı savunma/taktik/fatigue etkisini iki kez sayma.

Kabul: geçerli bütün action dağılımları normalize; aynı fixture'da controlled policy değişimi beklenen aksiyon karışımını etkiliyor; energy sınırları ve dakika muhasebesi doğru. Şans nedeniyle tek maçta sonuç yönü zorunlu test yapılmaz.

## M5 — Yönetici müdahalesi ve replay

Önerilen dosyalar: `Commands/ManagerCommand.cs`, `CommandQueue.cs`, `CommandValidator.cs`; `Core/StepResult.cs`; `Replay/MatchSnapshot.cs`, `ReplayMetadata.cs`; scripted command fixture'ları.

İşler: CommandId/order/boundary; tactic/pace/substitution/timeout; AI fallback; late-game policy; serialize/restore; event replay ile simulation replay ayrımı.

Kabul: T13–T16; duplicate komut etkiyi tekrarlamıyor; iki substitution beşliyi bozmuyor; pending komut legal anda uygulanıyor; aynı komut dizisi aynı maç; restore sonrası devam aynı.

## M6 — Simulator ve kalibrasyon

Önerilen dosyalar: simulator `SingleMatchCommand.cs`, `BatchCommand.cs`, `SummaryExporter.cs`, `ExperimentManifest.cs`; `config/engine/`; `fixtures/teams/`; `reports/balance/` çıktı yolu.

Hedef CLI sözleşmesi; **henüz mevcut komut değildir**:

```text
dotnet run --project src/DreamTeam.Simulator -- single --fixture neutral-mirror --seed 12345 --output reports/single
dotnet run --project src/DreamTeam.Simulator -- batch --fixture neutral-mirror --matches 10000 --seed-start 1 --summary-only --output reports/balance/neutral-10k
dotnet run --project src/DreamTeam.Simulator -- batch --fixture neutral-mirror --matches 100000 --seed-start 1 --summary-only --output reports/balance/neutral-100k
```

İşler: JSON+CSV export; memory-bounded summary mode; deterministic per-match seed; hata sayımı; simetri/kalite/taktik matrisi; holdout validation. Tuning parametrelerini değiştirmeden baseline kaydet.

Kabul: T17/T18; 10K ve final adayda 100K gerçek koşu; tam komut/runtime/duration/seed/metrics kaydı; failures gizlenmiyor; önceden belirlenen eşiklerle değerlendirme. Başarılı çalıştırma gerçekçi basketbolun tek başına kanıtı değildir.

## M7 — Sunucuya bağlama

Önce DB/runtime/auth kararlarını netleştir. API/application/infrastructure projelerini bu aşamada oluştur. Minimum use case: takım/setup edinme, AI maç başlatma, state/events alma, manager command gönderme ve sonuç saklama.

Match runner tek maç state'inin sahibi. SignalR reconnect/dedup ve authorization uygulanır. Endpoint adları bu pakette final değil; sözleşme bu aşamada çıkarılır.

Kabul: başka takım komut gönderemez; aynı maç iki runner tarafından eşzamanlı ilerletilmez; reconnect sequence boşluğu kapatılır; restart/abort policy açık; client'ın gönderdiği skor dikkate alınmaz.

## M8 — Yönetim arayüzü

Hesap/takım, kadro, taktik, maç hazırlığı, text PBP, skor ve box score. PixiJS olmadan da uçtan uca akış çalışmalıdır. İstek durumları ve validation hata metinleri kullanıcıya anlaşılır gösterilir.

Kabul: yeni kullanıcı başlangıç kadrosuyla AI maçı oynar; legal değişiklik yapabilir; reconnect sonrası doğru sonucu görür. Frontend engine formüllerini tekrar uygulamaz.

## M9 — PixiJS

Versiyonlar resmî dokümanla doğrulanır. Event → presentation cue eşlemesi, animasyon kuyruğu, skor zamanı, pause/replay ve event catch-up davranışı uygulanır. Tam fizik/3D yok.

Kabul: hızlı gelen event, düşük FPS, sekme odağı kaybı, reconnect, replay ve maç sonu durumlarında ekran event store ile tutarlıdır. Animasyon RNG'si skoru etkilemez.

## M10 — Kadro geliştirme döngüsü

Ödül ledger'ı, basit scout teklifleri, bütçe kontrolü ve atomik imzalama. Ücret/yenileme/maaş bütçesi bu aşamanın ürün kararlarıdır. Gerçek para veya eski oyunun otomatik oyuncu bırakma kuralını ekleme.

Kabul: T20; retry ile iki kez ödül alınamıyor; aynı teklif eşzamanlı iki kere tüketilemiyor; bakiye/ownership tutarlı; ilk playtest döngüsü tamamlanabiliyor.

## M11+ — Ayrı tasarlanacak işler

PvP matchmaking/ranking, çoklu instance ownership, trade/auction escrow, sezon/lig/draft, lisanslı provider ingestion, rating/değer geçmişleri, ek taktikler ve 3D. Bu listeden özellik kullanıcı kapsamı olmadan sıradaki sprint'e çekilmez.

## İzlenebilirlik özeti

| Gereksinim | Sahip belge | İlk teslim |
|---|---|---|
| Fictional oyuncular, 18 attribute | GDD + domain + engine | M1 |
| Possession/box score | Engine + rules | M2/M3 |
| Clock, foul, FT, OT | Rules | M3 |
| Taktik, enerji, rol | Engine | M4 |
| Müdahale, RNG replay | Events + engine | M5 |
| 100K ve denge | Testing | M6 |
| Server-authoritative live | Architecture + events | M7 |
| Yönetim/sunum | GDD | M8/M9 |
| Scout ve ekonomi | GDD + domain | M10 |
| Uzun vadeli ekonomi/veri | GDD + domain | M11+ |
