# Domain ve veri modeli

Bu bir mantıksal modeldir. Migration/SQL şeması değildir. Entity listesinde yer almak, ilk milestone'da tablo açmak anlamına gelmez.

## Kimlik ve snapshot ayrımı

- `Player`: kalıcı kurgusal oyuncu tanımı; gerçek hayattaki kişi varsa ileride provider eşlemesi.
- `PlayerRatings`: 18 attribute'ün belirli sürümü.
- `RosterEntry`: takımın belirli oyuncuya/oyuncu instance'ına sahipliği ve sözleşmesi.
- `MatchPlayerSnapshot`: maç girişindeki rating ve kimlik; maç boyunca değişmez.
- `PlayerMatchState`: enerji, fauller, sahada kalma süresi ve maç içi durum.
- `PlayerBoxScore`: olaylardan üretilen istatistik görünümü.

Aynı kurgusal oyuncu tanımının farklı takımlarda bulunup bulunamayacağı açık ürün kararıdır. Bu yüzden `PlayerDefinition` ile sahip olunan `PlayerInstance` ayrımına gerek olup olmadığı M7 öncesinde kararlaştırılmalı; gereksiz bir kart sistemi varsayılmamalı.

## İlk motor tipleri

| Tip | Önerilen alanlar | Invariant |
|---|---|---|
| PlayerId/TeamId/MatchId | Güçlü kimlik tipleri veya tutarlı Guid kullanımı | Boş/çakışan kimlik reddedilir |
| PlayerRatings | 18 adet 0–100 değer | Sınır dışı/NaN kabul edilmez |
| Player | Id, DisplayName, Position, Ratings | İsim simülasyon olasılığına girmez |
| Team | Id, Name, Roster | Oyuncu listesi deterministik sıralanabilir |
| Lineup | Beş oyuncu kimliği | Beş farklı, uygun ve kadroya ait oyuncu |
| TeamMatchSetup | Team snapshot, başlangıç lineup, tactics, pace | Başlangıçta geçerli |
| MatchSetup | MatchId, Home, Away, Seed, versions/config snapshot | Input sonradan değiştirilemez |
| MatchState | Period, clocks, score, phase, possession, teams, RNG state | Kurallarla uyumlu tek otorite |
| TeamMatchState | OnCourt, Bench, fouls, timeouts, tactics, pace | Oyuncu aynı anda bench ve sahada olmaz |
| PlayerMatchState | Energy, personal fouls, played milliseconds, form | Enerji 0–100; faulden çıkan oynayamaz |
| PossessionState | Id, offense, control, action context, shot/FT context | OREB ile Id değişmez |
| MatchResult | Status, score, box scores, metadata, diagnostics summary | Tamamlanan ile iptal/incomplete ayrılır |

`Stamina` attribute'ü dayanıklılık kapasitesidir. `Energy` maç içindeki kalan enerjidir. İkisine aynı alan adını vermek önlenir.

Saat birimi devir önerisi: tamsayı milisaniye. Periyot kalan süresi (`GameClockMs`), hücum saati (`ShotClockMs`) ve toplam oynanan süre (`ElapsedGameTimeMs`) farklıdır. Duvar saati motorda bulunmaz. Kesin zaman sözleşmesi M0'da kilitlenir.

## Enum sözlüğü

