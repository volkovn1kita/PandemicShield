import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../api.dart';
import '../l10n.dart';
import '../store.dart';
import '../theme.dart';
import '../widgets.dart';
import 'amr_view.dart' show drugName, isSequenceType;

// ==========================================
// SURVEILLANCE: стійкість до антибіотиків на відкритих даних NCBI Pathogen Detection
// ==========================================
class SurveillancePage extends StatefulWidget {
  final AppStore store;
  final bool active;
  final ValueChanged<String> onOpenRun;

  const SurveillancePage({super.key, required this.store, required this.active, required this.onOpenRun});

  @override
  State<SurveillancePage> createState() => _SurveillancePageState();
}

class _SurveillancePageState extends State<SurveillancePage> {
  List<String> _taxgroups = const ['Klebsiella'];
  String _taxgroup = 'Klebsiella';
  String? _country;
  Map<String, dynamic>? _summary;
  List<dynamic> _isolates = [];
  List<dynamic> _syncs = [];
  String? _family;
  int? _year;
  String? _lastCompletedSync;
  final Set<String> _importing = {};
  Timer? _timer;
  StreamSubscription<ChangeEvent>? _changes;

  @override
  void initState() {
    super.initState();
    _loadTaxgroups();
    _poll();
    _changes = widget.store.events.listen((e) {
      if (widget.active && e.topic == 'surveillance') _poll();
    });
    _timer = Timer.periodic(const Duration(seconds: 3), (_) {
      if (widget.active && !widget.store.live) _poll();
    });
  }

