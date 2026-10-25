import 'dart:math' as math;

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:url_launcher/url_launcher.dart';

import '../api.dart';
import '../l10n.dart';
import '../theme.dart';
import '../widgets.dart';

// ==========================================
// EXPLORER: навчальний режим «від ДНК до білка»
// ==========================================
/// Відкрити FASTA чи GenBank, побачити ORF у 6 рамках поруч з анотованими генами, клікнути ORF — білок,
/// пошук мотиву, 3D-структура (RCSB PDB, AlphaFold DB); порівняти дві послідовності.
class ExplorerPage extends StatefulWidget {
  const ExplorerPage({super.key});

  @override
  State<ExplorerPage> createState() => _ExplorerPageState();
}

class _ExplorerPageState extends State<ExplorerPage> {
  String? _token;
  String? _fileName;
  List<dynamic> _records = [];
  int _recordIndex = 0;
  int _minLength = 75;
  int _code = 1;
  bool _altStarts = false;
  bool? _circular;
  List<dynamic> _orfs = [];
  bool _truncated = false;
  bool _loading = false;
  double _zoom = 1;
  _Selection? _selected;

  dynamic get _record => _records.isEmpty ? null : _records[_recordIndex];

  Future<void> _open({String? filePath, String? text, String? name}) async {
    setState(() => _loading = true);
    try {
      final result = await Api.explorerOpen(filePath: filePath, text: text);
      setState(() {
        _token = result['token'];
        _fileName = name ?? result['fileName'] ?? context.tr('explorer.pasted');
        _records = result['records'];
        _recordIndex = 0;
        _circular = null;
        _selected = null;
        _zoom = 1;
      });
      await _loadOrfs();
    } on ApiException catch (e) {
      if (mounted) showMessage(context, e.message, error: true);
    } catch (_) {
      if (mounted) showMessage(context, context.tr('common.connectionFailed'), error: true);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _pickFile() async {
    final result = await FilePicker.platform.pickFiles(
      type: FileType.custom,
      allowedExtensions: ['fasta', 'fa', 'fna', 'gb', 'gbk', 'genbank', 'txt'],
    );
    final file = result?.files.firstOrNull;
    if (file?.path != null) await _open(filePath: file!.path, name: file.name);
  }

  Future<void> _paste() async {
    final controller = TextEditingController();
    final text = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(context.tr('explorer.pasteTitle'), style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600)),
        content: SizedBox(
          width: 520,
          child: TextField(
            controller: controller,
            minLines: 8,
            maxLines: 14,
            style: mono(context, size: 12),
            decoration: InputDecoration(hintText: '>my_sequence\nATGGCGT…'),
          ),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: Text(context.tr('common.cancel'))),
          FilledButton(onPressed: () => Navigator.pop(context, controller.text), child: Text(context.tr('explorer.open'))),
        ],
      ),
    );
    if (text != null && text.trim().isNotEmpty) await _open(text: text);
  }

  Future<void> _loadOrfs() async {
    final token = _token;
    if (token == null) return;
    try {
      final result = await Api.get('/explorer/$token/$_recordIndex/orfs', {
        'minLength': '$_minLength',
        'code': '$_code',
        'altStarts': '$_altStarts',
        if (_circular != null) 'circular': '$_circular',
      });
      setState(() {
        _orfs = result['orfs'];
        _truncated = result['truncated'];
        _circular = result['circular'];
      });
    } on ApiException catch (e) {
      if (mounted) showMessage(context, e.message, error: true);
    } catch (_) {
      if (mounted) showMessage(context, context.tr('common.connectionFailed'), error: true);
    }
  }

  void _setOption(VoidCallback change) {
    setState(() {
      change();
      _selected = null;
    });
    _loadOrfs();
  }

  Future<void> _compare() async {
    await showDialog<void>(
      context: context,
      builder: (_) => _CompareDialog(token: _token!, records: _records, selection: _selected, recordIndex: _recordIndex, code: _code),
    );
  }

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final record = _record;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TopBar(
          title: Row(
            children: [
              Text(context.tr('nav.explorer')),
              if (_fileName != null) ...[
                Padding(padding: const EdgeInsets.symmetric(horizontal: 8), child: Text('/', style: TextStyle(color: p.faint))),
                Flexible(
                  child: Text(_fileName!, overflow: TextOverflow.ellipsis, style: TextStyle(fontWeight: FontWeight.w500, color: p.text)),
                ),
              ],
            ],
          ),
          actions: [
            if (_records.length > 1)
              SizedBox(
                width: 220,
                child: DropdownButtonFormField<int>(
                  key: ValueKey('$_token-$_recordIndex'),
                  initialValue: _recordIndex,
                  isExpanded: true,
                  items: [
                    for (int i = 0; i < _records.length; i++)
                      DropdownMenuItem(value: i, child: Text(_records[i]['id'], overflow: TextOverflow.ellipsis, style: mono(context, size: 12.5))),
                  ],
                  onChanged: (v) => _setOption(() {
                    _recordIndex = v!;
                    _circular = null;
                  }),
                ),
              ),
            if (_token != null)
              OutlinedButton.icon(onPressed: _compare, icon: const Icon(Icons.compare_arrows, size: 15), label: Text(context.tr('explorer.compare'))),
            OutlinedButton(onPressed: _loading ? null : _paste, child: Text(context.tr('explorer.paste'))),
            FilledButton.icon(
              onPressed: _loading ? null : _pickFile,
              icon: const Icon(Icons.folder_open_outlined, size: 16),
              label: Text(context.tr('explorer.openFile')),
            ),
          ],
        ),
        Expanded(
          child: record == null
              ? _Intro(loading: _loading, onOpen: _pickFile, onPaste: _paste)
              : Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Expanded(child: _buildMap(record)),
                    if (_selected != null)
                      _DetailPanel(
                        key: ValueKey(_selected!.key),
                        token: _token!,
                        recordIndex: _recordIndex,
                        code: _code,
                        selection: _selected!,
                        recordLength: record['length'],
                        onClose: () => setState(() => _selected = null),
                      ),
                  ],
                ),
        ),
      ],
    );
  }

  Widget _buildMap(dynamic record) {
    final p = context.p;
    final features = List<dynamic>.from(record['features']).where((f) => f['type'] == 'CDS' || f['type'] == 'mat_peptide').toList();
    final matching = _orfs.where((o) => o['matchesCds'] != null).length;
    return ListView(
      padding: const EdgeInsets.fromLTRB(20, 14, 20, 24),
      children: [
        Text.rich(
          TextSpan(
            style: TextStyle(fontSize: 12.5, color: p.muted, height: 1.5),
            children: [
              TextSpan(text: record['id'], style: mono(context, size: 12.5, color: p.text)),
              if ((record['description'] as String).isNotEmpty) TextSpan(text: '  ·  ${record['description']}'),
              TextSpan(text: '  ·  ${formatBases(record['length'])}'),
              TextSpan(text: '  ·  GC ${record['gc']}%'),
              TextSpan(text: '  ·  ${context.tr(_circular == true ? 'explorer.circular' : 'explorer.linear')}'),
            ],
          ),
        ),
        const SizedBox(height: 12),
        Wrap(
          spacing: 16,
          runSpacing: 10,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            _Option(
              label: context.tr('explorer.minLength'),
              child: ChoiceTabs<int>(
                options: const [(75, '75'), (150, '150'), (300, '300'), (900, '900')],
                value: _minLength,
                onChanged: (v) => _setOption(() => _minLength = v),
              ),
            ),
            _Option(
              label: context.tr('explorer.code'),
              child: ChoiceTabs<int>(
                options: [(1, context.tr('explorer.code1')), (11, context.tr('explorer.code11')), (2, context.tr('explorer.code2'))],
                value: _code,
                onChanged: (v) => _setOption(() => _code = v),
              ),
            ),
            _Option(
              label: context.tr('explorer.topology'),
              child: ChoiceTabs<bool>(
                options: [(false, context.tr('explorer.linear')), (true, context.tr('explorer.circular'))],
                value: _circular ?? false,
                onChanged: (v) => _setOption(() => _circular = v),
              ),
            ),
            Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Checkbox(value: _altStarts, onChanged: (v) => _setOption(() => _altStarts = v ?? false)),
                Tooltip(
                  message: context.tr('explorer.altStartsHint'),
                  child: Text(context.tr('explorer.altStarts'), style: TextStyle(fontSize: 12.5, color: p.text)),
                ),
              ],
            ),
          ],
        ),
        const SizedBox(height: 16),
        Row(
          children: [
            Expanded(
              child: Text(
                [
                  context.tr('explorer.orfCount', {'n': _orfs.length, 'min': _minLength}),
                  if (features.isNotEmpty) context.tr('explorer.cdsCount', {'n': features.length, 'match': matching}),
                  if (_truncated) context.tr('explorer.truncated'),
                ].join('  ·  '),
                style: TextStyle(fontSize: 12.5, color: p.muted),
              ),
            ),
            Icon(Icons.zoom_out, size: 16, color: p.muted),
            SizedBox(
              width: 160,
              child: Slider(
                value: math.log(_zoom) / math.ln2,
                min: 0,
                max: 8,
                onChanged: (v) => setState(() => _zoom = math.pow(2, v).toDouble()),
              ),
            ),
            Icon(Icons.zoom_in, size: 16, color: p.muted),
          ],
        ),
        const SizedBox(height: 6),
        LayoutBuilder(
          builder: (context, constraints) {
            final width = constraints.maxWidth * _zoom;
            final map = _OrfMap(
              length: record['length'],
              orfs: _orfs,
              features: features,
              selectedKey: _selected?.key,
              onSelect: (s) => setState(() => _selected = s),
            );
            return SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: SizedBox(width: width, height: _OrfMap.height, child: map),
            );
          },
        ),
        const SizedBox(height: 10),
        Wrap(
          spacing: 18,
          runSpacing: 6,
          children: [
            _LegendSwatch(color: p.ink.withValues(alpha: 0.75), label: context.tr('explorer.legendCds')),
            _LegendSwatch(color: p.muted, label: context.tr('explorer.legendOrfCds')),
            _LegendSwatch(color: p.chartRest, label: context.tr('explorer.legendOrf')),
          ],
        ),
        const SizedBox(height: 14),
        Text(context.tr('explorer.lesson'), style: TextStyle(fontSize: 12, color: p.faint, height: 1.5)),
      ],
    );
  }
}

