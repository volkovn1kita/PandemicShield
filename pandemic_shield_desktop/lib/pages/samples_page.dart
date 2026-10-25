import 'dart:async';

import 'package:flutter/material.dart';

import '../api.dart';
import '../l10n.dart';
import '../theme.dart';
import '../widgets.dart';
import 'overview_page.dart' show RunTile;

// ==========================================
// PAGE 2: SAMPLES (Nextclade results per run)
// ==========================================
class SamplesPage extends StatefulWidget {
  final bool active;
  final String? selectedRunId;
  final ValueChanged<String> onSelectRun;

  const SamplesPage({super.key, required this.active, required this.selectedRunId, required this.onSelectRun});

  @override
  State<SamplesPage> createState() => _SamplesPageState();
}

class _SamplesPageState extends State<SamplesPage> {
  final _searchController = TextEditingController();

  List<dynamic> _runs = [];
  List<dynamic> _sequences = [];
  List<dynamic> _alerts = [];
  List<String> _datasets = [];
  List<dynamic>? _searchHits;
  String? _loadedRunId;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _refresh();
    _timer = Timer.periodic(const Duration(seconds: 3), (_) {
      if (widget.active) _refresh();
    });
  }

  @override
  void didUpdateWidget(covariant SamplesPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.selectedRunId != oldWidget.selectedRunId || (widget.active && !oldWidget.active)) _refresh();
  }

  @override
  void dispose() {
    _timer?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  dynamic get _selectedRun => _runs.where((r) => r['id'] == widget.selectedRunId).firstOrNull;

  Future<void> _refresh() async {
    try {
      final runs = await Api.list('/runs');
      if (_datasets.isEmpty) _datasets = List<String>.from(await Api.list('/nextclade/datasets'));

      final runId = widget.selectedRunId;
      List<dynamic> sequences = [], alerts = [];
      if (runId != null) {
        final results = await Future.wait([Api.list('/runs/$runId/sequences'), Api.list('/runs/$runId/threats')]);
        sequences = results[0];
        alerts = results[1].where((t) => t['source'] == 'nextclade').toList();
      }
      if (!mounted || runId != widget.selectedRunId) return;
      setState(() {
        _runs = runs;
        _sequences = sequences;
        _alerts = alerts;
        _loadedRunId = runId;
      });
    } catch (_) {}
  }

  Future<void> _search([String? text]) async {
    if (text != null) _searchController.text = text;
    final label = _searchController.text.trim();
    if (label.isEmpty) {
      setState(() => _searchHits = null);
      return;
    }
    try {
      final hits = await Api.list('/mutations', {'label': label});
      if (mounted) setState(() => _searchHits = hits);
    } catch (_) {
      if (mounted) showMessage(context, context.tr('common.connectionFailed'), error: true);
    }
  }

  Future<void> _rerun() async {
    final run = _selectedRun;
    if (run == null) return;
    String? dataset = run['nextcladeDataset'] ?? _datasets.where((d) => d.contains('sars-cov-2')).firstOrNull;

    final confirmed = await showDialog<String>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          title: Text(context.tr('samples.rerunTitle')),
          content: SizedBox(
            width: 460,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(context.tr('samples.rerunHint'), style: const TextStyle(color: AppColors.textMuted)),
                const SizedBox(height: 16),
                DatasetDropdown(datasets: _datasets, value: dataset, nullLabel: '—', onChanged: (v) => setDialogState(() => dataset = v)),
              ],
            ),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context), child: Text(context.tr('common.cancel'))),
            FilledButton(
              onPressed: dataset == null ? null : () => Navigator.pop(context, dataset),
              child: Text(context.tr('samples.rerunStart')),
            ),
          ],
        ),
      ),
    );
    if (confirmed == null) return;

    try {
      await Api.post('/runs/${run['id']}/nextclade', {'dataset': confirmed});
      _refresh();
    } on ApiException catch (e) {
      if (mounted) showMessage(context, e.statusCode == 409 ? context.tr('samples.rerunUnavailable') : e.message, error: true);
    } catch (_) {
      if (mounted) showMessage(context, context.tr('common.connectionFailed'), error: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(28),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          PageHeader(title: context.tr('samples.title'), subtitle: context.tr('samples.subtitle')),
          Expanded(
            child: LayoutBuilder(
              builder: (context, constraints) => Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  SizedBox(
                    width: constraints.maxWidth < 1100 ? 280 : 340,
                    child: Panel(
                      title: context.tr('samples.runs'),
                      padding: const EdgeInsets.fromLTRB(14, 20, 14, 10),
                      child: _runs.isEmpty
                          ? EmptyState(icon: Icons.inbox_outlined, text: context.tr('recentRuns.empty'))
                          : ListView.builder(
                              itemCount: _runs.length,
                              itemBuilder: (context, i) => RunTile(
                                run: _runs[i],
                                selected: _runs[i]['id'] == widget.selectedRunId,
                                onTap: () => widget.onSelectRun(_runs[i]['id']),
                              ),
                            ),
                    ),
                  ),
                  const SizedBox(width: 20),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        _buildSearch(),
                        const SizedBox(height: 16),
                        Expanded(child: _buildRunDetail()),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildSearch() {
    return Panel(
      expand: false,
      padding: const EdgeInsets.all(14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _searchController,
            style: monoStyle.copyWith(fontSize: 13.5),
            decoration: InputDecoration(
              hintText: context.tr('samples.search'),
              prefixIcon: const Icon(Icons.search, color: AppColors.textMuted),
              suffixIcon: _searchHits == null
                  ? null
                  : IconButton(
                      icon: const Icon(Icons.close, size: 18),
                      onPressed: () {
                        _searchController.clear();
                        setState(() => _searchHits = null);
                      },
                    ),
            ),
            onSubmitted: (_) => _search(),
          ),
          const SizedBox(height: 10),
          if (_searchHits == null)
            Wrap(
              spacing: 6,
              runSpacing: 6,
              children: [
                for (final example in ['S:N501Y', 'S:E484K', 'S:L452R', 'S:F157-', 'A23403G'])
                  ActionChip(
                    label: Text(example, style: monoStyle.copyWith(fontSize: 11.5)),
                    visualDensity: VisualDensity.compact,
                    onPressed: () => _search(example),
                  ),
              ],
            )
          else ...[
            Text(
              _searchHits!.isEmpty
                  ? context.tr('samples.searchNone', {'label': _searchController.text.trim()})
                  : context.tr('samples.searchFound', {'label': _searchController.text.trim(), 'n': _searchHits!.length}),
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
            const SizedBox(height: 8),
            ConstrainedBox(
              constraints: const BoxConstraints(maxHeight: 92),
              child: SingleChildScrollView(
                child: Wrap(
                  spacing: 6,
                  runSpacing: 6,
                  children: [
                    for (final hit in _searchHits!)
                      ActionChip(
                        avatar: CircleAvatar(backgroundColor: AppColors.forClade(hit['clade']), radius: 5),
                        label: Text(
                          '${hit['seqName']} · ${hit['cladeDisplay'] ?? hit['clade'] ?? '—'}',
                          style: const TextStyle(fontSize: 12),
                        ),
                        tooltip: hit['fileName'],
                        visualDensity: VisualDensity.compact,
                        onPressed: () => widget.onSelectRun(hit['runId']),
                      ),
                  ],
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildRunDetail() {
    final run = _selectedRun;
    if (run == null) {
      return Panel(
        child: EmptyState(icon: Icons.touch_app_outlined, text: context.tr('samples.selectRun')),
      );
    }

    final int status = run['nextcladeStatus'] ?? 0;
    final analysed = _sequences.where((s) => s['errors'] == null).length;

    return Panel(
      title: run['fileName'],
      subtitle: [
        relativeTime(context, run['startedAt']),
        context.tr('run.records', {'n': run['recordCount']}),
        formatBases(run['baseCount'] ?? 0),
        if (run['nextcladeDataset'] != null) datasetName(run['nextcladeDataset']),
      ].join(' · '),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Tooltip(
            message: run['nextcladeMessage'] ?? '',
            child: NextcladeStatusPill(status: status),
          ),
          const SizedBox(width: 12),
          OutlinedButton.icon(
            onPressed: status == 1 || status == 2 ? null : _rerun,
            icon: const Icon(Icons.refresh, size: 18),
            label: Text(context.tr('samples.rerun')),
          ),
        ],
      ),
      child: _sequences.isEmpty || _loadedRunId != run['id']
          ? (status == 1 || status == 2
                ? Center(
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const CircularProgressIndicator(color: AppColors.nextclade),
                        const SizedBox(height: 14),
                        Text(context.tr('samples.waiting'), style: const TextStyle(color: AppColors.textMuted)),
                      ],
                    ),
                  )
                : EmptyState(icon: Icons.science_outlined, text: context.tr('samples.noResults')))
          : LayoutBuilder(
              builder: (context, constraints) {
                // На вузькому екрані покриття й кількість мутацій переходять під назву геному.
                final wide = constraints.maxWidth >= 860;
                final columns = <(String, int)>[
                  ('col.sequence', 4),
                  ('col.clade', 3),
                  ('col.lineage', 3),
                  ('col.qc', 2),
                  if (wide) ('col.coverage', 3),
                  if (wide) ('col.mutations', 2),
                  ('col.alerts', 5),
                ];
                return Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Wrap(
                      spacing: 8,
                      children: [
                        Pill(
                          text: '$analysed / ${_sequences.length} ${context.tr('kpi.sequences').toLowerCase()}',
                          color: AppColors.nextclade,
                          icon: Icons.biotech_outlined,
                        ),
                        Pill(
                          text: '${_alerts.length} ${context.tr('kpi.alerts').toLowerCase()}',
                          color: AppColors.danger,
                          icon: Icons.notifications_none,
                        ),
                      ],
                    ),
                    const SizedBox(height: 14),
                    Padding(
                      padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 8),
                      child: Row(
                        children: [for (final (label, flex) in columns) Expanded(flex: flex, child: SectionLabel(context.tr(label)))],
                      ),
                    ),
                    const Divider(height: 1),
                    Expanded(
                      child: ListView.separated(
                        itemCount: _sequences.length,
                        separatorBuilder: (_, _) => const Divider(height: 1),
                        itemBuilder: (context, i) => _sequenceRow(_sequences[i], run, wide),
                      ),
                    ),
                  ],
                );
              },
            ),
    );
  }

  Widget _sequenceRow(dynamic seq, dynamic run, bool wide) {
    final alerts = _alerts.where((a) => a['sequenceResultId'] == seq['id']).toList();
    final failed = seq['errors'] != null;
    final int mutations = (seq['totalSubstitutions'] ?? 0) + (seq['totalDeletions'] ?? 0) + (seq['totalInsertions'] ?? 0);
    final coverage = (seq['coverage'] as num?)?.toDouble();

    Widget cell(int flex, Widget child) => Expanded(
      flex: flex,
      child: Align(alignment: Alignment.centerLeft, child: child),
    );

    return InkWell(
      onTap: failed ? null : () => _showDetail(seq, run, alerts),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 11, horizontal: 8),
        child: Row(
          children: [
            cell(
              4,
              Padding(
                padding: const EdgeInsets.only(right: 12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Tooltip(
                      message: seq['seqName'],
                      child: Text(
                        seq['seqName'],
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(fontWeight: FontWeight.w600),
                      ),
                    ),
                    if (!wide && !failed && coverage != null)
                      Text(
                        '${(coverage * 100).toStringAsFixed(1)}% · $mutations ${context.tr('col.mutations').toLowerCase()}',
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                  ],
                ),
              ),
            ),
            if (failed)
              Expanded(
                flex: wide ? 18 : 13,
                child: Align(
                  alignment: Alignment.centerLeft,
                  child: Tooltip(
                    message: seq['errors'],
                    child: Pill(text: context.tr('samples.notAnalysed'), color: AppColors.danger, icon: Icons.error_outline),
                  ),
                ),
              )
            else ...[
              cell(
                3,
                Padding(
                  padding: const EdgeInsets.only(right: 8),
                  child: CladePill(clade: seq['clade'], display: seq['cladeDisplay']),
                ),
              ),
              cell(
                3,
                Text(
                  seq['lineage'] ?? '—',
                  overflow: TextOverflow.ellipsis,
                  style: monoStyle.copyWith(color: AppColors.primary),
                ),
              ),
              cell(2, QcPill(status: seq['qcStatus'])),
              if (wide)
                cell(
                  3,
                  Padding(
                    padding: const EdgeInsets.only(right: 16),
                    child: CoverageBar(value: coverage),
                  ),
                ),
              if (wide) cell(2, Text('$mutations', style: const TextStyle(color: AppColors.textMuted))),
              cell(
                5,
                Wrap(
                  spacing: 4,
                  runSpacing: 4,
                  children: [
                    for (final a in alerts.take(3))
                      VariantTag(shortVariant(a['variant']), color: AppColors.danger, tooltip: a['threatName']),
                    if (alerts.length > 3)
                      Tooltip(
                        message: alerts.skip(3).map((a) => shortVariant(a['variant'])).join(', '),
                        child: Text('+${alerts.length - 3}', style: const TextStyle(color: AppColors.textMuted)),
                      ),
                  ],
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Future<void> _showDetail(dynamic seq, dynamic run, List<dynamic> alerts) async {
    List<dynamic> mutations = [];
    try {
      mutations = await Api.list('/sequences/${seq['id']}/mutations');
    } catch (_) {}
    if (!mounted) return;

    showDialog(
      context: context,
      builder: (context) => _SequenceDetailDialog(seq: seq, dataset: run['nextcladeDataset'], mutations: mutations, alerts: alerts),
    );
  }
}

// ---------- Sequence detail ----------
class _SequenceDetailDialog extends StatelessWidget {
  final dynamic seq;
  final String? dataset;
  final List<dynamic> mutations;
  final List<dynamic> alerts;

  const _SequenceDetailDialog({required this.seq, required this.dataset, required this.mutations, required this.alerts});

  @override
  Widget build(BuildContext context) {
    final alertLabels = alerts.map((a) => (a['variant'] as String).split(' (').first).toSet();
    final aa = mutations.where((m) => m['kind'] == 'AminoAcidSubstitution' || m['kind'] == 'AminoAcidDeletion').toList();
    final frameshifts = mutations.where((m) => m['kind'] == 'FrameShift').toList();
    final nuc = mutations.where((m) => (m['kind'] as String).startsWith('Nucleotide')).toList();

    // Білкові зміни групуються за генами в порядку геному.
    final byGene = <String, List<dynamic>>{};
    for (final m in aa) {
      byGene.putIfAbsent(m['gene'] ?? '?', () => []).add(m);
    }

    final bool sarsCov2 = dataset?.contains('sars-cov-2') ?? false;
    final substitutions = nuc.where((m) => m['kind'] == 'NucleotideSubstitution').map<int>((m) => m['position']).toList();
    final deletions = nuc
        .where((m) => m['kind'] == 'NucleotideDeletion')
        .map((m) => GenomeMarker(m['position'], m['endPosition'] ?? m['position']))
        .toList();
    final markers = alerts
        .where((a) => (a['ntStart'] ?? 0) > 0)
        .map((a) => GenomeMarker(a['ntStart'], a['ntEnd'], shortVariant(a['variant'])))
        .toList();
    final maxPosition = [...substitutions, ...deletions.map((d) => d.end), 1].reduce((a, b) => a > b ? a : b);

    return Dialog(
      insetPadding: const EdgeInsets.all(40),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 980),
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      seq['seqName'],
                      overflow: TextOverflow.ellipsis,
                      style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontSize: 20),
                    ),
                  ),
                  IconButton(onPressed: () => Navigator.pop(context), icon: const Icon(Icons.close)),
                ],
              ),
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                runSpacing: 6,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  CladePill(clade: seq['clade'], display: seq['cladeDisplay']),
                  if (seq['lineage'] != null) Pill(text: seq['lineage'], color: AppColors.primary, mono: true),
                  QcPill(status: seq['qcStatus']),
                  if (seq['coverage'] != null)
                    Pill(
                      text: context.tr('detail.coverage', {'value': '${((seq['coverage'] as num) * 100).toStringAsFixed(1)}%'}),
                      color: AppColors.textMuted,
                    ),
                  Pill(text: datasetName(dataset), color: AppColors.nextclade),
                ],
              ),
              const SizedBox(height: 20),
              Flexible(
                child: SingleChildScrollView(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      SectionLabel(context.tr('detail.genomeMap')),
                      const SizedBox(height: 2),
                      Text(context.tr('detail.genomeMapHint'), style: Theme.of(context).textTheme.bodySmall),
                      const SizedBox(height: 8),
                      GenomeMap(
                        length: sarsCov2 ? 29903 : (maxPosition * 1.02).round(),
                        genes: sarsCov2 ? sarsCov2Genes : const [],
                        substitutions: substitutions,
                        deletions: deletions,
                        alerts: markers,
                      ),
                      const SizedBox(height: 20),
                      SectionLabel('${context.tr('detail.aa')} (${aa.length})'),
                      const SizedBox(height: 10),
                      for (final entry in byGene.entries)
                        Padding(
                          padding: const EdgeInsets.only(bottom: 10),
                          child: Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              SizedBox(
                                width: 64,
                                child: Padding(
                                  padding: const EdgeInsets.only(top: 3),
                                  child: Text(entry.key, style: const TextStyle(fontWeight: FontWeight.w600)),
                                ),
                              ),
                              Expanded(
                                child: Wrap(
                                  spacing: 5,
                                  runSpacing: 5,
                                  children: [
                                    for (final m in entry.value)
                                      VariantTag(
                                        (m['label'] as String).substring(entry.key.length + 1),
                                        color: alertLabels.contains(m['label'])
                                            ? AppColors.danger
                                            : (m['kind'] == 'AminoAcidDeletion' ? AppColors.warning : AppColors.text),
                                      ),
                                  ],
                                ),
                              ),
                            ],
                          ),
                        ),
                      if (frameshifts.isNotEmpty) ...[
                        const SizedBox(height: 6),
                        SectionLabel(context.tr('detail.frameshifts')),
                        const SizedBox(height: 8),
                        Wrap(spacing: 5, children: [for (final f in frameshifts) VariantTag(f['label'], color: AppColors.danger)]),
                      ],
                      const SizedBox(height: 8),
                      Theme(
                        data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
                        child: ExpansionTile(
                          tilePadding: EdgeInsets.zero,
                          title: SectionLabel(context.tr('detail.nuc', {'n': nuc.length})),
                          children: [
                            Align(
                              alignment: Alignment.centerLeft,
                              child: Wrap(
                                spacing: 5,
                                runSpacing: 5,
                                children: [
                                  for (final m in nuc)
                                    VariantTag(
                                      m['label'],
                                      color: m['kind'] == 'NucleotideSubstitution' ? AppColors.text : AppColors.warning,
                                    ),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