  @override
  void didUpdateWidget(covariant SurveillancePage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.active && !oldWidget.active) _poll();
  }

  @override
  void dispose() {
    _timer?.cancel();
    _changes?.cancel();
    super.dispose();
  }

  Future<void> _loadTaxgroups() async {
    try {
      final list = List<String>.from(await Api.list('/surveillance/taxgroups'));
      if (mounted) setState(() => _taxgroups = list);
    } catch (_) {}
  }

  dynamic get _sync => _syncs.where((s) => s['taxgroup'] == _taxgroup).firstOrNull;

  Future<void> _poll() async {
    try {
      final syncs = await Api.list('/surveillance/syncs');
      if (!mounted) return;
      setState(() => _syncs = syncs);
      // Дані перечитуються, коли оновлення завершилось, або коли їх ще не завантажено.
      final sync = _sync;
      final completedId = sync != null && sync['status'] == 2 ? sync['id'] as String : null;
      if (_summary == null || _summary!['taxgroup'] != _taxgroup || completedId != _lastCompletedSync) {
        _lastCompletedSync = completedId;
        await _loadSummary();
      }
    } catch (_) {}
  }

  Future<void> _loadSummary() async {
    try {
      final summary = await Api.get('/surveillance/summary', {'taxgroup': _taxgroup, 'country': ?_country}) as Map<String, dynamic>;
      if (!mounted) return;
      setState(() {
        _summary = summary;
        _country = summary['country'];
      });
      await _loadIsolates();
    } catch (_) {}
  }

  Future<void> _loadIsolates() async {
    final country = _country;
    if (country == null) {
      setState(() => _isolates = []);
      return;
    }
    try {
      final isolates = await Api.list('/surveillance/isolates', {
        'taxgroup': _taxgroup,
        'country': country,
        'family': ?_family,
        if (_year != null) 'year': '$_year',
      });
      if (mounted) setState(() => _isolates = isolates);
    } catch (_) {}
  }

  void _selectTaxgroup(String taxgroup) {
    setState(() {
      _taxgroup = taxgroup;
      _country = null;
      _summary = null;
      _isolates = [];
      _family = null;
      _year = null;
      _lastCompletedSync = _sync?['id'];
    });
    _loadSummary();
  }

  void _filter({String? family, int? year}) {
    setState(() {
      _family = family;
      _year = year;
    });
    _loadIsolates();
  }

  Future<void> _startSync() async {
    final countries = await showDialog<String>(
      context: context,
      builder: (_) => _SyncDialog(taxgroup: _taxgroup, countries: _summary?['countries'] ?? const []),
    );
    if (countries == null) return;
    try {
      await Api.post('/surveillance/sync', {'taxgroup': _taxgroup, 'countries': countries});
      _poll();
    } on ApiException catch (e) {
      if (mounted) showMessage(context, e.message, error: true);
    } catch (_) {
      if (mounted) showMessage(context, context.tr('common.connectionFailed'), error: true);
    }
  }

  /// Ізолят з дашборда → імпорт його збірки з NCBI і власний аналіз (AMRFinderPlus, MLST).
  Future<void> _analyse(String assembly) async {
    setState(() => _importing.add(assembly));
    try {
      await Api.post('/imports/ncbi', {'accessions': assembly, 'category': 2});
      if (mounted) showMessage(context, context.tr('surv.analyseStarted', {'acc': assembly}));
      widget.store.refresh();
    } on ApiException catch (e) {
      if (mounted) showMessage(context, e.message, error: true);
      setState(() => _importing.remove(assembly));
    } catch (_) {
      if (mounted) showMessage(context, context.tr('common.connectionFailed'), error: true);
      setState(() => _importing.remove(assembly));
    }
  }

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final summary = _summary;
    final sync = _sync;
    final syncing = sync != null && (sync['status'] == 0 || sync['status'] == 1);
    final countries = List<dynamic>.from(summary?['countries'] ?? const []);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TopBar(
          title: Row(
            children: [
              Text(context.tr('nav.surveillance')),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 8),
                child: Text('/', style: TextStyle(color: p.faint)),
              ),
              PopupMenuButton<String>(
                tooltip: '',
                onSelected: _selectTaxgroup,
                position: PopupMenuPosition.under,
                itemBuilder: (context) => [
                  for (final t in _taxgroups)
                    PopupMenuItem(
                      value: t,
                      height: 36,
                      child: Text(taxgroupName(t), style: const TextStyle(fontStyle: FontStyle.italic)),
                    ),
                ],
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      taxgroupName(_taxgroup),
                      style: TextStyle(fontSize: 14, fontWeight: FontWeight.w500, fontStyle: FontStyle.italic, color: p.text),
                    ),
                    const SizedBox(width: 4),
                    Icon(Icons.expand_more, size: 16, color: p.muted),
                  ],
                ),
              ),
            ],
          ),
          actions: [
            if (countries.length > 1)
              ChoiceTabs<String>(
                options: [for (final c in countries) (c['name'] as String, c['name'] as String)],
                value: _country ?? countries.first['name'],
                onChanged: (c) {
                  setState(() {
                    _country = c;
                    _family = null;
                    _year = null;
                  });
                  _loadSummary();
                },
              ),
            OutlinedButton.icon(
              onPressed: syncing ? null : _startSync,
              icon: const Icon(Icons.sync, size: 15),
              label: Text(context.tr('surv.sync')),
            ),
          ],
        ),
        if (sync != null) _SyncStatus(sync: sync),
        Expanded(
          child: summary == null || (summary['isolates'] ?? 0) == 0
              ? _EmptyState(syncing: syncing, onSync: _startSync)
              : _buildDashboard(summary),
        ),
      ],
    );
  }

  Widget _buildDashboard(Map<String, dynamic> s) {
    final p = context.p;
    final int total = s['isolates'];
    String share(int n) => total == 0 ? '0%' : '${(100 * n / total).round()}%';
    final carbapenemases = List<dynamic>.from(s['carbapenemases']);
    final years = _fillYears(List<dynamic>.from(s['years']));

    return ListView(
      padding: const EdgeInsets.fromLTRB(20, 16, 20, 32),
      children: [
        // Головне одним реченням: скільки ізолятів і яка частка несе карбапенемазу.
        Text.rich(
          TextSpan(
            style: TextStyle(fontSize: 13, color: p.muted, height: 1.5),
            children: [
              TextSpan(
                text: context.tr('surv.isolates', {'n': total}),
                style: TextStyle(color: p.text, fontWeight: FontWeight.w600),
              ),
              TextSpan(text: '  ·  ${s['country']}'),
              if (s['firstYear'] != null) TextSpan(text: '  ·  ${s['firstYear']}–${s['lastYear']}'),
              TextSpan(text: '  ·  ${context.tr('surv.withAssembly', {'n': s['withAssembly']})}'),
            ],
          ),
        ),
        const SizedBox(height: 14),
        Wrap(
          spacing: 28,
          runSpacing: 10,
          children: [
            _Figure(label: context.tr('surv.carbapenemase'), value: share(s['carbapenemaseIsolates']), count: s['carbapenemaseIsolates'], alert: true),
            _Figure(label: context.tr('surv.cephalosporinase'), value: share(s['cephalosporinaseIsolates']), count: s['cephalosporinaseIsolates']),
            _Figure(label: context.tr('surv.colistin'), value: share(s['colistinIsolates']), count: s['colistinIsolates']),
          ],
        ),
        _Section(
          title: context.tr('surv.byYear'),
          trailing: _Legend(items: [(p.chartAlert, context.tr('surv.legendCarba')), (p.chartRest, context.tr('surv.legendRest'))]),
          child: _YearColumns(years: years, selected: _year, onSelect: (y) => _filter(family: _family, year: y == _year ? null : y)),
        ),
        LayoutBuilder(
          builder: (context, constraints) {
            final carbaBars = _Section(
              title: context.tr('surv.carbapenemases'),
              hint: context.tr('surv.carbapenemasesHint'),
              child: carbapenemases.isEmpty
                  ? Text(context.tr('surv.noCarbapenemases'), style: TextStyle(fontSize: 13, color: p.muted))
                  : _HBars(
                      items: [for (final c in carbapenemases) (c['family'] as String, c['isolates'] as int, null)],
                      total: total,
                      color: p.chartAlert,
                      selected: _family,
                      onTap: (f) => _filter(family: f == _family ? null : f, year: _year),
                    ),
            );
            final otherBars = _Section(
              title: context.tr('surv.otherGenes'),
              hint: context.tr('surv.otherGenesHint'),
              child: _HBars(
                items: [
                  for (final g in List<dynamic>.from(s['topGenes']).take(8))
                    (g['family'] as String, g['isolates'] as int, drugName(context, ((g['subclass'] ?? g['class']) as String).split('/').first)),
                ],
                total: total,
                color: p.chartRest,
                selected: _family,
                onTap: (f) => _filter(family: f == _family ? null : f, year: _year),
              ),
            );
            if (constraints.maxWidth < 900) return Column(children: [carbaBars, otherBars]);
            return Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(child: carbaBars),
                const SizedBox(width: 40),
                Expanded(child: otherBars),
              ],
            );
          },
        ),
        if (carbapenemases.isNotEmpty)
          _Section(
            title: context.tr('surv.heatmap'),
            hint: context.tr('surv.heatmapHint'),
            child: _Heatmap(families: carbapenemases, years: [for (final y in years) y['year'] as int]),
          ),
        LayoutBuilder(
          builder: (context, constraints) {
            final lists = [
              _Section(title: context.tr('surv.regions'), child: _CountList(items: s['regions'])),
              _Section(title: context.tr('surv.sources'), child: _CountList(items: s['sources'])),
              _Section(title: context.tr('surv.species'), child: _CountList(items: s['species'], italic: true)),
            ];
            if (constraints.maxWidth < 900) return Column(children: lists);
            return Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                for (int i = 0; i < lists.length; i++) ...[if (i > 0) const SizedBox(width: 40), Expanded(child: lists[i])],
              ],
            );
          },
        ),
        _Section(
          title: [
            context.tr('surv.table'),
            ?_family,
            if (_year != null) '$_year',
          ].join('  ·  '),
          trailing: _family != null || _year != null
              ? TextButton(onPressed: () => _filter(), child: Text(context.tr('surv.clearFilter')))
              : null,
          child: _IsolateTable(
            isolates: _isolates,
            importing: _importing,
            // Найновіший запуск кожної збірки (store.runs упорядковано від нових до старих).
            runs: {
              for (final r in widget.store.runs.reversed)
                if (r['sourceAccession'] != null) r['sourceAccession'] as String: r,
            },
            onAnalyse: _analyse,
            onOpenRun: widget.onOpenRun,
          ),
        ),
        const SizedBox(height: 18),
        Text(
          context.tr('surv.footer', {
            'pdg': s['pdgVersion'] ?? '—',
            'amrfinder': List<dynamic>.from(s['amrFinderVersions']).join(', '),
            'db': List<dynamic>.from(s['databaseVersions']).join(', '),
          }),
          style: TextStyle(fontSize: 11.5, color: p.faint, height: 1.5),
        ),
      ],
    );
  }

  /// Порожні роки між першим і останнім теж показуються: інакше вісь часу брехала б про інтервали.
  List<Map<String, dynamic>> _fillYears(List<dynamic> years) {
    if (years.isEmpty) return [];
    final byYear = {for (final y in years) y['year'] as int: y};
    final first = years.first['year'] as int, last = years.last['year'] as int;
    return [
      for (int y = first; y <= last; y++) Map<String, dynamic>.from(byYear[y] ?? {'year': y, 'isolates': 0, 'carbapenemase': 0}),
    ];
  }
}