/// Що вибрано на карті: ORF або анотований CDS.
class _Selection {
  final dynamic orf;
  final dynamic feature;
  const _Selection({this.orf, this.feature});

  String get key => orf != null ? 'orf-${orf['id']}' : 'cds-${feature['label']}-${feature['start']}';
}

class _Option extends StatelessWidget {
  final String label;
  final Widget child;
  const _Option({required this.label, required this.child});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(label, style: TextStyle(fontSize: 12.5, color: context.p.muted)),
        const SizedBox(width: 8),
        child,
      ],
    );
  }
}

class _LegendSwatch extends StatelessWidget {
  final Color color;
  final String label;
  const _LegendSwatch({required this.color, required this.label});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(width: 14, height: 8, decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(2))),
        const SizedBox(width: 6),
        Text(label, style: TextStyle(fontSize: 12, color: context.p.muted)),
      ],
    );
  }
}

class _Intro extends StatelessWidget {
  final bool loading;
  final VoidCallback onOpen;
  final VoidCallback onPaste;
  const _Intro({required this.loading, required this.onOpen, required this.onPaste});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 520),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(context.tr('explorer.introTitle'), style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: p.text)),
            const SizedBox(height: 8),
            Text(context.tr('explorer.introText'), textAlign: TextAlign.center, style: TextStyle(fontSize: 13, color: p.muted, height: 1.5)),
            const SizedBox(height: 16),
            if (loading)
              SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 1.6, color: p.muted))
            else
              Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  FilledButton(onPressed: onOpen, child: Text(context.tr('explorer.openFile'))),
                  const SizedBox(width: 8),
                  OutlinedButton(onPressed: onPaste, child: Text(context.tr('explorer.paste'))),
                ],
              ),
          ],
        ),
      ),
    );
  }
}

