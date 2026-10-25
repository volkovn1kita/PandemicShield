import 'package:flutter/material.dart';

import '../api.dart';
import '../l10n.dart';
import '../theme.dart';
import '../widgets.dart';

// ==========================================
// PAGE 3: VARIANT RULES (alerts on Nextclade results)
// ==========================================

/// Розбір позначення для живої підказки в UI. Остаточна перевірка — на сервері (VariantPattern).
({IconData icon, String key, Map<String, Object?> args})? describeNotation(String text) {
  final value = text.trim();
  final clade = RegExp(r'^clade:(.+)$', caseSensitive: false).firstMatch(value);
  if (clade != null) return (icon: Icons.account_tree_outlined, key: 'rules.kind.clade', args: {'name': clade[1]!.trim()});

  final lineage = RegExp(r'^lineage:([A-Za-z]{1,3}(\.\d+)*\*?)$', caseSensitive: false).firstMatch(value);
  if (lineage != null) return (icon: Icons.account_tree_outlined, key: 'rules.kind.lineage', args: {'name': lineage[1]!.toUpperCase()});

  final aa = RegExp(r'^([A-Za-z][A-Za-z0-9_.\-]*):([A-Za-z*])?(\d+)([A-Za-z*]|-)?$').firstMatch(value);
  if (aa != null && int.parse(aa[3]!) > 0) {
    return (icon: Icons.hub_outlined, key: aa[4] == '-' ? 'rules.kind.aaDel' : 'rules.kind.aa', args: {'gene': aa[1], 'pos': aa[3]});
  }

  final nuc = RegExp(r'^([ACGTacgt])?(\d+)([ACGTacgt]|\*|-)?$').firstMatch(value);
  if (nuc != null && int.parse(nuc[2]!) > 0) {
    return (icon: Icons.linear_scale, key: nuc[3] == '-' ? 'rules.kind.nucDel' : 'rules.kind.nuc', args: {'pos': nuc[2]});
  }
  return null;
}

class RulesPage extends StatefulWidget {
  final bool active;
  const RulesPage({super.key, required this.active});

  @override
  State<RulesPage> createState() => _RulesPageState();
}

class _RulesPageState extends State<RulesPage> {
  final _formKey = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _notation = TextEditingController();
  final _description = TextEditingController();

