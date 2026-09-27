# Dream Team Reborn — Codex devir paketi

Sürüm: 0.1 · Tarih: 24 Eylül 2026 · Dil: Türkçe · Durum: geliştirme öncesi tasarım devri.

Bu paket, Basketbol İmparatorluğu / Dream Team esinli web basketbol menajerlik oyununun araştırma, ürün ve teknik tasarımını Codex'e aktarır. **Uygulama kaynak kodu değildir.** Bir repository incelenmedi; hiçbir özelliğin geliştirildiği veya testlerin geçtiği varsayılmamalıdır.

## Nasıl kullanılır?

1. ZIP'i aç. Yeni repo kullanıyorsan bu klasörün içeriğini repo köküne koy. Mevcut repoda aynı isimli dosyaları ezmeden birleştir; özellikle mevcut `AGENTS.md` kurallarını koru.
2. Codex'i ilgili repository üzerinde aç.
3. [CODEX_START.md](CODEX_START.md) içindeki **İlk mesaj** bloğunu gönder.
4. Codex önce belgeleri ve gerçek repo durumunu değerlendirir, M0 kararları ve ilk uygulama dilimi için plan çıkarır. Bu ilk mesaj uygulama geliştirme yetkisi vermez.
5. Uygulamaya geçmek istediğinde aynı dosyadaki ikinci mesajı, uygun milestone kimliğini yazarak gönder. Her seferinde tek milestone üzerinde ilerle.

## Okuma sırası ve dosyaların sorumluluğu

| Sıra | Dosya | İçerik |
|---|---|---|
| 1 | [AGENTS.md](AGENTS.md) | Codex çalışma kuralları ve kapsam sınırları |
| 2 | [00_CONTEXT_AND_SCOPE.md](docs/00_CONTEXT_AND_SCOPE.md) | Proje hikâyesi, kullanıcı yönü, belge güven düzeyleri |
| 3 | [01_RESEARCH_AND_SOURCES.md](docs/01_RESEARCH_AND_SOURCES.md) | Eski oyun araştırması, kaynaklar, teyit durumu |
| 4 | [02_GDD.md](docs/02_GDD.md) | Oyun tasarımı, core loop, MVP ve sonraki sürümler |
| 5 | [03_TECHNICAL_ARCHITECTURE.md](docs/03_TECHNICAL_ARCHITECTURE.md) | Katmanlar, bağımlılıklar, teknoloji adayları |
| 6 | [04_DOMAIN_AND_DATA_MODEL.md](docs/04_DOMAIN_AND_DATA_MODEL.md) | Domain, maç snapshot'ları, gelecekteki veri modeli |
| 7 | [05_MATCH_ENGINE_SPEC.md](docs/05_MATCH_ENGINE_SPEC.md) | Attribute, resolver, taktik, stamina ve RNG tasarımı |
| 8 | [06_RULES_AND_STATE_MACHINE.md](docs/06_RULES_AND_STATE_MACHINE.md) | Saatler, possession, faul, FT ve sınır durumları |
| 9 | [07_EVENTS_COMMANDS_AND_REPLAY.md](docs/07_EVENTS_COMMANDS_AND_REPLAY.md) | Event/command sözleşmesi, reconnect, replay |
| 10 | [08_TESTING_AND_BALANCE.md](docs/08_TESTING_AND_BALANCE.md) | Invariant testleri, Monte Carlo ve kalibrasyon |
| 11 | [09_ROADMAP_AND_BACKLOG.md](docs/09_ROADMAP_AND_BACKLOG.md) | Milestone'lar, bağımlılıklar, çıkış ölçütleri |
| 12 | [10_DECISIONS_AND_OPEN_QUESTIONS.md](docs/10_DECISIONS_AND_OPEN_QUESTIONS.md) | Karar durumu, düzeltmeler ve açık sorular |
| 13 | [11_PROJECT_STATUS.md](docs/11_PROJECT_STATUS.md) | Yeni Codex oturumları için güncel durum kaydı |
| 14 | [M0_PLANNING_HANDOFF.md](docs/plans/M0_PLANNING_HANDOFF.md) | İlk oturumun somut çalışma planı |
| — | [CODEX_START.md](CODEX_START.md) | Başlangıç, uygulama ve devam mesajları |

## Tek doğru kaynak kuralı

- Ürün kapsamı: GDD. Teknik sınırlar: mimari. Motor davranışı: engine spec + rules. Taşıma/replay: events.
- Sayısal denge parametrelerinin sahibi engine spec; deney protokolünün sahibi testing belgesidir.
- Kararların onay durumunun sahibi karar günlüğüdür. Belgeler çatışırsa Codex bunu raporlar; geçmiş öneriyi kullanıcı onayı gibi sunmaz.
- Güncel kullanıcı talimatı ve incelenen gerçek kod, bu paketteki eski varsayımlardan üstündür. Farkı belgeleyerek ilerle.
- Kaynaklar normal URL ile yazılmıştır; ChatGPT'ye özgü citation kimlikleri gerekmez.

## Bilinen sınır

Görünen ayrıntılı Match Engine v0.1 konuşması ve erişilebilen eski araştırma özeti birleştirildi. Eski konuşmanın kelimesi kelimesine tam dökümü veya orijinal oyunun kodu bulunmuyor. Eksik bilgiler tamamlanmış gibi gösterilmedi. Yeni teknik netleştirmeler **Devir önerisi** olarak ayrıldı.
