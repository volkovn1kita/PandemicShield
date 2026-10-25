import 'package:flutter/material.dart';

import 'l10n.dart';
import 'theme.dart';

// ==========================================
// LAYOUT
// ==========================================

/// Верхня панель сторінки: заголовок ліворуч, дії праворуч, тонка межа знизу.
class TopBar extends StatelessWidget {
  final Widget title;
  final List<Widget> actions;

  const TopBar({super.key, required this.title, this.actions = const []});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return Container(
      height: 52,
      padding: const EdgeInsets.symmetric(horizontal: 20),
      decoration: BoxDecoration(
        border: Border(bottom: BorderSide(color: p.border)),
      ),
      child: Row(
        children: [
          Expanded(
            child: DefaultTextStyle.merge(
              style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: p.text),
              child: title,
            ),
          ),
          for (final action in actions) Padding(padding: const EdgeInsets.only(left: 8), child: action),
        ],
      ),
    );
  }
}

/// Колонка таблиці: або частка ширини (flex), або фіксована ширина.
class Col {
  final String label;
  final int flex;
  final double? width;
  final bool alignRight;

  /// Другорядна колонка ховається, коли таблиця вужча за це значення (вузьке вікно): решта не налазить одна на одну.
  final double? hideBelow;

  const Col(this.label, {this.flex = 1, this.width, this.alignRight = false, this.hideBelow});
}

/// Номери колонок, видимих при ширині таблиці [width].
List<int> _visibleColumns(List<Col> columns, double width) => [
  for (int i = 0; i < columns.length; i++)
    if (columns[i].hideBelow == null || width >= columns[i].hideBelow!) i,
];

/// Кожна комірка має відступ 16 px праворуч, щоб тексти сусідніх колонок не торкались;
/// тому правий відступ рядка — лише 4 px (разом із комірковим виходить 20, як зліва).
Widget _cell(Col col, Widget child) {
  final aligned = Padding(
    padding: const EdgeInsets.only(right: 16),
    child: Align(alignment: col.alignRight ? Alignment.centerRight : Alignment.centerLeft, child: child),
  );
  return col.width != null ? SizedBox(width: col.width, child: aligned) : Expanded(flex: col.flex, child: aligned);
}

class TableHeader extends StatelessWidget {
  final List<Col> columns;
  const TableHeader(this.columns, {super.key});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return Container(
      height: 34,
      padding: const EdgeInsets.only(left: 20, right: 4),
      decoration: BoxDecoration(
        border: Border(bottom: BorderSide(color: p.border)),
      ),
      child: LayoutBuilder(
        builder: (context, constraints) => Row(
          children: [
            for (final i in _visibleColumns(columns, constraints.maxWidth))
              _cell(
                columns[i],
                Text(
                  columns[i].label,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontSize: 12, color: p.muted, fontWeight: FontWeight.w500),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

/// Рядок таблиці: підсвічування при наведенні, виділення, тонкий роздільник.
class TableRowItem extends StatelessWidget {
  final List<Col> columns;
  final List<Widget> cells;
  final VoidCallback? onTap;
  final bool selected;

  const TableRowItem({super.key, required this.columns, required this.cells, this.onTap, this.selected = false});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return Material(
      color: selected ? p.selected : Colors.transparent,
      child: InkWell(
        onTap: onTap,
        hoverColor: p.hover,
        mouseCursor: onTap == null ? SystemMouseCursors.basic : SystemMouseCursors.click,
        child: Container(
          constraints: const BoxConstraints(minHeight: 40),
          padding: const EdgeInsets.only(left: 20, right: 4, top: 6, bottom: 6),
          decoration: BoxDecoration(
            border: Border(bottom: BorderSide(color: p.border.withValues(alpha: 0.6))),
          ),
          child: LayoutBuilder(
            builder: (context, constraints) =>
                Row(children: [for (final i in _visibleColumns(columns, constraints.maxWidth)) _cell(columns[i], cells[i])]),
          ),
        ),
      ),
    );
  }
}

class EmptyHint extends StatelessWidget {
  final String text;
  final Widget? action;
  const EmptyHint(this.text, {super.key, this.action});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            text,
            textAlign: TextAlign.center,
            style: TextStyle(color: context.p.muted, fontSize: 13),
          ),
          if (action != null) ...[const SizedBox(height: 12), action!],
        ],
      ),
    );
  }
}

