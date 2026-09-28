# M7 - Sunucuya Bağlama: Uygulama Planı

> **Durum: Onay bekliyor.** Bu plan 28 Eylül 2026'da hazırlandı. **Uygulama kodu
> yazılmadı.** §3'teki ortam gerçekleri bu oturumda **fiilen ölçüldü** (NuGet
> erişim denemesi, paket restore testi, PostgreSQL bağlantı testi, geçici probe
> dosyaları silindi). §14–§15 uygulama sırasında doldurulacak.

## 0. Bu oturumda kilitlenen kararlar

| ID | Karar | Durum |
|---|---|---|
| **D109** | **Q12 kapandı — canlı maç 8 gerçek dakika.** Simüle 48 dakika gerçek 8 dakikaya sığar; hız çarpanı **6.0** | Kullanıcı kararı |
| **D110** | **Q13 kapandı — PostgreSQL + JWT bearer.** Motor ve Domain **sıfır paketle kalır**; paket bağımlılığı yalnız yeni API/Infrastructure projelerindedir | Kullanıcı kararı |
| **D111** | **Q14 kapandı — oyuncu kişi başına kopya, kadro boyutu serbest.** `User → Player`, `Team → RosterEntry → Player` (04 §"Kalıcı ürün modeli") | Kullanıcı kararı |
| **D112** | **Q16 kapandı — M7'de PvP YOK, sadece AI.** Eşzamanlılık altyapısı yine kurulur (T19 iki bağlantı ister) ama rakip bulma ve çift taraf sahipliği kapsam dışı | Kullanıcı kararı |
| **D113** | **Restart/abort politikası: sunucu çökerse maç `Aborted`, kurtarma YOK.** Sebep kaydedilir, ödül verilmez | Kullanıcı kararı |
| **D114** | **Q19 soru tablosuna eklendi.** M6 oturumunda iki "hâlâ açık" listesine atıf yapılmış ama tabloda hiç yoktu. Bu bir belge tutarsızlığıydı | Düzeltme kararı |

### D110'un sınırı — hangi katman paket alabilir

```
src/DreamTeam.Domain/          sıfır paket      (değişmiyor)
src/DreamTeam.MatchEngine/     sıfır paket      (değişmiyor — H03, 05 §14)
src/DreamTeam.Simulator/       sıfır paket      (değişmiyor)
src/DreamTeam.Application/     sıfır paket      (YENİ — portlar + use case)
src/DreamTeam.Infrastructure/  paket ALABİLİR   (YENİ — DB, dış adaptörler)
src/DreamTeam.Api/             paket ALABİLİR   (YENİ — HTTP, auth, SignalR)
```

Motorun sıfır paketli kalması bir tesadüf değil, **sözleşmedir**: motor
`DateTime`, dosya, ağ ve duvar saati çağıramaz. Paket ihtiyacı olan her şey
`Application` portlarının arkasına, `Infrastructure` uygulamasına gider.

### D111'in sınırı — "serbest kadro" ne demek, ne demek değil

- **Roster (kadro) boyutu serbest:** kullanıcı 8, 12, 30 oyuncu seçebilir.
- **Lineup (saha) 5 oyuncu:** bu bir **motor kuralıdır** (D31, `MatchSetupValidator`).
  Ürün kuralı değildir ve değiştirilmez.
- M6 kalibrasyonunda kullanılan **10 kişilik kadro bir ölçüm aracıdır**, ürün
  kuralı değildir. 08 §"engine fixture'ından ürün kuralı çıkarma" uyarısı bu
  ayrımı yapmamızı gerektiriyor. Kalibrasyon 10/5 ile yapıldı; ürün 8/5 de
  olabilir ve motor ikisinde de çalışır.

## 1. Bu milestone gerçekten neyi kanıtlayacak

M1–M6 boyunca motor **tek bir sunucuda, tek bir iş parçacığında, kimliksiz**
çalıştı. M7'nin varlık sebebi:

1. **Kimlik:** başka bir takımın komutu reddedilir (T19).
2. **Tek sahiplik:** aynı maç iki yürütücü tarafından eşzamanlı ilerletilmez.
3. **Reconnect:** bağlantı kopunca sequence boşluğu kapanır (07 §8).
4. **Kalıcılık:** maç sonucu ve komut kaydı yeniden başlatmayı atlatır.
5. **Canlı yürütme:** duvar saati ile simüle zaman eşlenir — **domain sonucunu
   değiştirmeden**.

