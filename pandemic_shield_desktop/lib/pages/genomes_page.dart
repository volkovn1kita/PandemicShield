import 'dart:async';

import 'package:flutter/material.dart';

import '../api.dart';
import '../l10n.dart';
import '../store.dart';
import '../theme.dart';
import '../report_button.dart';
import '../widgets.dart';
import 'amr_view.dart';

// ==========================================
// GENOMES: результати Nextclade для запуску
// ==========================================
class GenomesPage extends StatefulWidget {
  final AppStore store;
  final bool active;
  final String? selectedRunId;
  final ValueChanged<String> onSelectRun;

  const GenomesPage({super.key, required this.store, required this.active, required this.selectedRunId, required this.onSelectRun});

  @override
  State<GenomesPage> createState() => _GenomesPageState();
}

class _GenomesPageState extends State<GenomesPage> {
  final _search = TextEditingController();

  List<dynamic> _sequences = [];
  List<dynamic> _alerts = [];
  Map<String, dynamic> _marks = {};
  String? _loadedRunId;
  String? _selectedSequenceId;
  String? _pendingSequenceId;
  List<dynamic>? _hits;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _load();
    _timer = Timer.periodic(const Duration(seconds: 3), (_) {
      if (widget.active) _load();
    });
  }

  @override
  void didUpdateWidget(covariant GenomesPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.selectedRunId != oldWidget.selectedRunId || (widget.active && !oldWidget.active)) _load();
  }

  @override
  void dispose() {
    _timer?.cancel();
    _search.dispose();
    super.dispose();
  }

  dynamic get _run => widget.store.runs.where((r) => r['id'] == widget.selectedRunId).firstOrNull;

  Future<void> _load() async {
    final runId = widget.selectedRunId;
    if (runId == null || _run?['category'] == 2) return;
    try {
      final results = await Future.wait([
        Api.list('/runs/$runId/sequences'),
        Api.list('/runs/$runId/threats'),
        Api.list('/runs/$runId/genome-marks'),
      ]);
      if (!mounted || runId != widget.selectedRunId) return;
      setState(() {
        if (_loadedRunId != runId) _selectedSequenceId = _pendingSequenceId;
        _pendingSequenceId = null;
        _sequences = results[0];
        _alerts = results[1].where((t) => t['source'] == 'nextclade').toList();
        _marks = {for (final m in results[2]) m['sequenceResultId'] as String: m};
        _loadedRunId = runId;
      });
    } catch (_) {}
  }

  Future<void> _runSearch(String text) async {
    final label = text.trim();
    if (label.isEmpty) {
      setState(() => _hits = null);
      return;
    }
    try {
      final hits = await Api.list('/mutations', {'label': label});
      if (mounted) setState(() => _hits = hits);
    } catch (_) {
      if (mounted) showMessage(context, context.tr('common.connectionFailed'), error: true);
    }
  }

  void _clearSearch() {
    _search.clear();
    setState(() => _hits = null);
  }

  Future<void> _rerun() async {
    final run = _run;
    if (run == null) return;
    String? dataset = run['nextcladeDataset'] ?? widget.store.datasets.where((d) => d.contains('sars-cov-2')).firstOrNull;

    final chosen = await showDialog<String>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          title: Text(context.tr('genomes.rerunTitle'), style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600)),
          content: SizedBox(
            width: 420,
            child: FieldLabel(
              context.tr('upload.dataset'),
              hint: context.tr('genomes.rerunHint'),
              child: DatasetDropdown(
                datasets: widget.store.datasets,
                value: dataset,
                nullLabel: '—',
                onChanged: (v) => setDialogState(() => dataset = v),
              ),
            ),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context), child: Text(context.tr('common.cancel'))),
            FilledButton(
              onPressed: dataset == null ? null : () => Navigator.pop(context, dataset),
              child: Text(context.tr('genomes.rerun')),
            ),
          ],
        ),
      ),
    );
    if (chosen == null) return;

    try {
      await Api.post('/runs/${run['id']}/nextclade', {'dataset': chosen});
      setState(() => _selectedSequenceId = null);
      widget.store.refresh();
      _load();
    } on ApiException catch (e) {
      if (mounted) showMessage(context, e.statusCode == 409 ? context.tr('genomes.rerunUnavailable') : e.message, error: true);
    } catch (_) {
      if (mounted) showMessage(context, context.tr('common.connectionFailed'), error: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final run = _run;
    // Бактеріальні збірки аналізує AMRFinderPlus: інша таблиця, інший пошук.
    if (run?['category'] == 2) {
      return AmrRunView(key: ValueKey(run['id']), store: widget.store, run: run, active: widget.active, onSelectRun: widget.onSelectRun);
    }
    final selected = _sequences.where((s) => s['id'] == _selectedSequenceId).firstOrNull;
    final status = (run?['nextcladeStatus'] ?? 0) as int;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TopBar(
          title: RunPicker(store: widget.store, run: run, onSelect: widget.onSelectRun),
          actions: [
            SizedBox(
              width: 300,
              height: 34,
              child: TextField(
                controller: _search,
                style: mono(context, size: 12.5),
                decoration: InputDecoration(
                  hintText: context.tr('genomes.search'),
                  hintStyle: TextStyle(fontFamily: sansFont, fontSize: 12.5, color: context.p.faint),
                  prefixIcon: Icon(Icons.search, size: 16, color: context.p.muted),
                  prefixIconConstraints: const BoxConstraints(minWidth: 34),
                  contentPadding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
                  suffixIcon: _hits == null
                      ? null
                      : IconButton(onPressed: _clearSearch, icon: const Icon(Icons.close, size: 14), padding: EdgeInsets.zero),
                ),
                onSubmitted: _runSearch,
              ),
            ),
            if (run != null) ReportButton(run: run),
            if (run != null)
              OutlinedButton.icon(
                onPressed: status == 1 || status == 2 ? null : _rerun,
                icon: const Icon(Icons.refresh, size: 15),
                label: Text(context.tr('genomes.rerun')),
              ),
          ],
        ),
        Expanded(
          child: _hits != null
              ? _buildSearchResults()
              : run == null
              ? EmptyHint(context.tr('genomes.noRun'))
              : Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Expanded(child: _buildTable(run, status)),
                    if (selected != null)
                      _DetailPanel(
                        key: ValueKey(selected['id']),
                        seq: selected,
                        dataset: run['nextcladeDataset'],
                        alerts: _alerts.where((a) => a['sequenceResultId'] == selected['id']).toList(),
                        onClose: () => setState(() => _selectedSequenceId = null),
                      ),
                  ],
                ),
        ),
      ],
    );
  }

  Widget _buildTable(dynamic run, int status) {
    final p = context.p;
    if (_sequences.isEmpty || _loadedRunId != run['id']) {
      if (status == 1 || status == 2) {
        return Center(
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              SizedBox(width: 14, height: 14, child: CircularProgressIndicator(strokeWidth: 1.6, color: p.muted)),
              const SizedBox(width: 10),
              Text(context.tr('genomes.waiting'), style: TextStyle(color: p.muted, fontSize: 13)),
            ],
          ),
        );
      }
      return EmptyHint(context.tr('genomes.empty'));
    }

    final bool sarsCov2 = (run['nextcladeDataset'] as String?)?.contains('sars-cov-2') ?? false;
    final analysed = _sequences.where((s) => s['errors'] == null).length;

    return LayoutBuilder(
      builder: (context, constraints) {
        // Чим вужча таблиця (наприклад, коли відкрито панель деталей), тим менше другорядних колонок.
        final full = constraints.maxWidth >= 980;
        final medium = constraints.maxWidth >= 680;
        final columns = [
          Col(context.tr('col.genome'), flex: 3),
          Col(context.tr('col.clade'), flex: 2),
          if (medium) Col(context.tr('col.lineage'), width: 110),
          if (full) Col(context.tr('col.qc'), width: 100),
          if (full) Col(context.tr('col.coverage'), width: 80, alignRight: true),
          if (full) const Col('', width: 20),
          if (medium) Col(context.tr('col.mutations'), flex: 4),
          if (medium) const Col('', width: 16),
          Col(context.tr('nav.alerts'), flex: 2),
        ];

        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 12, 20, 12),
              child: Text(
                [
                  datasetName(run['nextcladeDataset']),
                  context.tr('genomes.summary', {'ok': analysed, 'n': _sequences.length}),
                  '${_alerts.length} ${context.tr('nav.alerts').toLowerCase()}',
                ].join('  ·  '),
                style: TextStyle(fontSize: 12.5, color: p.muted),
              ),
            ),
            TableHeader(columns),
            Expanded(
              child: ListView.builder(
                itemCount: _sequences.length,
                itemBuilder: (context, i) {
                  final seq = _sequences[i];
                  final failed = seq['errors'] != null;
                  final alerts = _alerts.where((a) => a['sequenceResultId'] == seq['id']).toList();
                  final marks = _marks[seq['id']];
                  final coverage = (seq['coverage'] as num?)?.toDouble();
                  final substitutions = List<int>.from(marks?['substitutions'] ?? const []);
                  final deletions = [for (final d in (marks?['deletions'] ?? const [])) GenomeMarker(d[0], d[1])];
                  final length = sarsCov2
                      ? sarsCov2Length
                      : ([...substitutions, ...deletions.map((d) => d.end), 1].reduce((a, b) => a > b ? a : b) * 1.02).round();

                  return TableRowItem(
                    columns: columns,
                    selected: seq['id'] == _selectedSequenceId,
                    onTap: failed ? null : () => setState(() => _selectedSequenceId = seq['id']),
                    cells: [
                      Tooltip(
                        message: seq['seqName'],
                        child: Text(
                          seq['seqName'],
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w500),
                        ),
                      ),
                      failed
                          ? Tooltip(
                              message: seq['errors'],
                              child: Text(
                                context.tr('genomes.failed'),
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: TextStyle(fontSize: 13, color: p.danger),
                              ),
                            )
                          : CladeLabel(clade: seq['clade'], display: seq['cladeDisplay']),
                      if (medium) Text(seq['lineage'] ?? '', overflow: TextOverflow.ellipsis, style: mono(context, size: 12.5)),
                      if (full) QcLabel(status: failed ? null : seq['qcStatus']),
                      if (full)
                        Text(
                          coverage == null ? '' : '${(coverage * 100).toStringAsFixed(1)}%',
                          style: TextStyle(fontSize: 13, color: p.muted, fontFeatures: const [FontFeature.tabularFigures()]),
                        ),
                      if (full) const SizedBox(),
                      if (medium)
                        failed
                            ? const SizedBox()
                            : Tooltip(
                                message:
                                    '${substitutions.length} ${context.tr('detail.substitutions')}, ${deletions.length} ${context.tr('detail.deletions')}',
                                child: MiniGenome(
                                  length: length,
                                  genes: sarsCov2 ? sarsCov2Genes : const [],
                                  substitutions: substitutions,
                                  deletions: deletions,
                                  alerts: [
                                    for (final a in alerts)
                                      if ((a['ntStart'] ?? 0) > 0) a['ntStart'] as int,
                                  ],
                                ),
                              ),
                      if (medium) const SizedBox(),
                      Tooltip(
                        message: alerts.map((a) => a['threatName']).join('\n'),
                        child: Text(
                          alerts.map((a) => shortVariant(a['variant'])).join(', '),
                          overflow: TextOverflow.ellipsis,
                          style: mono(context, size: 12, color: p.danger),
                        ),
                      ),
                    ],
                  );
                },
              ),
            ),
          ],
        );
      },
    );
  }

  Widget _buildSearchResults() {
    final p = context.p;
    final hits = _hits!;
    final label = _search.text.trim();
    final columns = [
      Col(context.tr('col.genome'), flex: 3),
      Col(context.tr('col.run'), flex: 3),
      Col(context.tr('col.clade'), flex: 2),
      Col(context.tr('col.lineage'), flex: 2),
      Col(context.tr('col.qc'), width: 100),
    ];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 12, 20, 12),
          child: Text(
            hits.isEmpty
                ? context.tr('genomes.searchNone', {'label': label})
                : context.tr('genomes.searchResult', {'label': label, 'n': hits.length}),
            style: TextStyle(fontSize: 12.5, color: p.muted),
          ),
        ),
        if (hits.isNotEmpty) ...[
          TableHeader(columns),
          Expanded(
            child: ListView.builder(
              itemCount: hits.length,
              itemBuilder: (context, i) {
                final hit = hits[i];
                return TableRowItem(
                  columns: columns,
                  onTap: () {
                    _pendingSequenceId = hit['sequenceId'];
                    _clearSearch();
                    if (hit['runId'] == widget.selectedRunId) {
                      setState(() => _selectedSequenceId = hit['sequenceId']);
                    } else {
                      widget.onSelectRun(hit['runId']);
                    }
                  },
                  cells: [
                    Text(
                      hit['seqName'],
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w500),
                    ),
                    Text(
                      hit['fileName'],
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(fontSize: 13, color: p.muted),
                    ),
                    CladeLabel(clade: hit['clade'], display: hit['cladeDisplay']),
                    Text(hit['lineage'] ?? '', style: mono(context, size: 12.5)),
                    QcLabel(status: hit['qcStatus']),
                  ],
                );
              },
            ),
          ),
        ],
      ],
    );
  }
}