/// Підпис поля над ним (як у Linear/GitHub), а не плаваюча мітка всередині.
class FieldLabel extends StatelessWidget {
  final String text;
  final Widget child;
  final String? hint;
  const FieldLabel(this.text, {super.key, required this.child, this.hint});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          text,
          style: TextStyle(fontSize: 12.5, fontWeight: FontWeight.w500, color: p.text),
        ),
        const SizedBox(height: 6),
        child,
        if (hint != null) ...[const SizedBox(height: 6), Text(hint!, style: TextStyle(fontSize: 12, color: p.muted, height: 1.4))],
      ],
    );
  }
}

/// Компактний сегментований перемикач.
class ChoiceTabs<T> extends StatelessWidget {
  final List<(T, String)> options;
  final T value;
  final ValueChanged<T> onChanged;

  const ChoiceTabs({super.key, required this.options, required this.value, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return Container(
      padding: const EdgeInsets.all(2),
      decoration: BoxDecoration(
        color: p.hover,
        borderRadius: BorderRadius.circular(7),
        border: Border.all(color: p.border),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          for (final (option, label) in options)
            GestureDetector(
              onTap: () => onChanged(option),
              child: MouseRegion(
                cursor: SystemMouseCursors.click,
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 5),
                  decoration: BoxDecoration(
                    color: option == value ? p.surface : Colors.transparent,
                    borderRadius: BorderRadius.circular(5),
                    border: Border.all(color: option == value ? p.border : Colors.transparent),
                  ),
                  child: Text(
                    label,
                    style: TextStyle(
                      fontSize: 12.5,
                      fontWeight: option == value ? FontWeight.w600 : FontWeight.w400,
                      color: option == value ? p.text : p.muted,
                    ),
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}

// ==========================================
// DOMAIN LABELS (колір лише для змісту)
// ==========================================

class CladeLabel extends StatelessWidget {
  final String? clade;
  final String? display;
  const CladeLabel({super.key, this.clade, this.display});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    if (clade == null && display == null) return Text('—', style: TextStyle(color: p.faint));
    final text = display ?? clade!;
    return Tooltip(
      message: text,
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 9,
            height: 9,
            decoration: BoxDecoration(color: cladeColor(clade ?? display, p), borderRadius: BorderRadius.circular(2.5)),
          ),
          const SizedBox(width: 8),
          Flexible(
            child: Text(
              text,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(fontSize: 13, color: p.text),
            ),
          ),
        ],
      ),
    );
  }
}

class QcLabel extends StatelessWidget {
  final String? status;
  const QcLabel({super.key, this.status});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    if (status == null) return Text('—', style: TextStyle(color: p.faint));
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 6,
          height: 6,
          decoration: BoxDecoration(color: p.forQc(status), shape: BoxShape.circle),
        ),
        const SizedBox(width: 7),
        Text(context.tr('qc.$status'), style: TextStyle(fontSize: 13, color: p.text)),
      ],
    );
  }
}

/// Статус Nextclade запуску (NotRequested, Queued, Running, Completed, Failed): текст, без «пігулки».
class RunStatusLabel extends StatelessWidget {
  final int status;
  final String? message;
  const RunStatusLabel({super.key, required this.status, this.message});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final (key, color, busy) = switch (status) {
      1 => ('nc.queued', p.muted, true),
      2 => ('nc.running', p.text, true),
      3 => ('nc.completed', p.success, false),
      4 => ('nc.failed', p.danger, false),
      _ => ('nc.none', p.faint, false),
    };
    final label = Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        if (busy)
          Padding(
            padding: const EdgeInsets.only(right: 7),
            child: SizedBox(width: 10, height: 10, child: CircularProgressIndicator(strokeWidth: 1.5, color: p.muted)),
          )
        else if (status == 3 || status == 4)
          Padding(
            padding: const EdgeInsets.only(right: 5),
            child: Icon(status == 3 ? Icons.check : Icons.close, size: 14, color: color),
          ),
        Text(context.tr(key), style: TextStyle(fontSize: 13, color: color)),
      ],
    );
    return message == null || message!.isEmpty ? label : Tooltip(message: message!, child: label);
  }
}

/// Позначення мутації: моноширинний текст на ледь помітному тлі.
class MutationToken extends StatelessWidget {
  final String text;
  final bool highlight;
  final bool muted;
  final String? tooltip;

