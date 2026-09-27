# Doğrulama, Monte Carlo ve denge planı

Bu belge çalıştırılmış test raporu değildir. Mevcut durum: kod ve test yürütmesi yok. Hedef: hem kural doğruluğu hem dağılım davranışını kanıtlamak.

## 1. Test katmanları

| Katman | Kanıtladığı şey | Örnek |
|---|---|---|
| Saf fonksiyon testleri | Formül/sınır doğruluğu | Normalize ağırlık, sigmoid, enerji eğrisi |
| Senaryo testleri | Kural geçişi | And-one, son FT, clock expiry |
| Invariant/property testleri | Her geçerli maçta korunan yapı | Skor toplamı, geçerli lineup |
| Determinism testleri | Aynı girdinin tekrar üretimi | Seed + config + commands |
| Integration/CLI | Gerçek uçtan uca veri akışı | Fixture → maç → JSON/CSV |
| Monte Carlo | İstatistiksel denge ve etki yönü | Simetri ve taktik/roster etkisi |
| Ürün playtest | Kararların anlaşılması/eğlence | Rotasyon ve scout döngüsü |

Faul/clock gibi dallarda yalnız şanslı seed aramak yerine kontrollü RNG test double veya resolver fixture kullan. Test double production RNG algoritmasının doğruluğuna kanıt değildir; ayrıca golden sequence testi gerekir.

## 2. Zorunlu invariant'lar

- Skor = sayılabilir 2PT isabet × 2 + 3PT isabet × 3 + FT isabet.
- Takım ve oyuncu toplamları tutarlı; takım attribution'ları ayrı gösterilir.
- 3PM ≤ 3PA ≤ FGA, FGM ≤ FGA, 3PM ≤ FGM, FTM ≤ FTA.
- REB = OREB + DREB; team rebound oyuncuya rastgele dağıtılmaz.
- Rakip steal toplamı ilgili turnover sayısını geçmez; her steal ilişkili turnover'a bağlıdır.
- Oyuncu aynı anda tek lineup slotunda bulunur; sahada her takımın beş uygun oyuncusu vardır veya açık terminal policy uygulanır.
- Energy [0,100], oyun süresi ve faul sayıları negatif değildir; NaN/Infinity state'e girmez.
- Toplam oyuncu canlı süreleri takım başına 5 × takımın oynadığı süreye eşittir. OT dahil, dead-ball süreleri hariç.
- Bir shot/FT/turnover outcome iki kez muhasebeleştirilmez.
- Sequence tekil ve sıralı; eventler doğru match'e aittir.
- Her state transition legal; guard aşımı Completed sayılmaz.
- Maç başlangıç snapshot'ı sonradan değişen roster/config ile değişmez.

## 3. Senaryo matrisi ve milestone eşlemesi

| Test kimliği | Senaryo / beklenen | Aşama |
|---|---|---|
| T01 | Aynı seed/config/setup/commands → aynı canonical event stream | M1/M2/M5 |
| T02 | Input listesi sırası değişse de canonical ordering davranışı korunur | M1 |
| T03 | OVR değişimi motor outcome'larını değiştirmez | M1/M2 |
| T04 | Normal basket/miss/block tek FGA; doğru skor | M2/M3 |
| T05 | OREB aynı possession; DREB yeni possession | M2 |
| T06 | Rim hit OREB reset; airball OREB otomatik reset yapmaz | M3 |
| T07 | And-one; kaçan shooting foul; FT arası miss; canlı son FT | M3 |
| T08 | Bonus öncesi/sonrası, offensive foul, foul-out ve replacements | M3 |
| T09 | Son anda release, shot-clock sınırı, horn sonrası sonuç | M3 |
| T10 | Q4 eşitlik → OT; çoklu OT; guard → Aborted | M3 |
| T11 | Beşten az uygun oyuncu açık terminal policy'ye gider | M3 |
| T12 | Enerji drain/recovery sınırları ve dakika muhasebesi | M4 |
| T13 | Legal olmayan substitution ertelenir/reddedilir; geçerli anda uygulanır | M5 |
| T14 | Duplicate/stale/expired command, aynı anda iki substitution | M5 |
| T15 | Diagnostics açık/kapalı → aynı domain outcome | M2/M6 |
| T16 | Snapshot serialize/restore → kesintisiz koşuyla aynı devam | M5/M6 |
| T17 | Paralel ve sıralı batch → maç başına aynı sonuç | M6 |
| T18 | CSV/JSON summary ile event-derived sonuç tutarlı | M6 |
| T19 | API yetkisiz takım komutunu reddeder; reconnect sırası doğru | M7 |
| T20 | Aynı maç ödülü ve scout teklif tüketimi bir defa gerçekleşir | M10 |

Geçerli farklı seed'lerin her durumda farklı final skor üretmesi zorunlu test değildir; aynı skor olasılık dahilindedir. RNG akışının farklı seed'lerde farklılığı ve dağılım davranışı test edilir.

## 4. Determinism manifest'i

Her deneyde fixture/hash, config/hash, rules/engine/RNG sürümü, seed seti, command policy, runtime/OS ve çalıştırma komutu kaydedilir. JSON canonicalization, kimlikler ve duvar saati alanları tanımlanmadan byte equality bekleme.