**M7 bitince elimizde ilk kez "iki kullanıcı, iki bağlantı, bir sunucu" vardır.**
PvP olmayacak (D112) ama altyapısı ölçülmüş olacak.

## 2. Şu anki durumun dürüst özeti

| Yetenek | Durum |
|---|---|
| HTTP / SignalR / auth | **Yok.** Hiçbir web projesi yok |
| Application katmanı | **Yok.** Use case, port, runner yok |
| Persistence | **Yok.** Hiç tablo, migration yok |
| Canlı yürütme | **Yok.** `Simulate` bitene kadar koşar, duraklar yok |
| Reconnect | **Yok.** Event listesi bellekte tutulmuyor |
| Komut yolu | **Var (M5)** — `CommandQueue` FIFO, idempotency ledger, 15 reddetme sebebi |
| Snapshot | **Var (M5)** — `MatchSnapshot` + `MatchSnapshotData` + `MatchStateFingerprint` |
| Test altyapısı | **Var.** 443 test, iki test projesi |
| Motor paket bağımlılığı | **Sıfır** — korunacak |
| `web/` istemci | **Yok ve M7'de yok** (03 §tablo: `web/` = M8/M9) |

## 3. Ölçülmüş ortam gerçeği (bu oturumda koşturuldu)

Bu bölüm M7'nin en büyük riskini taşır: **bu ortamda paket indirilemiyor.**

| Ölçüm | Sonuç | Sonuç için ne yapmalı |
|---|---|---|
| `nuget.org` erişimi | **YOK** — `NU1101: paket bulunamıyor` | Sürümler **zorla** seçilmeli |
| Yerel NuGet önbelleği | **883 paket** | Gerekli her şey var |
| Planlanan paket setiyle `dotnet restore` | **BAŞARILI** (331 ms, çevrimdışı) | Sürümler sabitlenebilir |
| `Microsoft.Extensions.Hosting` açık `PackageReference` | **NU1510 uyarısı** — Web SDK'da otomatik geliyor | **Referans verilmeyecek** |
| PostgreSQL 18 kurulu | **Evet** — `C:\Program Files\PostgreSQL\18\bin` | Entegrasyon testi gerçek DB'ye gider |
| `localhost:5432` | **Dinliyor** | Testler çalışma anında bağlanabilir |
| `Testcontainers.PostgreSQL` | **Önbellekte YOK** | Kullanılamaz; yerel PostgreSQL 18 kullanılır |
| SDK | 10.0.401 (`global.json`, kilitli) | Değişmeyecek |
| ASP.NET Core runtime | 10.0.12 mevcut | `Microsoft.NET.Sdk.Web` ile hedeflenebilir |

### Kullanılacak paket sürümleri — ölçülmüş olarak mevcut

| Paket | Sürüm | Neden |
|---|---|---|
| `Npgsql` | **10.0.2** | PostgreSQL sürücüsü |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | **10.0.9** | D110 |
| `System.IdentityModel.Tokens.Jwt` | **8.22.0** | Token üretimi/okuma |
| `Dapper` | **2.1.79** | SQL (aşağıda gerekçe) |
| `Microsoft.AspNetCore.Mvc.Testing` | **10.0.10** | API testleri (in-process) |
| `Microsoft.AspNetCore.TestHost` | **10.0.10** | SignalR hub testi |

**Dapper, EF Core değil.** Gerekçe: 03 §"Kaçınılacak erken karmaşıklık"
generic repository'yi ve CQRS framework'ünü yasaklıyor; 04 §"Veri bütünlüğü
beklentileri" tekil kısıtların **şemada görünür** olmasını istiyor. Elle yazılmış
SQL'de `UNIQUE (match_id, team_id, reward_type)` kısıtı gözle görülür; EF Core
migration'ında gömer. M7'nin şeması **6 tablo**, el yazımı makul.

**Bu bir gerçek kısıttır:** şema SQL'i elle yazılacak ve migration aracı
olmayacak. Uygulamada ilk tablo yazıldığında bu, yorumla kayda geçecek.

## 4. Oluşturulacak dosyalar