| Enum | Aday değerler | Durum |
|---|---|---|
| Position | PG, SG, SF, PF, C | Taslak; çoklu pozisyon sonra |
| OffensiveTactic | Balanced, PickAndRoll, PerimeterMotion, InsidePost | Taslak 4 seçenek |
| DefensiveTactic | ManToMan, Drop, Switch, ZonePackPaint | Paket yaklaşımı için öneri |
| Pace | Slow, Normal, Fast | Taslak |
| OffensiveAction | PickAndRoll, Drive, SpotUp, Isolation, Cut, PostUp, OffBallScreen | Temel aksiyon ailesi |
| ShotType | AtRim, ClosePost, MidRange, ThreePoint | FT ayrı çözülür |
| PossessionContext | Transition, HalfCourt, LateClock, AfterTimeout, EndGame | Bazıları aynı anda geçerli olabilir; tek enum zorunlu değil |
| FoulType | Shooting, NonShooting, Offensive | Teknik/flagrant v0.1 dışında |
| TurnoverKind | BadPass, LostBall, OffensiveFoul, Travel, ShotClockViolation | Steal ayrıca attribution |
| MatchPhase | NotStarted, LiveBall, DeadBall, FreeThrows, PeriodBreak, Completed, Aborted | Durum geçişleri 06 belgesinde |
| CommandStatus | Pending, Applied, Rejected, Expired | Transport ack ile karıştırılmaz |

`Steal` top kaybı nedeni ile savunma istatistiğini aynı enum düzeyinde karıştırmamalı. BadPass/LostBall + `StealerId?` gibi ilişki önerilir.

## Kalıcı ürün modeli — M7 ve sonrası

| Model | İlişki / amaç | Aşama |
|---|---|---|
| User | Hesap; kimlik sağlayıcısı ayrıca seçilir | MVP |
| Team | Kullanıcıya ait takım | MVP |
| TeamPlayer / RosterEntry | Team ile sahip olunan oyuncu/sözleşme ilişkisi | MVP |
| Player | Oyuncu tanımı | MVP |
| PlayerRatingHistory | Player + effective time + rating snapshot/version | Değişen rating devreye girince |
| PlayerMarketValueHistory | Player + zaman + değer + hesap kaynağı | Değer güncellemesi devreye girince |
| Tactic / TeamTactic | Taktik tanımı ve takım seçimi | İlk aşamada enum/config yeterli olabilir |
| ScoutResult | Takıma özel, süreli ve tüketilebilir teklif | MVP |
| Match | Setup snapshot/version, lifecycle ve sonuç | MVP |
| MatchParticipant | Match–Team; home/away ve snapshot | MVP |
| MatchPlayer | Maça katılan oyuncu snapshot'ı/sonuç istatistiği | MVP |
| MatchEvent | Match + Sequence; sürümlü payload | MVP |
| EconomyTransaction | Bakiye hareketi, neden, correlation/idempotency key | MVP |
| MarketListing | Satış ilanı, satıcı, oyuncu, fiyat ve lifecycle | Ertelenmiş |
| Trade | Taraflar ve takas teklif/sonuç lifecycle'ı | Ertelenmiş |

## Veri bütünlüğü beklentileri

- Bir maçta `(MatchId, Sequence)` benzersizdir.
- Ödül `(MatchId, TeamId, RewardType)` veya eşdeğer idempotency anahtarıyla bir kez yazılır.
- Scout teklifinin tüketimi ve para düşümü/ownership edinimi atomiktir.
- Bakiye sadece client'tan gelen son değerle güncellenmez; işlem kaynağı kayıtlıdır.
- Para türü decimal veya küçük para birimi cinsinden integer olarak ayrıca seçilir; floating point para hesabı yapılmaz.
- History geçmişi güncel değerle ezilmez. Maçta kullanılan snapshot, history'nin canlı sorgusundan bağımsızdır.
- Zaman damgaları kalıcı katmanda UTC; UI gerektiğinde yerel saat gösterir.
- `ExternalProviderId` tek başına evrensel kimlik değildir. İleride Provider + ExternalId benzersiz eşlemesi önerilir.

## İlk maç fixture'ları

NeutralMirror, StrongRoster, WeakRoster, PerimeterSpecialists, InteriorSpecialists, LowEndurance, HighPressure gibi kurgusal fixture aileleri. İsimler öneridir; sayısal rating'ler henüz üretilmedi. Fixture sürümü ve digest'i test raporunda tutulur. Mirror karşılaştırmasında kimlik dışındaki tüm yetenek/taktik/enerji girdileri aynı olmalıdır.
