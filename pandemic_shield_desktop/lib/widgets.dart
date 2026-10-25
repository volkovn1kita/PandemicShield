import 'package:flutter/material.dart';

import 'l10n.dart';
import 'theme.dart';

// ==========================================
// SHARED WIDGETS
// ==========================================

/// Біла картка-панель із заголовком.
class Panel extends StatelessWidget {
  final String? title;
  final String? subtitle;
  final Widget? trailing;
  final Widget child;
  final EdgeInsetsGeometry padding;

  /// true — панель у межах фіксованої висоти, вміст розтягується (списки); false — висота за вмістом.
  final bool expand;

  const Panel({
    super.key,
    this.title,
    this.subtitle,
    this.trailing,
    required this.child,
    this.padding = const EdgeInsets.all(20),
    this.expand = true,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
        boxShadow: const [BoxShadow(color: Color(0x0A0F172A), blurRadius: 18, offset: Offset(0, 6))],
      ),
      padding: padding,
      child: Column(
        mainAxisSize: expand ? MainAxisSize.max : MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (title != null) ...[
            Row(
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(title!, style: Theme.of(context).textTheme.titleMedium),
                      if (subtitle != null) ...[const SizedBox(height: 2), Text(subtitle!, style: Theme.of(context).textTheme.bodySmall)],
                    ],
                  ),
                ),
                ?trailing,
              ],
            ),
            const SizedBox(height: 16),
          ],
          if (expand) Expanded(child: child) else child,
        ],
      ),
    );
  }
}

class PageHeader extends StatelessWidget {
  final String title;
  final String subtitle;
  final Widget? trailing;

  const PageHeader({super.key, required this.title, required this.subtitle, this.trailing});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 24),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: Theme.of(context).textTheme.headlineSmall),
                const SizedBox(height: 4),
                Text(subtitle, style: const TextStyle(color: AppColors.textMuted, fontSize: 14)),
              ],
            ),
          ),
          ?trailing,
        ],
      ),
    );
  }
}

class KpiTile extends StatelessWidget {
  final String label;
  final String value;
  final IconData icon;
  final Color color;

  const KpiTile({super.key, required this.label, required this.value, required this.icon, required this.color});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        children: [
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(color: color.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(12)),
            child: Icon(icon, color: color, size: 22),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  value,
                  style: const TextStyle(fontSize: 24, fontWeight: FontWeight.w600, color: AppColors.text, height: 1.1),
                ),
                const SizedBox(height: 2),
                Text(
                  label,
                  style: const TextStyle(fontSize: 12.5, color: AppColors.textMuted),
                  overflow: TextOverflow.ellipsis,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Невелика кольорова «пігулка» зі статусом.
class Pill extends StatelessWidget {
  final String text;
  final Color color;
  final IconData? icon;
  final bool spinning;
  final bool mono;

  const Pill({super.key, required this.text, required this.color, this.icon, this.spinning = false, this.mono = false});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 3.5),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: color.withValues(alpha: 0.25)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (spinning)
            Padding(
              padding: const EdgeInsets.only(right: 6),
              child: SizedBox(width: 10, height: 10, child: CircularProgressIndicator(strokeWidth: 1.6, color: color)),
            )
          else if (icon != null)
            Padding(
              padding: const EdgeInsets.only(right: 5),
              child: Icon(icon, size: 12, color: color),
            ),
          Flexible(
            child: Text(
              text,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(fontSize: 11.5, fontWeight: FontWeight.w600, color: color, fontFamily: mono ? monoFont : sansFont),
            ),
          ),
        ],
      ),
    );
  }
}

class CladePill extends StatelessWidget {
  final String? clade;
  final String? display;

  const CladePill({super.key, this.clade, this.display});

  @override
  Widget build(BuildContext context) {
    if (clade == null && display == null) return const Text('—', style: TextStyle(color: AppColors.textFaint));
    return Tooltip(
      message: display ?? clade!,
      child: Pill(text: display ?? clade!, color: AppColors.forClade(clade ?? display)),
    );
  }
}

class QcPill extends StatelessWidget {
  final String? status;
  const QcPill({super.key, this.status});

  @override
  Widget build(BuildContext context) {
    if (status == null) return const Text('—', style: TextStyle(color: AppColors.textFaint));
    final color = AppColors.forQc(status);
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 8,
          height: 8,
          decoration: BoxDecoration(color: color, shape: BoxShape.circle),
        ),
        const SizedBox(width: 6),
        Flexible(
          child: Text(
            context.tr('qc.$status'),
            overflow: TextOverflow.ellipsis,
            style: TextStyle(color: color, fontWeight: FontWeight.w600, fontSize: 12.5),
          ),
        ),
      ],
    );
  }
}

/// Статус Nextclade для запуску (NotRequested, Queued, Running, Completed, Failed).
class NextcladeStatusPill extends StatelessWidget {
  final int status;
  const NextcladeStatusPill({super.key, required this.status});