// ==========================================
// КАРТА ORF
// ==========================================
/// Смуги: лінійка, анотовані гени (GenBank), три рамки прямого ланцюга (+1…+3) і три зворотного (−1…−3).
class _OrfMap extends StatelessWidget {
  static const double height = 236;
  static const double rulerHeight = 22;
  static const double laneHeight = 26;
  static const double labelWidth = 44;

  final int length;
  final List<dynamic> orfs;
  final List<dynamic> features;
  final String? selectedKey;
  final ValueChanged<_Selection> onSelect;

  const _OrfMap({required this.length, required this.orfs, required this.features, required this.selectedKey, required this.onSelect});

  static int laneOf(int frame) => frame > 0 ? frame : 3 - frame; // +1..+3 → 1..3, −1..−3 → 4..6

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return LayoutBuilder(
      builder: (context, constraints) {
        final plotWidth = constraints.maxWidth - labelWidth;
        double x(num position) => labelWidth + (position / length).clamp(0.0, 1.0) * plotWidth;

        return GestureDetector(
          onTapUp: (details) {
            final pos = details.localPosition;
            if (pos.dx < labelWidth) return;
            final position = ((pos.dx - labelWidth) / plotWidth * length).round();
            final lane = ((pos.dy - rulerHeight) / laneHeight).floor();
            bool covers(num start, num end) {
              final tolerance = length / plotWidth * 3; // ±3 px, щоб у короткий ORF теж можна було влучити
              if (end > length) return position >= start - tolerance || position <= end - length + tolerance;
              return position >= start - tolerance && position <= end + tolerance;
            }

            if (lane == 0) {
              final hit = features.where((f) => covers(f['start'], f['end'])).firstOrNull;
              if (hit != null) onSelect(_Selection(feature: hit));
              return;
            }
            final hits = orfs.where((o) => laneOf(o['frame']) == lane && covers(o['start'], o['end'])).toList()
              ..sort((a, b) => (b['length'] as int).compareTo(a['length'] as int));
            if (hits.isNotEmpty) onSelect(_Selection(orf: hits.first));
          },
          child: CustomPaint(
            size: Size(constraints.maxWidth, height),
            painter: _OrfMapPainter(
              length: length,
              orfs: orfs,
              features: features,
              selectedKey: selectedKey,
              palette: p,
              x: x,
              plotWidth: plotWidth,
            ),
          ),
        );
      },
    );
  }
}

class _OrfMapPainter extends CustomPainter {
  final int length;
  final List<dynamic> orfs;
  final List<dynamic> features;
  final String? selectedKey;
  final Palette palette;
  final double Function(num) x;
  final double plotWidth;

  _OrfMapPainter({
    required this.length,
    required this.orfs,
    required this.features,
    required this.selectedKey,
    required this.palette,
    required this.x,
    required this.plotWidth,
  });

  static const _laneLabels = ['CDS', '+1', '+2', '+3', '−1', '−2', '−3'];

