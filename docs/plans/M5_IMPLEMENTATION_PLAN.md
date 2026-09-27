# M5 - Yönetici Müdahalesi ve Replay: Uygulama Planı

> **Durum:** Onay bekliyor. Bu plan 27 Eylül 2026'da hazırlandı. Uygulama kodu
> yazılmadı. §2'deki ölçümler bu oturumda gerçekten koşturuldu; §16 ve §17
> uygulama sırasında doldurulacak.

## 0. Bu oturumda kilitlenen kararlar

| ID | Karar | Durum |
|---|---|---|
| **D79** | Uzatma üst sınırı **2** (toplam en fazla 6 periyot). Sınıra gelindiğinde hâlâ eşitse maç **`Aborted`** olur; kazanan uydurulmaz. Değer `RulesProfile`'a girer ve `ConfigHash`'e yazılır. | Kullanıcı kararı — **D78 kapatıldı** |
| **D80** | **Clutch yok.** Motor son dakikalarda oyun davranışını değiştirmez. Timeout sayısı/penceresi ayrı konu. | Kullanıcı kararı |
| **D81** | **AI fallback: motor yönetir.** Taktik/tempo/substitution kararlarını motor verir (mevcut `Balanced`/`ManToMan`/`Normal`); yönetici istediği anda müdahale eder, "devralma" modu yok. | Kullanıcı kararı — Q11 parçası |
| **D82** | **Substitution penceresi: yalnız düdük sonrası dead-ball ve devre arası.** Normal basket sonrası pencere **açılmaz** (06 §7'de M5'e bırakılan karar). DREB/steal sonrası oyun canlıdır → pencere yok. | Kullanıcı kararı — 06 §7 kapandı |
| **D83** | **20 saniyelik timeout'un tipi motorun bilir, süresi M7'nin.** `TimeoutKind.Full` ve `TimeoutKind.Short20` ikisi de sayaçtan düşer ve aynı mantıksal pencereyi açar; 20 saniyenin duvar saati süresi canlı runner'da yaşar. | Kullanıcı kararı |
| **D84** | **Timeout: takım başına maçlık 4 tam, son 2'si yalnız düzenleme periyodunun son 2 dakikasında.** Uzatma başına **+1**. Timeout canlı saati tüketmez ve hücum saatini **başlatmaz** (06 §6). Uygulama yalnız dead-ball sınırında olur — NBA'daki canlı top anında çağırma kuralı **bilinçli sapma** olarak kaydedilir. | Kullanıcı kararı — **Q11 kapandı** |
| **D85** | Substitution komutu **çıkan ve giren oyuncuyu açıkça adlandırır.** Motor "kimi çıkaracağım" tahmininde bulunmaz. Otomatik çıkarma yalnız foul-out yolunda vardır (M3, `EligibilityPolicy`). | Uygulama kararı — 07 §5'e göre |
| **D86** | Bekleyen komut sırası **`AcceptedOrder` ile FIFO.** Aynı tip taktik komutlarında last-write-wins doğal olarak FIFO'dan çıkar; ikinci substitution ilk uygulanmış lineup'e karşı yeniden doğrulanır. Sessizce keyfî seçim yok (07 §6). | Uygulama kararı — 07 §6'yı karşılıyor |

### Kaynaktan gelmeyen sayı — dürüst işaretleme

`RulesProfile.ShortTimeoutsPerTeam = 3` **hiçbir kaynaktan gelmiyor**. NBA'da
20 saniyelik timeout sayısı 5'tir, ama kullanıcı kararı (D83) motora yalnız
**tipi** vermeyi söyledi, sayıyı değil. 3 bir yer tutucudur; 06 §23'ün
"sayılar özel oyun basitleştirmesidir" uyarısının parçasıdır. `ConfigHash`'e
gireceği için kolayca değiştirilebilir. **Kullanıcı onayı bekliyor.**

### M5'te değişmesi gereken M4 sözleşmeleri

