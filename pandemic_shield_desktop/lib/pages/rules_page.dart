import 'package:flutter/material.dart';

import '../api.dart';
import '../l10n.dart';
import '../store.dart';
import '../theme.dart';
import '../widgets.dart';

// ==========================================
// RULES: варіанти Nextclade і мотиви ORF
// ==========================================

/// Розбір позначення для живої підказки. Остаточна перевірка — на сервері (VariantPattern).
({String key, Map<String, Object?> args})? describeNotation(String text) {
  final value = text.trim();
  final clade = RegExp(r'^clade:(.+)$', caseSensitive: false).firstMatch(value);
  if (clade != null) return (key: 'rules.kind.clade', args: {'name': clade[1]!.trim()});

  final lineage = RegExp(r'^lineage:([A-Za-z]{1,3}(\.\d+)*\*?)$', caseSensitive: false).firstMatch(value);
  if (lineage != null) return (key: 'rules.kind.lineage', args: {'name': lineage[1]!.toUpperCase()});

  final aa = RegExp(r'^([A-Za-z][A-Za-z0-9_.\-]*):([A-Za-z*])?(\d+)([A-Za-z*]|-)?$').firstMatch(value);
  if (aa != null && int.parse(aa[3]!) > 0) {
    return (key: aa[4] == '-' ? 'rules.kind.aaDel' : 'rules.kind.aa', args: {'gene': aa[1], 'pos': aa[3]});
  }

  final nuc = RegExp(r'^([ACGTacgt])?(\d+)([ACGTacgt]|\*|-)?$').firstMatch(value);
  if (nuc != null && int.parse(nuc[2]!) > 0) {
    return (key: nuc[3] == '-' ? 'rules.kind.nucDel' : 'rules.kind.nuc', args: {'pos': nuc[2]});
  }
  return null;
}

class RulesPage extends StatefulWidget {
  final AppStore store;
  const RulesPage({super.key, required this.store});

  @override
  State<RulesPage> createState() => _RulesPageState();
}

class _RulesPageState extends State<RulesPage> {
  String _tab = 'variants';