String taxgroupName(String taxgroup) => switch (taxgroup) {
  'Escherichia_coli_Shigella' => 'Escherichia coli / Shigella',
  _ => taxgroup.replaceAll('_', ' '),
};

// ==========================================
// PIECES
// ==========================================
class _SyncStatus extends StatelessWidget {
  final dynamic sync;
  const _SyncStatus({required this.sync});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final status = sync['status'] as int;
    final active = status == 0 || status == 1;
    final total = (sync['bytesTotal'] ?? 0) as int;
    final read = (sync['bytesRead'] ?? 0) as int;

    final String text = switch (status) {
      0 => context.tr('surv.syncQueued'),
      1 => context.tr('surv.syncRunning', {
        'mb': (read / 1e6).toStringAsFixed(0),
        'total': (total / 1e6).toStringAsFixed(0),
        'rows': sync['rowsScanned'],
        'found': sync['isolatesStored'],
      }),
      2 => context.tr('surv.syncDone', {'when': relativeTime(context, sync['completedAt']), 'pdg': sync['pdgVersion'] ?? ''}),
      _ => '${context.tr('surv.syncFailed')}: ${sync['message'] ?? ''}',
    };

    return Container(
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 8),
      decoration: BoxDecoration(border: Border(bottom: BorderSide(color: p.border))),
      child: Row(
        children: [
          Icon(status == 3 ? Icons.error_outline : Icons.public, size: 15, color: status == 3 ? p.danger : p.faint),
          const SizedBox(width: 10),
          if (active && total > 0) ...[
            SizedBox(
              width: 120,
              child: ClipRRect(
                borderRadius: BorderRadius.circular(2),
                child: LinearProgressIndicator(value: read / total, minHeight: 3, backgroundColor: p.hover, color: p.muted),
              ),
            ),
            const SizedBox(width: 12),
          ],
          Expanded(
            child: Text(
              text,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(fontSize: 12.5, color: status == 3 ? p.danger : p.muted),
            ),
          ),
        ],
      ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  final bool syncing;
  final VoidCallback onSync;
  const _EmptyState({required this.syncing, required this.onSync});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 460),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(context.tr('surv.emptyTitle'), style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: p.text)),
            const SizedBox(height: 8),
            Text(
              context.tr('surv.emptyText'),
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 13, color: p.muted, height: 1.5),
            ),
            const SizedBox(height: 16),
            if (!syncing) FilledButton(onPressed: onSync, child: Text(context.tr('surv.sync'))),
          ],
        ),
      ),
    );
  }
}