```
src/DreamTeam.Application/                    (YENİ, sıfır paket)
  UseCases/
    RegisterUser.cs / RegisterUserResult.cs
    CreateTeam.cs / CreateTeamResult.cs
    AddPlayerToRoster.cs
    CreateLineup.cs
    StartMatch.cs / StartMatchResult.cs
    SendManagerCommand.cs / SendManagerCommandResult.cs
    CompleteMatch.cs / CompleteMatchResult.cs
  Runner/
    MatchSession.cs           tek sahip; durum + olay + komut kuyruğu
    MatchSessionStore.cs      matchId -> MatchSession (bellek içi)
    LivePacer.cs              duvar saati <-> simüle zaman eşlemesi
    MatchSessionLock.cs       sahiplik kilidi
  Ports/
    IUserRepository.cs
    IPlayerRepository.cs
    ITeamRepository.cs
    IMatchRepository.cs
    IEventRepository.cs
    ICommandLogRepository.cs
    IUnitOfWork.cs
    IClock.cs                 duvar saati (testte sahte)
    ISetupDigest.cs           D87 için kanonik digest
    IMatchConfigProvider.cs   EngineConfig'i belgeden yükler
  Setup/
    SetupDigest.cs            MatchSetup -> SHA-256 (kayitli kanonik sira)
    MatchSetupFactory.cs      kalip kadro/lineup -> engine MatchSetup
  Security/
    ICurrentUser.cs           JWT sub -> UserId
  Time/
    IClock.cs (Ports altinda)

src/DreamTeam.Infrastructure/                 (YENİ, paketli)
  Postgres/
    NpgsqlConnectionFactory.cs
    UserRepository.cs / PlayerRepository.cs / TeamRepository.cs
    MatchRepository.cs / EventRepository.cs / CommandLogRepository.cs
    Migrations/
      001_initial_schema.sql
      MigrationRunner.cs
  Security/
    JwtTokenIssuer.cs
  Time/
    SystemClock.cs

src/DreamTeam.Api/                             (YENİ, paketli)
  Program.cs                                 composition root
  Auth/
    JwtOptions.cs / TokenEndpoints.cs
  Hubs/
    MatchHub.cs                              SendCommand, RequestState
    MatchEventPublisher.cs
  Endpoints/
    UserEndpoints.cs / TeamEndpoints.cs / PlayerEndpoints.cs
    MatchEndpoints.cs
    MapEndpoints.cs
  DependencyInjection.cs

config/
  database/connection-string.example.txt       (gizli deger YOK)
  auth/jwt-signing-key.example.txt            (gizli deger YOK)

migrations/                                   (YENİ — SQL dosyalari)
  001_initial_schema.sql
  002_match_lifecycle.sql

tests/DreamTeam.Application.Tests/            (YENİ)
  LivePacerTests.cs
  MatchSessionOwnershipTests.cs
  SetupDigestTests.cs
  UseCaseTests.cs
  CommandPathTests.cs
  CompleteMatchIdempotencyTests.cs
  RestartPolicyTests.cs
  M7TestData.cs
tests/DreamTeam.Api.Tests/                    (YENİ)
  AuthorizationTests.cs                      (T19)
  ReconnectOrderingTests.cs
  HubTests.cs
  EndpointTests.cs
```

## 5. Canlı yürütme — duvar saati eşlemesi (D109)

### 5.1 Çekirdek değişmez

