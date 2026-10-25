# Як запустити PandemicShield і де брати дані

> Що вміє система — [`README.md`](../README.md). Основи біології — [`DOMAIN_GUIDE.md`](DOMAIN_GUIDE.md).
> Що робити далі — [`DIRECTION.md`](DIRECTION.md), розділ 6.

---

## 1. Запуск на своєму комп'ютері

### Що потрібно

| Що | Навіщо | Примітки |
|---|---|---|
| **Docker Desktop** (Windows/macOS) або Docker Engine + Compose (Linux) | Бекенд: 10 контейнерів | Виділіть Docker'у ≥ 6 ГБ RAM і ~10 ГБ диску; для збирання бактерій з FASTQ — ≥ 10 ГБ RAM |
| **Flutter SDK** 3.38+ (Dart 3.10+) | Desktop-застосунок | `flutter doctor` підкаже, чого бракує |
| Windows: Visual Studio 2022 з «Desktop development with C++» | Збірка під Windows | Не VS Code, а саме Visual Studio |
| macOS: Xcode | Збірка під macOS | |
| Linux: `clang cmake ninja-build pkg-config libgtk-3-dev` | Збірка під Linux | |
| Git | Отримати код | |

### Кроки

```bash
git clone https://github.com/volkovn1kita/PandemicShield.git
cd PandemicShield

# Якщо колись уже запускали стару версію: прибрати старі контейнери і базу,
# бо схема бази змінилася (тепер вона створюється міграціями).
docker compose down

docker compose up --build -d
```

Перша збірка триває 10–20 хв: завантажуються образи .NET, `nextstrain/nextclade` з наборами даних
(SARS-CoV-2, грип, RSV, mpox), `ncbi/amr` з базою генів резистентності і `staphb/mlst` зі схемами PubMLST.
Наступні запуски — секунди.

Перевірити, що бекенд живий:

```bash
docker compose ps                          # усі сервіси Up
curl http://localhost:5020/api/runs        # [] — порожній список запусків
curl http://localhost:5020/api/nextclade/datasets
```

Desktop-застосунок:

```bash
cd pandemic_shield_desktop
flutter pub get
flutter run -d windows    # або -d macos, -d linux
```

Застосунок звертається до `http://localhost:5020/api`.

### Перший аналіз (файли вже є в репозиторії)

| Файл | Тип зразка | Що побачите |
|---|---|---|
| `PandemicShield.Nextclade.Tests/Data/sars-cov-2-examples.fasta` | Вірус, SARS-CoV-2 | 8 геномів: Beta, Alpha, Delta і кілька ліній Omicron (клади 20H–23I), лінії Pango, QC, мутації, сповіщення за правилами |
| `PandemicShield.AmrFinder.Tests/Data/kpneumoniae-demo.fasta` | Бактерія, *Klebsiella pneumoniae* | Карбапенемази NDM-1 і OXA-48, ESBL CTX-M-15 |
| `PandemicShield.AmrFinder.Tests/Data/amrfinder-test-contigs.fasta` | Бактерія, *Escherichia* | Колістин (`pmrB_C84R`), ванкоміцин (`vanG`), шига-токсини |
| `PandemicShield.Nextclade.Tests/Data/sort/mixed.fasta` | Вірус, «Визначити автоматично» | SARS-CoV-2, грип H3N2 і RSV-A в одному файлі: кожен геном отримує свій набір даних |
| `PandemicShield.Bio.Tests/Data/MN908947.fasta` | Пошук мотивів (ORF) | Референсний геном SARS-CoV-2, навчальний режим |
| `PandemicShield.Bio.Tests/Data/MN908947.gb`, `pBR322.gb` | Вкладка **Explorer** | GenBank SARS-CoV-2 і кільцевої плазміди: карта ORF поруч з генами, білок, 3D-структура |

### Без власних файлів: відкриті дані NCBI

1. **Нагляд AMR** → «Оновити з NCBI» (група *Klebsiella*, країна Ukraine). За хвилину-дві: ~240 українських ізолятів,
   які карбапенемази в них трапляються, за роками й регіонами.