class _Section extends StatelessWidget {
  final String title;
  final String? hint;
  final Widget? trailing;
  final Widget child;
  const _Section({required this.title, required this.child, this.hint, this.trailing});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return Padding(
      padding: const EdgeInsets.only(top: 28),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(title, style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: p.text)),
              ),
              ?trailing,
            ],
          ),
          if (hint != null) ...[const SizedBox(height: 3), Text(hint!, style: TextStyle(fontSize: 12, color: p.muted))],
          const SizedBox(height: 12),
          child,
        ],
      ),
    );
  }
}

/// Частка ізолятів: велике число й підпис під ним (без плитки — лише типографіка).
class _Figure extends StatelessWidget {
  final String label;
  final String value;
  final int count;
  final bool alert;
  const _Figure({required this.label, required this.value, required this.count, this.alert = false});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.baseline,
          textBaseline: TextBaseline.alphabetic,
          children: [
            Text(value, style: TextStyle(fontSize: 26, fontWeight: FontWeight.w600, color: p.text, letterSpacing: -0.5)),
            const SizedBox(width: 6),
            Text('$count', style: TextStyle(fontSize: 12.5, color: p.muted)),
          ],
        ),
        Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            // Позначка того ж кольору, що й сегменти графіка: так число пов'язане з легендою.
            if (alert) ...[
              Container(width: 8, height: 8, decoration: BoxDecoration(color: p.chartAlert, borderRadius: BorderRadius.circular(2))),
              const SizedBox(width: 6),
            ],
            Text(label, style: TextStyle(fontSize: 12.5, color: p.muted)),
          ],
        ),
      ],
    );
  }
}

