# Game Design Document — Dream Team Reborn

Sürüm 0.1. Ürün yönü aktarımıdır; ölçülmüş denge veya tamamlanmış ürün spesifikasyonu değildir. Teknik ayrıntıların sahibi ilgili teknik belgelerdir.

## 1. Ürün tanımı

Web üzerinden oynanan basketbol menajerlik oyunu. Oyuncu bir takım kurar, kadrosunun güçlü/zayıf yönlerini değerlendirir, beşini ve taktiklerini seçer, maç sırasında rotasyon ve tempo kararları verir; kazandığı kaynaklarla kadrosunu geliştirir.

Temel deneyim: “Bu oyuncuyu neden seçtim, neden bu taktiği uyguladım ve neden şimdi değişiklik yaptım?” sorularının maç üzerinde gözlenebilir karşılığı olması.

Hedef oyuncu hipotezi: basketbol ve kadro yönetimi seven, tam aksiyon oyunu refleksleri yerine stratejik karar vermek isteyen web oyuncuları. Pazar araştırmasıyla doğrulanmış segment değildir. İlk odak masaüstü tarayıcı; mobil kullanılabilirlik hedefi ekran tasarımında ayrıca kararlaştırılacak.

## 2. Tasarım ilkeleri

- Oyuncu kalitesi önemlidir; bütün sonucu tek OVR belirlemez.
- Kadro kompozisyonu farklı taktikleri mümkün kılar.
- Taktikler aksiyon dağılımını ve karşılaşma bağlamını değiştirir; otomatik zafer vermez.
- Yedek oyuncu ve stamina yönetimi gerçek değer taşır.
- RNG belirsizlik üretir; saklı güçlendirme veya dramatik skor düzeltme ile sonucu yönlendirmeyiz.
- Kullanıcı skor/box score/play-by-play üzerinden kararlarının etkisini okuyabilir.
- Sunum, motorun ürettiği sonucu doğru anlatır; sonradan sonucu uydurmaz.

## 3. Ana oyun döngüsü

| Adım | Kullanıcı kararı | Sistem çıktısı |
|---|---|---|
| Kayıt ve takım | Takım adı/kimliği | Hesap ve takım |
| Başlangıç kadrosu | Oyuncuları tanıma | Oynanabilir kurgusal kadro |
| Kadro yönetimi | İlk beş ve yedekler | Geçerli lineup |
| Maç hazırlığı | Hücum, savunma, tempo | Başlangıç taktik snapshot'ı |
| Maç | Takip, taktik değişikliği, substitution, timeout | Yetkili server eventleri |
| Sonuç | İstatistiklerden öğrenme | Sonuç ve bir defa verilen ödül |
| Scout | Adayları kıyaslama | İmzalanabilir oyuncu teklifleri |
| Gelişim | Bütçeye göre oyuncu edinme | Yeni kadro seçenekleri |

İlk ürün hipotezi: **Maç oynamak ve kadroyu iyileştirmek tekrar oynamayı istemeye yetecek mi?** Transfer piyasası bu sorunun ön koşulu değildir.

## 4. Engine v0.1 ile ürün MVP ayrımı

Engine; hesap, market veya web ekranı içermez. Sabit kurgusal takımlarla çalışan console ve kalibrasyon ortamıdır. Ürün MVP, bu doğrulanmış motoru kullanılabilir yönetim döngüsüne bağlar.

| Özellik | Engine v0.1 | Ürün MVP v1 | Sonraki sürümler |
|---|---|---|---|
| 18 attribute ve türetilmiş rating | Var | Gösterim eklenir | Veri sağlayıcı/progression |
| 4 hücum + 4 savunma | Var | Seçilebilir | Daha fazla set |
| Stamina/rotasyon | Var | Canlı müdahale | Daha kapsamlı sezon yorgunluğu |
| Saatler/fauller/FT/uzatma | Seçilen profil | Aynı profil | Daha ayrıntılı kurallar |
| Console/Monte Carlo | Var | Geliştirme aracı | Kalıcı balance hattı |
| Hesap/takım | Yok | Var | Sosyal özellikler |
| AI rakip | Deterministik policy | İlk oynanabilir rakip | Daha iyi rakip davranışı |
| SignalR ve 2D maç sunumu | Yok | Var | Spectator/ölçek |
| Scout + imzalama + ödül | Yok | Var | Ekonomi genişletme |
| PvP matchmaking | Yok | Zorunlu değil | Ayrı milestone |
| Market/trade/auction/draft | Yok | Zorunlu değil | Ayrı ürün tasarımı |
| Gerçek oyuncu/NBA veri akışı | Yok | Yok | Erişim ve kullanım koşulları sonrası |

## 5. Oyuncu ve kadro deneyimi

Oyuncu profilinde pozisyon, temel özellikler, türetilmiş rol güçlü yönleri ve kullanıcı için OVR bulunabilir. OVR formülü henüz seçilmedi. OVR motor girdisi olamaz.

