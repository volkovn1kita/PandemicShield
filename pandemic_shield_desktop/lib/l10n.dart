import 'package:flutter/widgets.dart';

enum AppLanguage { uk, en }

/// Мова інтерфейсу. Перемикач у бічній панелі змінює значення, і весь застосунок перебудовується.
class LanguageController extends ValueNotifier<AppLanguage> {
  LanguageController() : super(AppLanguage.uk);

  void toggle() => value = value == AppLanguage.uk ? AppLanguage.en : AppLanguage.uk;
}

class L10n extends InheritedNotifier<LanguageController> {
  const L10n({super.key, required LanguageController controller, required super.child}) : super(notifier: controller);

  static LanguageController controllerOf(BuildContext context) => context.dependOnInheritedWidgetOfExactType<L10n>()!.notifier!;

  static AppLanguage languageOf(BuildContext context) => controllerOf(context).value;
}

extension Tr on BuildContext {
  /// Переклад за ключем. Параметри підставляються замість {name}.
  String tr(String key, [Map<String, Object?> args = const {}]) {
    final language = L10n.languageOf(this);
    String text = _strings[key]?[language] ?? _strings[key]?[AppLanguage.en] ?? key;
    args.forEach((name, value) => text = text.replaceAll('{$name}', '$value'));
    return text;
  }
}

