# Bağlam, kapsam ve kararların kökeni

## Projenin çıkışı

Kullanıcı eski Basketbol İmparatorluğu / Dream Team hissini modern web teknolojileriyle yeniden oluşturmak istiyor. Önceki konuşmada PixiJS ve Three.js bağlamında araştırma yapıldı; ardından possession tabanlı C# maç motoru tasarımına geçilmesi uygun bulundu. Şimdiki açık istek, geliştirmeyi Codex'te sürdürmek üzere araştırma ve planları ayrı Markdown belgelerine taşımaktır.

Çalışma adı **Dream Team Reborn**. Marka/adın ticari uygunluğu bu pakette araştırılmadı. Oyun, gerçek zamanlı input isteyen bir basketbol aksiyon oyunu değil; kadro ve taktik kararlarıyla müdahale edilen menajerlik oyunudur.

## Kaynakların kökeni

| Katman | Elimizdeki içerik | Kullanım sınırı |
|---|---|---|
| Kullanıcı talimatı | Araştırma, tasarım yönüne “Uygundur”, Codex'e düzenli devir isteği | Kullanıcı iradesinin doğrudan kanıtı |
| Ayrıntılı konuşma | Match Engine v0.1'in 40 başlıklı tasarımı ve önceki MVP/mimari son bölümleri | Öneri ve formüllerin ana kaynağı |
| Geri kazanılan bağlam | Eski oyun araştırmasının kimlik/mekanik özeti ve bazı URL'ler | Tam konuşma dökümü değildir |
| Yeniden kontrol edilen web kaynakları | Araştırma dosyasındaki durumlarıyla listelenmiştir | Bir blogda yazılması ürünün tüm sürümlerinde doğrulandığı anlamına gelmez |
| Bu devirdeki teknik değerlendirme | Determinism sürümlemesi, saat/rules ayrımı, command sırası, kabul ölçütleri | Yeni devir önerisi; eski onay gibi gösterilmez |

## Etiket sözlüğü

- **Yön:** Konuşmanın benimsediği tasarım yaklaşımı. Tüm ayrıntılarının ayrı ayrı onaylandığı iddiası değildir.
- **Taslak:** Önceki asistanın önerdiği model, sayı veya teknoloji.
- **Devir önerisi:** Bu paket hazırlanırken kodlamayı güvenilir kılmak için eklenen netleştirme.
- **Açık:** Seçim henüz yapılmadı; karar sahibi kullanıcı, teknik öneri sahibi Codex.
- **Ertelenmiş:** Vizyon/backlog içinde tutulur, aktif milestone'da uygulanmaz.
- **Kaynakta görüldü:** Belirtilen kaynağın içeriği okunabildi. Birincil tarihsel kanıt veya bağımsız teyit anlamına gelmez.
- **Doğrulanamadı:** Tam kaynak/kod/deney yok; kesin gerçek veya gereksinim olarak kullanma.

## Korunacak ana yön

1. Maç + kadro geliştirme döngüsünün eğlenceli olup olmadığını önce kanıtla.
2. İyi oyuncu avantaj sağlasın; kadro uyumu, taktik ve canlı rotasyon da etkili olsun.
3. Tek OVR karşılaştırması veya taş-kâğıt-makas tarzı otomatik taktik galibiyeti olmasın.
4. Önce saf C# motor ve console simulator; 10K/100K deney altyapısı; sonra sunucu ve görselleştirme.
5. Motor, sunumdan bağımsız olsun. PixiJS yerine gelecekte başka renderer kullanılabilsin.
6. İlk içerik kurgusal olsun; gerçek veri bağlantısı daha sonra ayrıca ele alınsın.

## Üç ayrı kapsam

- **Engine v0.1:** Offline motor, state, kurallar, taktik, yorgunluk, komutlar, console ve testler. Roadmap M1–M6.
- **Oynanabilir MVP v1:** Hesap/takım, kadro, AI maç, canlı izleme/müdahale, sonuç/ödül, scout ve imzalama. M7–M10.
- **Uzun vadeli vizyon:** PvP ölçekleme, oyuncular arası market/trade/auction, lig/draft, gerçek veriye dayalı rating/değer güncellemeleri. M11+.

İlk sürümde bunların hepsini teslim etmeye çalışmak kapsam hatasıdır. Repo/SDK/DB/provider/hosting bu konuşmada kesinleştirilmedi. Bu paket bir altyapının mevcut olduğunu ima etmez.