2. У таблиці ізолятів унизу — «Аналізувати» біля будь-якого: збірка імпортується з NCBI, і PandemicShield сам проганяє
   AMRFinderPlus та MLST (тип штаму). Наприклад, `GCA_059491835.1` — гіпервірулентний ST23 з NDM-1 і OXA-48.
3. Або **Запуски** → «Імпорт з NCBI»: до 500 номерів одразу (`OQ291490.1 MN908947.3` для вірусів, `GCA_009887825.1` для бактерій)
   чи ZIP-архів, завантажений з NCBI Virus / NCBI Datasets.
4. На сторінці запуску — «Звіт»: PDF, щоб передати результат, або CSV для Excel чи скриптів.

Імпорт і нагляд — єдині кроки, яким потрібен інтернет; аналіз далі працює офлайн. З ключем NCBI
(`NCBI_API_KEY=… docker compose up -d`) імпорт утричі швидший.

### Якщо щось не так

| Симптом | Причина й рішення |
|---|---|
| `docker compose logs api` показує помилку міграції | Стара база: `docker compose down`, потім `up` |
| Запуск «Бактерія» висить у статусі `Queued` на Mac з Apple Silicon | Воркер AMRFinderPlus працює через емуляцію x86 — він повільніший, але працює. Перевірте `docker compose logs amrfinder` |
| Застосунок на macOS не бачить API або не відкриває файли | Потрібні права `network.client` і `files.user-selected` (вже додані в `macos/Runner/*.entitlements`) |
| Порт 5020/5433/5672 зайнятий | Змініть ліву частину `ports:` у `docker-compose.yaml`, а для 5020 — ще й `baseUrl` у `lib/api.dart` |
| Подивитися черги | RabbitMQ UI: http://localhost:15672 (guest / guest) |

> **Перевірка.** Усі 10 образів зібрано й прогнано наскрізно в Docker Desktop на Windows: холодний старт
> (`docker compose down -v`, потім `up`) піднімає все без ручного втручання, бо сервіси чекають на готовність
> Postgres і RabbitMQ. Автоматичної збірки на кожен push (CI) поки немає — це перший пункт плану.

### Тести

```bash
dotnet test PandemicShield.slnx      # 209 тестів: ядро, Nextclade, AMRFinderPlus, MLST, FASTQ, імпорт, нагляд, звіти, Explorer
```

---

## 2. Де брати дані

Власна лабораторія не потрібна: мільйони геномів лежать у відкритому доступі.

### Віруси (тип «Вірус», FASTA повних геномів)

| Джерело | Що там | Як отримати |
|---|---|---|
| **NCBI Virus** — ncbi.nlm.nih.gov/labs/virus | SARS-CoV-2, грип, RSV, mpox з GenBank, фільтри за країною й датою | Фільтр *Geographic Region → Ukraine*, *Nucleotide Completeness → complete*, кнопка Download → FASTA |
| **NCBI Datasets CLI** | Те саме з командного рядка | `datasets download virus genome taxon SARS-CoV-2 --geo-location Ukraine --complete-only` |
| **Pathoplexus** — pathoplexus.org | Відкрита база mpox, RSV, вірусу Ебола та ін. | Пошук → Download FASTA |
| **Приклади Nextclade** | У кожному наборі даних є `sequences.fasta` | `nextclade dataset get --name nextstrain/flu/h3n2/ha/EPI1857216 -o h3n2` |
| **GISAID** — gisaid.org | Найбільша база SARS-CoV-2 і грипу | Потрібна реєстрація; дані **не можна** викладати в публічні репозиторії |

### Бактерії (тип «Бактерія», збірки контигів FASTA)

| Джерело | Що там | Як отримати |
|---|---|---|
| **NCBI Pathogen Detection** — ncbi.nlm.nih.gov/pathogens | Сотні тисяч ізолятів з уже порахованими генами AMR (колонка *AMR genotypes*) | Isolates Browser → фільтр за видом і `geo_loc_name` (Ukraine) → колонка *Assembly* дає номер `GCA_…` |
| **NCBI Datasets CLI** | Збірка геному за номером | `datasets download genome accession GCA_XXXXXXXXX.1` → розпакувати `*.fna` |
| **MicroBIGG-E** (у Pathogen Detection) | Окремі знахідки AMR по всіх ізолятах | Для дашборду: «у яких країнах і роках з'являвся `blaNDM-1`» |
| **ENA** — ebi.ac.uk/ena | Європейське дзеркало (часто там є ізоляти з ЄС-шпиталів) | Пошук за видом і країною |