  @override
  Widget build(BuildContext context) {
    return switch (status) {
      1 => Pill(text: context.tr('nc.queued'), color: AppColors.warning, spinning: true),
      2 => Pill(text: context.tr('nc.running'), color: AppColors.nextclade, spinning: true),
      3 => Pill(text: context.tr('nc.completed'), color: AppColors.success, icon: Icons.check),
      4 => Pill(text: context.tr('nc.failed'), color: AppColors.danger, icon: Icons.error_outline),
      _ => Pill(text: context.tr('nc.none'), color: AppColors.textFaint),
    };
  }
}

class CoverageBar extends StatelessWidget {
  final double? value;
  const CoverageBar({super.key, this.value});

  @override
  Widget build(BuildContext context) {
    if (value == null) return const Text('—', style: TextStyle(color: AppColors.textFaint));
    final color = value! >= 0.95 ? AppColors.success : (value! >= 0.85 ? AppColors.warning : AppColors.danger);
    return Row(
      children: [
        Expanded(
          child: ClipRRect(
            borderRadius: BorderRadius.circular(4),
            child: LinearProgressIndicator(value: value, minHeight: 6, backgroundColor: AppColors.border, color: color),
          ),
        ),
        const SizedBox(width: 8),
        SizedBox(
          width: 44,
          child: Text('${(value! * 100).toStringAsFixed(1)}%', style: const TextStyle(fontSize: 12, color: AppColors.textMuted)),
        ),
      ],
    );
  }
}

class EmptyState extends StatelessWidget {
  final IconData icon;
  final String text;
  const EmptyState({super.key, required this.icon, required this.text});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 40, color: AppColors.textFaint),
            const SizedBox(height: 10),
            Text(
              text,
              textAlign: TextAlign.center,
              style: const TextStyle(color: AppColors.textMuted),
            ),
          ],
        ),
      ),
    );
  }
}

/// Позначення варіанта моноширинним шрифтом (S:N501Y, A23403G).
class VariantTag extends StatelessWidget {
  final String text;
  final Color color;
  final String? tooltip;

  const VariantTag(this.text, {super.key, this.color = AppColors.text, this.tooltip});

  @override
  Widget build(BuildContext context) {
    final tag = Container(
      padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3),
      decoration: BoxDecoration(
        color: color == AppColors.text ? AppColors.surfaceMuted : color.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(6),
        border: Border.all(color: color == AppColors.text ? AppColors.border : color.withValues(alpha: 0.25)),
      ),
      child: Text(text, style: monoStyle.copyWith(fontSize: 11.5, color: color)),
    );
    return tooltip == null ? tag : Tooltip(message: tooltip!, child: tag);
  }
}

/// Коротка форма позначення для тегів: "lineage:BA.2.86* (23I (BA.2.86), JN.3.3)" → "BA.2.86*".
String shortVariant(String? variant) {
  if (variant == null) return '';
  final label = variant.split(' (').first;
  return label.replaceFirst(RegExp(r'^(clade|lineage):'), '');
}

class SectionLabel extends StatelessWidget {
  final String text;
  const SectionLabel(this.text, {super.key});

  @override
  Widget build(BuildContext context) => Text(text.toUpperCase(), style: labelStyle);
}

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

void showMessage(BuildContext context, String text, {bool error = false}) {
  ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text), backgroundColor: error ? AppColors.danger : AppColors.sidebar));
}

// ==========================================
// NEXTCLADE DATASETS
// ==========================================
const Map<String, String> _datasetNames = {
  'nextstrain/sars-cov-2/wuhan-hu-1/orfs': 'SARS-CoV-2',
  'nextstrain/flu/h1n1pdm/ha/CY121680': 'Influenza A H1N1pdm · HA',
  'nextstrain/flu/h3n2/ha/EPI1857216': 'Influenza A H3N2 · HA',
  'nextstrain/rsv/a/EPI_ISL_412866': 'RSV-A',
  'nextstrain/rsv/b/EPI_ISL_1653999': 'RSV-B',
  'nextstrain/mpox/all-clades': 'Mpox',
};

String datasetName(String? dataset) => dataset == null ? '—' : (_datasetNames[dataset] ?? dataset);

class DatasetDropdown extends StatelessWidget {
  final List<String> datasets;
  final String? value;
  final String nullLabel;
  final ValueChanged<String?> onChanged;

  const DatasetDropdown({super.key, required this.datasets, required this.value, required this.nullLabel, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    return DropdownButtonFormField<String?>(
      // initialValue читається лише при створенні поля, тож ключ оновлює його, коли змінюється список чи вибір.
      key: ValueKey('${datasets.length}-$value'),
      initialValue: value,
      isExpanded: true,
      borderRadius: BorderRadius.circular(12),
      items: [
        DropdownMenuItem<String?>(value: null, child: Text(nullLabel, overflow: TextOverflow.ellipsis)),
        ...datasets.map(
          (d) => DropdownMenuItem<String?>(
            value: d,
            child: Tooltip(
              message: d,
              child: Text(datasetName(d), overflow: TextOverflow.ellipsis),
            ),
          ),
        ),
      ],
      onChanged: onChanged,
    );
  }
}

// ==========================================
// GENOME MAP
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

class GenomeMarker {
  final int start;
  final int end;
  final String? label;
  const GenomeMarker(this.start, this.end, [this.label]);
}

/// Геном як смуга: гени, риски замін, червоні делеції та підписані сповіщення.
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
      height: 108,
      width: double.infinity,
      child: CustomPaint(painter: _GenomePainter(this)),
    );
  }
}

