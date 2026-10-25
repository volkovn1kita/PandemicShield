import 'dart:async';

import 'package:flutter/material.dart';

import '../api.dart';
import '../l10n.dart';
import '../store.dart';
import '../theme.dart';
import '../widgets.dart';
import 'genomes_page.dart' show RunPicker;

// ==========================================
// AMR: результати AMRFinderPlus для бактеріальної збірки
// ==========================================

/// Назви груп антибіотиків українською (підкласи AMRFinderPlus). Невідомі показуються як є.
const Map<String, String> _drugNamesUk = {
  'CARBAPENEM': 'Карбапенеми',
  'CEPHALOSPORIN': 'Цефалоспорини',
  'BETA-LACTAM': 'Бета-лактами',
  'METHICILLIN': 'Метицилін',
  'COLISTIN': 'Колістин',
  'VANCOMYCIN': 'Ванкоміцин',
  'AMINOGLYCOSIDE': 'Аміноглікозиди',
  'GENTAMICIN': 'Гентаміцин',
  'AMIKACIN': 'Амікацин',
  'TOBRAMYCIN': 'Тобраміцин',
  'STREPTOMYCIN': 'Стрептоміцин',
  'KANAMYCIN': 'Канаміцин',
  'SULFONAMIDE': 'Сульфаніламіди',
  'TRIMETHOPRIM': 'Триметоприм',
  'TETRACYCLINE': 'Тетрацикліни',
  'QUINOLONE': 'Хінолони',
  'CIPROFLOXACIN': 'Ципрофлоксацин',
  'MACROLIDE': 'Макроліди',
  'AZITHROMYCIN': 'Азитроміцин',
  'ERYTHROMYCIN': 'Еритроміцин',
  'TELITHROMYCIN': 'Телітроміцин',
  'CLINDAMYCIN': 'Кліндаміцин',
  'LINEZOLID': 'Лінезолід',
  'DAPTOMYCIN': 'Даптоміцин',
  'TIGECYCLINE': 'Тигециклін',
  'CEFTAZIDIME': 'Цефтазидим',
  'CEFTAZIDIME-AVIBACTAM': 'Цефтазидим-авібактам',
  'MUPIROCIN': 'Мупіроцин',
  'FUSIDIC ACID': 'Фузидова кислота',
  'PHENICOL': 'Феніколи',
  'LINCOSAMIDE': 'Лінкозаміди',
  'STREPTOGRAMIN': 'Стрептограміни',
  'GLYCOPEPTIDE': 'Глікопептиди',
  'POLYMYXIN': 'Поліміксини',
  'NITROFURANTOIN': 'Нітрофурантоїн',
  'FOSFOMYCIN': 'Фосфоміцин',
  'CHLORAMPHENICOL': 'Хлорамфенікол',
  'FLORFENICOL': 'Флорфенікол',
  'RIFAMYCIN': 'Рифаміцини',
  'EFFLUX': 'Ефлюкс-насоси (кілька груп)',
  'PIPERACILLIN-TAZOBACTAM': 'Піперацилін-тазобактам',
  'AMOXICILLIN-CLAVULANIC ACID': 'Амоксицилін-клавуланат',
  'TICARCILLIN-CLAVULANIC ACID': 'Тикарцилін-клавуланат',
};

String drugName(BuildContext context, String subclass) {
  final key = subclass.replaceAll('_', ' ').toUpperCase();
  if (L10n.controllerOf(context).value == AppLanguage.uk && _drugNamesUk.containsKey(key)) return _drugNamesUk[key]!;
  final lower = key.toLowerCase();
  return lower.isEmpty ? lower : lower[0].toUpperCase() + lower.substring(1);
}

/// Вид у форматі AMRFinderPlus (Klebsiella_pneumoniae) → «Klebsiella pneumoniae».
String organismName(String? organism) => organism == null ? '—' : organism.replaceAll('_', ' ');

class AmrRunView extends StatefulWidget {
  final AppStore store;
  final dynamic run;
  final bool active;
  final ValueChanged<String> onSelectRun;

  const AmrRunView({super.key, required this.store, required this.run, required this.active, required this.onSelectRun});

  @override
  State<AmrRunView> createState() => _AmrRunViewState();
}