Motor **hâlâ `DateTime` okumaz, hâlâ beklemez** (05 §14, 03 §"Offline ile canlı
yürütme"). Bekleme `Application` katmanındadır. `LivePacer` motorun dışındadır.

### 5.2 Hız çarpanı

```
48 simüle dakika / 8 gerçek dakika = 6.0
```

Bu bir **katsayı**, `IConfig` üzerinden okunur. 8 dakika bir **ürün kararıdır**
(D109); çarpan ondan türetilir.

### 5.3 Pacer algoritması — "gecikmeyi yakala", "önde koşma"

```csharp
// Concept pseudocode. Bekleme motorun DIŞINDA ve SONRASINDA.
while (!state.IsTerminal) {
    step = engine.Advance(state, drainPendingCommands());
    state = step.State;
    publish(step.Events);            // SignalR'ye

    var targetMs = stopwatch.ElapsedMilliseconds * speedup;  // 6.0
    var simMs    = state.Clock.ElapsedGameTimeMs;

    if (simMs < targetMs) {
        await delay(targetMs - simMs);   // MOTOR ONDE: yakala
    }
    // MOTOR GERIDE: bekleme YOK, olabildigince hizlan.
}
```

**Neden "lag-behind"?** Eğer pacer "her adımda sabit bekle" olsaydı, motor
bir adımda 9.5 saniye ilerlediğinde 8 dakikalık hedef **aşılırdı**; puan
kazanmak için motoru yavaşlatmak gerekir ki bu **domain sonucunu değiştirir**.
Lag-behind ile:

- Motor öndeyse bekleme olur → toplam süre **8 dakikaya yaklaşır**
- Motor gerideyse bekleme olmaz → süre **8 dakikayı geçmez**
- Süre **asla 8 dakikayı aşmaz**, motor yavaşlatılmaz

### 5.4 Kritik test: canlı == çevrimdışı

**Aynı seed + aynı komut listesi için canlı koşu (duvar saati eşlemesiyle)
`Simulate` ile bayt bayt aynı event akışını üretmelidir.** Pacer yalnız
`Advance` döndükten **sonra** bekler; motorun girdisini değiştirmez.

Bu, 03 §"motor maçın tamamını baştan üretip client'a yavaşça göstermesi canlı
müdahaleyle çelişir" uyarısının karşılığıdır ve **ölçülebilir** bir testtir.

### 5.5 Ayarlanabilir hız ve duraklatma

- Pacer `IClock` ve `IDelay` portları üzerinden çalışır → testte **anında**.
- Duraklatma (pause) M7'de **yok**. Kullanıcı ürün kararı isterse eklenir.

## 6. Komut yolu — kimlik, idempotency, ACK/Applied

07 §5'i birebir izleyen akış:

```
İstemci (hub)                 Sunucu (Application)                Motor
    |                              |                             |
    |-- SendCommand(command) ----->|                             |
    |                              |-- 1) JWT sub -> UserId       |
    |                              |-- 2) Bu user bu maci sahipleniyor mu? (DB)
    |                              |-- 3) Ayni CommandId daha once geldi mi? (DB UNIQUE)
    |                              |-- 4) komut kuyruguna ekle   |
    |<-- ACK (alindi) ------------|                             |
    |                              |-- bir sonraki adimda ------->|
    |                              |                             |-- Advance(state, [cmd])
    |                              |<-- StepResult --------------|
    |                              |-- sonucu DB'ye yaz          |
    |<-- CommandApplied/Rejected -|                             |
```

**Kritik ayrım (07 §5):** `ACK = alındı/kuyruğa girdi`, `Applied = state'e
işlendi`. İkisi **tek mesajda karıştırılmaz.** M5'in `CommandResult` zaten bu
ayrımı taşıyor (`Applied1` / `Rejected1`); sunucu bunu olduğu gibi iletir.

**"Ben şu takımım" beyanına güvenilmez** (07 §5). Sahiplik JWT `sub`'undan
ve veritabanından çözülür. İstemcinin gönderdiği `TeamId` yalnız **hedef**tir,
sahiplik kanıtı değildir.

**Reddedilen komut RNG tüketmez** (M5'in `CommandValidator` sözleşmesi, M7'de de
geçerli). Yetkisiz bir komut motora **hiç girmez** — daha runner'a ulaşmadan
403 ile döner. Bu, T19'un güvenlik testinin temelidir.

## 7. Tek sahiplik (09 kabul: "aynı maç iki runner'dan eşzamanlı ilerletilmez")

03 §"API ve persistence": *"Match runner aynı maçın state'ini tek sahip altında
seri değiştirir."* 03 ayrıca *"Dağıtık worker, lease/fencing ... ancak çoklu
instance ... gereksinimi geldiğinde"* diyor. M7 tek instance modüler monittir,
dolayısıyla:

| Gereksinim | M7 çözümü | Neden yeterli |
|---|---|---|
| Aynı anda iki ilerletme | Maç başına **tek `SemaphoreSlim(1,1)`**; `Advance` kilit altında | İki iş parçacığı sıralı gelir |
| Bayat istek | Oturum **epoch** sayacı; dış istek epoch'u okur, kilit altında doğrulanır | Kilit dışında okunan epoch yarış kaybı üretmez |
| Motor içi bayatlık | M5'in `CommandQueue` `ExpiresAfterSequence` (D95) | Zaten var, testli |
| Çoklu sunucu | **M7'de yok** | 03'e göre gerekli değil; eklenecekse lease/fencing ayrı tasarım |

**Kabul testi:** iki iş parçacığı eşzamanlı `Advance` çağırır → ikincisi
birincinin **sonraki** durumunu görür, yarık (torn) durum asla oluşmaz. Bu
**100.000 kez tekrar edilerek** test edilir.

## 8. Reconnect ve mesaj sırası (07 §8)

Sunucu her oturumda event'leri **sequence sırasıyla** tutar (maç başına
ölçülen ~1228 event — M6). Client son uyguladığı `Sequence`'ı bildirir:

| Durum | Sunucu yanıtı |
|---|---|
| `lastApplied >= current` | Boş (client zaten güncel) |
| Boşluk hâlâ bellekte | `Events(afterSequence: lastApplied)` |
| Boşluk **kırpılmış** (sunucu yeniden başladıysa) | `Snapshot(at: S) + Events(after: S)` |

**Atomik sınır (07 §8: "örtüşme/boşluk sınırı atomik tanımlanır"):** yanıt
**kilit altında ve tek bir `sequence` okunarak** üretilir. Snapshot ve ona eşlik
eden event listesi farklı adımlardan alınırsa araya event girer. Bu yüzden
`MatchSession.CaptureFor(lastApplied)` kilidi alır, `sequence`'i bir kez okur,
snapshot'ı ve event listesini **aynı okumanın sonucunu** kullanarak döndürür.

**Client'ın sorumluluğu (07 §8):** duplicate atar, boşlukta tamponlar, eksik
aralığı ister. M7'de client **yok** (`web/` = M8/M9). Reconnect sözleşmesi bu
yüzden **sunucu tarafında, in-process test istemcisiyle** kanıtlanır.

**Sunucu yeniden başlarsa** (D113): oturum bellekte yok, snapshot da yok →
client "bu maç artık canlı değil" alır. Kurtarma yok.

## 9. Persistence — 6 tablo, fazlası değil

AGENTS.md: *"Gelecekteki veri modellerinin tamamına ilk milestone'da
tablo/migration açma."* 04'ün tablosunda 14 model var; **M7'de 6'sı**:

| Tablo | 04'teki karşılık | M7'de neden |
|---|---|---|
| `users` | `User` | D111'in sahibi |
| `players` | `Player` | Kullanıcıya ait kopya (D111) |
| `teams` + `roster_entries` | `Team`, `TeamPlayer/RosterEntry` | Kadro serbest (D111) |
| `matches` | `Match` | Setup snapshot + lifecycle + sonuç |
| `match_participants` | `MatchParticipant` | home/away + snapshot |
| `match_events` | `MatchEvent` | Event replay (07 §7) |
| `manager_commands` | (yeni) | Komut denetimi + idempotency |

**M7'de OLMAYAN tablo:** `economy_transactions`, `scout_results`,
`market_listings`, `trades`, `player_rating_history`. Bunlar M10/M11'in;
şimdi açmak "geleceğin modelini şimdi açmak" yasağına girer.

### 9.1 Tekil kısıtlar = idempotency (04 §"Veri bütünlüğü beklentileri")

```sql
UNIQUE (match_id, sequence)              -- 04: event sırası
UNIQUE (match_id, team_id, reward_type)  -- 04: ödül bir kez  (M10'a kadar yazılmaz)
UNIQUE (match_id, command_id)            -- 07: aynı CommandId aynı sonuç
UNIQUE (match_id)                        -- maç sonucu bir kez (03: idempotent)
```

### 9.2 D87 ve kalıcı eşitlik