  @override
  void paint(Canvas canvas, Size size) {
    final p = palette;
    const top = _OrfMap.rulerHeight, lane = _OrfMap.laneHeight;

    // Лінійка: «круглі» позначки, щоб на будь-якому масштабі їх було 5–12.
    final step = _niceStep(length / math.max(1, plotWidth / 90));
    final tick = Paint()..color = p.border;
    for (num pos = 0; pos <= length; pos += step) {
      final px = x(pos);
      canvas.drawLine(Offset(px, top - 6), Offset(px, size.height), tick);
      _text(canvas, _short(pos), Offset(px + 3, 2), 10, p.faint);
    }

    for (int i = 0; i < _laneLabels.length; i++) {
      final y = top + i * lane;
      if (i == 4) canvas.drawLine(Offset(_OrfMap.labelWidth, y), Offset(size.width, y), Paint()..color = p.border);
      _text(canvas, _laneLabels[i], Offset(4, y + 7), 10.5, p.muted);
    }

    // Анотовані CDS: сегменти (join) з'єднано тонкою лінією, підпис над смугою, якщо вміщається.
    for (final f in features) {
      final selected = selectedKey == 'cds-${f['label']}-${f['start']}';
      final paint = Paint()..color = selected ? p.ink : p.ink.withValues(alpha: 0.75);
      final y = top + 6;
      final segments = List<dynamic>.from(f['segments']);
      for (final s in segments) {
        _bar(canvas, s[0], s[1], y, 14, paint);
      }
      if (segments.length > 1) {
        canvas.drawLine(Offset(x(f['start']), y + 7), Offset(x(f['end']), y + 7), Paint()..color = p.ink.withValues(alpha: 0.4));
      }
      final label = f['label'] as String;
      final width = x(f['end']) - x(f['start']);
      if (width > label.length * 6.5 + 6) _text(canvas, label, Offset(x(f['start']) + 3, y + 1), 10, p.bg);
    }

    for (final o in orfs) {
      final laneIndex = _OrfMap.laneOf(o['frame']);
      final y = top + laneIndex * lane + 7;
      final selected = selectedKey == 'orf-${o['id']}';
      final color = selected ? p.ink : (o['matchesCds'] != null ? p.muted : p.chartRest);
      _bar(canvas, o['start'], o['end'], y, 12, Paint()..color = color);
      // Старт-кодон — тонка вертикальна риска, щоб було видно напрямок.
      final startX = o['strand'] == '+' ? x(o['start']) : x((o['end'] as int) > length ? (o['end'] as int) - length : o['end']);
      canvas.drawLine(Offset(startX, y - 2), Offset(startX, y + 14), Paint()
        ..color = selected ? p.ink : p.text.withValues(alpha: 0.35)
        ..strokeWidth = 1);
    }
  }

  // ORF через «нуль» кільцевої молекули малюється двома шматками.
  void _bar(Canvas canvas, num start, num end, double y, double h, Paint paint) {
    if (end > length) {
      _bar(canvas, start, length, y, h, paint);
      _bar(canvas, 1, end - length, y, h, paint);
      return;
    }
    final left = x(start - 1), right = math.max(x(end), left + 1.5);
    canvas.drawRRect(RRect.fromRectAndRadius(Rect.fromLTRB(left, y, right, y + h), const Radius.circular(2)), paint);
  }

  static num _niceStep(double raw) {
    final magnitude = math.pow(10, (math.log(math.max(raw, 1)) / math.ln10).floor());
    for (final m in [1, 2, 5, 10]) {
      if (m * magnitude >= raw) return m * magnitude;
    }
    return 10 * magnitude;
  }

  static String _short(num n) => n >= 1e6
      ? '${(n / 1e6).toStringAsFixed(n % 1000000 == 0 ? 0 : 1)}M'
      : n >= 1e3
      ? '${(n / 1e3).toStringAsFixed(n % 1000 == 0 ? 0 : 1)}k'
      : '$n';

  void _text(Canvas canvas, String text, Offset at, double size, Color color) {
    final painter = TextPainter(
      text: TextSpan(
        text: text,
        style: TextStyle(fontSize: size, color: color, fontFamily: monoFont),
      ),
      textDirection: TextDirection.ltr,
    )..layout();
    painter.paint(canvas, at);
  }

  @override
  bool shouldRepaint(covariant _OrfMapPainter old) => true;
}

// ==========================================
// ДЕТАЛІ: білок, мотив, 3D
// ==========================================
class _DetailPanel extends StatefulWidget {
  final String token;
  final int recordIndex;
  final int code;
  final _Selection selection;
  final int recordLength;
  final VoidCallback onClose;

  const _DetailPanel({
    super.key,
    required this.token,
    required this.recordIndex,
    required this.code,
    required this.selection,
    required this.recordLength,
    required this.onClose,
  });

  @override
  State<_DetailPanel> createState() => _DetailPanelState();
}

class _DetailPanelState extends State<_DetailPanel> {
  String? _protein;
  String? _nucleotides;
  final _motif = TextEditingController();
  bool _searching = false;
  Map<String, dynamic>? _structures;
  String? _structureError;

