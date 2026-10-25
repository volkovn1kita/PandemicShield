import 'dart:async';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';

import '../api.dart';
import '../l10n.dart';
import '../theme.dart';
import '../widgets.dart';

// ==========================================
// PAGE 1: OVERVIEW
// ==========================================
class OverviewPage extends StatefulWidget {
  final bool active;
  final ValueChanged<String> onOpenRun;

  const OverviewPage({super.key, required this.active, required this.onOpenRun});

  @override
  State<OverviewPage> createState() => _OverviewPageState();
}

class _OverviewPageState extends State<OverviewPage> {
  List<dynamic> _runs = [];
  List<dynamic> _alerts = [];
  List<String> _datasets = [];
  int _ruleCount = 0;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _refresh();
    _loadDatasets();
    _timer = Timer.periodic(const Duration(seconds: 3), (_) {
      if (widget.active) _refresh();
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _loadDatasets() async {
    try {
      final datasets = List<String>.from(await Api.list('/nextclade/datasets'));
      if (mounted) setState(() => _datasets = datasets);
    } catch (_) {}
  }

  Future<void> _refresh() async {
    try {
      final results = await Future.wait([Api.list('/runs'), Api.list('/threats'), Api.list('/variant-markers'), Api.list('/dictionary')]);
      if (!mounted) return;
      setState(() {
        _runs = results[0];
        _alerts = results[1];
        _ruleCount = results[2].length + results[3].length;
      });
      if (_datasets.isEmpty) _loadDatasets();
    } catch (_) {}
  }

  Future<void> _deleteAlert(String id) async {
    try {
      await Api.delete('/threats/$id');
      _refresh();
    } catch (_) {}
  }

  @override
  Widget build(BuildContext context) {
    final genomes = _runs.fold<int>(0, (sum, r) => sum + ((r['sequencesAnalyzed'] ?? 0) as int));

    return SingleChildScrollView(
      padding: const EdgeInsets.all(28),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          PageHeader(title: context.tr('overview.title'), subtitle: context.tr('overview.subtitle')),
          Row(
            children: [
              Expanded(
                child: KpiTile(
                  label: context.tr('kpi.runs'),
                  value: '${_runs.length}',
                  icon: Icons.play_circle_outline,
                  color: AppColors.primary,
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: KpiTile(
                  label: context.tr('kpi.sequences'),
                  value: '$genomes',
                  icon: Icons.biotech_outlined,
                  color: AppColors.nextclade,
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: KpiTile(
                  label: context.tr('kpi.alerts'),
                  value: '${_alerts.length}',
                  icon: Icons.notifications_active_outlined,
                  color: AppColors.danger,
                ),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: KpiTile(label: context.tr('kpi.rules'), value: '$_ruleCount', icon: Icons.rule, color: AppColors.warning),
              ),
            ],
          ),
          const SizedBox(height: 20),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              SizedBox(
                width: 380,
                child: _NewAnalysisPanel(datasets: _datasets, onStarted: _refresh),
              ),
              const SizedBox(width: 20),
              Expanded(
                child: Column(
                  children: [
                    _RecentRunsPanel(runs: _runs, onOpenRun: widget.onOpenRun),
                    const SizedBox(height: 20),
                    _TopAlertsPanel(alerts: _alerts),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 20),
          _AlertsPanel(alerts: _alerts, onDelete: _deleteAlert),
        ],
      ),
    );
  }
}

// ---------- New analysis ----------
class _NewAnalysisPanel extends StatefulWidget {
  final List<String> datasets;
  final VoidCallback onStarted;

  const _NewAnalysisPanel({required this.datasets, required this.onStarted});

  @override
  State<_NewAnalysisPanel> createState() => _NewAnalysisPanelState();
}

class _NewAnalysisPanelState extends State<_NewAnalysisPanel> {
  int _category = 0;
  String? _dataset;
  bool _datasetTouched = false;
  PlatformFile? _file;
  bool _uploading = false;

  String? get _effectiveDataset {
    if (_category != 0) return null;
    if (_datasetTouched) return _dataset;
    // За замовчуванням — SARS-CoV-2, якщо цей набір встановлено.
    return widget.datasets.where((d) => d.contains('sars-cov-2')).firstOrNull;
  }

  Future<void> _pick() async {
    final result = await FilePicker.platform.pickFiles(type: FileType.custom, allowedExtensions: ['fasta', 'fa', 'fna', 'txt']);
    if (result != null) setState(() => _file = result.files.first);
  }

  Future<void> _start() async {
    if (_file?.path == null) return;
    setState(() => _uploading = true);
    final dataset = _effectiveDataset;
    try {
      final response = await Api.analyze(filePath: _file!.path!, category: _category, nextcladeDataset: dataset);
      if (!mounted) return;
      showMessage(
        context,
        dataset != null
            ? context.tr('upload.sentNextclade')
            : context.tr('upload.sent', {'records': response['recordCount'], 'bases': formatBases(response['baseCount'] ?? 0)}),
      );
      setState(() => _file = null);
      widget.onStarted();
    } on ApiException catch (e) {
      if (mounted) showMessage(context, context.tr('upload.error', {'message': e.message}), error: true);
    } catch (_) {
      if (mounted) showMessage(context, context.tr('common.connectionFailed'), error: true);
    } finally {
      if (mounted) setState(() => _uploading = false);
    }
  }

  Widget _step(int number, String title) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Row(
        children: [
          Container(
            width: 22,
            height: 22,
            alignment: Alignment.center,
            decoration: BoxDecoration(color: AppColors.primary.withValues(alpha: 0.12), shape: BoxShape.circle),
            child: Text(
              '$number',
              style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: AppColors.primary),
            ),
          ),
          const SizedBox(width: 10),
          Text(
            title,
            style: const TextStyle(fontWeight: FontWeight.w600, color: AppColors.text),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Panel(
      expand: false,
      title: context.tr('upload.title'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _step(1, context.tr('upload.step1')),
          SegmentedButton<int>(
            segments: [
              ButtonSegment(value: 0, icon: const Icon(Icons.coronavirus_outlined, size: 18), label: Text(context.tr('common.virus'))),
              ButtonSegment(value: 1, icon: const Icon(Icons.person_outline, size: 18), label: Text(context.tr('common.human'))),
            ],
            selected: {_category},
            showSelectedIcon: false,
            onSelectionChanged: (s) => setState(() => _category = s.first),
          ),
          const SizedBox(height: 20),
          _step(2, context.tr('upload.step2')),
          if (_category == 0) ...[
            DatasetDropdown(
              datasets: widget.datasets,
              value: _effectiveDataset,
              nullLabel: context.tr('upload.noNextclade'),
              onChanged: (v) => setState(() {
                _dataset = v;
                _datasetTouched = true;
              }),
            ),
            const SizedBox(height: 6),
            Text(context.tr('upload.step2.hint'), style: Theme.of(context).textTheme.bodySmall),
          ] else
            Text(context.tr('upload.humanHint'), style: Theme.of(context).textTheme.bodySmall),
          const SizedBox(height: 20),
          _step(3, context.tr('upload.step3')),
          InkWell(
            onTap: _uploading ? null : _pick,
            borderRadius: BorderRadius.circular(12),
            child: AnimatedContainer(
              duration: const Duration(milliseconds: 200),
              padding: const EdgeInsets.symmetric(vertical: 22, horizontal: 16),
              decoration: BoxDecoration(
                color: _file == null ? AppColors.surfaceMuted : AppColors.primary.withValues(alpha: 0.06),
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: _file == null ? AppColors.border : AppColors.primary, width: 1.4),
              ),
              child: Row(
                children: [
                  Icon(
                    _file == null ? Icons.upload_file : Icons.description_outlined,
                    color: _file == null ? AppColors.textMuted : AppColors.primary,
                    size: 30,
                  ),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          _file?.name ?? context.tr('upload.pick'),
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(fontWeight: FontWeight.w600, color: AppColors.text),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          _file == null ? context.tr('upload.pickHint') : formatBases(_file!.size).replaceAll('b', 'B'),
                          style: Theme.of(context).textTheme.bodySmall,
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 20),
          FilledButton.icon(
            onPressed: _uploading || _file == null ? null : _start,
            icon: _uploading
                ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                : const Icon(Icons.play_arrow_rounded),
            label: Text(context.tr('upload.start')),
          ),
        ],
      ),
    );
  }
}

// ---------- Recent runs ----------
class _RecentRunsPanel extends StatelessWidget {
  final List<dynamic> runs;
  final ValueChanged<String> onOpenRun;

  const _RecentRunsPanel({required this.runs, required this.onOpenRun});

  @override
  Widget build(BuildContext context) {
    final recent = runs.take(5).toList();
    return Panel(
      expand: false,
      title: context.tr('recentRuns.title'),
      padding: const EdgeInsets.fromLTRB(20, 20, 20, 10),
      child: recent.isEmpty
          ? EmptyState(icon: Icons.inbox_outlined, text: context.tr('recentRuns.empty'))
          : Column(
              children: [for (final run in recent) RunTile(run: run, onTap: () => onOpenRun(run['id']))],
            ),
    );
  }
}

class RunTile extends StatelessWidget {
  final dynamic run;
  final VoidCallback onTap;
  final bool selected;

  const RunTile({super.key, required this.run, required this.onTap, this.selected = false});

  @override
  Widget build(BuildContext context) {
    final details = [
      context.tr('run.records', {'n': run['recordCount']}),
      formatBases(run['baseCount'] ?? 0),
      if (run['nextcladeDataset'] != null) datasetName(run['nextcladeDataset']),
    ].join(' · ');

    return Material(
      color: selected ? AppColors.primary.withValues(alpha: 0.07) : Colors.transparent,
      borderRadius: BorderRadius.circular(10),
      child: InkWell(
        borderRadius: BorderRadius.circular(10),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 10),
          child: Row(
            children: [
              Icon(Icons.description_outlined, size: 20, color: selected ? AppColors.primary : AppColors.textMuted),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      run['fileName'],
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(fontWeight: FontWeight.w600),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      '${relativeTime(context, run['startedAt'])} · $details',
                      overflow: TextOverflow.ellipsis,
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 10),
              NextcladeStatusPill(status: run['nextcladeStatus'] ?? 0),
            ],
          ),
        ),
      ),
    );
  }
}

// ---------- Top alerts ----------
class _TopAlertsPanel extends StatelessWidget {
  final List<dynamic> alerts;
  const _TopAlertsPanel({required this.alerts});

  @override
  Widget build(BuildContext context) {
    final counts = <String, int>{};
    for (final a in alerts) {
      final name = a['threatName'] as String;
      counts[name] = (counts[name] ?? 0) + 1;
    }
    final top = counts.entries.toList()..sort((a, b) => b.value.compareTo(a.value));
    final max = top.isEmpty ? 1 : top.first.value;

    return Panel(
      expand: false,
      title: context.tr('alerts.top'),
      child: top.isEmpty
          ? EmptyState(icon: Icons.bar_chart, text: context.tr('alerts.empty'))
          : Column(
              children: [
                for (final entry in top.take(5))
                  Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: Row(
                      children: [
                        Expanded(
                          flex: 5,
                          child: Text(entry.key, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 13)),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          flex: 3,
                          child: ClipRRect(
                            borderRadius: BorderRadius.circular(4),
                            child: LinearProgressIndicator(
                              value: entry.value / max,
                              minHeight: 8,
                              backgroundColor: AppColors.surfaceMuted,
                              color: AppColors.danger.withValues(alpha: 0.7),
                            ),
                          ),
                        ),
                        SizedBox(
                          width: 36,
                          child: Text(
                            '${entry.value}',
                            textAlign: TextAlign.right,
                            style: const TextStyle(fontWeight: FontWeight.w600),
                          ),
                        ),
                      ],
                    ),
                  ),
              ],
            ),
    );
  }
}

// ---------- Alerts feed ----------
class _AlertsPanel extends StatelessWidget {
  final List<dynamic> alerts;
  final ValueChanged<String> onDelete;