  Future<void> _delete(String path) async {
    try {
      await Api.delete(path);
      widget.store.refresh();
    } catch (_) {}
  }

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final store = widget.store;
    final variants = _tab == 'variants';

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TopBar(
          title: Text(context.tr('nav.rules')),
          actions: [
            ChoiceTabs<String>(
              options: [
                ('variants', '${context.tr('rules.variants')}  ${store.variantRules.length}'),
                ('motifs', '${context.tr('rules.motifs')}  ${store.motifs.length}'),
              ],
              value: _tab,
              onChanged: (v) => setState(() => _tab = v),
            ),
            FilledButton.icon(
              onPressed: () => showDialog(
                context: context,
                builder: (_) => variants ? _NewRuleDialog(store: store) : _NewMotifDialog(store: store),
              ),
              icon: const Icon(Icons.add, size: 16),
              label: Text(context.tr(variants ? 'rules.new' : 'rules.newMotif')),
            ),
          ],
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 12, 20, 12),
          child: Text(context.tr(variants ? 'rules.appliesHint' : 'motifs.caveat'), style: TextStyle(fontSize: 12.5, color: p.muted)),
        ),
        Expanded(child: variants ? _variantTable(context) : _motifTable(context)),
      ],
    );
  }

  Widget _variantTable(BuildContext context) {
    final p = context.p;
    final rules = widget.store.variantRules;
    if (rules.isEmpty) return EmptyHint(context.tr('rules.empty'));

    final columns = [
      Col(context.tr('rules.name'), flex: 4),
      Col(context.tr('rules.notation'), flex: 2),
      Col(context.tr('col.dataset'), flex: 2),
      Col(context.tr('rules.description'), flex: 4),
      const Col('', width: 36),
    ];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TableHeader(columns),
        Expanded(
          child: ListView.builder(
            itemCount: rules.length,
            itemBuilder: (context, i) {
              final rule = rules[i];
              return TableRowItem(
                columns: columns,
                cells: [
                  Text(rule['name'], overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 13)),
                  Text(rule['notation'], style: mono(context, size: 12.5)),
                  Text(
                    rule['dataset'] == null ? context.tr('common.anyDataset') : datasetName(rule['dataset']),
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(fontSize: 13, color: p.muted),
                  ),
                  Tooltip(
                    message: rule['description'] ?? '',
                    child: Text(
                      rule['description'] ?? '',
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(fontSize: 13, color: p.muted),
                    ),
                  ),
                  IconButton(
                    tooltip: context.tr('common.delete'),
                    icon: Icon(Icons.close, size: 15, color: p.faint),
                    onPressed: () => _delete('/variant-markers/${rule['id']}'),
                  ),
                ],
              );
            },
          ),
        ),
      ],
    );
  }

  Widget _motifTable(BuildContext context) {
    final p = context.p;
    final motifs = widget.store.motifs;
    if (motifs.isEmpty) return EmptyHint(context.tr('rules.empty'));

    final columns = [
      Col(context.tr('rules.name'), flex: 4),
      Col(context.tr('motifs.sequence'), flex: 3),
      Col(context.tr('motifs.category'), width: 100),
      const Col('', width: 36),
    ];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TableHeader(columns),
        Expanded(
          child: ListView.builder(
            itemCount: motifs.length,
            itemBuilder: (context, i) {
              final motif = motifs[i];
              return TableRowItem(
                columns: columns,
                cells: [
                  Text(motif['name'], overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 13)),
                  SelectableText(motif['sequence'], maxLines: 1, style: mono(context, size: 12.5)),
                  Text(
                    context.tr(motif['category'] == 0 ? 'common.virus' : 'common.human'),
                    style: TextStyle(fontSize: 13, color: p.muted),
                  ),
                  IconButton(
                    tooltip: context.tr('common.delete'),
                    icon: Icon(Icons.close, size: 15, color: p.faint),
                    onPressed: () => _delete('/dictionary/${motif['id']}'),
                  ),
                ],
              );
            },
          ),
        ),
      ],
    );
  }
}

// ---------- New variant rule ----------
class _NewRuleDialog extends StatefulWidget {
  final AppStore store;
  const _NewRuleDialog({required this.store});

  @override
  State<_NewRuleDialog> createState() => _NewRuleDialogState();
}

class _NewRuleDialogState extends State<_NewRuleDialog> {
  final _formKey = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _notation = TextEditingController();
  final _description = TextEditingController();
  String? _dataset;
  String? _serverError;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    _notation.addListener(() => setState(() => _serverError = null));
  }

  @override
  void dispose() {
    _name.dispose();
    _notation.dispose();
    _description.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _saving = true);
    try {
      await Api.post('/variant-markers', {
        'name': _name.text.trim(),
        'notation': _notation.text.trim(),
        'dataset': _dataset,
        'description': _description.text.trim().isEmpty ? null : _description.text.trim(),
      });
      widget.store.refresh();
      if (mounted) Navigator.pop(context);
    } on ApiException catch (e) {
      setState(() => _serverError = e.message);
    } catch (_) {
      if (mounted) showMessage(context, context.tr('common.connectionFailed'), error: true);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final preview = describeNotation(_notation.text);

    return AlertDialog(
      title: Text(context.tr('rules.new'), style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600)),
      content: SizedBox(
        width: 460,
        child: Form(
          key: _formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              FieldLabel(
                context.tr('rules.name'),
                child: TextFormField(
                  controller: _name,
                  style: const TextStyle(fontSize: 13),
                  validator: (v) => (v ?? '').trim().isEmpty ? context.tr('rules.nameRequired') : null,
                ),
              ),
              const SizedBox(height: 16),
              FieldLabel(
                context.tr('rules.notation'),
                child: TextFormField(
                  controller: _notation,
                  style: mono(context, size: 13),
                  decoration: InputDecoration(hintText: 'S:N501Y', errorText: _serverError, errorMaxLines: 3),
                  validator: (v) => describeNotation(v ?? '') == null ? context.tr('rules.kind.unknown') : null,
                ),
              ),
              const SizedBox(height: 8),
              SizedBox(
                height: 18,
                child: Text(
                  preview == null ? '' : context.tr(preview.key, preview.args),
                  style: TextStyle(fontSize: 12.5, color: p.success),
                ),
              ),
              const SizedBox(height: 6),
              Wrap(
                spacing: 6,
                runSpacing: 6,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  Text(context.tr('rules.examples'), style: TextStyle(fontSize: 12, color: p.muted)),
                  for (final example in ['S:N501Y', 'S:E484', 'S:F157-', 'A23403G', 'clade:Delta', 'lineage:BA.2.86*'])
                    InkWell(
                      borderRadius: BorderRadius.circular(4),
                      onTap: () => _notation.text = example,
                      child: MutationToken(example, muted: true),
                    ),
                ],
              ),
              const SizedBox(height: 16),
              FieldLabel(
                context.tr('col.dataset'),
                child: DatasetDropdown(
                  datasets: widget.store.datasets,
                  value: _dataset,
                  nullLabel: context.tr('common.anyDataset'),
                  onChanged: (v) => setState(() => _dataset = v),
                ),
              ),
              const SizedBox(height: 16),
              FieldLabel(
                context.tr('rules.descriptionOptional'),
                child: TextFormField(controller: _description, maxLines: 2, style: const TextStyle(fontSize: 13)),
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: Text(context.tr('common.cancel'))),
        FilledButton(onPressed: _saving ? null : _save, child: Text(context.tr('rules.add'))),
      ],
    );
  }
}