  const MutationToken(this.text, {super.key, this.highlight = false, this.muted = false, this.tooltip});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final token = Container(
      padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 1.5),
      decoration: BoxDecoration(color: highlight ? p.danger.withValues(alpha: 0.1) : p.hover, borderRadius: BorderRadius.circular(4)),
      child: Text(text, style: mono(context, size: 12, color: highlight ? p.danger : (muted ? p.muted : p.text))),
    );
    return tooltip == null ? token : Tooltip(message: tooltip!, child: token);
  }
}

// ==========================================
// HELPERS
// ==========================================

String relativeTime(BuildContext context, String? iso) {
  if (iso == null) return '';
  final time = DateTime.parse(iso).toLocal();
  final diff = DateTime.now().difference(time);
  if (diff.inMinutes < 1) return context.tr('common.justNow');
  if (diff.inMinutes < 60) return context.tr('common.minutesAgo', {'n': diff.inMinutes});
  if (diff.inHours < 24) return context.tr('common.hoursAgo', {'n': diff.inHours});
  return '${time.day.toString().padLeft(2, '0')}.${time.month.toString().padLeft(2, '0')}.${time.year}';
}

String formatBases(num bases) {
  if (bases >= 1e9) return '${(bases / 1e9).toStringAsFixed(1)} Gb';
  if (bases >= 1e6) return '${(bases / 1e6).toStringAsFixed(1)} Mb';
  if (bases >= 1e3) return '${(bases / 1e3).toStringAsFixed(1)} kb';
  return '$bases b';
}

/// Коротка форма позначення: "lineage:BA.2.86* (23I (BA.2.86), JN.3.3)" → "BA.2.86*".
String shortVariant(String? variant) {
  if (variant == null) return '';
  return variant.split(' (').first.replaceFirst(RegExp(r'^(clade|lineage):'), '');
}

/// Якщо зразок прийшов сирими прочитаннями: як їх перетворено на геном (картування й консенсус або збірка).
class ReadsSummaryLine extends StatelessWidget {
  final String? summary;
  const ReadsSummaryLine(this.summary, {super.key});

  @override
  Widget build(BuildContext context) {
    if (summary == null) return const SizedBox.shrink();
    final p = context.p;
    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 0, 20, 10),
      child: Text.rich(
        TextSpan(
          style: TextStyle(fontSize: 12, color: p.muted, height: 1.4),
          children: [
            TextSpan(text: '${context.tr('reads.title')}: ', style: TextStyle(color: p.text, fontWeight: FontWeight.w500)),
            TextSpan(text: summary),
          ],
        ),
      ),
    );
  }
}

void showMessage(BuildContext context, String text, {bool error = false}) {
  ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text), backgroundColor: error ? context.p.danger : null));
}

// ==========================================
// NEXTCLADE DATASETS
// ==========================================
const Map<String, String> _datasetNames = {
  'nextstrain/sars-cov-2/wuhan-hu-1/orfs': 'SARS-CoV-2',
  'nextstrain/flu/h1n1pdm/ha/CY121680': 'Influenza A/H1N1pdm HA',
  'nextstrain/flu/h3n2/ha/EPI1857216': 'Influenza A/H3N2 HA',
  'nextstrain/rsv/a/EPI_ISL_412866': 'RSV-A',
  'nextstrain/rsv/b/EPI_ISL_1653999': 'RSV-B',
  'nextstrain/mpox/all-clades': 'Mpox',
};

/// Автовибір набору: для кожної послідовності його визначає `nextclade sort` (віруси різних видів в одному файлі).
const String autoDataset = 'auto';

String datasetName(String? dataset) => dataset == null ? '—' : (_datasetNames[dataset] ?? dataset);

/// Назва набору з урахуванням автовибору («Автоматично» / «Auto»).
String datasetLabel(BuildContext context, String? dataset) => dataset == autoDataset ? context.tr('dataset.auto') : datasetName(dataset);

class DatasetDropdown extends StatelessWidget {
  final List<String> datasets;
  final String? value;
  final String nullLabel;
  final ValueChanged<String?> onChanged;

  /// Показувати варіант «Визначити автоматично» першим.
  final bool allowAuto;

