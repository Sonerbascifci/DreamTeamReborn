# Codex'e verilecek mesajlar

Aşağıdaki blokları ilgili aşamada kopyala. Bunlar uygulama kodu değildir.

## İlk mesaj — yalnızca inceleme ve planlama

```text
Bu repository'de Dream Team Reborn adlı web tabanlı basketbol menajerlik oyununu geliştireceğiz.

Repo kökündeki README.md ve AGENTS.md dosyalarını, ardından README'deki sırayla docs altındaki devir belgelerini oku. Özellikle GDD, mimari, domain, match engine, kurallar, events, test/balance, karar günlüğü ve M0 planını birlikte değerlendir.

Bu oturumda uygulama kodu yazma, mevcut kodu değiştirme, paket kurma, migration veya deploy yapma. Önce repository'nin gerçek durumunu ve bu belgeleri karşılaştır. Repo boşsa bunu açıkça belirt.

Beklediğim çıktı:
1. Oyunun amacı ve MVP sınırı hakkında kısa anlama özeti.
2. Mevcut repo/solution/SDK/test envanteri; doğrulanamayanlar.
3. Belgelerdeki çelişkiler, teknik eksikler ve M1'i gerçekten engelleyen kararlar. Geçmiş asistan önerilerini kullanıcı onayı sayma. Sonraki milestone kararları M1'i gereksiz engellemesin.
4. M1 için oluşturulacak/değişecek dosyalar, model ve interface sözleşmeleri, bağımlılıklar, test senaryoları ve kabul kriterleri içeren küçük adımlı uygulama planı.
5. Varsayımlarını ve karar önerilerini kısa gerekçeleriyle belirt. Benden cevap gerekiyorsa yalnızca ilk milestone'ı etkileyen soruları sor.

M1 kapsamı: engine temel modelleri, setup doğrulama, snapshot/version kimliği ve deterministik RNG. Henüz tam maç, API, DB veya frontend yok. İlk tamamlanmış motor v0.1 M1–M6 sonunda ortaya çıkacak.

Planlama çıktısını docs/plans/M1_IMPLEMENTATION_PLAN.md dosyasına yazabilirsin; karar günlüğü ve proje durumunu güncelleyebilirsin. Onaylanmamış konuları onaylı işaretleme. Uygulamaya başlamadan planı bana sun.
```

## Uygulamaya geçiş mesajı

Milestone kimliğini ve plan dosyasını, ilk oturumda kararlaştırılan değerlerle değiştir.

```text
M1 uygulama planını, bu oturumda netleştirdiğimiz kararlarla birlikte onaylıyorum. Planı uygula.

AGENTS.md ve ilgili spec'leri takip et. Önce gerçek repo durumunu kontrol et; mevcut kullanıcı değişikliklerini koru. M1 kabul kriterlerine kadar gerekli geliştirmeyi ve davranış testlerini tamamla. Kapsamı API, DB, SignalR veya frontend'e genişletme.

Bu kapsamda rutin implementation kararlarını verip ilerle. Yalnızca planı esaslı değiştiren bir engel çıkarsa durumu açıkla. Sonunda değişiklikleri, gerçekten çalıştırılan testleri ve kalan riskleri raporla; docs/11_PROJECT_STATUS.md dosyasını güncelle.
```

## Sonraki Codex oturumunda devam mesajı

```text
README.md, AGENTS.md, docs/10_DECISIONS_AND_OPEN_QUESTIONS.md ve docs/11_PROJECT_STATUS.md dosyalarını oku. Ardından aktif milestone'ın planını ve bağlı spec'leri oku, git durumuyla karşılaştır.

Sonraki tek görevi ve önceki doğrulamaları belirle. Daha önce tamamlanmış işi tekrar üretme. Bu oturumda görev uygulaması için açık yetki yoksa önce kısa devam planını sun; daha önce verilmiş ve halen geçerli uygulama yetkisi varsa kapsamı dahilinde devam et.
```

## Neden ilk oturum planlama?

Bu paket geliştirme için bağlam sağlar; bazı kural ve matematik kararları taslaktır. Codex'in ilk işi bütün sistemi bir kerede üretmek değil, gerçek repo üzerinden uygulanabilir ilk dilimi netleştirmektir. M1 sonrasındaki planlar ilgili milestone'a gelindiğinde gerçek kod yollarıyla hazırlanır.