class _GenomePainter extends CustomPainter {
  final GenomeMap map;
  _GenomePainter(this.map);

  static const _labelRows = 3;
  static const _barTop = 54.0;
  static const _barHeight = 22.0;

  @override
  void paint(Canvas canvas, Size size) {
    const barTop = _barTop;
    const barHeight = _barHeight;
    double x(int position) => (position / map.length).clamp(0.0, 1.0) * size.width;

    // Підкладка геному.
    final base = RRect.fromRectAndRadius(Rect.fromLTWH(0, barTop, size.width, barHeight), const Radius.circular(6));
    canvas.drawRRect(base, Paint()..color = AppColors.surfaceMuted);

    // Гени.
    for (int i = 0; i < map.genes.length; i++) {
      final gene = map.genes[i];
      final rect = Rect.fromLTRB(x(gene.start), barTop, x(gene.end), barTop + barHeight);
      final color = gene.name == 'S' ? AppColors.primary : AppColors.cladePalette[i % AppColors.cladePalette.length];
      canvas.drawRRect(
        RRect.fromRectAndRadius(rect.deflate(0.5), const Radius.circular(4)),
        Paint()..color = color.withValues(alpha: gene.name == 'S' ? 0.28 : 0.14),
      );
      if (rect.width > 16) {
        _text(canvas, gene.name, Offset(rect.center.dx, barTop + barHeight / 2), 10.5, AppColors.text, center: true, bold: true);
      }
    }

    // Делеції.
    final deletionPaint = Paint()..color = AppColors.danger.withValues(alpha: 0.75);
    for (final d in map.deletions) {
      canvas.drawRect(
        Rect.fromLTRB(x(d.start), barTop - 4, x(d.end).clamp(x(d.start) + 2, size.width), barTop + barHeight + 4),
        deletionPaint,
      );
    }

    // Заміни.
    final tickPaint = Paint()
      ..color = AppColors.text.withValues(alpha: 0.55)
      ..strokeWidth = 1;
    for (final p in map.substitutions) {
      canvas.drawLine(Offset(x(p), barTop + barHeight + 2), Offset(x(p), barTop + barHeight + 10), tickPaint);
    }

    // Сповіщення з підписами: кожен підпис стає в перший вільний із трьох рядків над смугою.
    final alertPaint = Paint()
      ..color = AppColors.danger
      ..strokeWidth = 2;
    final rowEnds = List<double>.filled(_labelRows, -1000);
    final sorted = [...map.alerts]..sort((a, b) => a.start.compareTo(b.start));
    for (final a in sorted) {
      final ax = x(a.start);
      canvas.drawLine(Offset(ax, _barTop - 6), Offset(ax, _barTop + _barHeight), alertPaint);
      if (a.label == null) continue;
      final width = _measure(a.label!, 10.5);
      final left = (ax - width / 2).clamp(0.0, size.width - width);
      final row = List.generate(_labelRows, (r) => r).where((r) => left > rowEnds[r] + 6).firstOrNull;
      if (row == null) continue;
      final y = row * 14.0;
      canvas.drawLine(
        Offset(ax, y + 14),
        Offset(ax, _barTop - 6),
        Paint()
          ..color = AppColors.danger.withValues(alpha: 0.35)
          ..strokeWidth = 1,
      );
      _text(canvas, a.label!, Offset(left, y), 10.5, AppColors.danger, mono: true);
      rowEnds[row] = left + width;
    }

    _text(canvas, '1', Offset(0, barTop + barHeight + 14), 10, AppColors.textFaint);
    final endLabel = '${map.length}';
    _text(canvas, endLabel, Offset(size.width - _measure(endLabel, 10), barTop + barHeight + 14), 10, AppColors.textFaint);
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

  void _text(
    Canvas canvas,
    String text,
    Offset at,
    double fontSize,
    Color color, {
    bool center = false,
    bool bold = false,
    bool mono = false,
  }) {
    final painter = TextPainter(
      text: TextSpan(
        text: text,
        style: TextStyle(
          fontSize: fontSize,
          color: color,
          fontFamily: mono ? monoFont : sansFont,
          fontWeight: bold ? FontWeight.w600 : FontWeight.w400,
        ),
      ),
      textDirection: TextDirection.ltr,
    )..layout();
    final offset = center ? at - Offset(painter.width / 2, painter.height / 2) : at;
    painter.paint(canvas, offset);
  }

  @override
  bool shouldRepaint(covariant _GenomePainter oldDelegate) => true;
}