class _AmrRunViewState extends State<AmrRunView> {
  final _search = TextEditingController();
  List<dynamic> _hits = [];
  List<dynamic> _alerts = [];
  List<dynamic>? _searchHits;
  bool _loaded = false;
  String _type = 'all';
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
  void dispose() {
    _timer?.cancel();
    _search.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final id = widget.run['id'];
      final results = await Future.wait([Api.list('/runs/$id/amr'), Api.list('/runs/$id/threats')]);
      if (!mounted) return;
      setState(() {
        _hits = results[0];
        _alerts = results[1].where((t) => t['source'] == 'amrfinder').toList();
        _loaded = true;
      });
    } catch (_) {}
  }

  Future<void> _runSearch(String text) async {
    final gene = text.trim();
    if (gene.isEmpty) {
      setState(() => _searchHits = null);
      return;
    }
    try {
      final hits = await Api.list('/amr/search', {'gene': gene});
      if (mounted) setState(() => _searchHits = hits);
    } catch (_) {
      if (mounted) showMessage(context, context.tr('common.connectionFailed'), error: true);
    }
  }

  Future<void> _rerun() async {
    String? organism = widget.run['amrOrganism'];
    final chosen = await showDialog<(String?,)>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          title: Text(context.tr('amr.rerunTitle'), style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600)),
          content: SizedBox(
            width: 420,
            child: FieldLabel(
              context.tr('upload.organism'),
              hint: context.tr('amr.rerunHint'),
              child: OrganismDropdown(
                organisms: widget.store.organisms,
                value: organism,
                onChanged: (v) => setDialogState(() => organism = v),
              ),
            ),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context), child: Text(context.tr('common.cancel'))),
            FilledButton(onPressed: () => Navigator.pop(context, (organism,)), child: Text(context.tr('genomes.rerun'))),
          ],
        ),
      ),
    );
    if (chosen == null) return;

    try {
      await Api.post('/runs/${widget.run['id']}/amrfinder', {'organism': chosen.$1});
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
    final run = widget.run;
    final status = (run['amrStatus'] ?? 0) as int;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TopBar(
          title: RunPicker(store: widget.store, run: run, onSelect: widget.onSelectRun),
          actions: [
            SizedBox(
              width: 260,
              height: 34,
              child: TextField(
                controller: _search,
                style: mono(context, size: 12.5),
                decoration: InputDecoration(
                  hintText: context.tr('amr.search'),
                  hintStyle: TextStyle(fontFamily: sansFont, fontSize: 12.5, color: context.p.faint),
                  prefixIcon: Icon(Icons.search, size: 16, color: context.p.muted),
                  prefixIconConstraints: const BoxConstraints(minWidth: 34),
                  contentPadding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
                  suffixIcon: _searchHits == null
                      ? null
                      : IconButton(
                          onPressed: () {
                            _search.clear();
                            setState(() => _searchHits = null);
                          },
                          icon: const Icon(Icons.close, size: 14),
                          padding: EdgeInsets.zero,
                        ),
                ),
                onSubmitted: _runSearch,
              ),
            ),
            OutlinedButton.icon(
              onPressed: status == 1 || status == 2 ? null : _rerun,
              icon: const Icon(Icons.refresh, size: 15),
              label: Text(context.tr('genomes.rerun')),
            ),
          ],
        ),
        Expanded(child: _searchHits != null ? _buildSearchResults() : _buildBody(run, status)),
      ],
    );
  }

  Widget _buildBody(dynamic run, int status) {
    final p = context.p;
    if (status == 1 || status == 2) {
      return Center(
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            SizedBox(width: 14, height: 14, child: CircularProgressIndicator(strokeWidth: 1.6, color: p.muted)),
            const SizedBox(width: 10),
            Text(context.tr('amr.waiting'), style: TextStyle(color: p.muted, fontSize: 13)),
          ],
        ),
      );
    }
    if (status == 4) return EmptyHint(run['amrMessage'] ?? context.tr('nc.failed'));
    if (!_loaded || status == 0) return EmptyHint(context.tr('amr.empty'));

    final alertSymbols = _alerts.map((a) => a['variant'] as String).toSet();
    final summary = [
      organismName(run['amrOrganism']),
      'AMRFinderPlus',
      if (run['amrDatabaseVersion'] != null) context.tr('amr.database', {'version': run['amrDatabaseVersion']}),
      context.tr('amr.findings', {'n': _hits.length}),
      '${_alerts.length} ${context.tr('nav.alerts').toLowerCase()}',
    ].join('  ·  ');

    return ListView(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 12, 20, 4),
          child: Text(summary, style: TextStyle(fontSize: 12.5, color: p.muted)),
        ),
        _buildResistance(alertSymbols),
        _buildHits(alertSymbols),
      ],
    );
  }

  /// Зведення «до яких антибіотиків стійка бактерія» — як антибіотикограма, але з генів.
  Widget _buildResistance(Set<String> alertSymbols) {
    final p = context.p;
    final byDrug = <String, List<dynamic>>{};
    for (final hit in _hits.where((h) => h['type'] == 'AMR')) {
      for (final subclass in (hit['subclass'] as String).split('/')) {
        byDrug.putIfAbsent(subclass.trim(), () => []).add(hit);
      }
    }
    final drugs = byDrug.keys.toList()
      ..sort((a, b) {
        // Спершу ті, що викликали сповіщення (карбапенеми, колістин…), далі за алфавітом.
        final alertA = byDrug[a]!.any((h) => alertSymbols.contains(h['elementSymbol']));
        final alertB = byDrug[b]!.any((h) => alertSymbols.contains(h['elementSymbol']));
        return alertA != alertB ? (alertA ? -1 : 1) : drugName(context, a).compareTo(drugName(context, b));
      });

    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 16, 20, 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            context.tr('amr.resistance'),
            style: TextStyle(fontSize: 13.5, fontWeight: FontWeight.w600, color: p.text),
          ),
          const SizedBox(height: 4),
          Text(context.tr('amr.resistanceHint'), style: TextStyle(fontSize: 12, color: p.muted)),
          const SizedBox(height: 12),
          if (drugs.isEmpty)
            Text(context.tr('amr.none'), style: TextStyle(fontSize: 13, color: p.muted))
          else
            Container(
              decoration: BoxDecoration(
                border: Border.all(color: p.border),
                borderRadius: BorderRadius.circular(8),
              ),
              child: Column(
                children: [
                  for (int i = 0; i < drugs.length; i++)
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 9),
                      decoration: BoxDecoration(
                        border: i == 0 ? null : Border(top: BorderSide(color: p.border)),
                      ),
                      child: Row(
                        children: [
                          SizedBox(
                            width: 260,
                            child: Row(
                              children: [
                                Container(
                                  width: 6,
                                  height: 6,
                                  decoration: BoxDecoration(
                                    color: byDrug[drugs[i]]!.any((h) => alertSymbols.contains(h['elementSymbol'])) ? p.danger : p.muted,
                                    shape: BoxShape.circle,
                                  ),
                                ),
                                const SizedBox(width: 10),
                                Flexible(
                                  child: Tooltip(
                                    message: drugs[i],
                                    child: Text(
                                      drugName(context, drugs[i]),
                                      overflow: TextOverflow.ellipsis,
                                      style: const TextStyle(fontSize: 13),
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                          Expanded(
                            child: Wrap(
                              spacing: 4,
                              runSpacing: 4,
                              children: [
                                for (final hit in byDrug[drugs[i]]!.map((h) => h['elementSymbol']).toSet())
                                  MutationToken(hit, highlight: alertSymbols.contains(hit)),
                              ],
                            ),
                          ),
                        ],
                      ),
                    ),
                ],
              ),
            ),
        ],
      ),
    );
  }

  Widget _buildHits(Set<String> alertSymbols) {
    final p = context.p;
    final hits = switch (_type) {
      'all' => _hits,
      _ => _hits.where((h) => h['type'] == _type).toList(),
    };
    int count(String type) => _hits.where((h) => h['type'] == type).length;

    final columns = [
      Col(context.tr('col.gene'), width: 160),
      Col(context.tr('col.description'), flex: 4),
      Col(context.tr('col.subclass'), flex: 2),
      Col(context.tr('col.location'), flex: 2),
      Col(context.tr('col.identity'), width: 96, alignRight: true),
      Col(context.tr('col.method'), width: 150),
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 18, 20, 10),
          child: Row(
            children: [
              Text(
                context.tr('amr.all'),
                style: TextStyle(fontSize: 13.5, fontWeight: FontWeight.w600, color: p.text),
              ),
              const Spacer(),
              ChoiceTabs<String>(
                options: [
                  ('all', '${context.tr('common.all')}  ${_hits.length}'),
                  ('AMR', '${context.tr('amr.typeAmr')}  ${count('AMR')}'),
                  ('STRESS', '${context.tr('amr.typeStress')}  ${count('STRESS')}'),
                  ('VIRULENCE', '${context.tr('amr.typeVirulence')}  ${count('VIRULENCE')}'),
                ],
                value: _type,
                onChanged: (v) => setState(() => _type = v),
              ),
            ],
          ),
        ),
        TableHeader(columns),
        for (final hit in hits)
          TableRowItem(
            columns: columns,
            cells: [
              Text(
                hit['elementSymbol'],
                overflow: TextOverflow.ellipsis,
                style: mono(context, size: 12.5, color: alertSymbols.contains(hit['elementSymbol']) ? p.danger : p.text),
              ),
              Tooltip(
                message: hit['elementName'] ?? '',
                child: Text(
                  [hit['elementName'] ?? '', if (hit['subtype'] == 'POINT') '(${context.tr('amr.point')})'].join(' '),
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontSize: 13, color: p.text),
                ),
              ),
              Text(
                (hit['subclass'] as String).split('/').map((s) => drugName(context, s)).join(', '),
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontSize: 13, color: p.muted),
              ),
              Text(
                '${hit['contigId']} : ${hit['start']}–${hit['stop']} ${hit['strand']}',
                overflow: TextOverflow.ellipsis,
                style: mono(context, size: 12, color: p.muted),
              ),
              Text(
                hit['identityPercent'] == null ? '—' : '${(hit['identityPercent'] as num).toStringAsFixed(1)}%',
                style: TextStyle(fontSize: 13, color: p.muted, fontFeatures: const [FontFeature.tabularFigures()]),
              ),
              Tooltip(
                message: context.tr('method.${hit['method']}'),
                child: Text(
                  hit['method'],
                  overflow: TextOverflow.ellipsis,
                  style: mono(context, size: 12, color: p.muted),
                ),
              ),
            ],
          ),
        const SizedBox(height: 24),
      ],
    );
  }

  Widget _buildSearchResults() {
    final p = context.p;
    final hits = _searchHits!;
    final label = _search.text.trim();
    final columns = [
      Col(context.tr('col.gene'), width: 160),
      Col(context.tr('col.run'), flex: 3),
      Col(context.tr('upload.organism'), flex: 2),
      Col(context.tr('col.subclass'), flex: 2),
      Col(context.tr('col.location'), flex: 2),
    ];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 12, 20, 12),
          child: Text(
            hits.isEmpty
                ? context.tr('amr.searchNone', {'label': label})
                : context.tr('amr.searchResult', {'label': label, 'n': hits.length}),
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
                  onTap: () => widget.onSelectRun(hit['runId']),
                  cells: [
                    Text(hit['elementSymbol'], style: mono(context, size: 12.5)),
                    Text(hit['fileName'], overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 13)),
                    Text(
                      organismName(hit['amrOrganism']),
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(fontSize: 13, color: p.muted),
                    ),
                    Text(
                      drugName(context, hit['subclass']),
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(fontSize: 13, color: p.muted),
                    ),
                    Text(
                      hit['contigId'],
                      overflow: TextOverflow.ellipsis,
                      style: mono(context, size: 12, color: p.muted),
                    ),
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

/// Вибір виду бактерії для AMRFinderPlus (вмикає пошук точкових мутацій).
class OrganismDropdown extends StatelessWidget {
  final List<String> organisms;
  final String? value;
  final ValueChanged<String?> onChanged;

  const OrganismDropdown({super.key, required this.organisms, required this.value, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return DropdownButtonFormField<String?>(
      key: ValueKey('${organisms.length}-$value'),
      initialValue: value,
      isExpanded: true,
      dropdownColor: p.surface,
      menuMaxHeight: 420,
      borderRadius: BorderRadius.circular(8),
      icon: Icon(Icons.unfold_more, size: 16, color: p.muted),
      style: TextStyle(fontSize: 13, color: p.text, fontFamily: sansFont),
      items: [
        DropdownMenuItem<String?>(value: null, child: Text(context.tr('upload.organismNone'))),
        ...organisms.map(
          (o) => DropdownMenuItem<String?>(
            value: o,
            child: Text(
              organismName(o),
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontStyle: FontStyle.italic),
            ),
          ),
        ),
      ],
      onChanged: onChanged,
    );
  }
}