| Sözleşme | Değişiklik | Gerekçe |
|---|---|---|
| `MatchSimulation.Advance(MatchState)` | → `Advance(MatchState, IReadOnlyList<ScheduledManagerCommand>)` | 03 §"Offline ile canlı yürütme" hedef sözleşmesi. Kayıtlı planlı değişiklik. |
| `MatchSimulation.Simulate(MatchSetup)` | → `Simulate(MatchSetup, IReadOnlyList<ScheduledManagerCommand>)` | Aynı. İkinci bir simülasyon algoritması **yazılmayacak** (H03). |
| `StepResult` | + `CommandResults` alanı | 03: "StepResult yeni state, events, **command sonuçları** ve durum taşır." |
| `MatchState` | + `PendingCommands`, + `NextCommandId` | Uygulanamayan komut kuyruğu state'in parçası olmalı; serileştirilebilir ve replay edilebilir olmalı. |
| `MatchEventType` | 18 → **24** | Müdahale: `TacticChanged`, `PaceChanged`, `Substitution`, `Timeout`. Sonuç: `CommandApplied`, `CommandRejected`. |
| `EventSchemaVersion` | 3 → **4** | Yeni event türleri. |
| `RulesProfile` | + `MaxOvertimePeriods`, + timeout alanları | D79, D84. `ComputeConfigHash`'e yazılır. |
| `TeamMatchState` | + `TimeoutsUsed` | Yedek olarak tutulmayan tek sayaç; `FoulCounters` genişletilmez (D55'in "roster sınırı" disiplini). |

**Dokunulmayacaklar:** `MatchClock`, `IRandomSource`, `SeededRandom`,
`EngineIdentity`, `PlayerRatings`, `MatchSetupValidator`'ın kadro/lineup kapsamı,
`RosterOrdering`, `PlayerRatingTables`.

### Bilinen yan etki: RNG çağrı sırası **değişmez**

M5'in en rahatlatıcı özelliği bu: **komutlar RNG tüketmez.** 07 §5 bunu zaten
şart koşuyor ("Invalid command RNG tüketmemeli"). Geçerli bir komut da öyle
olmalı — komut bir olasılık değildir, bir yazma işlemidir.

Böylece M4'ün determinizm sözleşmesi (`1-12` çağrı sırası) **korunur** ve
M5 sonrası `MatchSimulation` sınıfının determinizm testleri geçerli kalır.
Doğrulama: §11'de `CommandDoesNotShiftTheRngStream` testi.

## 1. Neden bu sıra

M3 kuralları, M4 kararları, M5 müdahale. Sıralama tesadüf değil:

- M5 olmadan M6 (10K/100K deney) **ölçülemez** çünkü deney komut üretmez ve
  hız ölçümü `Advance` maliyetine bağlıdır.
- M5 olmadan M7 (API/SignalR) **yazılamaz** çünkü "yetkili komut" sözleşmesi
  (03 §"API ve persistence") motorun command sözleşmesini bekler.
- M5 olmadan replay (09 §M5 çıkış kapısı) yok; `MatchResult` saklanan event'lere
  dayanır ama **motor yeniden çalıştırılamaz**, hata üretilemez.

## 2. Mevcut durumun dürüst özeti

M4 bittiğinde motor **yönetici müdahalesini hiç bilmiyor**:

| Yetenek | Durum |
|---|---|
| Taktik/tempo değiştirme | **Yok.** `TeamMatchState.OffensiveTactic`/`Pace` var ama maç boyunca **değişmez**; `MatchSetup`'tan okunur. |
| Substitution | **Yok.** Tek yol `EligibilityPolicy.SelectReplacement` → yalnız foul-out, otomatik. |
| Timeout | **Yok.** `MatchPhase.DeadBall` enum'da **tanımlı** ama bilinçli olarak **hiçbir zaman kalıcı yazılmıyor** (D54). |
| Command kavramı | **Yok.** `ManagerCommand`, `CommandId`, `AcceptedOrder`, `ApplyBoundary` yok. |
| Replay | **Yok.** `MatchResult.Events` bellekte tutuluyor; serialize/restore yok. |
| Uzatma üst sınırı | **Yok** (D78 açıktı). |
| Enjeksiyon noktası | `HandleAfterPossession` (satır 1336) her possession sonunda çağrılan **tek huni**. |

### Ölçülen teknik bulgular (bu oturumda koşturuldu)

Sıfır NuGet paketi kısıtı altında serileştirmenin çalışıp çalışmadığı **tahmin
edilmedi, ölçüldü**. Geçici bir prob dosyası yazıldı, koşturuldu ve silindi
(çalışma ağacında kalıntı yok, 222/222 test yeşil):

| Ölçüm | Sonuç | M5 için anlamı |
|---|---|---|
| `System.Text.Json`, `net10.0` sınıf kütüphanesinde **sıfır paketle** | **Çalışıyor** | `System.Text.Json` paketi eklemek gerekmiyor. Bağımlılık kısıtı bozulmuyor. |
| `ImmutableArray<T>` JSON round-trip | **Çalışıyor** | Durum belgesindeki risk 5'in bu yarısı kapanıyor. `byte[]` için hex converter gerekiyor. |
| `required` + `init` record round-trip | **Çalışıyor** | `PlayerMatchState`, `TeamMatchState` doğrudan serileştirilebilir. |
| **Aynı id + aynı roster içeren, AYRI örneklenmiş iki `Team`** | `Equals` = **False** | **`ImmutableArray<T>.Equals` referans eşitliğidir.** `record` üretici eşitliği bu yüzden bozuk. |
| `Player` (koleksiyon içermiyor) | `Equals` = **True** | Sorunun kaynağı kesin olarak `ImmutableArray<T>`. |
| `TeamMatchSetup` | `Equals` = **False** | `Team` → `ImmutableArray<Player>` zinciri. **`MatchSetup` ve `MatchState` de aynı şekilde bozuk.** |
| `Team` JSON round-trip | **bayt aynı**, ama `Equals` = **False** | Serileştirme doğru; **`==` karşılaştırması kullanılamaz.** |
| `IRandomSource.GetState`/`SetState` round-trip | **Aynı sonraki değer**, state = **8 bayt** | Simulation replay için RNG'de **sıfır yeni iş** gerekli. |
| Enum varsayılan JSON | **`3`** (sayısal) | Enum adı/ordering'i değişirse kayıtlı snapshot **sessizce** bozulur. |

**M5'in en önemli tasarım sonucu (D87):** T16 testi `Assert.Equal(state,
restored)` **yazamaz.** Bu ifade bugün **yanlış negatif** verir (state'ler
eşit olsa da `False` döner) ve `ImmutableArray` alanları aynı referansı
paylaşıyorsa **yanlış pozitif** verir. M5 bir **`MatchStateFingerprint`** yardımcısı
ekler ve tüm state karşılaştırmaları onu kullanır. `M2TestData.Fingerprint`
bugün yalnız event listesi için var; genel yüzeye taşınır.

Bu, `Assert.Equal` kullanılan mevcut testlerin sessizce zayıf olup olmadığının
ayrıca taranmasını gerektirir — M5 sırasında `grep` ile denetlenecek, sonuç
plana yazılacak.

## 3. Oluşturulacak dosyalar

```
src/DreamTeam.MatchEngine/
  Commands/
    ManagerCommand.cs          ManagerCommand, ManagerCommandKind, TimeoutKind
    ScheduledManagerCommand.cs komut + mantıksal sınır + kabul sırası
    CommandQueue.cs            kuyruk, FIFO, Expired sonlandırma
    CommandValidator.cs        sahiplik, yasallık, oyuncu uygunluğu, stale
    CommandResult.cs           CommandResult + CommandRejectionReason
  Rules/
    SubstitutionPolicy.cs      pencere + atomik lineup geçişi
    TimeoutPolicy.cs           bütçe, yasal pencere, son 2 dakika
    OvertimePolicy.cs          D79 üst sınırı
  Replay/
    MatchSnapshot.cs           tam durum + RNG state + bekleyen komutlar
    MatchStateFingerprint.cs   D87: durum karşılaştırma yüzeyi
    MatchSnapshotSerializer.cs System.Text.Json, sıfır paket
  Replay/Json/ImmutableArrayByteConverter.cs

