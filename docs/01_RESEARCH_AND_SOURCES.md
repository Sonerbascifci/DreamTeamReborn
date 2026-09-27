# Araştırma özeti ve kaynak kaydı

Kontrol tarihi: 24 Eylül 2026. Tarihsel araştırma ile yeni ürün kararları ayrı tutulur. Aşağıdaki kısa özetler yeniden yazılmıştır; kaynak makaleleri veya oyun görselleri pakete kopyalanmadı.

## Eski oyunun kimliği

Önceki araştırma Dream Team / 夢之隊 ile GameGamma bağlantısına işaret ediyor. Türkiye'deki “Basketbol İmparatorluğu” adıyla aynı dağıtımın teknik/ticari eşleşmesi **doğrulanamadı**. GameGamma'yı ayrıca doğrulamadan geliştirici, fikrî mülkiyet sahibi veya Türkiye distribütörü olarak tanımlama.

Önceki notlarda 5 Temmuz 2011 kapalı beta ve 12 Temmuz 2011 açık beta tarihleri var. Bu devirde tam tarih kaydı yeniden doğrulanamadı. Bunlar ürün geliştirme gereksinimi değildir.

## Tarihsel mekanikler — kısa kaynak özetleri

### R01 — Oyuncu incelemesi, 12 Mart 2012

Kaynakta takım kurma, oyuncu imzalama/serbest bırakma, saatlik beş aday, sekizer hücum ve savunma taktiği, 32 oyunculu draft, oyuncu ekranında maaş trendi ve maç sonrası ödüller anlatılıyor. Ekipman, yetenek ve koleksiyon kombinasyonları da gösteriliyor. Bunlar ilgili yazıdaki gözlemlerdir; yeni MVP'ye topluca alınmayacak.

URL: https://ganhuso.pixnet.net/blog/posts/16131169220

Tür: ikincil, dönemsel oyuncu yazısı. Durum: metin okundu. Görsel varlıkların orijinalleri bu pakette doğrulanmadı.

### R02 — Oyuncu rehberi, 25 Nisan 2013

Rehber; gerçek NBA performansıyla değişen piyasa değeri, imza anında sabitlenen maaş, bütçe aşımında dostluk maçı dışındaki maçlara kısıt, aşım tutarına saatlik %1 vergi ve ödeme yetersizliğinde en değerli oyuncunun otomatik bırakılmasını anlatıyor. Scout için saatlik beş aday ve gizemli oyuncu olasılığı; maçta taktik, değişiklik ve stamina yönetimi; takas, draft, serbest pazar, ekipman ve yeteneklerden söz ediyor. Rating'in kesin günlük güncelleme algoritması doğrulanmıyor.

URL: https://eleven71120.pixnet.net/blog/posts/9259447418

Tür: ikincil rehber. Durum: metin okundu. Eski oyunun server koduna veya tüm bölgesel sürümlerine kanıt değildir.

### R03 — ACG oyun kaydı

Arama sonucu, 夢之隊'yi gerçek basketbol verisi kullanan web menajerlik/strateji oyunu olarak tanımlıyor. Tam sayfa açılışı başarısız oldu; ayrıntılı tarih ve işletmeci bilgisi bu devirde teyit edilmedi.

URL: https://acg.gamer.com.tw/acgDetail.php?s=50677

Tür: oyun veri tabanı. Durum: arama özeti erişilebilir, tam içerik doğrulanamadı.

### R04 — Arşiv niteliğindeki tanıtım

Başka sitelere yönlendiren derleme içerik; bağımsız doğrulama veya teknik referans olarak kullanılmıyor.

URL: https://game2016.pixnet.net/blog/posts/9122599153

Durum: açıldı; güvenilirlik sınırlı.

### R05 — Eski adres izleri