  const DatasetDropdown({
    super.key,
    required this.datasets,
    required this.value,
    required this.nullLabel,
    required this.onChanged,
    this.allowAuto = true,
  });

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return DropdownButtonFormField<String?>(
      // initialValue читається лише при створенні поля, тож ключ оновлює його, коли змінюється список чи вибір.
      key: ValueKey('${datasets.length}-$value'),
      initialValue: value,
      isExpanded: true,
      dropdownColor: p.surface,
      borderRadius: BorderRadius.circular(8),
      icon: Icon(Icons.unfold_more, size: 16, color: p.muted),
      style: TextStyle(fontSize: 13, color: p.text, fontFamily: sansFont),
      items: [
        if (allowAuto)
          DropdownMenuItem<String?>(value: autoDataset, child: Text(context.tr('dataset.autoOption'), overflow: TextOverflow.ellipsis)),
        DropdownMenuItem<String?>(value: null, child: Text(nullLabel, overflow: TextOverflow.ellipsis)),
        ...datasets.map(
          (d) => DropdownMenuItem<String?>(
            value: d,
            child: Text(datasetName(d), overflow: TextOverflow.ellipsis),
          ),
        ),
      ],
      onChanged: onChanged,
    );
  }
}

// ==========================================
// GENOME VISUALISATION
// ==========================================
class GeneSpan {
  final String name;
  final int start;
  final int end;
  const GeneSpan(this.name, this.start, this.end);
}

/// Гени SARS-CoV-2 (MN908947, 1-based) з анотації Nextstrain.
const List<GeneSpan> sarsCov2Genes = [
  GeneSpan('ORF1a', 266, 13468),
  GeneSpan('ORF1b', 13468, 21555),
  GeneSpan('S', 21563, 25384),
  GeneSpan('3a', 25393, 26220),
  GeneSpan('E', 26245, 26472),
  GeneSpan('M', 26523, 27191),
  GeneSpan('6', 27202, 27387),
  GeneSpan('7a', 27394, 27759),
  GeneSpan('7b', 27756, 27887),
  GeneSpan('8', 27894, 28259),
  GeneSpan('N', 28274, 29533),
  GeneSpan('10', 29558, 29674),
];

const int sarsCov2Length = 29903;

class GenomeMarker {
  final int start;
  final int end;
  final String? label;
  const GenomeMarker(this.start, this.end, [this.label]);
}

/// Міні-карта геному для рядка таблиці (як «Sequence view» у Nextclade Web).
class MiniGenome extends StatelessWidget {
  final int length;
  final List<GeneSpan> genes;
  final List<int> substitutions;
  final List<GenomeMarker> deletions;
  final List<int> alerts;

  const MiniGenome({
    super.key,
    required this.length,
    this.genes = const [],
    this.substitutions = const [],
    this.deletions = const [],
    this.alerts = const [],
  });

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 16,
      width: double.infinity,
      child: CustomPaint(painter: _MiniGenomePainter(this, context.p)),
    );
  }
}

class _MiniGenomePainter extends CustomPainter {
  final MiniGenome g;
  final Palette p;
  _MiniGenomePainter(this.g, this.p);

  @override
  void paint(Canvas canvas, Size size) {
    double x(int position) => (position / g.length).clamp(0.0, 1.0) * size.width;
    final mid = size.height / 2;

    canvas.drawRRect(RRect.fromRectAndRadius(Rect.fromLTWH(0, mid - 3, size.width, 6), const Radius.circular(3)), Paint()..color = p.hover);
    // Ген S виділено: у ньому більшість мутацій, що змінюють властивості вірусу.
    for (final gene in g.genes.where((gene) => gene.name == 'S')) {
      canvas.drawRect(Rect.fromLTRB(x(gene.start), mid - 3, x(gene.end), mid + 3), Paint()..color = p.border);
    }
    final tick = Paint()
      ..color = p.muted
      ..strokeWidth = 1;
    for (final s in g.substitutions) {
      canvas.drawLine(Offset(x(s), mid - 5), Offset(x(s), mid + 5), tick);
    }
    final deletion = Paint()..color = p.danger.withValues(alpha: 0.55);
    for (final d in g.deletions) {
      canvas.drawRect(Rect.fromLTRB(x(d.start), mid - 5, x(d.end).clamp(x(d.start) + 1.5, size.width), mid + 5), deletion);
    }
    final alert = Paint()
      ..color = p.danger
      ..strokeWidth = 2;
    for (final a in g.alerts) {
      canvas.drawLine(Offset(x(a), 0), Offset(x(a), size.height), alert);
    }
  }

  @override
  bool shouldRepaint(covariant _MiniGenomePainter oldDelegate) => true;
}

/// Повна карта геному: гени, заміни, делеції та підписи сповіщень.
class GenomeMap extends StatelessWidget {
  final int length;
  final List<GeneSpan> genes;
  final List<int> substitutions;
  final List<GenomeMarker> deletions;
  final List<GenomeMarker> alerts;