  dynamic get _orf => widget.selection.orf;
  dynamic get _feature => widget.selection.feature;

  @override
  void initState() {
    super.initState();
    _loadRegion();
  }

  @override
  void dispose() {
    _motif.dispose();
    super.dispose();
  }

  Future<void> _loadRegion() async {
    final String segments;
    final String strand;
    if (_orf != null) {
      // ORF через «нуль»: кінець за довжиною, API продовжує з початку.
      segments = '${_orf['start']}-${_orf['end']}';
      strand = _orf['strand'];
    } else {
      segments = List<dynamic>.from(_feature['segments']).map((s) => '${s[0]}-${s[1]}').join(',');
      strand = _feature['strand'];
    }
    try {
      final result = await Api.get('/explorer/${widget.token}/${widget.recordIndex}/region', {
        'segments': segments,
        'strand': strand,
        'code': '${widget.code}',
      });
      if (mounted) {
        setState(() {
          _protein = result['protein'];
          _nucleotides = result['nucleotides'];
        });
      }
    } catch (_) {}
  }

  Future<void> _findStructures() async {
    setState(() {
      _searching = true;
      _structureError = null;
    });
    try {
      final result = await Api.post('/explorer/structures', {'protein': _protein}, wait: const Duration(seconds: 90));
      if (mounted) setState(() => _structures = result);
    } on ApiException catch (e) {
      if (mounted) setState(() => _structureError = e.message);
    } catch (_) {
      if (mounted) setState(() => _structureError = context.tr('common.connectionFailed'));
    } finally {
      if (mounted) setState(() => _searching = false);
    }
  }