- https://www.gamegamma.com.tw/games/nba — R01 yazısında eski resmî site bağlantısı olarak yer alıyor.
- http://nba.aboilgame.com — önceki araştırmada aktarıldı; bu devirde canlı içerik doğrulanmadı.
- https://apps.facebook.com/aboilgame/ — önceki araştırmada aktarıldı; bu devirde canlı içerik doğrulanmadı.
- PTT, Gamer Forum ve RaGEZONE önceki araştırma özetinde anılıyor; tam gönderi URL'leri geri kazanılamadı. Kanıt olarak kullanılmıyor.

## Teknik/basketbol referansları

| Kimlik | Kaynak / URL | Doğrulanan kısa bilgi | Kullanım |
|---|---|---|---|
| R06 | [NBA Stats Glossary](https://www.nba.com/stats/help/glossary) | Hücum ribaundu mevcut possession'ı uzatır. | Possession kavramı |
| R07 | [NBA Rule 7](https://official.nba.com/rule-no-7-24-second-clock/) | İlgili kaçan şut/FT sonrası hücum kontrolünde 14 saniye yenileme koşulları belirtilir. | Rules profile değerlendirmesi |
| R08 | [NBA Rule 5](https://official.nba.com/rule-no-5-scoring-and-timing/) | Normal periyot 12, uzatma 5 dakikadır. | Önerilen saat profili |
| R09 | [ZenGM repository](https://github.com/zengm-games/zengm) | README projenin açık kaynak olmadığını açıklar; özel lisansa yönlendirir. | Kopyalanacak kod tabanı değildir |
| R10 | [ZenGM basketball simulation](https://github.com/zengm-games/zengm/blob/master/src/worker/core/GameSim.basketball/index.ts) | Önceki konuşmada incelendi; bu devirde belirli commit ve ayrıntılı davranış yeniden doğrulanmadı. | Karşılaştırmalı inceleme adayı |
| R11 | [PixiJS Application v8](https://pixijs.com/8.x/guides/components/application) | Application dokümanı renderer/ticker entegrasyonunu açıklar. | Sunum katmanı için aday teknoloji |
| R12 | [nba-sim repository](https://github.com/OorjitSethi/nba-sim) | Önceki konuşma referansı; bu devirde doğrulanmadı. | Gereksinim veya doğruluk kanıtı değildir |
| R13 | [NBA Rulebook](https://official.nba.com/rulebook/) | Kural başlıklarının resmî başlangıç noktası. | M0/M3 ayrıntılı kurallar incelemesi |

Kaynaklar zaman içinde değişebilir. Belirli kod davranışına dayanılacaksa commit SHA, tarih ve lisans kaydedilmeli. Canlı “master” URL determinism sürümü değildir.

## Yeni oyuna taşınacak dersler

Aşağıdakiler kaynak alıntısı değil, projenin tasarım çıkarımlarıdır:

- Kadro inşası, ekonomik seçimler ve canlı müdahale birbirini beslemeli.
- Tarihsel cezalar yeni oyunda otomatik kabul edilmemeli; özellikle oyuncu kaybettiren vergi davranışı ayrı ürün kararıdır.
- Eski 8+8 kapsamı, ilk motor için önerilen 4+4 kapsamından ayrılmalı.
- Oyuncu rating ve piyasa değeri zaman serileri uzun vadede yararlı olabilir; maç sırasında değişen veri motor sonucunu etkilememeli.
- Eski ekranları, marka varlıklarını veya başka simülatörün kodunu kopyalamadan özgün bir ürün oluşturulmalı.

## Eksik araştırma listesi

Orijinal maç motoru formülleri; kesin maç duvar saati süresi; tarihsel taktiklerin tam isim/counter matrisi; market/auction transaction davranışı; tüm rating güncelleme algoritması; Türkiye dağıtım zinciri; geçmiş tüm kaynakların tam URL'leri bilinmiyor. Bunları “eski oyunda böyleydi” diyerek tamamlamak yasak.

Yeni uygulama için .NET/ASP.NET/SignalR/React sürümleri, DB seçimi, veri sağlayıcısı ve üretim hosting'i henüz değerlendirilip kilitlenmedi. İlk ilgili milestone'da resmî belgeler ve gerçek ortam üzerinden doğrulanacak.