Корисно звіряти себе: Pathogen Detection рахує гени тим самим AMRFinderPlus. Якщо завантажити збірку
звідти і проаналізувати в PandemicShield, список генів має збігтися з колонкою *AMR genotypes*.

### Сирі прочитання (FASTQ)

**SRA** (ncbi.nlm.nih.gov/sra) зберігає сирі дані секвенування. У «Новий аналіз» виберіть файли FASTQ
(для Illumina — пару `_R1`/`_R2`) і платформу: вірус стане консенсусним геномом для Nextclade, бактерія — збіркою
для AMRFinderPlus і MLST. Без справжніх даних прочитання можна змоделювати з будь-якого геному:

```bash
python docs/scripts/simulate_reads.py genome.fasta delta --platform illumina --depth 200   # delta_R1/R2.fastq.gz
python docs/scripts/simulate_reads.py genome.fasta delta --platform nanopore --depth 30    # delta.fastq.gz
```

Подробиці й обмеження (немає полірування Nanopore, вид для точкових мутацій) — [`STAGE5_READS_EXPLORER.md`](STAGE5_READS_EXPLORER.md).

### Довідкові бази

- **NCBI Reference Gene Catalog** — референсні послідовності генів резистентності (з них зроблено демо *Klebsiella*).
- **ClinVar** — клінічна значущість варіантів людини (лише для довідки, див. `BIOLOGY_REVIEW.md`).
- **PubMLST** — схеми MLST для типування бактерій (наступний крок).

---

## 3. Чи можливо це вдома

### Повністю можливо: робота з даними

Усе з розділу 2 — це файли, які завантажуються з браузера. Ноутбука з 16 ГБ RAM вистачає: Nextclade
обробляє тисячі геномів SARS-CoV-2 за хвилини, AMRFinderPlus — один ізолят за 1–2 хв. Приклади задач:

- зібрати всі українські геноми SARS-CoV-2 з NCBI Virus і подивитися, як змінювалися лінії за роками;
- завантажити 50–100 збірок *Klebsiella pneumoniae*, пов'язаних з Україною, і порахувати, які
  карбапенемази трапляються найчастіше;
- перевірити, чи збігаються результати PandemicShield з тим, що опублікувала NCBI.

### Можливо, але через сервіси: власні безпечні зразки

| Що | Як | Орієнтовно |
|---|---|---|
| ДНК-баркодинг грибів і рослин | Зразок → сервіс секвенування (Сенгер) → FASTA ділянки ITS / rbcL | Одиниці–десятки доларів за зразок; є громадські програми для мікологів |
| Плазміди (DIY-біологія, iGEM) | Повноплазмідне секвенування поштою | Від ~$15 за плазміду |
| Власний геном | Сервіс повногеномного секвенування людини | Сотні доларів; обирайте сервіс, що віддає сирі дані й видаляє їх на запит |
| Портативний секвенатор | Oxford Nanopore MinION / Flongle | Від ~$1 500–2 000 за стартовий набір, плюс реагенти й базове обладнання (піпетки, центрифуга) |

### Неприпустимо вдома

- **Культивувати бактерії** з ран, клінічних зразків, ґрунту чи стічних вод. Навіть «просто подивитися
  на кишкову паличку» — це робота, яка потребує лабораторії рівня BSL-2. Це небезпечно й незаконно.
- Працювати зі зразками **інших людей** без їхньої згоди та етичного дозволу.
- Робити **медичні висновки** зі свого генома чи чужого ізоляту: це інформаційний інструмент, рішення
  ухвалюють лікар-генетик або мікробіолог за лабораторним тестом.

Реальні бактеріальні й клінічні зразки — це шлях через партнерство з лабораторією (кафедра мікробіології,
регіональний лабораторний центр), а не через домашню кухню.