Başlangıç garantisi aynı kilitli runtime/platform olabilir. Cross-platform sonuç eşitliği ayrıca koşulup raporlanır. Debug modu RNG çağrı sayısını değiştiremez. Paralel maçlar shared RNG kullanmaz; maç seed'leri stable index'ten belirlenir.

## 5. Monte Carlo tasarımı

Aşamalı ölçek: küçük deterministic smoke corpus → 1.000 geliştirme deneyi → 10.000 kalibrasyon → 100.000 son aday doğrulaması. Her commit'te 100K gerekmiyor. Tam corpus ancak ilgili gate veya somut risk varsa tekrar çalıştırılır.

Temel aileler:

| Deney | Kontrol edilen değişken | Beklenti |
|---|---|---|
| Mirror neutral | Aynı rating/tactic/energy, home advantage kapalı | Taraf/başlangıç bias'ı görünmemeli |
| Quality gap | Diğer her şey sabit, beceri düzeyi farklı | Güçlü takım büyük örneklemde avantajlı |
| Roster fit | Shooter roster vs inside roster | Taktik etkisi kadroya bağlı olmalı |
| Pace | Tek değişken Slow/Normal/Fast | Possession, TOV ve fatigue ilişkisi ölçülür |
| Rotation | Aynı roster, farklı rotasyon | Yorgunluk ve bench değeri gözlenebilir |
| Defense matrix | Dört hücum × dört savunma | Her durumda kazanan tek taktik oluşmamalı |
| Form sensitivity | Form kapalı/açık | Variance etkisi görünür ve sınırlı olmalı |
| Extremes | 0/100 rating, düşük bench, uzun OT | NaN, sonsuz döngü, yapısal bozulma yok |

Home/away yer değiştirmeli eşleştirme kullan. Aynı seed'i farklı policy'lerde kullanmak yararlı olabilir; fakat RNG dal sayısı değişirse bire bir aynı şans akışı garanti etmez. Sonuçları paired deney varsayımına göre yorumla.

## 6. Raporlanacak metrikler

Win/loss, aborted/forfeit oranı, average points, score variance, possession sayısı, pace, FG/3PT/FT attempts ve yüzde, shot type dağılımı, OREB%, turnover/possession, foul/possession, OT sıklığı, oyuncu dakika/enerji dağılımı.

Tanımlar:

- FGP = toplam FGM / toplam FGA; sıfır denominator için null. Maç yüzdelerinin basit ortalamasıyla karıştırma.
- OREB% = OREB / (OREB + rakip DREB), raporda hangi canlı rebound fırsatlarının dahil olduğu belirtilir.
- TOV% = turnover / tamamlanmış possession, bu projedeki operasyonel possession tanımıyla. Analitik tahmin formülünü event sayacı yerine koyma.
- Pace48 = 48 × ortalama takım possession'ı / oynanan dakika; OT süreleri normalize edilir.
- ORtg = 100 × points / tanımlı possession. Periyot sonunda yarım kalan hücumların dahil edilmesi tutarlı olmalı.

Win-rate yanında güven aralığı (ör. Wilson %95), örneklem büyüklüğü ve seed seti bulunmalı. p≈0.5 ve bağımsız 100.000 maç için normal yaklaşımın hata payı yaklaşık ±0,31 yüzde puandır; bu yalnız hesaplama örneğidir. Tek %95 aralığın 0,5'i dışlaması tek başına kesin bug kanıtı değildir. Önceden belirlenmiş tolerans, farklı seed seti ve bias analizi kullan.

## 7. Önceki denge hedeflerinin doğru yorumu

- Eşit takımlarda 50/50 yönü: saha avantajı kapalı, başlangıç avantajı dengelenmiş, yeterli örneklem altında.
- “63/37 ise bozuk” sözü inceleme alarmıdır; fixture farkı/başlangıç protokolü/örneklem kontrol edilmeden kesin teşhis değildir.
- Taktik için %3–8 verimlilik değişimi ve örnek 56/44 win-rate, hipotezdir. Birinden diğerine sabit dönüşüm yoktur.
- Şut hedefleri engine spec'tedir; seçilmemiş NBA sezonunun gerçek ortalaması gibi sunulmaz.
- Güçlü kadro avantajlı olmalı; “şu rating farkı tam şu win-rate verir” henüz belirlenmedi.

## 8. Kalibrasyon protokolü

1. Kural/invariant hatalarını sıfırla; bunları balance katsayısıyla gizleme.
2. Neutral fixture ile tempo ve shot mix'i izle.
3. Temel şut/turnover/foul/rebound parametre ailelerini sırayla ayarla; aynı anda her şeyi değiştirme.
4. Stamina ve rotation ekleyip marjinal etkiyi ölç.
5. Tactic/roster eşleşmelerinde dominant strateji araması yap.
6. Form ve late-game policy'yi ekle.
7. Tuning'de kullanılmamış seed/fixture holdout'u ile aday sürümü doğrula.
8. İnsan playtest'i ile sayısal hedefin eğlenceye dönüp dönmediğini değerlendir.

Her değişimde önce/sonra config hash, gerekçe ve metrik farkları kaydedilir. 100K koşu için eventleri bellekte biriktirmek zorunlu olmamalı; summary mode ve seçili maç ayrıntısı yeterli olabilir. Donanım, çalışma süresi, throughput ve peak memory gerçek ölçümle raporlanır; şimdiden bir performans sayısı vaat edilmez.