const Map<String, Map<AppLanguage, String>> _strings = {
  // --- Shell ---
  'app.tagline': {AppLanguage.uk: 'Геномний нагляд', AppLanguage.en: 'Genomic surveillance'},
  'nav.overview': {AppLanguage.uk: 'Огляд', AppLanguage.en: 'Overview'},
  'nav.samples': {AppLanguage.uk: 'Зразки', AppLanguage.en: 'Samples'},
  'nav.rules': {AppLanguage.uk: 'Правила', AppLanguage.en: 'Variant rules'},
  'nav.motifs': {AppLanguage.uk: 'Мотиви ORF', AppLanguage.en: 'ORF motifs'},
  'status.online': {AppLanguage.uk: 'API на зв\'язку', AppLanguage.en: 'API online'},
  'status.offline': {AppLanguage.uk: 'API недоступний', AppLanguage.en: 'API offline'},
  'disclaimer': {AppLanguage.uk: 'Дослідницький інструмент. Не для діагностики.', AppLanguage.en: 'Research tool. Not for diagnosis.'},

  // --- Common ---
  'common.delete': {AppLanguage.uk: 'Видалити', AppLanguage.en: 'Delete'},
  'common.cancel': {AppLanguage.uk: 'Скасувати', AppLanguage.en: 'Cancel'},
  'common.close': {AppLanguage.uk: 'Закрити', AppLanguage.en: 'Close'},
  'common.connectionFailed': {
    AppLanguage.uk: 'Немає зв\'язку з API. Перевірте, чи запущено docker-compose.',
    AppLanguage.en: 'Cannot reach the API. Is docker-compose running?',
  },
  'common.anyDataset': {AppLanguage.uk: 'Будь-який набір даних', AppLanguage.en: 'Any dataset'},
  'common.virus': {AppLanguage.uk: 'Вірус', AppLanguage.en: 'Virus'},
  'common.human': {AppLanguage.uk: 'Людина', AppLanguage.en: 'Human'},
  'common.justNow': {AppLanguage.uk: 'щойно', AppLanguage.en: 'just now'},
  'common.minutesAgo': {AppLanguage.uk: '{n} хв тому', AppLanguage.en: '{n} min ago'},
  'common.hoursAgo': {AppLanguage.uk: '{n} год тому', AppLanguage.en: '{n} h ago'},

  // --- Overview ---
  'overview.title': {AppLanguage.uk: 'Огляд', AppLanguage.en: 'Overview'},
  'overview.subtitle': {
    AppLanguage.uk: 'Завантажте геноми, і система знайде клади, лінії та мутації, що вас цікавлять.',
    AppLanguage.en: 'Upload genomes and the system finds clades, lineages and the mutations you care about.',
  },
  'kpi.runs': {AppLanguage.uk: 'Запусків', AppLanguage.en: 'Runs'},
  'kpi.sequences': {AppLanguage.uk: 'Геномів', AppLanguage.en: 'Genomes'},
  'kpi.alerts': {AppLanguage.uk: 'Сповіщень', AppLanguage.en: 'Alerts'},
  'kpi.rules': {AppLanguage.uk: 'Активних правил', AppLanguage.en: 'Active rules'},
  'upload.title': {AppLanguage.uk: 'Новий аналіз', AppLanguage.en: 'New analysis'},
  'upload.step1': {AppLanguage.uk: 'Що в зразку', AppLanguage.en: 'Sample type'},
  'upload.step2': {AppLanguage.uk: 'Аналіз Nextclade', AppLanguage.en: 'Nextclade analysis'},
  'upload.step2.hint': {
    AppLanguage.uk: 'Вирівнювання на референс: клада, лінія, контроль якості, усі мутації.',
    AppLanguage.en: 'Alignment to a reference: clade, lineage, QC and every mutation.',
  },
  'upload.noNextclade': {AppLanguage.uk: 'Без Nextclade (лише пошук мотивів у ORF)', AppLanguage.en: 'No Nextclade (ORF motif scan only)'},
  'upload.humanHint': {
    AppLanguage.uk: 'Для людських послідовностей працює лише пошук мотивів у ORF (CDS/мРНК).',
    AppLanguage.en: 'Human sequences use the ORF motif scan only (CDS/mRNA).',
  },
  'upload.step3': {AppLanguage.uk: 'Файл FASTA', AppLanguage.en: 'FASTA file'},
  'upload.pick': {AppLanguage.uk: 'Натисніть, щоб обрати .fasta', AppLanguage.en: 'Click to choose a .fasta file'},
  'upload.pickHint': {AppLanguage.uk: 'Один або багато геномів в одному файлі', AppLanguage.en: 'One or many genomes in a single file'},
  'upload.start': {AppLanguage.uk: 'Запустити аналіз', AppLanguage.en: 'Start analysis'},
  'upload.sent': {
    AppLanguage.uk: 'Файл прийнято: {records} записів, {bases} нуклеотидів.',
    AppLanguage.en: 'File accepted: {records} records, {bases} bases.',
  },
  'upload.sentNextclade': {
    AppLanguage.uk: 'Файл прийнято. Результати Nextclade з\'являться на сторінці «Зразки».',
    AppLanguage.en: 'File accepted. Nextclade results will appear on the Samples page.',
  },
  'upload.error': {AppLanguage.uk: 'Помилка API: {message}', AppLanguage.en: 'API error: {message}'},
  'recentRuns.title': {AppLanguage.uk: 'Останні запуски', AppLanguage.en: 'Recent runs'},
  'recentRuns.empty': {AppLanguage.uk: 'Ще немає жодного запуску', AppLanguage.en: 'No runs yet'},
  'alerts.title': {AppLanguage.uk: 'Сповіщення', AppLanguage.en: 'Alerts'},
  'alerts.subtitle': {
    AppLanguage.uk: 'Спрацювання правил і мотивів у ваших зразках',
    AppLanguage.en: 'Rule and motif hits in your samples',
  },
  'alerts.empty': {AppLanguage.uk: 'Сповіщень немає. Це добре.', AppLanguage.en: 'No alerts. That is good news.'},
  'alerts.top': {AppLanguage.uk: 'Найчастіші', AppLanguage.en: 'Most frequent'},
  'source.nextclade': {AppLanguage.uk: 'Nextclade', AppLanguage.en: 'Nextclade'},
  'source.orf': {AppLanguage.uk: 'ORF-мотив', AppLanguage.en: 'ORF motif'},

  // --- Run status ---
  'nc.none': {AppLanguage.uk: 'Без Nextclade', AppLanguage.en: 'No Nextclade'},
  'nc.queued': {AppLanguage.uk: 'У черзі', AppLanguage.en: 'Queued'},
  'nc.running': {AppLanguage.uk: 'Аналізується', AppLanguage.en: 'Running'},
  'nc.completed': {AppLanguage.uk: 'Готово', AppLanguage.en: 'Done'},
  'nc.failed': {AppLanguage.uk: 'Помилка', AppLanguage.en: 'Failed'},
  'run.records': {AppLanguage.uk: '{n} записів', AppLanguage.en: '{n} records'},

  // --- Samples ---
  'samples.title': {AppLanguage.uk: 'Зразки', AppLanguage.en: 'Samples'},
  'samples.subtitle': {
    AppLanguage.uk: 'Результати Nextclade для кожного геному: клада, лінія Pango, якість і мутації.',
    AppLanguage.en: 'Nextclade results per genome: clade, Pango lineage, quality and mutations.',
  },
  'samples.runs': {AppLanguage.uk: 'Запуски', AppLanguage.en: 'Runs'},
  'samples.selectRun': {AppLanguage.uk: 'Оберіть запуск ліворуч', AppLanguage.en: 'Select a run on the left'},
  'samples.noResults': {
    AppLanguage.uk: 'Для цього запуску немає результатів Nextclade',
    AppLanguage.en: 'No Nextclade results for this run',
  },
  'samples.waiting': {AppLanguage.uk: 'Nextclade аналізує геноми…', AppLanguage.en: 'Nextclade is analysing genomes…'},
  'samples.rerun': {AppLanguage.uk: 'Повторити аналіз', AppLanguage.en: 'Re-run'},
  'samples.rerunTitle': {AppLanguage.uk: 'Повторний аналіз Nextclade', AppLanguage.en: 'Re-run Nextclade'},
  'samples.rerunHint': {
    AppLanguage.uk:
        'Попередні результати й сповіщення Nextclade цього запуску буде замінено. Так нові правила застосуються до старих зразків.',
    AppLanguage.en: 'Previous Nextclade results and alerts of this run will be replaced. This applies new rules to old samples.',
  },
  'samples.rerunStart': {AppLanguage.uk: 'Запустити', AppLanguage.en: 'Run'},
  'samples.rerunUnavailable': {
    AppLanguage.uk: 'Файл цього запуску не збережено: повторний аналіз недоступний.',
    AppLanguage.en: 'The file of this run was not stored: re-run is unavailable.',
  },
  'samples.search': {
    AppLanguage.uk: 'Пошук мутації в усіх зразках: S:E484K, A23403G, S:F157-…',
    AppLanguage.en: 'Find a mutation in all samples: S:E484K, A23403G, S:F157-…',
  },
  'samples.searchNone': {AppLanguage.uk: 'Жоден зразок не має {label}', AppLanguage.en: 'No sample carries {label}'},
  'samples.searchFound': {AppLanguage.uk: '{label} є в {n} зразках', AppLanguage.en: '{label} found in {n} samples'},
  'col.sequence': {AppLanguage.uk: 'Геном', AppLanguage.en: 'Genome'},
  'col.clade': {AppLanguage.uk: 'Клада', AppLanguage.en: 'Clade'},
  'col.lineage': {AppLanguage.uk: 'Лінія', AppLanguage.en: 'Lineage'},
  'col.qc': {AppLanguage.uk: 'Якість', AppLanguage.en: 'QC'},
  'col.coverage': {AppLanguage.uk: 'Покриття', AppLanguage.en: 'Coverage'},
  'col.mutations': {AppLanguage.uk: 'Мутацій', AppLanguage.en: 'Mutations'},
  'col.alerts': {AppLanguage.uk: 'Сповіщення', AppLanguage.en: 'Alerts'},
  'qc.good': {AppLanguage.uk: 'добра', AppLanguage.en: 'good'},
  'qc.mediocre': {AppLanguage.uk: 'посередня', AppLanguage.en: 'mediocre'},
  'qc.bad': {AppLanguage.uk: 'погана', AppLanguage.en: 'bad'},
  'samples.notAnalysed': {AppLanguage.uk: 'Не проаналізовано', AppLanguage.en: 'Not analysed'},
  'detail.genomeMap': {AppLanguage.uk: 'Карта геному', AppLanguage.en: 'Genome map'},
  'detail.genomeMapHint': {
    AppLanguage.uk: 'Риски — заміни нуклеотидів, червоне — делеції, підписи — сповіщення',
    AppLanguage.en: 'Ticks are substitutions, red marks deletions, labels are alerts',
  },
  'detail.aa': {AppLanguage.uk: 'Зміни в білках', AppLanguage.en: 'Protein changes'},
  'detail.nuc': {AppLanguage.uk: 'Зміни нуклеотидів ({n})', AppLanguage.en: 'Nucleotide changes ({n})'},
  'detail.frameshifts': {AppLanguage.uk: 'Зсуви рамки', AppLanguage.en: 'Frameshifts'},
  'detail.coverage': {AppLanguage.uk: 'покриття {value}', AppLanguage.en: 'coverage {value}'},

  // --- Rules ---
  'rules.title': {AppLanguage.uk: 'Правила сповіщень', AppLanguage.en: 'Alert rules'},
  'rules.subtitle': {
    AppLanguage.uk: 'Кажуть системі, на що звертати увагу в результатах Nextclade.',
    AppLanguage.en: 'Tell the system what to look for in Nextclade results.',
  },
  'rules.new': {AppLanguage.uk: 'Нове правило', AppLanguage.en: 'New rule'},
  'rules.name': {AppLanguage.uk: 'Назва', AppLanguage.en: 'Name'},
  'rules.notation': {AppLanguage.uk: 'Позначення варіанта', AppLanguage.en: 'Variant notation'},
  'rules.description': {AppLanguage.uk: 'Опис (необов\'язково)', AppLanguage.en: 'Description (optional)'},
  'rules.examples': {AppLanguage.uk: 'Приклади (натисніть, щоб вставити)', AppLanguage.en: 'Examples (click to insert)'},
  'rules.add': {AppLanguage.uk: 'Додати правило', AppLanguage.en: 'Add rule'},
  'rules.active': {AppLanguage.uk: 'Активні правила', AppLanguage.en: 'Active rules'},
  'rules.activeHint': {
    AppLanguage.uk: 'Діють на нові запуски. Щоб застосувати до старих, повторіть аналіз на сторінці «Зразки».',
    AppLanguage.en: 'Apply to new runs. To apply to old ones, re-run them on the Samples page.',
  },
  'rules.empty': {AppLanguage.uk: 'Правил ще немає', AppLanguage.en: 'No rules yet'},
  'rules.nameRequired': {AppLanguage.uk: 'Вкажіть назву', AppLanguage.en: 'Enter a name'},
  'rules.kind.aa': {AppLanguage.uk: 'Заміна в білку {gene}, позиція {pos}', AppLanguage.en: 'Protein change in {gene}, position {pos}'},
  'rules.kind.aaDel': {
    AppLanguage.uk: 'Делеція в білку {gene}, позиція {pos}',
    AppLanguage.en: 'Protein deletion in {gene}, position {pos}',
  },
  'rules.kind.nuc': {AppLanguage.uk: 'Заміна нуклеотиду, позиція {pos}', AppLanguage.en: 'Nucleotide change at {pos}'},
  'rules.kind.nucDel': {AppLanguage.uk: 'Делеція нуклеотидів, позиція {pos}', AppLanguage.en: 'Nucleotide deletion at {pos}'},
  'rules.kind.clade': {AppLanguage.uk: 'Клада {name}', AppLanguage.en: 'Clade {name}'},
  'rules.kind.lineage': {AppLanguage.uk: 'Лінія Pango {name}', AppLanguage.en: 'Pango lineage {name}'},
  'rules.kind.unknown': {AppLanguage.uk: 'Не вдалося розпізнати позначення', AppLanguage.en: 'Notation not recognised'},

  // --- Motifs ---
  'motifs.title': {AppLanguage.uk: 'Мотиви ORF', AppLanguage.en: 'ORF motifs'},
  'motifs.subtitle': {
    AppLanguage.uk: 'Навчальний режим: точна амінокислотна послідовність у білках відкритих рамок зчитування.',
    AppLanguage.en: 'Learning mode: an exact amino-acid sequence inside proteins of open reading frames.',
  },
  'motifs.new': {AppLanguage.uk: 'Новий мотив', AppLanguage.en: 'New motif'},
  'motifs.name': {AppLanguage.uk: 'Назва', AppLanguage.en: 'Name'},
  'motifs.sequence': {AppLanguage.uk: 'Амінокислоти (однолітерні коди)', AppLanguage.en: 'Amino acids (one-letter codes)'},
  'motifs.sequenceInvalid': {
    AppLanguage.uk: 'Лише однолітерні коди амінокислот, щонайменше 4',
    AppLanguage.en: 'One-letter amino-acid codes only, at least 4',
  },
  'motifs.add': {AppLanguage.uk: 'Додати мотив', AppLanguage.en: 'Add motif'},
  'motifs.list': {AppLanguage.uk: 'Мотиви', AppLanguage.en: 'Motifs'},
  'motifs.empty': {AppLanguage.uk: 'Мотивів ще немає', AppLanguage.en: 'No motifs yet'},
  'motifs.caveat': {
    AppLanguage.uk: 'Мотив перестає знаходитися, якщо поруч є ще одна мутація. Для справжнього нагляду використовуйте правила Nextclade.',
    AppLanguage.en: 'A motif stops matching when another mutation sits nearby. Use Nextclade rules for real surveillance.',
  },
};