  Widget _meta(String label, String value) => Padding(
    padding: const EdgeInsets.only(bottom: 6),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(width: 120, child: Text(label, style: TextStyle(fontSize: 12.5, color: context.p.muted))),
        Expanded(child: Text(value, style: const TextStyle(fontSize: 12.5))),
      ],
    ),
  );

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final orf = _orf;
    final feature = _feature;
    final title = orf != null
        ? 'ORF ${orf['frame'] > 0 ? '+' : '−'}${(orf['frame'] as int).abs()} · ${_range(orf['start'], orf['end'])}'
        : '${feature['label']} · ${_range(feature['start'], feature['end'])}';

    return Container(
      width: 460,
      decoration: BoxDecoration(color: p.surface, border: Border(left: BorderSide(color: p.border))),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            height: 44,
            padding: const EdgeInsets.only(left: 20, right: 8),
            decoration: BoxDecoration(border: Border(bottom: BorderSide(color: p.border))),
            child: Row(
              children: [
                Expanded(child: Text(title, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 13.5, fontWeight: FontWeight.w600))),
                IconButton(tooltip: context.tr('common.close'), onPressed: widget.onClose, icon: const Icon(Icons.close, size: 16)),
              ],
            ),
          ),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
              children: [
                if (orf != null) ...[
                  _meta(context.tr('explorer.strand'), orf['strand'] == '+' ? context.tr('explorer.forward') : context.tr('explorer.reverse')),
                  _meta(context.tr('explorer.length'), '${orf['length']} ${context.tr('explorer.nt')} · ${orf['aaLength']} ${context.tr('explorer.aa')}'),
                  if (orf['wraps'] == true) _meta(context.tr('explorer.wraps'), context.tr('explorer.wrapsText')),
                  _meta(
                    context.tr('explorer.annotation'),
                    orf['matchesCds'] == null
                        ? context.tr('explorer.noCds')
                        : orf['sameStartAsCds'] == true
                        ? context.tr('explorer.sameCds', {'gene': orf['matchesCds']})
                        : context.tr('explorer.longerCds', {'gene': orf['matchesCds']}),
                  ),
                ] else ...[
                  _meta(context.tr('explorer.type'), feature['type']),
                  if (feature['product'] != null) _meta(context.tr('explorer.product'), feature['product']),
                  _meta(context.tr('explorer.strand'), feature['strand'] == '+' ? context.tr('explorer.forward') : context.tr('explorer.reverse')),
                  if ((feature['segments'] as List).length > 1)
                    _meta(context.tr('explorer.join'), context.tr('explorer.joinText', {'n': (feature['segments'] as List).length})),
                ],
                const SizedBox(height: 14),
                Row(
                  children: [
                    Text(context.tr('explorer.protein'), style: TextStyle(fontSize: 12.5, fontWeight: FontWeight.w600, color: p.text)),
                    const Spacer(),
                    if (_protein != null)
                      TextButton(
                        onPressed: () => Clipboard.setData(ClipboardData(text: _protein!)),
                        child: Text(context.tr('explorer.copy')),
                      ),
                  ],
                ),
                const SizedBox(height: 6),
                SizedBox(
                  height: 34,
                  child: TextField(
                    controller: _motif,
                    style: mono(context, size: 12.5),
                    textCapitalization: TextCapitalization.characters,
                    onChanged: (_) => setState(() {}),
                    decoration: InputDecoration(
                      hintText: context.tr('explorer.motif'),
                      hintStyle: TextStyle(fontFamily: sansFont, fontSize: 12.5, color: p.faint),
                      prefixIcon: Icon(Icons.search, size: 16, color: p.muted),
                      prefixIconConstraints: const BoxConstraints(minWidth: 34),
                      contentPadding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
                    ),
                  ),
                ),
                const SizedBox(height: 8),
                if (_protein == null)
                  SizedBox(height: 16, child: Center(child: CircularProgressIndicator(strokeWidth: 1.5, color: p.muted)))
                else
                  _ProteinView(protein: _protein!, motif: _motif.text.trim().toUpperCase()),
                const SizedBox(height: 18),
                Text(context.tr('explorer.structure'), style: TextStyle(fontSize: 12.5, fontWeight: FontWeight.w600, color: p.text)),
                const SizedBox(height: 4),
                Text(context.tr('explorer.structureHint'), style: TextStyle(fontSize: 12, color: p.muted, height: 1.4)),
                const SizedBox(height: 8),
                if (_structures == null)
                  Align(
                    alignment: Alignment.centerLeft,
                    child: OutlinedButton.icon(
                      onPressed: _protein == null || _searching || _protein!.length < 20 ? null : _findStructures,
                      icon: _searching
                          ? SizedBox(width: 12, height: 12, child: CircularProgressIndicator(strokeWidth: 1.5, color: p.muted))
                          : const Icon(Icons.view_in_ar_outlined, size: 15),
                      label: Text(context.tr('explorer.findStructure')),
                    ),
                  )
                else
                  _Structures(result: _structures!),
                if (_structureError != null) Text(_structureError!, style: TextStyle(fontSize: 12, color: p.danger)),
                if (_nucleotides != null) ...[
                  const SizedBox(height: 18),
                  ExpansionTile(
                    tilePadding: EdgeInsets.zero,
                    title: Text(
                      '${context.tr('explorer.nucleotides')} · ${_nucleotides!.length}',
                      style: TextStyle(fontSize: 12.5, fontWeight: FontWeight.w600, color: p.text),
                    ),
                    children: [SelectableText(_blocks(_nucleotides!, 10, 60), style: mono(context, size: 11.5, color: p.muted))],
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }

  static String _range(num start, num end) => '$start–$end';
}

String _blocks(String sequence, int block, int line) {
  final buffer = StringBuffer();
  for (int i = 0; i < sequence.length; i += block) {
    if (i > 0) buffer.write(i % line == 0 ? '\n' : ' ');
    buffer.write(sequence.substring(i, math.min(i + block, sequence.length)));
  }
  return buffer.toString();
}

/// Білок блоками по 10 амінокислот; збіги мотиву підсвічено.
class _ProteinView extends StatelessWidget {
  final String protein;
  final String motif;
  const _ProteinView({required this.protein, required this.motif});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final highlight = List<bool>.filled(protein.length, false);
    int matches = 0;
    if (motif.length >= 2) {
      int index = protein.indexOf(motif);
      while (index >= 0) {
        matches++;
        for (int k = index; k < index + motif.length; k++) {
          highlight[k] = true;
        }
        index = protein.indexOf(motif, index + 1);
      }
    }

    final spans = <TextSpan>[];
    for (int i = 0; i < protein.length; i++) {
      if (i > 0 && i % 10 == 0) spans.add(TextSpan(text: i % 50 == 0 ? '\n' : ' '));
      spans.add(
        TextSpan(
          text: protein[i],
          style: highlight[i] ? TextStyle(color: p.onInk, backgroundColor: p.ink, fontWeight: FontWeight.w600) : null,
        ),
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (motif.length >= 2)
          Padding(
            padding: const EdgeInsets.only(bottom: 6),
            child: Text(
              matches == 0 ? context.tr('explorer.motifNone') : context.tr('explorer.motifFound', {'n': matches}),
              style: TextStyle(fontSize: 12, color: matches == 0 ? p.faint : p.text),
            ),
          ),
        SelectableText.rich(TextSpan(style: mono(context, size: 12, color: p.text), children: spans)),
      ],
    );
  }
}

class _Structures extends StatelessWidget {
  final Map<String, dynamic> result;
  const _Structures({required this.result});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final hits = List<dynamic>.from(result['hits'] ?? const []);
    final alphaFold = result['alphaFold'];
    if (hits.isEmpty && alphaFold == null) {
      return Text(context.tr('explorer.noStructures'), style: TextStyle(fontSize: 12.5, color: p.muted));
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (alphaFold != null)
          _StructureRow(
            title: 'AlphaFold · ${alphaFold['accession']}',
            subtitle: [
              alphaFold['description'],
              if (alphaFold['plddt'] != null) 'pLDDT ${(alphaFold['plddt'] as num).toStringAsFixed(0)}',
              alphaFold['organism'],
            ].whereType<String>().join(' · '),
            imageUrl: alphaFold['paeImageUrl'],
            url: alphaFold['url'],
          ),
        for (final h in hits)
          _StructureRow(
            title: 'PDB ${h['pdbId']}',
            subtitle: [
              h['title'],
              context.tr('explorer.identity', {'pct': ((h['identity'] as num) * 100).toStringAsFixed(0)}),
              context.tr('explorer.covers', {'from': h['queryStart'], 'to': h['queryEnd']}),
            ].whereType<String>().join(' · '),
            imageUrl: h['imageUrl'],
            url: h['viewerUrl'],
          ),
      ],
    );
  }
}