  const GenomeMap({
    super.key,
    required this.length,
    this.genes = const [],
    this.substitutions = const [],
    this.deletions = const [],
    this.alerts = const [],
  });

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 104,
      width: double.infinity,
      child: CustomPaint(painter: _GenomePainter(this, context.p)),
    );
  }
}

class _GenomePainter extends CustomPainter {
  final GenomeMap map;
  final Palette p;
  _GenomePainter(this.map, this.p);

  static const _labelRows = 3;
  static const _barTop = 50.0;
  static const _barHeight = 20.0;

  @override
  void paint(Canvas canvas, Size size) {
    double x(int position) => (position / map.length).clamp(0.0, 1.0) * size.width;

    canvas.drawRRect(
      RRect.fromRectAndRadius(Rect.fromLTWH(0, _barTop, size.width, _barHeight), const Radius.circular(4)),
      Paint()..color = p.hover,
    );

    for (final gene in map.genes) {
      final rect = Rect.fromLTRB(x(gene.start), _barTop, x(gene.end), _barTop + _barHeight).deflate(0.75);
      canvas.drawRRect(RRect.fromRectAndRadius(rect, const Radius.circular(3)), Paint()..color = gene.name == 'S' ? p.border : p.selected);
      if (rect.width > 18) _text(canvas, gene.name, Offset(rect.center.dx, _barTop + _barHeight / 2), 10.5, p.muted, center: true);
    }

    final deletionPaint = Paint()..color = p.danger.withValues(alpha: 0.6);
    for (final d in map.deletions) {
      canvas.drawRect(
        Rect.fromLTRB(x(d.start), _barTop - 3, x(d.end).clamp(x(d.start) + 2, size.width), _barTop + _barHeight + 3),
        deletionPaint,
      );
    }

    final tickPaint = Paint()
      ..color = p.muted
      ..strokeWidth = 1;
    for (final s in map.substitutions) {
      canvas.drawLine(Offset(x(s), _barTop + _barHeight + 3), Offset(x(s), _barTop + _barHeight + 11), tickPaint);
    }

    // Сповіщення: підпис стає в перший вільний із трьох рядків над смугою.
    final alertPaint = Paint()
      ..color = p.danger
      ..strokeWidth = 1.5;
    final rowEnds = List<double>.filled(_labelRows, -1000);
    final sorted = [...map.alerts]..sort((a, b) => a.start.compareTo(b.start));
    for (final a in sorted) {
      final ax = x(a.start);
      canvas.drawLine(Offset(ax, _barTop - 4), Offset(ax, _barTop + _barHeight), alertPaint);
      if (a.label == null) continue;
      final width = _measure(a.label!, 10.5);
      final left = (ax - width / 2).clamp(0.0, size.width - width);
      final row = List.generate(_labelRows, (r) => r).where((r) => left > rowEnds[r] + 6).firstOrNull;
      if (row == null) continue;
      final y = row * 14.0;
      canvas.drawLine(
        Offset(ax, y + 14),
        Offset(ax, _barTop - 4),
        Paint()
          ..color = p.danger.withValues(alpha: 0.3)
          ..strokeWidth = 1,
      );
      _text(canvas, a.label!, Offset(left, y), 10.5, p.danger, mono: true);
      rowEnds[row] = left + width;
    }

    _text(canvas, '1', Offset(0, _barTop + _barHeight + 15), 10, p.faint, mono: true);
    final end = '${map.length}';
    _text(canvas, end, Offset(size.width - _measure(end, 10), _barTop + _barHeight + 15), 10, p.faint, mono: true);
  }

  double _measure(String text, double fontSize) {
    final painter = TextPainter(
      text: TextSpan(
        text: text,
        style: TextStyle(fontSize: fontSize, fontFamily: monoFont),
      ),
      textDirection: TextDirection.ltr,
    )..layout();
    return painter.width;
  }

  void _text(Canvas canvas, String text, Offset at, double fontSize, Color color, {bool center = false, bool mono = false}) {
    final painter = TextPainter(
      text: TextSpan(
        text: text,
        style: TextStyle(fontSize: fontSize, color: color, fontFamily: mono ? monoFont : sansFont),
      ),
      textDirection: TextDirection.ltr,
    )..layout();
    painter.paint(canvas, center ? at - Offset(painter.width / 2, painter.height / 2) : at);
  }

  @override
  bool shouldRepaint(covariant _GenomePainter oldDelegate) => true;
}