tests/DreamTeam.MatchEngine.Tests/
  M5TestData.cs
  CommandEnvelopeTests.cs
  CommandValidationTests.cs
  ApplicationBoundaryTests.cs
  SubstitutionTests.cs
  TimeoutTests.cs
  OvertimeCapTests.cs
  SnapshotReplayTests.cs
  CommandDeterminismTests.cs
```

## 4. Komut zarfı ve sıralama modeli

03'ün hedef sözleşmesi `ScheduledManagerCommand` diyor. Neden "scheduled"?
Çünkü komut **mantıksal sınıra** bağlanır, duvar saatine değil:

```csharp
public readonly record struct ScheduledManagerCommand
{
    public required Guid CommandId { get; init; }        // idempotency (07 §5)
    public required TeamSide Side { get; init; }         // sahiplik
    public required ManagerCommandKind Kind { get; init; }
    public required long? ExpectedSequence { get; init; } // stale tespiti
    public required CommandPayload Payload { get; init; }
    public required long AcceptedOrder { get; init; }     // motorun atadığı sıra
    public required CommandBoundary TargetBoundary { get; init; }
}
```

`AcceptedOrder` **istemci tarafından belirlenemez** (07 §5). Motor, komutu
kabul ettiği sırayı atar. `TargetBoundary` şunlardan biri:

| Sınır | Nerede | Hangi komutlar |
|---|---|---|
| `ActionDecision` | `RunAction` içinde, `_offense.Select(...)` çağrısından **önce** | `ChangeOffense`, `ChangeDefense`, `ChangePace` |
| `DeadBall` | `HandleAfterPossession` içinde, `StartPossession`'dan **önce** | `Substitute`, `RequestTimeout` |
| `PeriodBreak` | `ClosePeriod`/`StartPeriod` arasında | `Substitute`, `RequestTimeout` |

**Neden yeni `MatchPhase.DeadBall` yazılmıyor.** D54 bu fazı bilinçli olarak
kalıcı yapmamıştı, çünkü event üretmeyen bir adım "ilerleme yok" güvenlik ağına
takılır. M5 komutları **aynı adımın içinde** bu sınırlarda boşaltıyor; faz
hâlâ yazılmıyor. Sonuç: D54'ün amacı korunur, yeni bir terminal-guard riski
doğmaz. Bu, plandaki en önemli mimari tercihtir.

## 5. Uygulama sınırları — komutun "ne zaman etki eder" sözleşmesi

07 §6'nın dört maddesi birer kural:

1. **Taktik/tempo bir sonraki aksiyon karar sınırında uygulanır.** Çözülmeye
   başlamış şutu geriye dönük değiştirmez. → `ActionDecision` sınırı.
2. **Substitution yalnız legal dead-ball penceresinde.** → `DeadBall`/`PeriodBreak`.
3. **Timeout istenen anda kurala uygunsa uygulanır; değilse queue/expire/reject
   açık olmalı.** → kuyruk + sebep kodu.
4. **Maç bittiğinde bekleyen komutlar Expired/Rejected.** → `ClosePeriod` ve
   terminal yollarda kuyruk boşaltılır, her biri `CommandRejected(Expired)`
   event'iyle kapanır.

**Bilinen sapma (D84):** NBA'da timeout son iki dakikada canlı top anında
çağrılabilir. M5'te timeout **yalnız dead-ball sınırında** uygulanır. Canlı
topu kesmek mevcut possession state machine'ini bölüp yeni bir
`PossessionEndReason.Timeout` ve possession sayacı kayması demektir; bu M5
kapsamı değil. 06 §23 zaten "NBA profili etiketi bu ayrıntıları içermez"
diyor. Sapma planın kabul kriterlerinde açıkça yazılır.

## 6. Substitution kuralları (D82, D85)

**Yasal pencere — hangi anlar dead-ball:**

| An | Pencere | Gerekçe |
|---|---|---|
| Hücum faulü / ihlal (turnover, hücum saati) | **Evet** | 06 §4 tablosu: düdük, devir |
| Hücum değişimi (isabetli basket sonrası devir) | **Evet** | 06 §4: düdük, devir |
| FT serisi sonrası | **Evet** | 06 §4: "serbest atışlar tamamlandı → dead ball" |
| Defansif ribaund sonrası | **HAYIR** | 06 §7: "DREB/steal sonrası oyun canlıdır" |
| Periyot arası | **Evet** | Tanım gereği |
| Normal basket (isabetli) sonrası | **HAYIR** | **D82** — 06 §7'nin varsayılanı, M5'te açıkça seçildi |

**Atomiklik (07 §3):** substitution "eski beşten yeni beşe **tek atomik
geçiş**"tir. Ara durumda sahada 4 veya 6 kişi olmaz. Uygulama tek `with`
ifadesiyle iki `OnCourt` dilimini birden değiştirir ve `PlayerStates` dizisini
**aynı sırada** korur (04 §11 sıra kanoniktir).

**Kabul anında ve uygulama anında iki kez doğrulama (06 §7).** Uygulama
sınırında yeniden doğrulanır çünkü araya başka bir komut girebilir:

- Gelen oyuncu kadroda mı, sahada mı, foul-out mu?
- Çıkan oyuncu şu an sahada mı, foul-out mu?
- Sonuç beş yasal oyuncu mu? (aksi halde `Aborted` — D43, mevcut yol)
- Aynı oyuncu iki istekle sahaya girmiş mi? → reddedilir

**Reddedilme sebebi kodları:** `PlayerNotInRoster`, `PlayerAlreadyOnCourt`,
`PlayerFouledOut`, `OutgoingNotOnCourt`, `NotADeadBallWindow`,
`MatchAlreadyTerminal`, `StaleSequence`, `DuplicateCommand`,
`TimeoutBudgetExhausted`, `Expired`, `WrongSide`.

## 7. Timeout kuralları (D83, D84)

`RulesProfile` alanları:

| Alan | Değer | Kaynak |
|---|---|---|
| `FullTimeoutsPerTeam` | 4 | Kullanıcı kararı |
| `FullTimeoutsInFinalTwoMinutes` | 2 | Kullanıcı kararı |
| `ShortTimeoutsPerTeam` | 3 | **KAYNAK YOK — yer tutucu, onay bekliyor** |
| `OvertimeTimeoutBonus` | 1 | 06 §18 ("OT'ye +1") |
| `FinalTwoMinutesMs` | `PeriodDurationMs` türetilir (720 000) | Türetilmiş, ayrı alan değil |

**Bütçe kuralı.** Dördüncü ve sonraki tam timeout, düzenleme periyodunun son
2 dakikasında kullanılabilir. İlk ikisi herhangi bir dead-ball'da. Uzatma her
birine +1 verir (06 §18) ve sayaç **kümülatif** kalır.

**İki kanal etkisi:**
- Canlı saati **tüketmez** (06 §27: "substitution ve dead-ball işlemleri canlı
  game clock tüketmez").
- **Hücum saatini başlatmaz** (06 §6 reset tablosu: "Timeout, aynı hücum devam
  — kendi başına reset sebebi değildir"). Yani timeout sonrası aynı hücum
  kalan süresiyle devam eder. Bu, M3'ün `ClockResetPolicy`'sine **hiçbir şey
  eklemez** — mevcut tablo zaten doğru.

**20 saniyelik timeout (D83):** `TimeoutKind.Short20` sayaçtan düşer ve aynı
pencerede uygulanır. 20 saniyenin duvar saati süresi motorda **yoktur** —
motor duvar saati tutmaz (03 §"Motor `DateTime.Now`, sleep ... beklemez").

## 8. Uzatma üst sınırı (D79)

```csharp
// RulesProfile
public required int MaxOvertimePeriods { get; init; }  // 2
```

`ClosePeriod` içinde: `PeriodController.ShouldStartOvertime` true döndüğünde
`period - PeriodCount > MaxOvertimePeriods` ise maç `Aborted` olur. Sebep
`OvertimeLimitReached`. **Skor eşitliği korunur ama `IsTie = false`'tır**
(06 §8: yarım kalan maç beraberlik sayılmaz, skor geçersizdir) — M3'teki mevcut
`DegenerateZeroScoreMatchIsAbortedByTheGuard` testiyle aynı gerekçe.

`ComputeConfigHash`'e yazılır, böylece farklı sınırlı config'ler
`ConfigHash`'ten ayrışır.

## 9. Snapshot, replay ve serileştirme

İki replay türü 07 §7'de ayrıdır; ikisi de M5 kapsamında **motor tarafı**:

| Tür | Girdi | M5'te |
|---|---|---|
| **Event replay** | Saklanan event'ler + başlangıç snapshot'ı | Motor tarafı yok; `MatchResult.Events` zaten taşıyor. Tüketici M8. |
| **Simulation replay** | Setup/config/versions + **RNG state** + komut kaydı | **M5'in konusu (T16).** `MatchSnapshot` üretir. |

`MatchSnapshot` içeriği — **her alan gerçekten gerekli mi, tek tek:**

| Alan | Gerekli mi | Gerekçe |
|---|---|---|
| `MatchState` tamamı | Evet | `Create` + `Advance` zincirini kaldığı yerden sürdürmek için |
| RNG state (8 bayt) | Evet | Ölçüldü: `GetState`/`SetState` round-trip kesin |
| `PendingCommands` | Evet | Kuyruk state'in parçası; restore olmazsa komutlar kaybolur |
| `NextCommandId` | Evet | Sonraki `AcceptedOrder`'ı üretmek için |
| `Setup`, `Config` | Evet | `Config` zaten `MatchState.Config`'te |
| `ConfigHash`, `EngineVersion`, `EventSchemaVersion` | Evet | 07 §7 "sürüm uyumluluğu gerektirir" |

**Enum serileştirmesi (ölçüm H):** varsayılan **sayısal**. 07 §1 "JSON'da
Guid/string ve long serileştirme seçimi frontend ile birlikte kilitlenir" diyor.
M5'in seçimi: `JsonStringEnumConverter` ile **isim tabanlı**. Gerekçe: enum
ordering'i bir kez bile değişse kayıtlı snapshot sessizce bozulur; isim tabanlı
serileştirmede derleme hatası değil ama **reddedilebilir** bir hata verir.
`ImmutableArray<byte>` için hex converter gerekiyor (ölçüm B: ham `byte[]`
dizi olarak çıkar, RNG state'i base64/hex okunabilir olmalı).

**T16 testi nasıl yazılacak (D87):** `Assert.Equal(state, restored)` **yazılamaz**
(ölçüm C/F). Yerine:

```
1) snapshot(state) -> JSON -> deserialize -> MatchState
2) fingerprint(state) == fingerprint(restored)        // alan alan
3) 40 adım ilerlet: fingerprint(yol1) == fingerprint(yol2)
4) kesintisiz koşunun 40. adımı == restore edilenin 40. adımı (event fingerprint)
```

## 10. Komutların determinizmi

Sözleşme: **aynı `MatchSetup` + aynı seed + aynı komut listesi = bit düzeyinde
aynı maç.** Bunun sağlanması için:

1. Komutlar RNG tüketmez (D87'in parçası; §0'da not edildi).
2. `AcceptedOrder` motor tarafından, komutun **kabul edildiği sıra numarasıyla**
   atanır — kuyruğa giriş sırası, listedeki indeks değil.
3. Kuyruk FIFO ve `OrderBy(AcceptedOrder)` ile boşaltılır; `ImmutableDictionary`
   veya `HashSet` yineleme sırasına **bağlı olmaz**.
4. Aynı `CommandId` ikinci kez gelirse **etki tekrarlanmaz**; aynı sonuç
   döner (07 §5, T14).

**Beklenen risk:** `CommandValidator` içinde `expectedSequence` kontrolü, replay
sırasında **aynı sonucu** vermelidir. Replay komutları aynı `ExpectedSequence`
değerleriyle saklandığı için sağlanır; ama bu ayrı bir test konusudur.

## 11. Test senaryoları (hedef ~45–50; kesin sayı uygulamada belirlenir)

| # | Test | Hedef |
|---|---|---|
| 1 | `CommandsConsumeNoRng` | Geçerli ve geçersiz komut sonrası RNG state'i **aynen aynı**. |
| 2 | `DuplicateCommandIdHasNoSecondEffect` | T14: aynı `CommandId` iki kez → bir etki, iki `CommandApplied` yok (ikincisi `DuplicateCommand`). |
| 3 | `StaleSequenceIsRejected` | T14: `ExpectedSequence` geçmişe aitse reddedilir. |
| 4 | `CommandAtActionBoundaryDoesNotAlterAResolvedShot` | Taktik değişimi çözülmüş şutu geriye almaz. |
| 5 | `TacticChangeAppliesAtNextActionBoundary` | Komut bir sonraki aksiyonda görünür. |
| 6 | `PaceChangeAppliesAtNextActionBoundary` | Tempo bir sonraki aksiyonun süresini değiştirir. |
| 7 | `TacticTakesEffectWithoutResettingTheShotClock` | 06 §6 |
| 8 | `TwoPendingTacticCommandsApplyInAcceptedOrder` | D86: FIFO, last-write-wins. |
| 9 | `SubstitutionIsAtomicFiveToFive` | 07 §3: ara durumda 4/6 olmaz. |
| 10 | `SubstitutionIsRejectedOutsideADeadBallWindow` | T13 |
| 11 | `SubstitutionIsRejectedForAFouledOutPlayer` | T13 |
| 12 | `SubstitutionRejectsIncomingPlayerAlreadyOnCourt` | T13 |
| 13 | `SubstitutionRejectsOutgoingPlayerNotOnCourt` | T13 |
| 14 | `TwoSimultaneousSubstitutionsDoNotCorruptTheLineup` | 09 M5 kabul maddesi; ikinci ilk uygulanmış lineup'e karşı doğrulanır. |
| 15 | `SubstitutionAfterAMadeBasketIsNotLegal` | **D82'nin doğrudan testi.** |
| 16 | `SubstitutionAfterDefensiveReboundIsNotLegal` | 06 §7 "DREB sonrası canlı". |
| 17 | `SubstitutionIsLegalAfterATurnover` | Pencere açık. |
| 18 | `SubstitutionIsLegalAtPeriodBreak` | Pencere açık. |
| 19 | `FullTimeoutBudgetIsFour` | D84 |
| 20 | `LastTwoFullTimeoutsRequireTheFinalTwoMinutes` | D84 |
| 21 | `TimeoutDoesNotConsumeLiveClock` | 06 §27 |
| 22 | `TimeoutDoesNotResetTheShotClock` | 06 §6 |
| 23 | `TimeoutAddsOnePerOvertime` | 06 §18 |
| 24 | `ShortTimeoutCountsAgainstTheShortBudget` | D83 |
| 25 | `TimeoutIsRejectedWhenTheBudgetIsExhausted` | `TimeoutBudgetExhausted` |
| 26 | `OvertimeStopsAtTheConfiguredLimit` | D79 |
| 27 | `OvertimeLimitAbortDoesNotFalsifyATie` | `IsTie == false`, `MatchEnded` yok. |
| 28 | `MaxOvertimePeriodsChangesTheConfigHash` | D79 hash'e giriyor. |
| 29 | `SnapshotRoundTripIsByteIdentical` | Ölçüm B/E'nin gerçek state için tekrarı. |
| 30 | `SnapshotPreservesRngState` | Ölçüm G'nin gerçek state için tekrarı. |
| 31 | `RestoreContinuesIdenticallyFor40Steps` | **T16** |
| 32 | `RestoreProducesTheSameEventStream` | **T16** |
| 33 | `StateFingerprintDetectsASingleEnergyChange` | D87'nin yardımcısı gerçekten ayırt edici. |
| 34 | `StateFingerprintIgnoresReferenceIdentityOnly` | Aynı içerik, farklı referans → aynı fingerprint. |
| 35 | `SameCommandsProduceTheSameMatch` | 09 kabul maddesi. |
| 36 | `DifferentCommandOrderProducesADifferentMatch` | Sıralamanın gerçekten etkili olduğu. |
| 37 | `PendingCommandsSurviveASnapshotRestore` | Kuyruk kaybolmaz. |
| 38 | `PendingCommandsAreExpiredWhenTheMatchEnds` | 07 §6 madde 4. |
| 39 | `ExpiredCommandEmitsCommandRejected` | Sebep kodu açık. |
| 40 | `EnumSerializationIsNameBased` | Ölçüm H'nin kararı. |
| 41 | `CommandRejectionDoesNotAbortTheMatch` | 07 §5: reddetme maçı bitirmez. |
| 42 | `CommandAppliedCarriesBeforeAndAfterValues` | 07 §2 "önce/sonra değerleri". |
| 43 | `EventSchemaVersionIsFour` | Sözleşme sayısı. |
| 44 | `MatchEventTypeCountIsTwentyFour` | 18 + 6. |
| 45 | `NoAssembleEqualityAssertionsOnStateLikeTypes` | Mevcut testlerde `Assert.Equal` ile state/team karşılaştırması taraması; bulunan varsa düzeltilir. |

**T15 (diagnostics) bu milestone'da yazılmayacak.** Tanımı T15'i uygulanabilir
kılmak için bir diagnostics yüzeyi gerekir; bu yeni bir kapsam ve 05 §3'te
yasaklanan "etkisiz mekanizmayı gizleme" riski taşır. **Sahte "geçti" konmayacak**;
T15 M6'da. M5 yalnız not düşer.

## 12. Kabul kriterleri

| Kriter | Nasıl doğrulanır | Sonuç |
|---|---|---|
| Temiz build | `dotnet build DreamTeam.slnx -c Release --no-incremental` | (uygulamada) |
| Tüm testler | `dotnet test -c Release` ve `-c Debug` | (uygulamada) |
| Motor saf C# | `dotnet list package` → MatchEngine'da sıfır paket | Ölçüldü: `System.Text.Json` **paket gerektirmiyor**; yeniden doğrulanacak |
| T13–T16 | Yukarıdaki 45 senaryo | (uygulamada) |
| Determinism | Aynı komut listesi → SHA256 aynı | (uygulamada) |
| Komutlar RNG tüketmez | `CommandsConsumeNoRng` | (uygulamada) |
| Snapshot → aynı devam | T16, fingerprint ile | (uygulamada) |
| Sıfır sahte işaret | T15 yazılmadı, açıkça not düşüldü | — |
| Karar günlüğü | D79–D87 eklendi | Bu oturumda |

## 13. Bilinçli olarak yapılmayacaklar

- **API, SignalR, auth, DB, React, PixiJS.** M7/M8/M9.
- **Reconnect ve mesaj sırası** (07 §8). M7.
- **Steal atfedimi** (D66). M5 adayıydı ama 07 §2'de ayrı event; M5'in command
  yüzeyiyle karışır. M6'ya bırakıldı — karar §14'te kayıtlı.
- **Transition aksiyonu, mismatch, takım ribaundu** (D66).
- **Pozisyon cezası / mismatch** (D35) — konum modeli yok.
- **Ball out of bounds, jump ball, technical/flagrant** (06 §1).
- **Wall-clock replay** (07 §6: "Maçın 18. gerçek saniyesi gibi komut, tek
  başına replay girdisi değildir").
- **Canlı top anında timeout kesme** — §5'teki sapma, bilinçli.
- **Denge kalibrasyonu.** M6.

## 14. Riskler

| # | Risk | Etki | Azaltma |
|---|---|---|---|
| 1 | **Substitution M5'te olmasa bile maçlar kimse tarafından yönetilemez**; AI fallback (D81) motoru yönetir | Düşük | Açık ve kabul edilmiş. |
| 2 | **Record eşitliği bozuk** (ölçüm C/F). M7'de `Match completion idempotent` (03) kıyaslama gerektirecek | Yüksek | M5'te `MatchStateFingerprint` ile aşıldı. **M7'de kalıcı katmanda çözülmeli.** |
| 3 | Timeout'un canlı topta uygulanmaması (D84 sapması) üründe hissedilir | Orta | Bilinçli; 06 §23'e dayanıyor. Kullanıcı bilgilendirildi. |
| 4 | `ShortTimeoutsPerTeam = 3` **kaynaksız** | Düşük | ConfigHash'e giriyor, kolay değişir. Onay bekliyor. |
| 5 | `HandleAfterPossession` tek huni olarak kalır; ileride yeni bir dead-ball yolu açılırsa bu nokta kaçırılabilir | Orta | `CommandQueue.DrainAt(...)` tek giriş; testler her pencereyi açıkça sayar. |
| 6 | Snapshot boyutu: `MatchState` tüm kadroyu taşıyor; 100K deneyde snapshot maliyeti ölçülmedi | Orta | M6'da ölçülür. |
| 7 | `ImmutableArray<byte>` JSON'da ham dizi olarak çıkıyor (ölçüm B) | Düşük | Hex converter planlandı. |
| 8 | M5 sonrası engine sürümü değişir; eski simulation replay geri hesaplanamaz | Orta | 07 §7 bunu zaten söylüyor; snapshot `EngineVersion` taşır, uyumsuzluk açık hata verir. |
| 9 | CI hâlâ yok | Orta | Doğrulama yalnız yerel. |

## 15. Sonraki milestone bağlantısı

**M6** (CLI batch + 10K/100K deney + denge adayı) M5'in üstüne inşa olur:
komut listesiyle toplu koşu, ölçüm raporu, katsayı kalibrasyonu. T15
(diagnostics) ve Q10 (GameForm dağılımı/birimi) M6'da çözülür.

**M7** (API/SignalR) M5'in komut sözleşmesini tüketir: `ExpectedSequence`,
`AuthenticatedUserId`'nin bağlamdan çözülmesi, reconnect. Risk 2 (record
eşitliği) M7'de çözülmeli.