class _StructureRow extends StatelessWidget {
  final String title;
  final String subtitle;
  final String? imageUrl;
  final String url;
  const _StructureRow({required this.title, required this.subtitle, required this.imageUrl, required this.url});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return InkWell(
      onTap: () => launchUrl(Uri.parse(url)),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 6),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            ClipRRect(
              borderRadius: BorderRadius.circular(4),
              child: Container(
                width: 64,
                height: 64,
                color: p.hover,
                child: imageUrl == null
                    ? null
                    : Image.network(imageUrl!, fit: BoxFit.cover, errorBuilder: (_, _, _) => const SizedBox()),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(title, style: mono(context, size: 12.5, color: p.text)),
                  const SizedBox(height: 2),
                  Text(subtitle, style: TextStyle(fontSize: 12, color: p.muted, height: 1.35)),
                ],
              ),
            ),
            Icon(Icons.open_in_new, size: 14, color: p.faint),
          ],
        ),
      ),
    );
  }
}

// ==========================================
// ПОРІВНЯННЯ
// ==========================================
class _CompareDialog extends StatefulWidget {
  final String token;
  final List<dynamic> records;
  final _Selection? selection;
  final int recordIndex;
  final int code;

  const _CompareDialog({required this.token, required this.records, required this.selection, required this.recordIndex, required this.code});

  @override
  State<_CompareDialog> createState() => _CompareDialogState();
}

class _CompareDialogState extends State<_CompareDialog> {
  late int _a = 0;
  late int _b = widget.records.length > 1 ? 1 : 0;
  late String _mode = _canCompareOrf ? 'orf' : 'records';
  final _textA = TextEditingController();
  final _textB = TextEditingController();
  bool _busy = false;
  Map<String, dynamic>? _result;
  String? _error;

  bool get _canCompareOrf => widget.selection?.orf?['matchesCds'] != null;

  @override
  void dispose() {
    _textA.dispose();
    _textB.dispose();
    super.dispose();
  }

  Future<String> _region(String segments, String strand) async {
    final result = await Api.get('/explorer/${widget.token}/${widget.recordIndex}/region', {
      'segments': segments,
      'strand': strand,
      'code': '${widget.code}',
    });
    return result['protein'];
  }

  Future<void> _run() async {
    setState(() {
      _busy = true;
      _error = null;
      _result = null;
    });
    try {
      final Map<String, dynamic> body;
      if (_mode == 'records') {
        body = {'token': widget.token, 'recordA': _a, 'recordB': _b, 'protein': false};
      } else if (_mode == 'orf') {
        // ORF проти анотованого білка того самого гена: видно, на скільки ORF довший (інший старт) і чи збігається решта.
        final orf = widget.selection!.orf;
        final feature = List<dynamic>.from(widget.records[widget.recordIndex]['features'])
            .firstWhere((f) => f['type'] == 'CDS' && f['label'] == orf['matchesCds']);
        final cdsProtein = await _region(List<dynamic>.from(feature['segments']).map((s) => '${s[0]}-${s[1]}').join(','), feature['strand']);
        final orfProtein = await _region('${orf['start']}-${orf['end']}', orf['strand']);
        body = {'sequenceA': cdsProtein, 'sequenceB': orfProtein, 'protein': true};
      } else {
        body = {'sequenceA': _textA.text, 'sequenceB': _textB.text, 'protein': _mode == 'protein'};
      }
      final result = await Api.post('/explorer/compare', body, wait: const Duration(seconds: 60));
      if (mounted) setState(() => _result = result);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } catch (_) {
      if (mounted) setState(() => _error = context.tr('common.connectionFailed'));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final result = _result;
    return AlertDialog(
      title: Text(context.tr('explorer.compareTitle'), style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600)),
      content: SizedBox(
        width: 680,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Align(
                alignment: Alignment.centerLeft,
                child: ChoiceTabs<String>(
                  options: [
                    if (_canCompareOrf) ('orf', context.tr('explorer.compareOrf')),
                    if (widget.records.length > 1) ('records', context.tr('explorer.compareRecords')),
                    ('nucleotide', context.tr('explorer.compareDna')),
                    ('protein', context.tr('explorer.compareProtein')),
                  ],
                  value: _mode,
                  onChanged: (v) => setState(() {
                    _mode = v;
                    _result = null;
                  }),
                ),
              ),
              const SizedBox(height: 14),
              if (_mode == 'records')
                Row(
                  children: [
                    Expanded(child: _recordPicker(_a, (v) => setState(() => _a = v))),
                    const Padding(padding: EdgeInsets.symmetric(horizontal: 10), child: Text('↔')),
                    Expanded(child: _recordPicker(_b, (v) => setState(() => _b = v))),
                  ],
                )
              else if (_mode == 'orf')
                Text(
                  context.tr('explorer.compareOrfHint', {'gene': widget.selection!.orf['matchesCds']}),
                  style: TextStyle(fontSize: 12.5, color: p.muted, height: 1.4),
                )
              else ...[
                TextField(controller: _textA, minLines: 3, maxLines: 5, style: mono(context, size: 12), decoration: const InputDecoration(hintText: 'A')),
                const SizedBox(height: 8),
                TextField(controller: _textB, minLines: 3, maxLines: 5, style: mono(context, size: 12), decoration: const InputDecoration(hintText: 'B')),
              ],
              const SizedBox(height: 14),
              if (_error != null) Text(_error!, style: TextStyle(fontSize: 12.5, color: p.danger)),
              if (result != null) _CompareResult(result: result, protein: _mode == 'orf' || _mode == 'protein'),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: Text(context.tr('common.close'))),
        FilledButton(
          onPressed: _busy ? null : _run,
          child: _busy
              ? SizedBox(width: 14, height: 14, child: CircularProgressIndicator(strokeWidth: 1.6, color: p.onInk))
              : Text(context.tr('explorer.compareRun')),
        ),
      ],
    );
  }

  Widget _recordPicker(int value, ValueChanged<int> onChanged) {
    return DropdownButtonFormField<int>(
      initialValue: value,
      isExpanded: true,
      items: [
        for (int i = 0; i < widget.records.length; i++)
          DropdownMenuItem(value: i, child: Text(widget.records[i]['id'], overflow: TextOverflow.ellipsis, style: mono(context, size: 12.5))),
      ],
      onChanged: (v) => onChanged(v!),
    );
  }
}

