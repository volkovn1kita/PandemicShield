import 'package:flutter/widgets.dart';

enum AppLanguage { uk, en }

/// Мова інтерфейсу. Перемикач у бічній панелі змінює значення, і весь застосунок перебудовується.
class LanguageController extends ValueNotifier<AppLanguage> {
  LanguageController() : super(AppLanguage.uk);
}

class L10n extends InheritedNotifier<LanguageController> {
  const L10n({super.key, required LanguageController controller, required super.child}) : super(notifier: controller);

  static LanguageController controllerOf(BuildContext context) => context.dependOnInheritedWidgetOfExactType<L10n>()!.notifier!;
}

extension Tr on BuildContext {
  /// Переклад за ключем. Параметри підставляються замість {name}.
  String tr(String key, [Map<String, Object?> args = const {}]) {
    final language = L10n.controllerOf(this).value;
    String text = _strings[key]?[language] ?? _strings[key]?[AppLanguage.en] ?? key;
    args.forEach((name, value) => text = text.replaceAll('{$name}', '$value'));
    return text;
  }
}

const _uk = AppLanguage.uk;
const _en = AppLanguage.en;

const Map<String, Map<AppLanguage, String>> _strings = {
  // --- Shell ---
  'nav.runs': {_uk: 'Запуски', _en: 'Runs'},
  'nav.genomes': {_uk: 'Геноми', _en: 'Genomes'},
  'nav.alerts': {_uk: 'Сповіщення', _en: 'Alerts'},
  'nav.rules': {_uk: 'Правила', _en: 'Rules'},
  'status.online': {_uk: 'API підключено', _en: 'API connected'},
  'status.offline': {_uk: 'API недоступний', _en: 'API offline'},
  'status.live': {_uk: 'API підключено · наживо', _en: 'API connected · live'},
  'status.polling': {_uk: 'API підключено · опитування', _en: 'API connected · polling'},
  'theme.toggle': {_uk: 'Світла / темна тема', _en: 'Light / dark theme'},
  'disclaimer': {_uk: 'Дослідницький інструмент, не для діагностики', _en: 'Research tool, not for diagnosis'},

  // --- Common ---
  'common.delete': {_uk: 'Видалити', _en: 'Delete'},
  'common.cancel': {_uk: 'Скасувати', _en: 'Cancel'},
  'common.close': {_uk: 'Закрити', _en: 'Close'},
  'common.connectionFailed': {
    _uk: 'Немає зв\'язку з API. Перевірте, чи запущено docker-compose.',
    _en: 'Cannot reach the API. Is docker-compose running?',
  },
  'common.anyDataset': {_uk: 'Будь-який набір даних', _en: 'Any dataset'},
  'common.virus': {_uk: 'Вірус', _en: 'Virus'},
  'common.human': {_uk: 'Людина', _en: 'Human'},
  'common.justNow': {_uk: 'щойно', _en: 'just now'},
  'common.minutesAgo': {_uk: '{n} хв тому', _en: '{n} min ago'},
  'common.hoursAgo': {_uk: '{n} год тому', _en: '{n} h ago'},
  'common.all': {_uk: 'Усі', _en: 'All'},

  // --- Runs ---
  'runs.new': {_uk: 'Новий аналіз', _en: 'New analysis'},
  'runs.empty': {_uk: 'Запусків ще немає. Почніть з нового аналізу.', _en: 'No runs yet. Start with a new analysis.'},
  'col.file': {_uk: 'Файл', _en: 'File'},
  'col.dataset': {_uk: 'Набір даних', _en: 'Dataset'},
  'col.records': {_uk: 'Записів', _en: 'Records'},
  'col.size': {_uk: 'Розмір', _en: 'Size'},
  'col.nextclade': {_uk: 'Nextclade', _en: 'Nextclade'},
  'col.alerts': {_uk: 'Сповіщень', _en: 'Alerts'},
  'col.started': {_uk: 'Запущено', _en: 'Started'},
  'nc.none': {_uk: '—', _en: '—'},
  'nc.queued': {_uk: 'У черзі', _en: 'Queued'},
  'nc.running': {_uk: 'Аналізується', _en: 'Running'},
  'nc.completed': {_uk: 'Готово', _en: 'Done'},
  'nc.failed': {_uk: 'Помилка', _en: 'Failed'},

  // --- New analysis dialog ---
  'upload.type': {_uk: 'Тип зразка', _en: 'Sample type'},
  'upload.dataset': {_uk: 'Набір даних Nextclade', _en: 'Nextclade dataset'},
  'upload.datasetHint': {
    _uk: 'Nextclade вирівнює геном на референс і визначає кладу, лінію, якість і мутації.',
    _en: 'Nextclade aligns each genome to a reference and reports clade, lineage, QC and mutations.',
  },
  'upload.noNextclade': {_uk: 'Без Nextclade, лише мотиви ORF', _en: 'No Nextclade, ORF motifs only'},
  'upload.humanHint': {
    _uk: 'Людські послідовності перевіряються лише мотивами ORF (CDS/мРНК).',
    _en: 'Human sequences are checked with ORF motifs only (CDS/mRNA).',
  },
  'upload.file': {_uk: 'Файл FASTA', _en: 'FASTA file'},
  'upload.pick': {_uk: 'Обрати файл…', _en: 'Choose file…'},
  'upload.start': {_uk: 'Запустити', _en: 'Start'},
  'upload.sent': {_uk: 'Прийнято: {records} записів, {bases}.', _en: 'Accepted: {records} records, {bases}.'},
  'upload.sentNextclade': {_uk: 'Прийнято. Nextclade аналізує геноми.', _en: 'Accepted. Nextclade is analysing the genomes.'},

  // --- Genomes ---
  'genomes.noRun': {_uk: 'Оберіть запуск', _en: 'Select a run'},
  'genomes.empty': {_uk: 'Для цього запуску немає результатів Nextclade.', _en: 'This run has no Nextclade results.'},
  'genomes.waiting': {_uk: 'Nextclade аналізує геноми…', _en: 'Nextclade is analysing the genomes…'},
  'genomes.search': {_uk: 'Пошук мутації, напр. S:E484K', _en: 'Find mutation, e.g. S:E484K'},
  'genomes.searchResult': {_uk: '{label}: {n} геномів', _en: '{label}: {n} genomes'},
  'genomes.searchNone': {_uk: '{label}: не знайдено в жодному геномі', _en: '{label}: not found in any genome'},
  'genomes.rerun': {_uk: 'Повторити аналіз', _en: 'Re-run'},
  'genomes.rerunTitle': {_uk: 'Повторити Nextclade', _en: 'Re-run Nextclade'},
  'genomes.rerunHint': {
    _uk: 'Результати й сповіщення Nextclade цього запуску буде замінено. Так нові правила застосуються до старих зразків.',
    _en: 'Nextclade results and alerts of this run will be replaced. This applies new rules to old samples.',
  },
  'genomes.rerunUnavailable': {_uk: 'Файл цього запуску не збережено.', _en: 'The file of this run was not stored.'},
  'genomes.summary': {_uk: '{ok} з {n} геномів проаналізовано', _en: '{ok} of {n} genomes analysed'},
  'col.genome': {_uk: 'Геном', _en: 'Genome'},
  'col.clade': {_uk: 'Клада', _en: 'Clade'},
  'col.lineage': {_uk: 'Лінія', _en: 'Lineage'},
  'col.qc': {_uk: 'Якість', _en: 'QC'},
  'col.coverage': {_uk: 'Покриття', _en: 'Coverage'},
  'col.mutations': {_uk: 'Мутації', _en: 'Mutations'},
  'col.run': {_uk: 'Запуск', _en: 'Run'},
  'qc.good': {_uk: 'добра', _en: 'good'},
  'qc.mediocre': {_uk: 'посередня', _en: 'mediocre'},
  'qc.bad': {_uk: 'погана', _en: 'bad'},
  'genomes.failed': {_uk: 'не проаналізовано', _en: 'not analysed'},
  'detail.substitutions': {_uk: 'замін', _en: 'substitutions'},
  'detail.deletions': {_uk: 'делецій', _en: 'deletions'},
  'detail.protein': {_uk: 'Зміни в білках', _en: 'Protein changes'},
  'detail.nucleotide': {_uk: 'Зміни нуклеотидів', _en: 'Nucleotide changes'},
  'detail.frameshifts': {_uk: 'Зсуви рамки', _en: 'Frameshifts'},
  'detail.alerts': {_uk: 'Спрацювали правила', _en: 'Matched rules'},
  'detail.legend': {
    _uk: 'Риски — заміни, червоне — делеції, підписи — сповіщення',
    _en: 'Ticks are substitutions, red marks deletions, labels are alerts',
  },

  // --- Alerts ---
  'alerts.empty': {_uk: 'Сповіщень немає.', _en: 'No alerts.'},
  'alerts.rule': {_uk: 'Правило', _en: 'Rule'},
  'alerts.variant': {_uk: 'Варіант', _en: 'Variant'},
  'alerts.position': {_uk: 'Позиція', _en: 'Position'},
  'alerts.source': {_uk: 'Джерело', _en: 'Source'},
  'alerts.when': {_uk: 'Коли', _en: 'When'},
  'source.nextclade': {_uk: 'Nextclade', _en: 'Nextclade'},
  'source.orf': {_uk: 'Мотив ORF', _en: 'ORF motif'},
  'alerts.refHint': {_uk: 'Координати референсу набору даних', _en: 'Coordinates of the dataset reference'},
  'alerts.recordHint': {_uk: 'Координати в самому записі FASTA', _en: 'Coordinates in the FASTA record itself'},

  // --- Rules ---
  'rules.variants': {_uk: 'Варіанти й гени', _en: 'Variants & genes'},
  'rules.motifs': {_uk: 'Мотиви ORF', _en: 'ORF motifs'},
  'rules.new': {_uk: 'Нове правило', _en: 'New rule'},
  'rules.newMotif': {_uk: 'Новий мотив', _en: 'New motif'},
  'rules.name': {_uk: 'Назва', _en: 'Name'},
  'rules.notation': {_uk: 'Позначення', _en: 'Notation'},
  'rules.description': {_uk: 'Опис', _en: 'Description'},
  'rules.descriptionOptional': {_uk: 'Опис (необов\'язково)', _en: 'Description (optional)'},
  'rules.examples': {_uk: 'Приклади', _en: 'Examples'},
  'rules.add': {_uk: 'Додати', _en: 'Add'},
  'rules.empty': {_uk: 'Правил ще немає.', _en: 'No rules yet.'},
  'rules.nameRequired': {_uk: 'Вкажіть назву', _en: 'Enter a name'},
  'rules.appliesHint': {
    _uk: 'Правила діють на нові запуски. Для старих: Геноми → Повторити аналіз.',
    _en: 'Rules apply to new runs. For old ones: Genomes → Re-run.',
  },
  'rules.kind.aa': {_uk: 'Заміна в білку {gene}, позиція {pos}', _en: 'Change in protein {gene} at {pos}'},
  'rules.kind.aaDel': {_uk: 'Делеція в білку {gene}, позиція {pos}', _en: 'Deletion in protein {gene} at {pos}'},
  'rules.kind.nuc': {_uk: 'Заміна нуклеотиду в позиції {pos}', _en: 'Nucleotide change at {pos}'},
  'rules.kind.nucDel': {_uk: 'Делеція нуклеотидів у позиції {pos}', _en: 'Nucleotide deletion at {pos}'},
  'rules.kind.clade': {_uk: 'Клада {name}', _en: 'Clade {name}'},
  'rules.kind.lineage': {_uk: 'Лінія Pango {name}', _en: 'Pango lineage {name}'},
  'rules.kind.unknown': {_uk: 'Позначення не розпізнано', _en: 'Notation not recognised'},
  'motifs.sequence': {_uk: 'Амінокислоти', _en: 'Amino acids'},
  'motifs.category': {_uk: 'Тип', _en: 'Type'},
  'motifs.sequenceInvalid': {_uk: 'Однолітерні коди амінокислот, щонайменше 4', _en: 'One-letter amino-acid codes, at least 4'},
  'motifs.caveat': {
    _uk: 'Навчальний режим: точний збіг у білках ORF. Ламається, якщо поруч є ще одна мутація.',
    _en: 'Learning mode: exact match inside ORF proteins. Breaks when another mutation sits nearby.',
  },

  // --- Bacteria / AMRFinderPlus ---
  'common.bacteria': {_uk: 'Бактерія', _en: 'Bacterium'},
  'col.analysis': {_uk: 'Аналіз', _en: 'Analysis'},
  'col.datasetOrOrganism': {_uk: 'Набір / вид', _en: 'Dataset / species'},
  'upload.organism': {_uk: 'Вид бактерії', _en: 'Bacterial species'},
  'upload.organismNone': {_uk: 'Не вказано (лише гени)', _en: 'Not specified (genes only)'},
  'upload.organismHint': {
    _uk: 'AMRFinderPlus шукає гени резистентності, стресу й вірулентності. Вид додає пошук точкових мутацій (наприклад, gyrA, pmrB).',
    _en: 'AMRFinderPlus finds resistance, stress and virulence genes. The species adds point mutations (e.g. gyrA, pmrB).',
  },
  'upload.bacteriaFile': {_uk: 'Збірка геному (контиги FASTA)', _en: 'Genome assembly (FASTA contigs)'},
  'upload.sentAmr': {
    _uk: 'Прийнято. AMRFinderPlus шукає гени резистентності.',
    _en: 'Accepted. AMRFinderPlus is looking for resistance genes.',
  },
  'amr.waiting': {_uk: 'AMRFinderPlus аналізує збірку…', _en: 'AMRFinderPlus is analysing the assembly…'},
  'amr.empty': {_uk: 'Для цього запуску немає результатів AMRFinderPlus.', _en: 'This run has no AMRFinderPlus results.'},
  'amr.none': {_uk: 'Генів резистентності не знайдено.', _en: 'No resistance genes found.'},
  'amr.database': {_uk: 'база {version}', _en: 'database {version}'},
  'amr.findings': {_uk: '{n} знахідок', _en: '{n} findings'},
  'amr.resistance': {_uk: 'Прогнозована стійкість', _en: 'Predicted resistance'},
  'amr.resistanceHint': {
    _uk: 'За генами й мутаціями. Це прогноз, а не антибіотикограма: остаточне рішення — за лабораторним тестом чутливості.',
    _en: 'From genes and mutations. A prediction, not an antibiogram: confirm with a lab susceptibility test.',
  },
  'amr.drug': {_uk: 'Антибіотики', _en: 'Antibiotics'},
  'amr.genes': {_uk: 'Гени та мутації', _en: 'Genes and mutations'},
  'amr.all': {_uk: 'Усі знахідки', _en: 'All findings'},
  'amr.typeAmr': {_uk: 'Резистентність', _en: 'Resistance'},
  'amr.typeStress': {_uk: 'Стрес', _en: 'Stress'},
  'amr.typeVirulence': {_uk: 'Вірулентність', _en: 'Virulence'},
  'col.gene': {_uk: 'Ген', _en: 'Gene'},
  'col.description': {_uk: 'Опис', _en: 'Description'},
  'col.subclass': {_uk: 'Підклас', _en: 'Subclass'},
  'col.location': {_uk: 'Контиг : позиція', _en: 'Contig : position'},
  'col.identity': {_uk: 'Ідентичність', _en: 'Identity'},
  'col.method': {_uk: 'Метод', _en: 'Method'},
  'amr.point': {_uk: 'точкова мутація', _en: 'point mutation'},
  'amr.rerunTitle': {_uk: 'Повторити AMRFinderPlus', _en: 'Re-run AMRFinderPlus'},
  'amr.rerunHint': {
    _uk: 'Знахідки й сповіщення AMRFinderPlus цього запуску буде замінено.',
    _en: 'AMRFinderPlus findings and alerts of this run will be replaced.',
  },
  'amr.search': {_uk: 'Пошук гена або ST, напр. blaNDM, ST147', _en: 'Find gene or ST, e.g. blaNDM, ST147'},
  'amr.searchResult': {_uk: '{label}: {n} знахідок у запусках', _en: '{label}: {n} findings in runs'},
  'amr.searchNone': {_uk: '{label}: не знайдено в жодному запуску', _en: '{label}: not found in any run'},
  'source.amrfinder': {_uk: 'AMRFinderPlus', _en: 'AMRFinderPlus'},
  'source.mlst': {_uk: 'MLST', _en: 'MLST'},
  'alerts.mlstHint': {_uk: 'Тип штаму описує весь геном, позиції немає', _en: 'A sequence type describes the whole genome, no position'},
  'alerts.contigHint': {_uk: 'Координати в контигу збірки', _en: 'Coordinates in the assembly contig'},
  'method.ALLELEX': {_uk: 'Точний відомий алель', _en: 'Exact known allele'},
  'method.EXACTX': {_uk: 'Точний збіг з референсом', _en: 'Exact match to reference'},
  'method.EXACTP': {_uk: 'Точний збіг з референсом', _en: 'Exact match to reference'},
  'method.BLASTX': {_uk: 'Схожий на відомий ген (не точний)', _en: 'Similar to a known gene (not exact)'},
  'method.PARTIALX': {_uk: 'Частковий збіг', _en: 'Partial match'},
  'method.PARTIAL_CONTIG_ENDX': {_uk: 'Обірваний кінцем контигу', _en: 'Cut by the contig end'},
  'method.POINTX': {_uk: 'Точкова мутація в білку', _en: 'Protein point mutation'},
  'method.POINTN': {_uk: 'Точкова мутація в нуклеотидах', _en: 'Nucleotide point mutation'},
  'method.INTERNAL_STOP': {_uk: 'Внутрішній стоп-кодон: ген, імовірно, не працює', _en: 'Internal stop codon: gene likely inactive'},
  'method.HMM': {_uk: 'Знайдено за профілем родини білків', _en: 'Found by protein family profile'},
  'method.COMPLETE': {_uk: 'Повний оперон', _en: 'Complete operon'},
  'rules.kind.amrGene': {_uk: 'Ген резистентності {name}', _en: 'Resistance gene {name}'},
  'rules.kind.amrClass': {_uk: 'Клас антибіотиків {name}', _en: 'Antibiotic class {name}'},
  'rules.kind.amrSubclass': {_uk: 'Стійкість до {name}', _en: 'Resistance to {name}'},

  // Імпорт відкритих даних NCBI
  'import.button': {_uk: 'Імпорт з NCBI', _en: 'Import from NCBI'},
  'import.title': {_uk: 'Імпорт відкритих даних NCBI', _en: 'Import open NCBI data'},
  'import.source': {_uk: 'Джерело', _en: 'Source'},
  'import.accessions': {_uk: 'Номери', _en: 'Accessions'},
  'import.archive': {_uk: 'Архів NCBI Datasets', _en: 'NCBI Datasets archive'},
  'import.accessionsLabel': {_uk: 'Номери NCBI (до 500)', _en: 'NCBI accessions (up to 500)'},
  'import.hintVirus': {
    _uk: 'Номери послідовностей GenBank через пробіл або з нового рядка. Усі геноми стануть одним запуском, як файл з кількома записами.',
    _en: 'GenBank sequence accessions separated by spaces or new lines. All genomes become one run, like a multi-record file.',
  },
  'import.hintBacteria': {
    _uk: 'Номери збірок (GCA_…, GCF_…) або повних хромосом (CP…). Кожна збірка стане окремим запуском: це окремий ізолят.',
    _en: 'Assembly accessions (GCA_…, GCF_…) or complete chromosomes (CP…). Each assembly becomes its own run: it is a separate isolate.',
  },
  'import.archiveLabel': {_uk: 'ZIP-архів', _en: 'ZIP archive'},
  'import.archiveHint': {
    _uk: 'Кнопка Download на NCBI Virus чи NCBI Datasets (Genome, формат FASTA) або команда datasets download.',
    _en: 'The Download button on NCBI Virus or NCBI Datasets (Genome, FASTA) or the datasets download command.',
  },
  'import.pickArchive': {_uk: 'Обрати архів…', _en: 'Choose archive…'},
  'import.organismAuto': {_uk: 'Визначити за даними NCBI', _en: 'Detect from NCBI metadata'},
  'import.organismHint': {
    _uk: 'Вид кожної збірки береться з метаданих NCBI, якщо AMRFinderPlus його знає.',
    _en: 'The species of each assembly is taken from NCBI metadata if AMRFinderPlus supports it.',
  },
  'import.start': {_uk: 'Імпортувати', _en: 'Import'},
  'import.started': {
    _uk: 'Імпорт поставлено в чергу. Прогрес показано над таблицею запусків.',
    _en: 'Import queued. Progress is shown above the runs table.',
  },
  'import.row': {_uk: 'Імпорт NCBI', _en: 'NCBI import'},
  'import.status.0': {_uk: 'У черзі', _en: 'Queued'},
  'import.status.1': {_uk: 'Завантажується', _en: 'Downloading'},
  'import.status.2': {_uk: 'Готово', _en: 'Done'},
  'import.status.3': {_uk: 'Помилка', _en: 'Failed'},
  'import.progress': {_uk: '{done} з {total}', _en: '{done} of {total}'},
  'import.failedSome': {_uk: 'з помилкою: {n}', _en: 'failed: {n}'},

  // Нагляд за резистентністю (NCBI Pathogen Detection)
  // Сирі прочитання (FASTQ)
  'upload.fastqHint': {
    _uk: 'FASTA з геномом або контигами. Або сирі прочитання FASTQ (.fastq, .fq, можна .gz): для Illumina оберіть разом R1 і R2.',
    _en: 'FASTA with a genome or contigs. Or raw FASTQ reads (.fastq, .fq, optionally .gz): for Illumina select R1 and R2 together.',
  },
  'upload.oneFasta': {_uk: 'FASTA — по одному файлу. Кілька файлів можна лише для FASTQ.', _en: 'One FASTA at a time. Several files only for FASTQ.'},
  'upload.illuminaPairs': {_uk: 'Для Illumina — один файл або пара R1 і R2.', _en: 'For Illumina: one file or an R1/R2 pair.'},
  'upload.platform': {_uk: 'Платформа секвенування', _en: 'Sequencing platform'},
  'upload.readsVirus': {
    _uk: 'Прочитання картуються на референси всіх наборів Nextclade; вірус — той, на який лягло найбільше. Далі консенсус (N там, де глибина < 10) і Nextclade.',
    _en: 'Reads are mapped to every Nextclade reference; the virus is the one most reads map to. Then a consensus (N where depth < 10) and Nextclade.',
  },
  'upload.readsBacteriaIllumina': {
    _uk: 'fastp обрізає адаптери, SKESA збирає контиги (так само NCBI збирає ізоляти Pathogen Detection). Далі AMRFinderPlus і MLST. Займає кілька хвилин.',
    _en: 'fastp trims adapters, SKESA assembles contigs (as NCBI does for Pathogen Detection). Then AMRFinderPlus and MLST. Takes a few minutes.',
  },
  'upload.readsBacteriaOnt': {
    _uk: 'Flye збирає довгі прочитання, часто до повних хромосом і плазмід. Далі AMRFinderPlus і MLST. Займає 10–30 хвилин.',
    _en: 'Flye assembles long reads, often into complete chromosomes and plasmids. Then AMRFinderPlus and MLST. Takes 10–30 minutes.',
  },
  'upload.sentReads': {
    _uk: 'Прочитання прийнято. Прогрес — над таблицею запусків; готовий геном стане звичайним запуском.',
    _en: 'Reads accepted. Progress is shown above the runs table; the finished genome becomes a regular run.',
  },
  'import.rowReads': {_uk: 'Прочитання FASTQ', _en: 'FASTQ reads'},
  'import.status.processing': {_uk: 'Обробка', _en: 'Processing'},
  'reads.title': {_uk: 'Із сирих прочитань', _en: 'From raw reads'},
  'amr.speciesHint': {
    _uk: 'Вид не вказано, тож точкові мутації (gyrA, parC, ompK…) не шукались. За MLST (схема {scheme}) це, ймовірно, {species}.',
    _en: 'No species given, so point mutations (gyrA, parC, ompK…) were not searched. By MLST (scheme {scheme}) this is likely {species}.',
  },
  'amr.speciesRerun': {_uk: 'Повторити як {species}', _en: 'Re-run as {species}'},

  // Автовибір набору даних (nextclade sort)
  'dataset.auto': {_uk: 'Автоматично', _en: 'Auto'},
  'dataset.autoOption': {_uk: 'Визначити автоматично (nextclade sort)', _en: 'Detect automatically (nextclade sort)'},
  'dataset.unknown': {_uk: 'не визначено', _en: 'unknown'},

  // Звіт по зразку
  'report.button': {_uk: 'Звіт', _en: 'Report'},
  'report.pdf': {_uk: 'PDF для передачі', _en: 'PDF to share'},
  'report.csv': {_uk: 'CSV для таблиць і скриптів', _en: 'CSV for spreadsheets and scripts'},
  'report.saveTitle': {_uk: 'Зберегти звіт', _en: 'Save report'},
  'report.saved': {_uk: 'Звіт збережено', _en: 'Report saved'},

  // MLST: тип штаму
  'mlst.title': {_uk: 'Тип штаму (MLST)', _en: 'Sequence type (MLST)'},
  'mlst.hint': {
    _uk: 'Алелі семи генів «домашнього господарства» за схемою PubMLST. Ген показує, що є в бактерії, а ST — чий це штам: '
        'той самий ST в інших ізолятах може означати спільну лінію або спалах.',
    _en: 'Alleles of seven housekeeping genes by the PubMLST scheme. Genes tell what the bacterium carries, the ST tells which strain it is: '
        'the same ST in other isolates may mean a shared lineage or an outbreak.',
  },
  'mlst.waiting': {_uk: 'MLST визначає тип штаму…', _en: 'MLST is typing the strain…'},
  'mlst.findOthers': {_uk: 'Знайти ST{st} в інших запусках', _en: 'Find ST{st} in other runs'},
  'mlst.scheme': {_uk: 'Схема', _en: 'Scheme'},
  'mlst.searchResult': {_uk: '{label}: {n} ізолятів', _en: '{label}: {n} isolates'},
  'mlst.searchNone': {_uk: '{label}: не знайдено в жодному запуску', _en: '{label}: not found in any run'},
  'rules.kind.st': {_uk: 'Тип штаму ST{st} (будь-яка схема MLST)', _en: 'Sequence type ST{st} (any MLST scheme)'},
  'rules.kind.stScheme': {_uk: 'Тип штаму ST{st} у схемі {scheme}', _en: 'Sequence type ST{st} in scheme {scheme}'},

  'nav.surveillance': {_uk: 'Нагляд AMR', _en: 'AMR surveillance'},
  'surv.sync': {_uk: 'Оновити з NCBI', _en: 'Update from NCBI'},
  'surv.syncTitle': {_uk: 'Оновити {taxgroup} з NCBI Pathogen Detection', _en: 'Update {taxgroup} from NCBI Pathogen Detection'},
  'surv.countries': {_uk: 'Країни', _en: 'Countries'},
  'surv.countriesHint': {
    _uk: 'Англійською, як у NCBI, через кому. Наприклад, Ukraine, Poland, Germany — щоб порівняти з сусідами. '
        'Файл групи (від 14 МБ до 830 МБ) читається потоком, зберігаються лише ізоляти з цих країн.',
    _en: 'In English as in NCBI, comma-separated, e.g. Ukraine, Poland, Germany to compare with neighbours. '
        'The group file (14 MB to 830 MB) is streamed; only isolates from these countries are kept.',
  },
  'surv.syncStart': {_uk: 'Оновити', _en: 'Update'},
  'surv.syncQueued': {_uk: 'Оновлення в черзі…', _en: 'Update queued…'},
  'surv.syncRunning': {
    _uk: 'Читаю NCBI: {mb} з {total} МБ · переглянуто {rows} ізолятів · збіглося {found}',
    _en: 'Reading NCBI: {mb} of {total} MB · {rows} isolates scanned · {found} matched',
  },
  'surv.syncDone': {_uk: 'NCBI Pathogen Detection {pdg} · оновлено {when}', _en: 'NCBI Pathogen Detection {pdg} · updated {when}'},
  'surv.syncFailed': {_uk: 'Оновлення не вдалося', _en: 'Update failed'},
  'surv.emptyTitle': {_uk: 'Даних нагляду ще немає', _en: 'No surveillance data yet'},
  'surv.emptyText': {
    _uk: 'NCBI Pathogen Detection уже порахував гени резистентності тим самим AMRFinderPlus для сотень тисяч ізолятів з усього світу. '
        'Оновіть групу, щоб побачити, які карбапенемази трапляються в Україні, за роками й регіонами.',
    _en: 'NCBI Pathogen Detection has already computed resistance genes with the same AMRFinderPlus for hundreds of thousands of isolates. '
        'Update a group to see which carbapenemases occur in Ukraine, by year and region.',
  },
  'surv.isolates': {_uk: '{n} ізолятів', _en: '{n} isolates'},
  'surv.withAssembly': {_uk: 'зі збіркою геному: {n}', _en: 'with genome assembly: {n}'},
  'surv.carbapenemase': {_uk: 'з карбапенемазою', _en: 'carry a carbapenemase'},
  'surv.cephalosporinase': {_uk: 'з ESBL / цефалоспориназою', _en: 'carry an ESBL / cephalosporinase'},
  'surv.colistin': {_uk: 'зі стійкістю до колістину', _en: 'colistin resistance'},
  'surv.byYear': {_uk: 'Ізоляти за роком збору', _en: 'Isolates by collection year'},
  'surv.legendCarba': {_uk: 'з карбапенемазою', _en: 'with carbapenemase'},
  'surv.legendRest': {_uk: 'без', _en: 'without'},
  'surv.yearTooltip': {
    _uk: '{year}: {n} ізолятів, з карбапенемазою {alert} ({pct}%). Клік — фільтр таблиці',
    _en: '{year}: {n} isolates, {alert} with carbapenemase ({pct}%). Click to filter the table',
  },
  'surv.yearNone': {_uk: '{year}: ізолятів немає', _en: '{year}: no isolates'},
  'surv.carbapenemases': {_uk: 'Карбапенемази', _en: 'Carbapenemases'},
  'surv.carbapenemasesHint': {
    _uk: 'Ферменти, що руйнують карбапенеми — антибіотики резерву. Частка — від усіх ізолятів.',
    _en: 'Enzymes that destroy carbapenems, last-resort antibiotics. Share of all isolates.',
  },
  'surv.noCarbapenemases': {_uk: 'Карбапенемаз не знайдено.', _en: 'No carbapenemases found.'},
  'surv.otherGenes': {_uk: 'Найчастіші інші гени', _en: 'Most frequent other genes'},
  'surv.otherGenesHint': {_uk: 'Родини генів резистентності та клас антибіотиків.', _en: 'Resistance gene families and drug class.'},
  'surv.heatmap': {_uk: 'Карбапенемази за роками', _en: 'Carbapenemases by year'},
  'surv.heatmapHint': {
    _uk: 'Кількість ізолятів; що темніше, то більше. Порожні клітинки — ізолятів з цим геном того року немає.',
    _en: 'Number of isolates; darker means more. Empty cells: no isolates with this gene that year.',
  },
  'surv.regions': {_uk: 'Регіони', _en: 'Regions'},
  'surv.unknown': {_uk: 'не вказано', _en: 'not specified'},
  'surv.sources': {_uk: 'Джерело зразка', _en: 'Isolation source'},
  'surv.species': {_uk: 'Види', _en: 'Species'},
  'surv.table': {_uk: 'Ізоляти', _en: 'Isolates'},
  'surv.clearFilter': {_uk: 'Скинути фільтр', _en: 'Clear filter'},
  'surv.colAssembly': {_uk: 'Збірка', _en: 'Assembly'},
  'surv.colDate': {_uk: 'Дата', _en: 'Date'},
  'surv.colRegion': {_uk: 'Регіон', _en: 'Region'},
  'surv.colSource': {_uk: 'Джерело', _en: 'Source'},
  'surv.colCarba': {_uk: 'Карбапенемази', _en: 'Carbapenemases'},
  'surv.colGenes': {_uk: 'Генів', _en: 'Genes'},
  'surv.analyse': {_uk: 'Аналізувати', _en: 'Analyse'},
  'surv.open': {_uk: 'Відкрити', _en: 'Open'},
  'surv.importing': {_uk: 'Імпортується…', _en: 'Importing…'},
  'surv.noAssembly': {_uk: 'NCBI не має збірки цього ізоляту', _en: 'NCBI has no assembly for this isolate'},
  'surv.analyseStarted': {
    _uk: '{acc}: імпорт з NCBI і аналіз поставлено в чергу (сторінка «Запуски»).',
    _en: '{acc}: NCBI import and analysis queued (Runs page).',
  },
  'surv.more': {_uk: 'Ще {n} — звузьте фільтром.', _en: '{n} more — narrow with a filter.'},
  'surv.footer': {
    _uk: 'Джерело: NCBI Pathogen Detection {pdg}, гени пораховано AMRFinderPlus {amrfinder} (бази {db}). '
        'Це дані, які лабораторії самі опублікували: вибірка не репрезентативна для країни, а ген — прогноз, не антибіотикограма.',
    _en: 'Source: NCBI Pathogen Detection {pdg}, genes by AMRFinderPlus {amrfinder} (databases {db}). '
        'These are isolates labs chose to publish: not a representative sample, and a gene is a prediction, not an antibiogram.',
  },
  'nav.explorer': {_uk: 'Explorer', _en: 'Explorer'},
  'explorer.openFile': {_uk: 'Відкрити файл', _en: 'Open file'},
  'explorer.open': {_uk: 'Відкрити', _en: 'Open'},
  'explorer.paste': {_uk: 'Вставити текст', _en: 'Paste text'},
  'explorer.fromNcbi': {_uk: 'З NCBI', _en: 'From NCBI'},
  'explorer.ncbiTitle': {_uk: 'Відкрити запис GenBank за номером', _en: 'Open a GenBank record by accession'},
  'explorer.ncbiExamples': {_uk: 'Або візьміть приклад:', _en: 'Or try an example:'},
  'explorer.ex.pbr322': {_uk: 'плазміда pBR322, кільцева: гени стійкості bla і tet', _en: 'plasmid pBR322, circular: resistance genes bla and tet'},
  'explorer.ex.phix': {
    _uk: 'фаг phiX174, кільцевий: гени вкладені один в одного і проходять через «нуль»',
    _en: 'phage phiX174, circular: genes nested inside each other and crossing the origin',
  },
  'explorer.ex.mito': {
    _uk: 'мітохондрії людини: код «Мітохондрії» вмикається з анотації, бо TGA там — триптофан, а не стоп',
    _en: 'human mitochondrion: the mitochondrial code comes from the annotation, TGA is tryptophan there, not a stop',
  },
  'explorer.ex.hiv': {_uk: 'ВІЛ-1 HXB2: гени gag, pol, env перекриваються в різних рамках', _en: 'HIV-1 HXB2: gag, pol and env overlap in different frames'},
  'explorer.ex.lambda': {_uk: 'фаг лямбда, 48,5 тис. нт: щільні гени на обох ланцюгах', _en: 'phage lambda, 48.5 kb: dense genes on both strands'},
  'explorer.ex.sars': {_uk: 'SARS-CoV-2: зсув рамки ORF1ab і старт Spike', _en: 'SARS-CoV-2: the ORF1ab frameshift and the Spike start'},
  'explorer.pasteTitle': {_uk: 'Вставте FASTA, GenBank або просто послідовність', _en: 'Paste FASTA, GenBank or a bare sequence'},
  'explorer.pasted': {_uk: 'вставлений текст', _en: 'pasted text'},
  'explorer.introTitle': {_uk: 'Навчальний режим: від ДНК до білка', _en: 'Learning mode: from DNA to protein'},
  'explorer.introText': {
    _uk: 'Відкрийте FASTA чи GenBank (геном вірусу, плазміду, ген). Explorer знайде відкриті рамки зчитування (ORF) у шести рамках, '
        'покаже їх поруч з анотованими генами, перекладе ORF у білок, знайде мотив, 3D-структуру в PDB і AlphaFold '
        'та порівняє дві послідовності. Нічого не зберігається в базі.',
    _en: 'Open a FASTA or GenBank file (a viral genome, a plasmid, a gene). Explorer finds open reading frames (ORFs) in all six frames, '
        'shows them next to the annotated genes, translates an ORF into protein, finds a motif, a 3D structure in PDB and AlphaFold, '
        'and compares two sequences. Nothing is stored in the database.',
  },
  'explorer.nt': {_uk: 'нт', _en: 'nt'},
  'explorer.aa': {_uk: 'ак', _en: 'aa'},
  'explorer.circular': {_uk: 'кільцева', _en: 'circular'},
  'explorer.linear': {_uk: 'лінійна', _en: 'linear'},
  'explorer.topology': {_uk: 'Молекула', _en: 'Molecule'},
  'explorer.minLength': {_uk: 'Мін. довжина, нт', _en: 'Min. length, nt'},
  'explorer.code': {_uk: 'Генетичний код', _en: 'Genetic code'},
  'explorer.code1': {_uk: 'Стандартний', _en: 'Standard'},
  'explorer.code11': {_uk: 'Бактерії', _en: 'Bacteria'},
  'explorer.code2': {_uk: 'Мітохондрії', _en: 'Mitochondria'},
  'explorer.altStarts': {_uk: 'Альтернативні старти', _en: 'Alternative starts'},
  'explorer.altStartsHint': {
    _uk: 'Крім ATG, старт-кодонами вважаються й ті, що дозволяє таблиця коду (у бактерій GTG і TTG).',
    _en: 'Besides ATG, also accept the start codons the code table allows (GTG and TTG in bacteria).',
  },
  'explorer.orfCount': {_uk: '{n} ORF від {min} нт', _en: '{n} ORFs of {min} nt or longer'},
  'explorer.cdsCount': {_uk: '{n} анотованих CDS, {match} ORF збігаються з ними', _en: '{n} annotated CDS, {match} ORFs match them'},
  'explorer.truncated': {_uk: 'показано лише найдовші', _en: 'only the longest are shown'},
  'explorer.legendCds': {_uk: 'Анотований ген (CDS)', _en: 'Annotated gene (CDS)'},
  'explorer.legendOrfCds': {_uk: 'ORF, що збігається з геном', _en: 'ORF matching a gene'},
  'explorer.legendOrf': {_uk: 'ORF без анотації', _en: 'ORF without annotation'},
  'explorer.lesson': {
    _uk: 'ORF — ділянка від старт-кодону до стоп-кодону в одній рамці. Не кожен ORF — справжній ген: короткі трапляються випадково, '
        'а справжній старт може бути пізніше (Spike SARS-CoV-2 починається на 9 кодонів пізніше за перший ATG). '
        'Гени зі зсувом рамки (ORF1ab) видно як два ORF у різних рамках. Риска — старт-кодон, вона показує напрямок.',
    _en: 'An ORF runs from a start codon to a stop codon in one frame. Not every ORF is a real gene: short ones occur by chance, '
        'and the real start can be later (SARS-CoV-2 Spike starts 9 codons after the first ATG). '
        'Frameshifted genes (ORF1ab) show up as two ORFs in different frames. The tick marks the start codon and shows direction.',
  },
  'explorer.strand': {_uk: 'Ланцюг', _en: 'Strand'},
  'explorer.forward': {_uk: 'прямий (+)', _en: 'forward (+)'},
  'explorer.reverse': {_uk: 'зворотний (−)', _en: 'reverse (−)'},
  'explorer.length': {_uk: 'Довжина', _en: 'Length'},
  'explorer.wraps': {_uk: 'Через «нуль»', _en: 'Across the origin'},
  'explorer.wrapsText': {_uk: 'ORF проходить через початок кільцевої молекули', _en: 'The ORF crosses the start of the circular molecule'},
  'explorer.annotation': {_uk: 'Анотація', _en: 'Annotation'},
  'explorer.noCds': {_uk: 'не збігається з жодним анотованим геном', _en: 'matches no annotated gene'},
  'explorer.sameCds': {_uk: 'точно ген {gene}', _en: 'exactly gene {gene}'},
  'explorer.longerCds': {_uk: 'той самий стоп, що й у гена {gene}, але інший старт', _en: 'same stop as gene {gene}, but a different start'},
  'explorer.type': {_uk: 'Тип', _en: 'Type'},
  'explorer.product': {_uk: 'Продукт', _en: 'Product'},
  'explorer.join': {_uk: 'Сегменти', _en: 'Segments'},
  'explorer.joinText': {_uk: '{n} шматки (join): зсув рамки чи сплайсинг', _en: '{n} pieces (join): a frameshift or splicing'},
  'explorer.protein': {_uk: 'Білок', _en: 'Protein'},
  'explorer.copy': {_uk: 'Копіювати', _en: 'Copy'},
  'explorer.motif': {_uk: 'Мотив, напр. RRAR', _en: 'Motif, e.g. RRAR'},
  'explorer.motifNone': {_uk: 'Мотив не знайдено', _en: 'Motif not found'},
  'explorer.motifFound': {_uk: 'Знайдено: {n}', _en: 'Found: {n}'},
  'explorer.structure': {_uk: '3D-структура', _en: '3D structure'},
  'explorer.structureHint': {
    _uk: 'Пошук схожих білків у RCSB PDB (експериментальні структури) і модель AlphaFold. Потрібен інтернет.',
    _en: 'Searches RCSB PDB for similar proteins (experimental structures) and the AlphaFold model. Needs internet.',
  },
  'explorer.findStructure': {_uk: 'Знайти 3D-структуру', _en: 'Find 3D structure'},
  'explorer.noStructures': {_uk: 'Схожих структур не знайдено', _en: 'No similar structures found'},
  'explorer.identity': {_uk: 'ідентичність {pct}%', _en: '{pct}% identity'},
  'explorer.covers': {_uk: 'амінокислоти {from}–{to}', _en: 'residues {from}–{to}'},
  'explorer.nucleotides': {_uk: 'Нуклеотиди', _en: 'Nucleotides'},
  'explorer.compare': {_uk: 'Порівняти', _en: 'Compare'},
  'explorer.compareTitle': {_uk: 'Порівняння послідовностей', _en: 'Compare sequences'},
  'explorer.compareOrf': {_uk: 'ORF ↔ ген', _en: 'ORF ↔ gene'},
  'explorer.compareOrfHint': {
    _uk: 'Білок вибраного ORF проти анотованого білка гена {gene}: видно, чи збігаються вони і на скільки ORF довший.',
    _en: 'The selected ORF protein against the annotated {gene} protein: shows whether they match and how much longer the ORF is.',
  },
  'explorer.compareRecords': {_uk: 'Два записи файлу', _en: 'Two records'},
  'explorer.compareDna': {_uk: 'ДНК', _en: 'DNA'},
  'explorer.compareProtein': {_uk: 'Білки', _en: 'Proteins'},
  'explorer.compareRun': {_uk: 'Вирівняти', _en: 'Align'},
  'explorer.identityResult': {_uk: 'Ідентичність {pct}%', _en: 'Identity {pct}%'},
  'explorer.diffCounts': {
    _uk: 'замін {subs}, вставок {ins}, делецій {del}, невідомих {n}',
    _en: '{subs} substitutions, {ins} insertions, {del} deletions, {n} unknown',
  },
  'explorer.identical': {_uk: 'Послідовності ідентичні', _en: 'The sequences are identical'},
  'explorer.identicalCore': {
    _uk: 'У спільній частині відмінностей немає; {longer} довша на {n} на кінцях (кінцеві розриви не рахуються).',
    _en: 'No differences in the shared part; {longer} is {n} longer at the ends (end gaps are not counted).',
  },
};