  List<dynamic> _rules = [];
  List<String> _datasets = [];
  String? _dataset;
  String? _serverError;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    _load();
    _notation.addListener(() => setState(() => _serverError = null));
  }

  @override
  void didUpdateWidget(covariant RulesPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.active && !oldWidget.active) _load();
  }

  @override
  void dispose() {
    _name.dispose();
    _notation.dispose();
    _description.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final results = await Future.wait([Api.list('/variant-markers'), Api.list('/nextclade/datasets')]);
      if (!mounted) return;
      setState(() {
        _rules = results[0];
        _datasets = List<String>.from(results[1]);
      });
    } catch (_) {}
  }

  Future<void> _add() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _saving = true);
    try {
      await Api.post('/variant-markers', {
        'name': _name.text.trim(),
        'notation': _notation.text.trim(),
        'dataset': _dataset,
        'description': _description.text.trim().isEmpty ? null : _description.text.trim(),
      });
      _name.clear();
      _notation.clear();
      _description.clear();
      _load();
    } on ApiException catch (e) {
      setState(() => _serverError = e.message);
    } catch (_) {
      if (mounted) showMessage(context, context.tr('common.connectionFailed'), error: true);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _delete(dynamic rule) async {
    try {
      await Api.delete('/variant-markers/${rule['id']}');
      _load();
    } catch (_) {}
  }

  @override
  Widget build(BuildContext context) {
    final preview = describeNotation(_notation.text);

    return Padding(
      padding: const EdgeInsets.all(28),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          PageHeader(title: context.tr('rules.title'), subtitle: context.tr('rules.subtitle')),
          Expanded(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                SizedBox(
                  width: 400,
                  child: SingleChildScrollView(
                    child: Panel(
                      expand: false,
                      title: context.tr('rules.new'),
                      child: Form(
                        key: _formKey,
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            TextFormField(
                              controller: _name,
                              decoration: InputDecoration(labelText: context.tr('rules.name')),
                              validator: (v) => (v ?? '').trim().isEmpty ? context.tr('rules.nameRequired') : null,
                            ),
                            const SizedBox(height: 14),
                            TextFormField(
                              controller: _notation,
                              style: monoStyle.copyWith(fontSize: 14),
                              decoration: InputDecoration(
                                labelText: context.tr('rules.notation'),
                                errorText: _serverError,
                                errorMaxLines: 3,
                              ),
                              validator: (v) => describeNotation(v ?? '') == null ? context.tr('rules.kind.unknown') : null,
                            ),
                            const SizedBox(height: 8),
                            AnimatedSwitcher(
                              duration: const Duration(milliseconds: 150),
                              child: Row(
                                key: ValueKey(preview?.key),
                                children: [
                                  Icon(
                                    preview == null ? Icons.help_outline : preview.icon,
                                    size: 16,
                                    color: preview == null ? AppColors.textFaint : AppColors.success,
                                  ),
                                  const SizedBox(width: 6),
                                  Expanded(
                                    child: Text(
                                      preview == null
                                          ? (_notation.text.trim().isEmpty ? '' : context.tr('rules.kind.unknown'))
                                          : context.tr(preview.key, preview.args),
                                      style: TextStyle(fontSize: 12.5, color: preview == null ? AppColors.textMuted : AppColors.success),
                                    ),
                                  ),
                                ],
                              ),
                            ),
                            const SizedBox(height: 14),
                            SectionLabel(context.tr('rules.examples')),
                            const SizedBox(height: 8),
                            Wrap(
                              spacing: 6,
                              runSpacing: 6,
                              children: [
                                for (final example in ['S:N501Y', 'S:E484', 'S:F157-', 'A23403G', 'clade:Delta', 'lineage:BA.2.86*'])
                                  ActionChip(
                                    label: Text(example, style: monoStyle.copyWith(fontSize: 11.5)),
                                    visualDensity: VisualDensity.compact,
                                    onPressed: () => _notation.text = example,
                                  ),
                              ],
                            ),
                            const SizedBox(height: 16),
                            DatasetDropdown(
                              datasets: _datasets,
                              value: _dataset,
                              nullLabel: context.tr('common.anyDataset'),
                              onChanged: (v) => setState(() => _dataset = v),
                            ),
                            const SizedBox(height: 14),
                            TextFormField(
                              controller: _description,
                              maxLines: 2,
                              decoration: InputDecoration(labelText: context.tr('rules.description')),
                            ),
                            const SizedBox(height: 18),
                            FilledButton.icon(
                              onPressed: _saving ? null : _add,
                              icon: const Icon(Icons.add),
                              label: Text(context.tr('rules.add')),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
                const SizedBox(width: 20),
                Expanded(
                  child: Panel(
                    title: context.tr('rules.active'),
                    subtitle: context.tr('rules.activeHint'),
                    child: _rules.isEmpty
                        ? EmptyState(icon: Icons.rule, text: context.tr('rules.empty'))
                        : ListView.separated(
                            itemCount: _rules.length,
                            separatorBuilder: (_, _) => const SizedBox(height: 8),
                            itemBuilder: (context, i) => _RuleCard(rule: _rules[i], onDelete: () => _delete(_rules[i])),
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

class _RuleCard extends StatelessWidget {
  final dynamic rule;
  final VoidCallback onDelete;

  const _RuleCard({required this.rule, required this.onDelete});

  @override
  Widget build(BuildContext context) {
    final kind = describeNotation(rule['notation']);
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.surfaceMuted,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(color: AppColors.danger.withValues(alpha: 0.1), borderRadius: BorderRadius.circular(10)),
            child: Icon(kind?.icon ?? Icons.rule, size: 18, color: AppColors.danger),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(rule['name'], style: const TextStyle(fontWeight: FontWeight.w600)),
                const SizedBox(height: 6),
                Wrap(
                  spacing: 6,
                  runSpacing: 4,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    VariantTag(rule['notation'], color: AppColors.danger),
                    Pill(
                      text: rule['dataset'] == null ? context.tr('common.anyDataset') : datasetName(rule['dataset']),
                      color: AppColors.nextclade,
                    ),
                  ],
                ),
                if (rule['description'] != null) ...[
                  const SizedBox(height: 6),
                  Text(rule['description'], style: Theme.of(context).textTheme.bodySmall),
                ],
              ],
            ),
          ),
          IconButton(
            tooltip: context.tr('common.delete'),
            icon: const Icon(Icons.delete_outline, color: AppColors.textMuted),
            onPressed: onDelete,
          ),
        ],
      ),
    );
  }
}