/// Вибір запуску в заголовку сторінки.
class RunPicker extends StatelessWidget {
  final AppStore store;
  final dynamic run;
  final ValueChanged<String> onSelect;

  const RunPicker({super.key, required this.store, required this.run, required this.onSelect});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return Row(
      children: [
        Text(context.tr('nav.genomes')),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 8),
          child: Text('/', style: TextStyle(color: p.faint)),
        ),
        Flexible(
          child: PopupMenuButton<String>(
            tooltip: '',
            onSelected: onSelect,
            position: PopupMenuPosition.under,
            itemBuilder: (context) => [
              for (final r in store.runs)
                PopupMenuItem(
                  value: r['id'] as String,
                  height: 36,
                  child: Row(
                    children: [
                      Expanded(child: Text(r['fileName'], overflow: TextOverflow.ellipsis)),
                      const SizedBox(width: 16),
                      Text(relativeTime(context, r['startedAt']), style: TextStyle(fontSize: 12, color: p.muted)),
                    ],
                  ),
                ),
            ],
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: 6),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Flexible(
                    child: Text(
                      run?['fileName'] ?? context.tr('genomes.noRun'),
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(fontSize: 14, fontWeight: FontWeight.w500, color: run == null ? p.muted : p.text),
                    ),
                  ),
                  const SizedBox(width: 4),
                  Icon(Icons.expand_more, size: 16, color: p.muted),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }
}