Kadro ekranı; sahadaki beş oyuncuyu, yedekleri, rol uyumunu ve maç öncesi geçerlilik sorunlarını gösterir. PG/SG/SF/PF/C etiketleri ilk beş kurmada yardımcıdır. Pozisyon dışı oynatma cezası henüz tanımlı değildir; Codex gizli bir ceza uydurmamalı.

Kadro büyüklüğü ve başlangıç dağılımı açık ürün kararıdır. Test fixture'ları için örneğin on kişilik kadro kullanılması ürün kadrosunun on kişi olarak onaylandığı anlamına gelmez.

## 6. Taktikler

Önceki taslak: Balanced, PickAndRoll, PerimeterMotion, InsidePost; savunmada ManToMan, Drop, Switch, ZonePackPaint. Bunlar ilk UI seçenekleridir. Drop/Switch pick-and-roll coverage türleri, ManToMan/Zone ise daha geniş şemalar olduğundan aynı enum'da neyi temsil ettikleri motor belgesinde netleştirilmelidir.

Tempo: Slow/Normal/Fast taslağı. Hızlı tempo otomatik avantaj değildir; daha fazla possession, farklı transition fırsatları, top kaybı ve yorgunluk maliyetiyle dengelenir.

Oyuncu değişiklikleri istek olarak gönderilir ve geçerli pencerede uygulanır. Arayüz “bekliyor/uygulandı/reddedildi” durumlarını göstermelidir.

## 7. Maç ekranı ve kullanıcı akışı

Önerilen ilk ekranlar:

| Ekran | Asgari içerik | Kabul davranışı |
|---|---|---|
| Takım merkezi | Kadro, bakiye, sonraki maç aksiyonu | Yapılacak ilk iş açık |
| Kadro | İlk beş/yedek, özellikler | Aynı oyuncu iki slotta olamaz |
| Maç hazırlığı | Rakip, taktik, tempo | Geçersiz kadroyla maç başlamaz |
| Canlı maç | Skor, periyot/saat, saha, PBP, stamina/fauller | Event sırası korunur |
| Müdahale paneli | Taktik, tempo, değişiklik, timeout | Server sonucunu bekler |
| Sonuç | Box score, kazanç, rövanş/devam | Ödül tekrar alınamaz |
| Scout | Aday, bedel, bütçe etkisi | Aynı teklif iki kez alınamaz |
| Oyuncu profili | Yetenekler ve gelecekte değer/rating trendi | Veri yoksa sahte geçmiş grafiği çizilmez |

Live viewer'ın gerçek zaman hızı henüz seçilmedi. Simüle basketbol süresi ile kullanıcının ekran başında geçirdiği süre farklı ayarlardır.

## 8. Ekonomi ve ilerleme

İlk hedef basit, server-authoritative bir kazan–incele–imzala döngüsüdür. Para birimi adı, başlangıç bütçesi, kazanç miktarı, scout ücreti/yenileme süresi ve maaş bütçesi henüz açık.

Maaş (Salary), piyasa değeri (MarketValue) ve imza bedeli (SigningCost) ayrı kavramlardır. Aralarında formül belirlenmeden eşit varsayılmamalı. Eski vergi/otomatik oyuncu bırakma davranışı MVP kapsamına alınmış değildir.

İlk sürümde maç içi stamina maça ait state'tir. Maçlar arası enerji bekletme, gerçek parayla toparlanma veya sakatlık sistemi yoktur. Gerçek para mağazası ve gelir modeli bu pakette tasarlanmadı.

## 9. Uzun vadeli sistemler

PlayerRatingHistory ve PlayerMarketValueHistory; transfer pazarı/takas/auction; draft; lig ve sezonlar; gelişmiş PvP; lisanslı veri adaptörleri; ek taktikler ve isteğe bağlı 3D renderer. Hepsi ayrı kapsam ve kabul kriterleri ister.

## 10. İlk sürüm dışı

Sakatlık, moral, oyuncu kimyası, hakem kişiliği, saha avantajı, koç rating'i, seyirci bonusu, ekipman, güçlendirme item'ı, momentum bonusu, ayrıntılı fizik/şut koordinatları ve çok sayıda set. Önceki state örneğindeki Momentum alanı bu nedenle aktif davranışa dönüşmemelidir.

## 11. Oynanış değerlendirmesi

Sayı hedefleri ölçülmüş başarı eşiği değildir. İlk playtest şu soruları yanıtlamalı: kullanıcı taktiğin etkisini açıklayabiliyor mu; yorgun yıldız ile taze yedek arasında anlamlı tercih var mı; kayıptan sonra kadrosunu değiştirmek istiyor mu; scout ile yeni oyuncu alınca yeni bir oyun tarzı açılıyor mu?

Salt “ortalama skor gerçekçi” olması eğlenceli ürün kanıtı değildir. Sayısal doğrulama ile insan playtest'i ayrı raporlanmalı.