  const _AlertsPanel({required this.alerts, required this.onDelete});

  @override
  Widget build(BuildContext context) {
    return Panel(
      expand: false,
      title: context.tr('alerts.title'),
      subtitle: context.tr('alerts.subtitle'),
      padding: const EdgeInsets.fromLTRB(20, 20, 20, 8),
      child: alerts.isEmpty
          ? EmptyState(icon: Icons.verified_outlined, text: context.tr('alerts.empty'))
          : Column(
              children: [for (final alert in alerts.take(40)) AlertRow(alert: alert, onDelete: () => onDelete(alert['id']))],
            ),
    );
  }
}

class AlertRow extends StatelessWidget {
  final dynamic alert;
  final VoidCallback? onDelete;

  const AlertRow({super.key, required this.alert, this.onDelete});

  @override
  Widget build(BuildContext context) {
    final bool nextclade = alert['source'] == 'nextclade';
    final color = nextclade ? AppColors.nextclade : AppColors.primary;
    final String tag = nextclade ? shortVariant(alert['variant']) : '${alert['ntStart']}–${alert['ntEnd']} (${alert['strand']})';
    final String? where = nextclade && (alert['ntStart'] ?? 0) > 0 ? 'ref ${alert['ntStart']}' : null;

    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: AppColors.border),
      ),
      child: IntrinsicHeight(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Container(
              width: 4,
              decoration: BoxDecoration(
                color: color,
                borderRadius: const BorderRadius.horizontal(left: Radius.circular(10)),
              ),
            ),
            const SizedBox(width: 14),
            Expanded(
              flex: 6,
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 10),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Text(
                      alert['threatName'],
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(fontWeight: FontWeight.w600),
                    ),
                    const SizedBox(height: 2),
                    Tooltip(
                      message: alert['recordId'],
                      child: Text(
                        alert['recordId'],
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              flex: 4,
              child: Align(
                alignment: Alignment.centerLeft,
                child: Wrap(
                  spacing: 6,
                  runSpacing: 4,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    VariantTag(tag, color: color, tooltip: alert['variant']),
                    if (where != null) Text(where, style: monoStyle.copyWith(fontSize: 11, color: AppColors.textMuted)),
                  ],
                ),
              ),
            ),
            SizedBox(
              width: 100,
              child: Align(
                alignment: Alignment.centerLeft,
                child: Pill(text: context.tr(nextclade ? 'source.nextclade' : 'source.orf'), color: color),
              ),
            ),
            SizedBox(
              width: 92,
              child: Align(
                alignment: Alignment.centerRight,
                child: Text(relativeTime(context, alert['detectedAt']), style: Theme.of(context).textTheme.bodySmall),
              ),
            ),
            if (onDelete != null)
              IconButton(
                tooltip: context.tr('common.delete'),
                icon: const Icon(Icons.close, size: 18, color: AppColors.textFaint),
                onPressed: onDelete,
              )
            else
              const SizedBox(width: 12),
          ],
        ),
      ),
    );
  }
}