class _Legend extends StatelessWidget {
  final List<(Color, String)> items;
  const _Legend({required this.items});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        for (final (color, label) in items) ...[
          Container(width: 10, height: 10, decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(2))),
          const SizedBox(width: 6),
          Text(label, style: TextStyle(fontSize: 12, color: p.muted)),
          const SizedBox(width: 14),
        ],
      ],
    );
  }
}

/// Стовпчики за роками: знизу (від осі) — ізоляти з карбапенемазою, зверху — решта; підпис — усього за рік.
class _YearColumns extends StatelessWidget {
  final List<Map<String, dynamic>> years;
  final int? selected;
  final ValueChanged<int> onSelect;
  const _YearColumns({required this.years, required this.selected, required this.onSelect});

  static const double _plotHeight = 150;

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final max = years.fold<int>(1, (m, y) => math.max(m, y['isolates'] as int));

    return SizedBox(
      height: _plotHeight + 40,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          for (final y in years)
            Expanded(
              child: _YearColumn(
                year: y['year'],
                total: y['isolates'],
                alert: y['carbapenemase'],
                scale: _plotHeight / max,
                dimmed: selected != null && selected != y['year'],
                onTap: () => onSelect(y['year']),
                palette: p,
              ),
            ),
        ],
      ),
    );
  }
}

class _YearColumn extends StatelessWidget {
  final int year;
  final int total;
  final int alert;
  final double scale;
  final bool dimmed;
  final VoidCallback onTap;
  final Palette palette;

  const _YearColumn({
    required this.year,
    required this.total,
    required this.alert,
    required this.scale,
    required this.dimmed,
    required this.onTap,
    required this.palette,
  });

