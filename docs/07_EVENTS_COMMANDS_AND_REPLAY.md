# Event, command, live akış ve replay sözleşmesi

Önceki yön: server authoritative motor event üretir; React/PixiJS bu akışı gösterir. Bu belgedeki sürümleme, tipli payload ve sequencing ayrıntıları devir önerisidir.

## 1. Event zarfı

Önceki `object? Data` örneği taslak içindi. Kalıcı sözleşmede event türüne göre doğrulanan payload önerilir. Farklı eventlerde aynı property'nin anlamını değiştirme.

| Alan | Anlam |
|---|---|
| MatchId | Maç kimliği |
| Sequence | Maç içinde artan, tekil long; başlangıç tercihi 1 |
| SchemaVersion | Event sözleşmesinin sürümü |
| EngineVersion / ConfigHash | Metadata'da bir defa veya envelope ilişkisinde |
| Period | 1–4 normal, 5+ uzatma |
| GameClockMs / ElapsedGameTimeMs | Simülasyon zamanı |
| PossessionId / ActionId | İlgili domain bağlamı; uygun eventlerde nullable |
| TeamId / PlayerId / SecondaryPlayerId | Atfedilen aktörler |
| Type | Event discriminator |
| Payload | Türüne göre tanımlı veri |

Eventler aynı GameClockMs'de oluşabilir; sıralama Sequence'tır. Diagnostic roll ve gizli state public payload'a girmez. JSON'da Guid/string ve long serileştirme seçimi frontend ile birlikte kilitlenir.

## 2. Event ailesi

| Aile | Olaylar | Temel payload ihtiyacı |
|---|---|---|
| Lifecycle | MatchStarted, PeriodStarted, PeriodEnded, MatchEnded, MatchAborted | Profil/süre veya sonuç ve sebep |
| Possession | PossessionStarted, PossessionEnded | Kontrol, başlangıç/bitiş sebebi |
| Aksiyon | Pass, Drive, PostUp, Screen | Aktör, hedef, actionId; sunum ipuçları |
| Şut | ShotAttempt, ShotMade, ShotMissed, Block | ShotId, ShotType, Points, CountsAsFGA |
| Top kaybı | Turnover, Steal | TurnoverId, Kind, kaybeden ve stealer |
| Ribaund | Rebound, TeamRebound, BallOutOfBounds | ShotId, offense/defense, kontrol |
| Faul/FT | Foul, FreeThrowAttempt, FreeThrowMade, FreeThrowMissed | FoulId, FTSeriesId, index/total, canlı son atış |
| Müdahale | Timeout, Substitution, TacticChanged, PaceChanged | CommandId, önce/sonra değerleri |
| Komut sonucu | CommandApplied, CommandRejected | Sebep kodu ve ilgili state sequence |

Önceki listede olmayan yeni lifecycle/id alanları korelasyon için önerildi. Her görsel ayrıntıyı domain event haline getirme; Screen gibi sunum olayları gerekiyorsa eklenir. Maç istatistiklerini değiştirmeyen kozmetik animasyonlar client'ta kalabilir.

## 3. Çifte istatistik riskini önleme

- ShotAttempt şutun kimliğini açar; FGA sayılabilirliği foul resolution ile kesinleşir. Box score projector tek canonical settlement üzerinden yazar.
- ShotMade Points'i bir kez skora işler; ShotAttempt ayrıca sayı yazmaz.
- Block aynı ShotId'nin niteliğidir; yeni miss/FGA üretmez.
- Steal, TurnoverId'ye bağlı savunma attribution'ıdır; ikinci turnover yazmaz.
- Substitution eski beşten yeni beşe tek atomik geçiştir.
- Aynı Sequence ikinci defa gelirse client/store/projector tekrar uygulamaz.

Devir önerisi: motorun anlık skorunu güncelleyen tek reducer ve box-score projector'ın hangi eventleri tükettiği açık bir tabloyla M2'de sabitlensin. Tam event-sourcing framework'ü zorunlu değil.

## 4. Örnek event — yalnız sözleşme taslağı

```json
{
  "matchId": "22222222-2222-2222-2222-222222222222",
  "sequence": 342,
  "schemaVersion": 1,
  "period": 3,
  "gameClockMs": 412400,
  "elapsedGameTimeMs": 1747600,
  "possessionId": 88,
  "actionId": 164,
  "teamId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "playerId": "11111111-1111-1111-1111-111111111119",
  "type": "ShotMade",
  "payload": {
    "shotId": 64,
    "shotType": "ThreePoint",
    "points": 3,
    "countsAsFieldGoalAttempt": true,
    "assistPlayerId": "11111111-1111-1111-1111-111111111117"
  }
}
```