class _CompareResult extends StatelessWidget {
  final Map<String, dynamic> result;
  final bool protein;
  const _CompareResult({required this.result, required this.protein});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final differences = List<dynamic>.from(result['differences']);
    String label(dynamic d) => switch (d['kind'] as int) {
      0 => '${d['a']}${d['positionA']}${d['b']}',
      1 => 'ins ${d['positionA']}^${d['positionA'] + 1} ${d['b']}',
      _ => protein ? '${d['a']}${d['positionA']}-' : 'del ${d['positionA']}–${d['positionA'] + (d['a'] as String).length - 1}',
    };

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text.rich(
          TextSpan(
            style: TextStyle(fontSize: 13, color: p.muted, height: 1.5),
            children: [
              TextSpan(
                text: context.tr('explorer.identityResult', {'pct': ((result['identity'] as num) * 100).toStringAsFixed(2)}),
                style: TextStyle(color: p.text, fontWeight: FontWeight.w600),
              ),
              TextSpan(
                text: '  ·  ${context.tr('explorer.diffCounts', {
                  'subs': result['substitutions'],
                  'ins': result['insertions'],
                  'del': result['deletions'],
                  'n': result['unknown'],
                })}',
              ),
              TextSpan(text: '  ·  A ${result['lengthA']}, B ${result['lengthB']}'),
            ],
          ),
        ),
        const SizedBox(height: 10),
        // Розриви на кінцях вирівнювання безкоштовні й у відмінності не потрапляють: різницю довжин кажемо окремо.
        if (differences.isEmpty)
          Text(
            result['lengthA'] == result['lengthB']
                ? context.tr('explorer.identical')
                : context.tr('explorer.identicalCore', {
                    'longer': (result['lengthA'] as int) > (result['lengthB'] as int) ? 'A' : 'B',
                    'n': ((result['lengthA'] as int) - (result['lengthB'] as int)).abs(),
                  }),
            style: TextStyle(fontSize: 12.5, color: p.muted),
          )
        else
          Wrap(
            spacing: 4,
            runSpacing: 4,
            children: [for (final d in differences.take(400)) MutationToken(label(d), muted: d['kind'] != 0)],
          ),
        if (protein && result['alignedA'] != null) ...[
          const SizedBox(height: 12),
          _AlignedView(a: result['alignedA'], b: result['alignedB']),
        ],
      ],
    );
  }
}

/// Вирівняні білки рядками по 60: відмінності підсвічено.
class _AlignedView extends StatelessWidget {
  final String a;
  final String b;
  const _AlignedView({required this.a, required this.b});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final lines = <Widget>[];
    for (int i = 0; i < a.length; i += 60) {
      final end = math.min(i + 60, a.length);
      TextSpan row(String s, String other) => TextSpan(
        children: [
          for (int k = i; k < end; k++)
            TextSpan(text: s[k], style: s[k] != other[k] ? TextStyle(color: p.onInk, backgroundColor: p.ink) : null),
        ],
      );
      lines.add(
        Padding(
          padding: const EdgeInsets.only(bottom: 8),
          child: SelectableText.rich(
            TextSpan(
              style: mono(context, size: 11.5, color: p.text),
              children: [row(a, b), const TextSpan(text: '\n'), row(b, a)],
            ),
          ),
        ),
      );
    }
    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: lines);
  }
}