  @override
  Widget build(BuildContext context) {
    final p = palette;
    final rest = total - alert;
    final alertHeight = alert * scale;
    final restHeight = rest * scale;
    const radius = Radius.circular(4);

    return Tooltip(
      message: total == 0
          ? context.tr('surv.yearNone', {'year': year})
          : context.tr('surv.yearTooltip', {'year': year, 'n': total, 'alert': alert, 'pct': (100 * alert / total).round()}),
      waitDuration: const Duration(milliseconds: 150),
      child: InkWell(
        onTap: total == 0 ? null : onTap,
        child: Opacity(
          opacity: dimmed ? 0.35 : 1,
          child: Column(
            mainAxisAlignment: MainAxisAlignment.end,
            children: [
              if (total > 0)
                Text('$total', style: TextStyle(fontSize: 11.5, color: p.muted, fontFeatures: const [FontFeature.tabularFigures()])),
              const SizedBox(height: 4),
              // Смуга не ширша за 24 px; між сегментами — 2 px тла; закруглено лише верхній кінець.
              SizedBox(
                width: 24,
                child: Column(
                  children: [
                    if (rest > 0)
                      Container(
                        height: math.max(restHeight, 2),
                        decoration: BoxDecoration(color: p.chartRest, borderRadius: const BorderRadius.vertical(top: radius)),
                      ),
                    if (rest > 0 && alert > 0) const SizedBox(height: 2),
                    if (alert > 0)
                      Container(
                        height: math.max(alertHeight, 2),
                        decoration: BoxDecoration(
                          color: p.chartAlert,
                          borderRadius: rest == 0 ? const BorderRadius.vertical(top: radius) : BorderRadius.zero,
                        ),
                      ),
                  ],
                ),
              ),
              Container(height: 1, color: p.border),
              const SizedBox(height: 6),
              Text(
                "'${'$year'.substring(2)}",
                style: TextStyle(fontSize: 11.5, color: p.muted, fontFeatures: const [FontFeature.tabularFigures()]),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Горизонтальні смуги: родина гена, частка ізолятів; клік фільтрує таблицю.
class _HBars extends StatelessWidget {
  final List<(String, int, String?)> items;
  final int total;
  final Color color;
  final String? selected;
  final ValueChanged<String> onTap;
  const _HBars({required this.items, required this.total, required this.color, required this.selected, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return Column(
      children: [
        for (final (label, value, note) in items)
          InkWell(
            onTap: () => onTap(label),
            child: Opacity(
              opacity: selected != null && selected != label ? 0.4 : 1,
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 3),
                child: Row(
                  children: [
                    SizedBox(
                      width: 116,
                      child: Text(label, overflow: TextOverflow.ellipsis, style: mono(context, size: 12.5)),
                    ),
                    Expanded(
                      child: LayoutBuilder(
                        builder: (context, constraints) => Align(
                          alignment: Alignment.centerLeft,
                          child: Container(
                            width: math.max(2, constraints.maxWidth * value / math.max(total, 1)),
                            height: 12,
                            decoration: BoxDecoration(color: color, borderRadius: const BorderRadius.horizontal(right: Radius.circular(4))),
                          ),
                        ),
                      ),
                    ),
                    SizedBox(
                      width: 92,
                      child: Text(
                        '$value · ${(100 * value / math.max(total, 1)).round()}%',
                        textAlign: TextAlign.right,
                        style: TextStyle(fontSize: 12, color: p.muted, fontFeatures: const [FontFeature.tabularFigures()]),
                      ),
                    ),
                    if (note != null)
                      SizedBox(
                        width: 110,
                        child: Padding(
                          padding: const EdgeInsets.only(left: 12),
                          child: Text(note, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: p.faint)),
                        ),
                      ),
                  ],
                ),
              ),
            ),
          ),
      ],
    );
  }
}

/// Карбапенемази × роки: насиченість клітинки — кількість ізолятів (одна барва, від світлої до темної).
class _Heatmap extends StatelessWidget {
  final List<dynamic> families;
  final List<int> years;
  const _Heatmap({required this.families, required this.years});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    int max = 1;
    for (final f in families) {
      for (final v in Map<String, dynamic>.from(f['years']).values) {
        max = math.max(max, v as int);
      }
    }

    Widget cell(String text, {Color? fill, Color? color, bool header = false}) => Container(
      height: 28,
      margin: const EdgeInsets.all(1),
      alignment: Alignment.center,
      decoration: BoxDecoration(color: fill, borderRadius: BorderRadius.circular(3)),
      child: Text(
        text,
        style: TextStyle(fontSize: 11.5, color: color ?? p.muted, fontWeight: header ? FontWeight.w500 : FontWeight.w400),
      ),
    );

    return Table(
      columnWidths: {0: const FixedColumnWidth(116), for (int i = 1; i <= years.length; i++) i: const FlexColumnWidth()},
      children: [
        TableRow(children: [const SizedBox(), for (final y in years) cell("'${'$y'.substring(2)}", header: true)]),
        for (final f in families)
          TableRow(
            children: [
              Container(
                height: 30,
                alignment: Alignment.centerLeft,
                child: Text(f['family'], style: mono(context, size: 12.5)),
              ),
              for (final y in years)
                Builder(
                  builder: (context) {
                    final int v = (f['years']['$y'] ?? 0) as int;
                    if (v == 0) return cell('', fill: p.hover);
                    final t = v / max;
                    // Підпис усередині клітинки: світлий на насиченому тлі, звичайний — на блідому.
                    return Tooltip(
                      message: '${f['family']} · $y: $v',
                      child: cell('$v', fill: p.chartAlert.withValues(alpha: 0.15 + 0.85 * t), color: t > 0.5 ? Colors.white : p.text),
                    );
                  },
                ),
            ],
          ),
      ],
    );
  }
}

class _CountList extends StatelessWidget {
  final List<dynamic> items;
  final bool italic;
  const _CountList({required this.items, this.italic = false});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return Column(
      children: [
        for (final item in items)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 3),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    item['name'] == '—' ? context.tr('surv.unknown') : item['name'],
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontSize: 12.5,
                      color: item['name'] == '—' ? p.muted : p.text,
                      fontStyle: italic && item['name'] != '—' ? FontStyle.italic : null,
                    ),
                  ),
                ),
                Text('${item['isolates']}', style: TextStyle(fontSize: 12.5, color: p.muted, fontFeatures: const [FontFeature.tabularFigures()])),
              ],
            ),
          ),
      ],
    );
  }
}

class _IsolateTable extends StatelessWidget {
  final List<dynamic> isolates;
  final Set<String> importing;
  final Map<String, dynamic> runs;
  final ValueChanged<String> onAnalyse;
  final ValueChanged<String> onOpenRun;

