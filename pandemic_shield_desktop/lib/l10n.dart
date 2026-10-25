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
  'rules.variants': {_uk: 'Варіанти Nextclade', _en: 'Nextclade variants'},
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
};