// ==========================================
// DETAIL PANEL
// ==========================================
class _DetailPanel extends StatefulWidget {
  final dynamic seq;
  final String? dataset;
  final List<dynamic> alerts;
  final VoidCallback onClose;

  const _DetailPanel({super.key, required this.seq, required this.dataset, required this.alerts, required this.onClose});

  @override
  State<_DetailPanel> createState() => _DetailPanelState();
}

class _DetailPanelState extends State<_DetailPanel> {
  List<dynamic> _mutations = [];
  bool _showNucleotides = false;

  @override
  void initState() {
    super.initState();
    Api.list('/sequences/${widget.seq['id']}/mutations').then((m) {
      if (mounted) setState(() => _mutations = m);
    }, onError: (_) {});
  }

  Widget _section(String title, Widget child) {
    return Padding(
      padding: const EdgeInsets.only(top: 22),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            title,
            style: TextStyle(fontSize: 12.5, fontWeight: FontWeight.w600, color: context.p.text),
          ),
          const SizedBox(height: 10),
          child,
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final seq = widget.seq;
    final alertLabels = widget.alerts.map((a) => (a['variant'] as String).split(' (').first).toSet();
    final aa = _mutations.where((m) => m['kind'] == 'AminoAcidSubstitution' || m['kind'] == 'AminoAcidDeletion').toList();
    final frameshifts = _mutations.where((m) => m['kind'] == 'FrameShift').toList();
    final nucleotide = _mutations.where((m) => (m['kind'] as String).startsWith('Nucleotide')).toList();
    final byGene = <String, List<dynamic>>{};
    for (final m in aa) {
      byGene.putIfAbsent(m['gene'] ?? '?', () => []).add(m);
    }

    final bool sarsCov2 = widget.dataset?.contains('sars-cov-2') ?? false;
    final substitutions = [
      for (final m in nucleotide)
        if (m['kind'] == 'NucleotideSubstitution') m['position'] as int,
    ];
    final deletions = [
      for (final m in nucleotide)
        if (m['kind'] == 'NucleotideDeletion') GenomeMarker(m['position'], m['endPosition'] ?? m['position']),
    ];
    final maxPosition = [...substitutions, ...deletions.map((d) => d.end), 1].reduce((a, b) => a > b ? a : b);
    final coverage = (seq['coverage'] as num?)?.toDouble();

    Widget meta(String label, Widget value) => Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        children: [
          SizedBox(
            width: 92,
            child: Text(label, style: TextStyle(fontSize: 12.5, color: p.muted)),
          ),
          Expanded(
            child: Align(alignment: Alignment.centerLeft, child: value),
          ),
        ],
      ),
    );

    return Container(
      width: 460,
      decoration: BoxDecoration(
        color: p.surface,
        border: Border(left: BorderSide(color: p.border)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            height: 44,
            padding: const EdgeInsets.only(left: 20, right: 8),
            decoration: BoxDecoration(
              border: Border(bottom: BorderSide(color: p.border)),
            ),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    seq['seqName'],
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 13.5, fontWeight: FontWeight.w600),
                  ),
                ),
                IconButton(tooltip: context.tr('common.close'), onPressed: widget.onClose, icon: const Icon(Icons.close, size: 16)),
              ],
            ),
          ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
              children: [
                meta(context.tr('col.clade'), CladeLabel(clade: seq['clade'], display: seq['cladeDisplay'])),
                meta(context.tr('col.lineage'), Text(seq['lineage'] ?? '—', style: mono(context))),
                meta(context.tr('col.qc'), QcLabel(status: seq['qcStatus'])),
                meta(
                  context.tr('col.coverage'),
                  Text(coverage == null ? '—' : '${(coverage * 100).toStringAsFixed(1)}%', style: const TextStyle(fontSize: 13)),
                ),
                meta(context.tr('col.dataset'), Text(datasetName(widget.dataset), style: const TextStyle(fontSize: 13))),
                if (widget.alerts.isNotEmpty)
                  _section(
                    context.tr('detail.alerts'),
                    Column(
                      children: [
                        for (final a in widget.alerts)
                          Padding(
                            padding: const EdgeInsets.only(bottom: 8),
                            child: Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                MutationToken(shortVariant(a['variant']), highlight: true),
                                const SizedBox(width: 10),
                                Expanded(
                                  child: Text(a['threatName'], style: TextStyle(fontSize: 12.5, color: p.muted, height: 1.35)),
                                ),
                              ],
                            ),
                          ),
                      ],
                    ),
                  ),
                _section(
                  context.tr('col.mutations'),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      GenomeMap(
                        length: sarsCov2 ? sarsCov2Length : (maxPosition * 1.02).round(),
                        genes: sarsCov2 ? sarsCov2Genes : const [],
                        substitutions: substitutions,
                        deletions: deletions,
                        alerts: [
                          for (final a in widget.alerts)
                            if ((a['ntStart'] ?? 0) > 0) GenomeMarker(a['ntStart'], a['ntEnd'], shortVariant(a['variant'])),
                        ],
                      ),
                      Text(context.tr('detail.legend'), style: TextStyle(fontSize: 11.5, color: p.faint)),
                    ],
                  ),
                ),
                _section(
                  '${context.tr('detail.protein')} · ${aa.length}',
                  Column(
                    children: [
                      for (final entry in byGene.entries)
                        Padding(
                          padding: const EdgeInsets.only(bottom: 8),
                          child: Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              SizedBox(
                                width: 52,
                                child: Padding(
                                  padding: const EdgeInsets.only(top: 2),
                                  child: Text(entry.key, style: TextStyle(fontSize: 12.5, color: p.muted)),
                                ),
                              ),
                              Expanded(
                                child: Wrap(
                                  spacing: 4,
                                  runSpacing: 4,
                                  children: [
                                    for (final m in entry.value)
                                      MutationToken(
                                        (m['label'] as String).substring(entry.key.length + 1),
                                        highlight: alertLabels.contains(m['label']),
                                        muted: m['kind'] == 'AminoAcidDeletion',
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
                if (frameshifts.isNotEmpty)
                  _section(
                    context.tr('detail.frameshifts'),
                    Wrap(spacing: 4, runSpacing: 4, children: [for (final f in frameshifts) MutationToken(f['label'], highlight: true)]),
                  ),
                _section(
                  '${context.tr('detail.nucleotide')} · ${nucleotide.length}',
                  _showNucleotides
                      ? Wrap(
                          spacing: 4,
                          runSpacing: 4,
                          children: [for (final m in nucleotide) MutationToken(m['label'], muted: m['kind'] != 'NucleotideSubstitution')],
                        )
                      : Align(
                          alignment: Alignment.centerLeft,
                          child: TextButton(
                            style: TextButton.styleFrom(padding: EdgeInsets.zero, minimumSize: const Size(0, 24)),
                            onPressed: () => setState(() => _showNucleotides = true),
                            child: Text('${nucleotide.take(6).map((m) => m['label']).join(', ')}${nucleotide.length > 6 ? ', …' : ''}'),
                          ),
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