  const _IsolateTable({
    required this.isolates,
    required this.importing,
    required this.runs,
    required this.onAnalyse,
    required this.onOpenRun,
  });

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final columns = [
      Col(context.tr('surv.colAssembly'), width: 150),
      Col(context.tr('surv.colDate'), width: 96),
      Col(context.tr('surv.colRegion'), flex: 2),
      Col(context.tr('surv.colSource'), flex: 2),
      Col(context.tr('surv.colCarba'), flex: 3),
      Col(context.tr('surv.colGenes'), width: 70, alignRight: true),
      // ST у даних NCBI не заповнено; його дає наш MLST після «Аналізувати».
      Col('ST', width: 70, alignRight: true),
      const Col('', width: 120),
    ];

    return Container(
      decoration: BoxDecoration(border: Border.all(color: p.border), borderRadius: BorderRadius.circular(8)),
      clipBehavior: Clip.antiAlias,
      child: Column(
        children: [
          TableHeader(columns),
          for (final i in isolates.take(300))
            Builder(
              builder: (context) {
                final String? assembly = i['assemblyAccession'];
                final run = assembly == null ? null : runs[assembly];
                final runId = run?['id'] ?? i['runId'];
                final String? st = run?['mlstSequenceType'];
                return TableRowItem(
                  columns: columns,
                  cells: [
                    Tooltip(
                      message: [i['targetAccession'], i['bioSample'], i['strain']].whereType<String>().join(' · '),
                      child: Text(assembly ?? i['targetAccession'], style: mono(context, size: 12)),
                    ),
                    Text(i['collectionDate'] ?? '—', style: TextStyle(fontSize: 12.5, color: p.muted)),
                    Text(i['region'] ?? '—', overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 12.5)),
                    Text(i['isolationSource'] ?? '—', overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12.5, color: p.muted)),
                    Wrap(
                      spacing: 4,
                      children: [for (final g in List<dynamic>.from(i['carbapenemases'])) MutationToken(g, highlight: true)],
                    ),
                    Text('${i['amrGeneCount']}', style: TextStyle(fontSize: 12.5, color: p.muted, fontFeatures: const [FontFeature.tabularFigures()])),
                    Text(
                      isSequenceType(st) ? 'ST$st' : '',
                      textAlign: TextAlign.right,
                      style: mono(context, size: 12, color: p.text),
                    ),
                    Align(
                      alignment: Alignment.centerRight,
                      child: runId != null
                          ? TextButton(onPressed: () => onOpenRun(runId), child: Text(context.tr('surv.open')))
                          : assembly == null
                          ? Tooltip(message: context.tr('surv.noAssembly'), child: Text('—', style: TextStyle(color: p.faint)))
                          : importing.contains(assembly)
                          ? Text(context.tr('surv.importing'), style: TextStyle(fontSize: 12, color: p.muted))
                          : TextButton(onPressed: () => onAnalyse(assembly), child: Text(context.tr('surv.analyse'))),
                    ),
                  ],
                );
              },
            ),
          if (isolates.length > 300)
            Padding(
              padding: const EdgeInsets.all(10),
              child: Text(context.tr('surv.more', {'n': isolates.length - 300}), style: TextStyle(fontSize: 12, color: p.muted)),
            ),
        ],
      ),
    );
  }
}

// ==========================================
// SYNC DIALOG
// ==========================================
class _SyncDialog extends StatefulWidget {
  final String taxgroup;
  final List<dynamic> countries;
  const _SyncDialog({required this.taxgroup, required this.countries});

  @override
  State<_SyncDialog> createState() => _SyncDialogState();
}

class _SyncDialogState extends State<_SyncDialog> {
  late final _countries = TextEditingController(
    text: widget.countries.isEmpty ? 'Ukraine' : widget.countries.map((c) => c['name']).join(', '),
  );

  @override
  void dispose() {
    _countries.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      titlePadding: const EdgeInsets.fromLTRB(20, 18, 20, 0),
      contentPadding: const EdgeInsets.fromLTRB(20, 16, 20, 4),
      actionsPadding: const EdgeInsets.fromLTRB(20, 12, 20, 16),
      title: Text(
        context.tr('surv.syncTitle', {'taxgroup': taxgroupName(widget.taxgroup)}),
        style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600),
      ),
      content: SizedBox(
        width: 440,
        child: FieldLabel(
          context.tr('surv.countries'),
          hint: context.tr('surv.countriesHint'),
          child: TextField(controller: _countries),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: Text(context.tr('common.cancel'))),
        FilledButton(onPressed: () => Navigator.pop(context, _countries.text), child: Text(context.tr('surv.syncStart'))),
      ],
    );
  }
}
