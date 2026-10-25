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
  'amr.search': {_uk: 'Пошук гена, напр. blaNDM', _en: 'Find gene, e.g. blaNDM'},
  'amr.searchResult': {_uk: '{label}: {n} знахідок у запусках', _en: '{label}: {n} findings in runs'},
  'amr.searchNone': {_uk: '{label}: не знайдено в жодному запуску', _en: '{label}: not found in any run'},
  'source.amrfinder': {_uk: 'AMRFinderPlus', _en: 'AMRFinderPlus'},
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
};
