# M0 — Codex keşif ve planlama Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> Bu paketin uyarlaması: İş uygulama kodu değil doküman/keşif çalışmasıdır. Skill kuruluysa uygun inline workflow kullan; kurulu değilse standart araçlarla aynı çıktıları üret. Bu başlık paralel ajan çalışması yetkisi veya zorunluluğu oluşturmaz. Kullanıcı talimatları önceliklidir.

**Goal:** Gerçek repository ile devir belgelerini eşleştirip uygulanabilir ve kullanıcıya sunulabilir M1 planı üretmek.

**Architecture:** Önce saf C# domain/setup/RNG temeli; API/DB/UI sonraki milestone'lar. Bu oturumda uygulama kodu oluşturulmaz. Var olan kod varsa belgeye uymuyor diye refactor edilmez.

**Tech Stack:** Ortamda doğrulanacak C#/.NET; repo keşfi için mevcut shell/git araçları. Test framework ve SDK sürümü M0 çıktısıdır.

**Spec:** [Bağlam](../00_CONTEXT_AND_SCOPE.md), [Mimari](../03_TECHNICAL_ARCHITECTURE.md), [Domain](../04_DOMAIN_AND_DATA_MODEL.md), [Kararlar](../10_DECISIONS_AND_OPEN_QUESTIONS.md).

## Global Constraints

- İlk devir isteği planlamadır; uygulama kodu, migration, paket kurulumu ve deploy yok.
- Önce repository'nin mevcut kullanıcı değişikliklerini koru.
- Motor saf C#; ASP.NET/EF/DB/SignalR/React/PixiJS bağımlılığı yok.
- M1: domain/setup validation ve sürümlü seeded RNG; tam maç M1 kapsamı değildir.
- Sayısal taslaklar ve devir önerileri kullanıcı onayı gibi işaretlenmez.
- Test çalışmadıysa başarı iddiası yok.

## Review Focus

1. Boş repo/eksik SDK: olmayan solution veya test altyapısına atıf yapılmaması.
2. Mevcut kullanıcı değişiklikleri: keşfin dosya ezmemesi veya reset yapmaması.
3. Eksik geçmiş: tarihsel oyun iddiasının kanıtsız gereksinime dönüşmemesi.
4. Belge/kod çatışması: gerçek kod görülmeden yeniden mimari kurma planı yazılmaması.
5. Milestone aşımı: DB, frontend ve bütün kural setinin M1'e çekilmemesi.

Bunların kontrolü aşağıdaki görevlerin çıktı incelemelerine dağıtıldı. Kod testi henüz yok; M1 planında davranış testleri gerçek framework'e göre yazılacak.

## Görev 1 — Belgeleri ve yetki sınırını anla

**Read:** README, AGENTS, 00–11 belgeleri ve başlangıç mesajı.
**Produces:** Gereksinim/kaynak durum matrisi; M1 için zorunlu belgelerin listesi.

- [ ] README okuma sırasını tamamla; okunamayan dosyayı adını vererek belirt.
- [ ] Kullanıcı yönünü, geçmiş asistan taslağını ve yeni devir önerisini ayır.
- [ ] İlk oturumun uygulama kodu yetkisi vermediğini teyit et.
- [ ] Review Focus 3 için “eski oyunda vardı” diye M1'e giren satır bulunmadığını kontrol et.

Kabul: bir paragraf ürün/MVP özeti ve kısa scope listesi; tüm GDD'nin tekrar yazımı yok.

## Görev 2 — Salt okunur repo envanteri

**Read:** Gerçek repo dosyaları ve ortam.
**Produces:** Solution/project/test/SDK envanteri ve varsa tutarsızlık listesi.

İlgili platformda çalıştırılacak salt okunur komut örnekleri:

```bash
git status --short
rg --files -g AGENTS.md -g '*.sln' -g '*.slnx' -g '*.csproj' -g global.json -g Directory.Build.props -g Directory.Packages.props -g NuGet.Config -g package.json
dotnet --info
```