Bu örnek isabetli şut settlement'ıdır. Release zamanı ve fiziksel başlangıç ayrı ShotAttempt'te bulunur. Ekran metni bu payload'dan yerelleştirilerek üretilir; motor Türkçe cümle üretmek zorunda değildir.

## 5. Manager command zarfı

| Alan | Anlam |
|---|---|
| CommandId | Tekrar gönderimlerde değişmeyen idempotency kimliği |
| MatchId / TeamId | Hedef ve sahiplik bağlamı |
| ExpectedSequence | İsteğin hangi görülen state'e göre yapıldığı |
| Type / Payload | ChangeOffense, ChangeDefense, ChangePace, Substitute, RequestTimeout |
| AcceptedOrder | Sunucunun atadığı deterministik sıra; client belirleyemez |
| ApplyBoundary | Uygulanan mantıksal motor sınırı; replay için kaydedilir |

AuthenticatedUserId bağlantı/session'dan çözülür; client'ın “ben şu takımım” beyanına güvenilmez. Client backdate yapıp geçmiş possession'ı değiştiremez.

Validation: sahiplik, maç lifecycle, oyuncu uygunluğu, izinli tactic/pace, timeout hakkı, stale-state politikası, rate limit ve payload schema. Invalid command RNG tüketmemeli; reddetme sebebi kaydedilmeli.

ACK = alındı/kuyruğa girdi. Applied = state'e işlendi. Bu ikisini tek başarı mesajında karıştırma. Aynı CommandId yeniden gelirse aynı sonuç döndürülür.

## 6. Uygulama sınırları

Devir önerisi:

- Taktik/tempo bir sonraki aksiyon karar sınırında uygulanır; çözülmeye başlamış şutu geriye dönük değiştirmez.
- Substitution yalnız legal dead-ball penceresinde uygulanır.
- Timeout istenen anda kurala uygunsa uygulanır; değilse queue/expire/reject policy açık olmalı.
- Birden fazla bekleyen aynı tip tactic komutunda FIFO veya last-write-wins seçilir ve replay kaydına yansır; sessizce keyfi seçme.
- Maç bittiğinde bekleyen komutlar Expired/Rejected olarak sonuçlanır.

Offline scripted commands aynı mantıksal sınırları kullanır. “Maçın 18. gerçek saniyesi” gibi duvar saati tabanlı komut, tek başına replay girdisi değildir.

## 7. İki replay türü

| Tür | Girdi | Amaç |
|---|---|---|
| Event replay | Saklanan sıralı eventler + başlangıç snapshot'ı | Maçı tekrar izleme; RNG gerektirmez |
| Simulation replay | Setup/config/versions + RNG + kabul/uygulama sırası kaydı | Hata üretme ve motor doğrulama |

Sunum RNG'si oyun RNG'sinden ayrıdır. Aynı domain eventlerin farklı estetik animasyonlarla gösterilmesi maç sonucunu değiştirmez. Engine upgrade sonrası eski simülasyonun yeniden hesaplanması sürüm uyumluluğu gerektirir.

## 8. Reconnect ve mesaj sırası — M7+

Client son uygulanan Sequence'ı bildirir. Sunucu o noktadan sonraki eventleri veya belirli Sequence'a ait snapshot + devam eventlerini döndürür. Snapshot ile eventlerin örtüşme/boşluk sınırı atomik tanımlanır.

Client duplicate'i atar, boşlukta tamponlar ve eksik aralığı ister. Daha yeni event gelmesi eski state üzerine körlemesine uygulanmaz. Bağlantı kopması match runner'ı veya ödül kaydını durdurmaz; kullanıcı kontrolü için AI fallback politikası ürün kararına bağlıdır.

## 9. Sunum sözleşmesi sınırı

İlk eventler skor, şut türü, aktörler ve sıralamayı anlatır. Tam saha koordinatı/fizik yoktur. PixiJS animasyonu temsili olmalı; veride olmayan gerçek hareket rotasının motor tarafından hesaplandığını ima etmemeli. Ayrıntılı koordinat istenirse ayrı versioned presentation contract geliştirilir.