// ---------- New ORF motif ----------
class _NewMotifDialog extends StatefulWidget {
  final AppStore store;
  const _NewMotifDialog({required this.store});

  @override
  State<_NewMotifDialog> createState() => _NewMotifDialogState();
}

class _NewMotifDialogState extends State<_NewMotifDialog> {
  final _formKey = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _sequence = TextEditingController();
  int _category = 0;

  static final _aminoAcids = RegExp(r'^[ACDEFGHIKLMNPQRSTVWY*]{4,}$');

  @override
  void dispose() {
    _name.dispose();
    _sequence.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    try {
      await Api.post('/dictionary', {'name': _name.text.trim(), 'sequence': _sequence.text.trim().toUpperCase(), 'category': _category});
      widget.store.refresh();
      if (mounted) Navigator.pop(context);
    } catch (_) {
      if (mounted) showMessage(context, context.tr('common.connectionFailed'), error: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(context.tr('rules.newMotif'), style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600)),
      content: SizedBox(
        width: 440,
        child: Form(
          key: _formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              FieldLabel(
                context.tr('motifs.category'),
                child: Align(
                  alignment: Alignment.centerLeft,
                  child: ChoiceTabs<int>(
                    options: [(0, context.tr('common.virus')), (1, context.tr('common.human'))],
                    value: _category,
                    onChanged: (v) => setState(() => _category = v),
                  ),
                ),
              ),
              const SizedBox(height: 16),
              FieldLabel(
                context.tr('rules.name'),
                child: TextFormField(
                  controller: _name,
                  style: const TextStyle(fontSize: 13),
                  validator: (v) => (v ?? '').trim().isEmpty ? context.tr('rules.nameRequired') : null,
                ),
              ),
              const SizedBox(height: 16),
              FieldLabel(
                context.tr('motifs.sequence'),
                hint: context.tr('motifs.caveat'),
                child: TextFormField(
                  controller: _sequence,
                  style: mono(context, size: 13).copyWith(letterSpacing: 1),
                  decoration: const InputDecoration(hintText: 'QVAVLYQGVNCTEVP', errorMaxLines: 2),
                  validator: (v) => _aminoAcids.hasMatch((v ?? '').trim().toUpperCase()) ? null : context.tr('motifs.sequenceInvalid'),
                ),
              ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: Text(context.tr('common.cancel'))),
        FilledButton(onPressed: _save, child: Text(context.tr('rules.add'))),
      ],
    );
  }
}