D87 (`ImmutableArray<T>` içeren `record`'larda bozuk değer eşitliği) M7'de
**kabul kriterlerini engellemez**, çünkü:

- **Idempotency veritabanı kısıtıyla sağlanır**, `record.Equals` ile değil.
  İkinci tamamlama denemesi `UNIQUE` ihlaliyle **no-op** olur. Bu, 03'ün istediği
  "kalıcı katman"dır ve D87'den **bağımsızdır**.
- Snapshot/setup karşılaştırması için `SetupDigest` kullanılır: kadro
  `RosterOrdering.Canonical` ile sıralanır, 18 rating alanı **açık sabit
  sırayla** yazılır, SHA-256 alınır. Bu `record.Equals` **değildir** ve 03 §"Sürüm
  ve tekrar üretilebilirlik"in istediği "setup digest"tir.

**Sapma (bilinçli):** `record`ların `Equals`'i kalıcı olarak düzeltilmiyor.
M7'nin kabul kriterlerini engellemiyor ve M1'in donmuş sözleşmelerine dokunmayı
gerektiriyor. **M11'e erteleniyor; gerekçe kayda geçecek.**

### 9.3 Event hacmi — ölçülecek, varsayılmayacak

M6 ölçtü: **maç başına 1228 event** (kalibre config ile 1264). Bu küçük. Ama
`matches × events` 100K maçta **126 milyon satır** olur. M7 bir API'dir,
100K maç buradan koşulmayacak; yine de:

- Satır başına ortalama bayt **ölçülecek** ve rapora yazılacak.
- Saklama kararı (tüm event / yalnız sonuç) **M11'in konusu**; M7'de ölçüm
  alınır ve karar **ertelenir**.

## 10. Restart / abort politikası (D113)

```
Sunucu çöker, maç Running
        ↓ yeniden başlatma
Başlangıç taraması: tüm Running maçlar → Aborted
        ↓
reason = "sunucu yeniden başlatıldı; maç sürdürülmedi (D113)"
        ↓
Ödül YOK, maç sonucu geçersiz (IsTie = false), sebep yazılır
```

**Neden kurtarma yok:** 03 kurtarma politikasının sahibini "M7/M11 tasarımı"
olarak bırakıyor ve kullanıcı **kurtarma olmayacak** dedi. Snapshot
kurtarma, D87'nin kalıcı çözümünü de gerektirirdi; iki belirsizliği bir
milestone'a yığmak yerine ikisini de kayda geçirmek daha dürüst.

**Kurtarma ileride istenirse** yol açık: `MatchSnapshot` zaten var. Kapı
kapatılmıyor, sadece M7'de kapı **kilitli**.

## 11. Test senaryoları (hedef ~45-55)

| # | Test | Kapsam |
|---|---|---|
| 1 | `PacerSleepsOnlyWhenTheEngineIsAhead` | D109 |
| 2 | `PacerNeverSleepsWhenTheEngineIsBehind` | D109 |
| 3 | `AWholeMatchNeverExceedsTheWallClockBudget` | 8 dk, toleranslı |
| 4 | **`ALiveMatchProducesTheSameEventsAsSimulate`** | **en kritik test** |
| 5 | `PacerConsumesNoRng` | M4/M5 sözleşmesi |
| 6 | `TwoConcurrentAdvancesNeverTearTheState` | 09 kabul |
| 7 | `AStaleEpochRequestIsRejectedWithoutTouchingTheState` | 09 kabul |
| 8 | `ACommandFromAnotherTeamIsRejected` | **T19** |
| 9 | `ARejectedCommandNeverReachesTheEngine` | T19 |
| 10 | `ARejectedCommandConsumesNoRng` | 07 §5 |
| 11 | `AckAndAppliedAreSeparateMessages` | 07 §5 |
| 12 | `TheSameCommandIdReturnsTheSameResult` | 07 §5 |
| 13 | `TheClientCannotDeclareTeamOwnership` | 07 §5 |
| 14 | `ReconnectAfterSequenceDeliversExactlyTheGap` | 07 §8 |
| 15 | `ReconnectBeyondTheBufferDeliversSnapshotPlusEvents` | 07 §8 |
| 16 | `SnapshotAndEventsComeFromTheSameSequence` | 07 §8 atomiklik |
| 17 | `AGapLargerThanTheBufferIsNotSilentlyTruncated` | 07 §8 |
| 18 | `DuplicateEventsAreDeliveredAtMostOnce` | 07 §8 |
| 19 | `RestartMarksRunningMatchesAborted` | **D113** |
| 20 | `RestartGrantsNoReward` | D113 |
| 21 | `RestartReasonIsRecorded` | D113 |
| 22 | `CompletingAMatchTwiceIsANoOp` | 03 idempotent |
| 23 | `CompletingTwoDifferentMatchesBothSucceed` | kontrol |
| 24 | `SetupDigestIsStableAcrossInstances` | D87 |
| 25 | `SetupDigestChangesWhenAnyRatingChanges` | D87 |
| 26 | `SetupDigestIsIndependentOfRosterInputOrder` | D87 |
| 27 | `MigrationsApplyToAnEmptyDatabase` | 09 |
| 28 | `MigrationsAreIdempotentWhenReapplied` | 09 |
| 29 | `UniqueMatchIdSequenceRejectsDuplicateEvents` | 04 |
| 30 | `UniqueMatchIdCommandIdRejectsDuplicates` | 07 |
| 31 | `TheEngineConfigComesFromTheBalanceDocument` | M6 D103 |
| 32 | `AnEngineConfigMismatchIsRefusedNotSilentlyUsed` | 07 §7 |
| 33 | `AUserOwnsThePlayersItCreates` | D111 |
| 34 | `TwoUsersCannotShareAPlayerRow` | D111 |
| 35 | `RosterSizeIsFreeButLineupMustBeFive` | D111 + D31 |
| 36 | `ALineupOutsideTheRosterIsRefused` | motor kuralı |
| 37 | `StartMatchFreezesTheSetupSnapshot` | 04 |
| 38 | `LaterRosterEditsDoNotChangeARunningMatch` | 04 "history ezilmez" |
| 39 | `PlayerMinutesAndEnergyArePersisted` | 08 §6 |
| 40 | `OnlyTheOwningUserCanSubscribeToAMatchHub` | T19 |
| 41 | `ANonOwnerReceivesNoEvents` | T19 |
| 42 | `AnAnonymousRequestIsRejected` | D110 |
| 43 | `AForgedTokenIsRejected` | D110 |
| 44 | `TheMatchCompletesWithTheEngineResultNotTheClientClaim` | 09 |
| 45 | `NoWallClockOrSleepCallReachesTheEngine` | 05 §14 regresyon |
| 46 | `TheEngineAndDomainProjectsStillHaveZeroPackages` | §0 sınırı |
| 47 | `EventRowSizeIsMeasuredAndReported` | §9.3 |
| 48 | `PvPEndpointsDoNotExist` | D112 kapsam |

**Not:** 1–6, 14–18, 19–21, 35–36, 45 **motorun mevcut testleriyle birlikte**
koşar ve hızlıdır. 27–31, 34, 47 gerçek PostgreSQL 18'e bağlanır ve **bu ortamda
çalıştırılabilir** (§3'te ölçüldü: 5432 dinliyor). Bu testler çalışma zamanında
DB'ye ihtiyaç duyar; DB yoksa **atlanır ve raporda "koşulmadı" yazılır** —
"geçti" yazılmaz.

## 12. Kabul kriterleri

| Kriter | Nasıl doğrulanır | Sonuç |
|---|---|---|
| `Application` sıfır paket | `dotnet list package` | (uygulamada) |
| Motor + Domain sıfır paket | `dotnet list package` × 5 | (uygulamada) |
| Engine'de duvar saati/sleep yok | kaynak taraması | (uygulamada) |
| Tüm testler | `dotnet test -c Release` ve `-c Debug` | (uygulamada) |
| T19 | `AuthorizationTests` | (uygulamada) |
| Tek sahiplik | 100.000 eşzamanlı çağrı | (uygulamada) |
| Reconnect | `ReconnectOrderingTests` | (uygulamada) |
| Canlı == çevrimdışı | `ALiveMatchProducesTheSameEventsAsSimulate` | (uygulamada) |
| Restart policy | `RestartMarksRunningMatchesAborted` | (uygulamada) |
| Migration'lar | boş DB'ye uygulanır, ikinci kez no-op | (uygulamada) |
| Idempotency | `CompletingAMatchTwiceIsANoOp` | (uygulamada) |
| 09 kabul maddeleri | §13 | (uygulamada) |

**Raporda yazılacak gerçek ölçümler:** canlı maçın duvar saati süresi
(hedef 480 sn, sapma yazılır), event satırının ortalama baytı, DB'ye yazılan
toplam maç/event sayısı, p50/p95 komut gecikmesi.

## 13. 09'un M7 kabul maddeleri — nasıl kanıtlanacak

| 09 kabul maddesi | Kanıt |
|---|---|
| Başka takım komut gönderemez | `AuthorizationTests` (T19) + kaynaktan DB'ye hiç inmeme |
| Aynı maç iki runner'dan eşzamanlı ilerletilmez | `TwoConcurrentAdvancesNeverTearTheState` (100K tekrar) |
| Reconnect sequence boşluğu kapanır | `ReconnectOrderingTests` |
| Restart/abort policy açık | **D113** + `RestartMarksRunningMatchesAborted` + docs |
| Client'ın gönderdiği skor dikkate alınmaz | `TheMatchCompletesWithTheEngineResultNotTheClientClaim` |

**Bu maddelerin beşi de testle kanıtlanacak.** "Policy açık" yazıyla değil,
çalışan kodla kapanacak.

## 14. Bilinçli olarak yapılmayacaklar

- **PvP** (D112). Rakip bulma, iki maç sahipliği, eşzamanlı iki kullanıcı
  yönetimi yok. Eşzamanlılık altyapısı yine kurulur.
- **`web/` istemci** (React, PixiJS). 03 §tablo bunu M8/M9'a koyuyor.
- **Ekonomi, ödül, bütçe, scout, transfer** (M10). Tabloları bile açılmaz.
- **`PlayerRatingHistory`, `PlayerMarketValueHistory`** (M11).
- **Çoklu sunucu / lease / fencing / transactional outbox** (03'e göre gerekli
  değil; gerekirse ayrı tasarım).
- **Redis.** 03: "memory state'in yedeği yerine rastgele eklenmez."
- **Maç kurtarma** (D113).
- **`record.Equals` kalıcı düzeltmesi** (D87). M7'yi engellemiyor; M11'e.
- **EF Core ve migration aracı.** Dapper + elle SQL (§3).
- **Hız ayarı, pause/resume, canlı istatistik panosu** (M8+).
- **Rate limit'in ayrıntılı politikası.** 07 §5 rate limit istiyor; M7'de
  kaba bir sınır (kullanıcı başına N komut/sn) uygulanır, ayrıntı M8'e.

## 15. Riskler

| # | Risk | Etiket | Azaltma |
|---|---|---|---|
| 1 | **Çevrimdışı NuGet.** Belgeye yazılmayan bir sürüm istenirse derleme durur | **Yüksek** | §3'teki sürümler ölçülmüş; `global.json` ve paket sürümleri kilitli. Yeni paket istenirse **önce** önbellek kontrolü |
| 2 | **T19 için iki bağlantı gerekiyor, client yok** | Orta | `Mvc.Testing` + in-process SignalR istemcisi; `web/` gerekmiyor |
| 3 | **Event saklama hacmi** (100K maç = 126M satır) | Orta | §9.3'te **ölçülür**, karar M11'e ertelenir |
| 4 | **Elle yazılmış migration** yanlış yazılır | Orta | Boş DB'ye uygulanıp doğrulanır; ikinci uygulama no-op olmalı |
| 5 | **Pacer motor sonucunu bozar** | **Yüksek** | §5.4 testi bunu doğrudan ölçer; bekleme `Advance` dönüşünden sonra |
| 6 | **D87 idempotency'yi engeller** | Orta | §9.2: idempotency **DB kısıtıyla**, `Equals` ile değil |
| 7 | **8 dakikalık hedef ulaşılmaz** (motor çok yavaş) | Düşük | Motor 100K'da 162 sn/100K = 616 maç/sn; tek maç saniyeler sürüyor. Pacer bekleyerek **yavaşlatır**, hız kaybı yok |
| 8 | **8 dakika çok uzun/çok kısa çıkarsa** | Düşük | D109 ürün kararı; `IConfig`'den okunur, değiştirmek tek satır |
| 9 | **JWT anahtarı sıza** | **Yüksek** | Anahtar **koda veya repo'ya girmez**; `example.txt` şablonları yalnız isim verir. Gerçek değer ortam değişkeni |
| 10 | **Bağlantı dizesi sıza** | **Yüksek** | Aynı; `example.txt`. Testler kendi geçici DB'sini kullanır |
| 11 | **Sunucu yeniden başlarken maç Running kalır** | Düşük | D113'ün taraması; testli |
| 12 | **Pacer + kilit etkileşimi kilitlenme yapabilir** | Orta | Bekleme **kilidi serbest bıraktıktan sonra**; kilit yalnız `Advance` ve komut kuyruğu etrafında |
| 13 | **"PvP yok" kararı geri alınır** | Düşük | Maç sahipliği tek takım varsayıyor; PvP eklemek M7 sonrası bir iş, D112'yi geri almayı gerektirir |

## 16. Sonraki milestone bağlantısı

**M8 (React yönetim ekranları)** M7'nin API'sini tüketir: kadro kurma, maç
başlatma, canlı maç ekranı. `M7`'nin `MatchHub` sözleşmesi M8'in client
store sözleşmesinin sunucu tarafıdır (07 §9).

**M9 (PixiJS sunumu)** aynı event akışını canlandırır; 07 §9 "ayrı versioned
presentation contract" der.

**M11** D87'nin kalıcı düzeltmesini, event saklama kararını ve gerçek veri
konusunu (Q17) taşır.