- [ ] Önce konumun gerçek git repository olup olmadığını belirle; değilse “repo yok/boş” olarak kaydet.
- [ ] Var olan AGENTS kapsamlarını ve kullanıcı değişikliklerini belirle; içeriklerini silme.
- [ ] Solution, target framework, references ve test framework'ünü varsa dosyadan oku.
- [ ] SDK yoksa bu oturumda otomatik kurma; M1 planında gerçek ön koşul olarak belirt.
- [ ] Review Focus 1/2/4 için her iddianın dosya/komut kanıtını kaydet; var olmayan path'i “mevcut” diye etiketleme.

Kabul: mevcut ve önerilen dosyalar ayrı listelenir. Restore/build bu salt okunur envanterin zorunlu parçası değildir.

## Görev 3 — M1 kararlarını daralt

**Modify:** `docs/10_DECISIONS_AND_OPEN_QUESTIONS.md` (yalnız gerçek yeni durum varsa).
**Produces:** Q01–Q03 için öneri veya karar ihtiyacı.

- [ ] Target framework/SDK/test seçimini mevcut repo ile uyumlu öner.
- [ ] RNG algoritması/version/state ve ilk determinism garanti kapsamını öner; gerekirse resmî/algoritmanın birincil kaynağını doğrula.
- [ ] Snapshot mutability, ID ve zaman birimi seçimini tanımla.
- [ ] Q04–Q18'in M1'i etkilemeyen kısmını sonraki milestone'a bırak.
- [ ] Kullanıcı cevabı gerekiyorsa yalnız M1'i gerçekten etkileyenleri sor; cevap olmadan kabul edildi işaretleme.

Kabul: açık kararların kapsam/etki/gerekçesi var. DB/hosting soruları M1 engeli yapılmamış.

## Görev 4 — M1 uygulama planını yaz

**Create:** `docs/plans/M1_IMPLEMENTATION_PLAN.md`.
**Consumes:** Repo envanteri ve netleşmiş M1 kararları.
**Produces:** Bir sonraki oturumda uygulanabilecek testli küçük görevler.

- [ ] Mevcut repo varsa gerçek dosya yollarını, boşsa önerilen yeni yolları kullan.
- [ ] Görevleri: setup modelleri/validation; immutable snapshot ve engine identity; deterministic RNG/state roundtrip olarak anlamlı teslimlere ayır.
- [ ] Her görevde exact file paths, tükettiği/ürettiği tip imzaları, davranış testleri ve kabul kriterini yaz.
- [ ] Test framework'ü bilindiğinde actual test kodu ve çalıştırma komutunu plana ekle; “uygun test ekle” diye bırakma.
- [ ] Testler: duplicate/foreign lineup, 0/100/sınır dışı rating, dış input mutasyonu, RNG golden sequence, serialize/restore devamı ve stable ordering.
- [ ] Kod adımlarını kullanıcıya öneri olarak sun; bu oturumda uygulama dosyası üretme.
- [ ] Review Focus 5 için M1'de API/DB/SignalR/React/PixiJS/tam maç ekleyen görev bulunmadığını kontrol et.

Kabul: tanımlanmamış tipe veya kurulmamış araca sessiz bağımlılık yok; her görevin test/çıkış ölçütü mevcut.

## Görev 5 — İncele ve devret

**Modify:** `docs/11_PROJECT_STATUS.md`.
**Produces:** İnsan tarafından değerlendirilebilir plan ve doğru başlangıç durumu.

- [ ] Planın domain/mimari sınırlarıyla çelişmediğini kontrol et.
- [ ] Plan içi yolların ve tip adlarının tutarlı olduğunu kontrol et.
- [ ] Gerçek yapılmış iş ile gelecekteki işin ayrıldığını doğrula.
- [ ] Proje durumuna keşif sonuçlarını, karar ihtiyaçlarını ve sonraki tek işi yaz.
- [ ] Kullanıcıya M1 planını sun. Uygulama açıkça yetkilendirilene kadar application koduna geçme.

M0 tamamlanma ölçütü: repo temelli M1 planı, sınırlı açık sorular, güncel durum kaydı. Bu tamamlanma motor geliştirildiği veya testler geçtiği anlamına gelmez.
