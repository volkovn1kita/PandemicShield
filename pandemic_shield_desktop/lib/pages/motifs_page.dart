import 'package:flutter/material.dart';

import '../api.dart';
import '../l10n.dart';
import '../theme.dart';
import '../widgets.dart';

// ==========================================
// PAGE 4: ORF MOTIFS (learning mode)
// ==========================================
class MotifsPage extends StatefulWidget {
  final bool active;
  const MotifsPage({super.key, required this.active});

  @override
  State<MotifsPage> createState() => _MotifsPageState();
}

class _MotifsPageState extends State<MotifsPage> {
  final _formKey = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _sequence = TextEditingController();
  int _category = 0;
  List<dynamic> _motifs = [];

  static final _aminoAcids = RegExp(r'^[ACDEFGHIKLMNPQRSTVWY*]{4,}$');

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void didUpdateWidget(covariant MotifsPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.active && !oldWidget.active) _load();
  }

  @override
  void dispose() {
    _name.dispose();
    _sequence.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final motifs = await Api.list('/dictionary');
      if (mounted) setState(() => _motifs = motifs);
    } catch (_) {}
  }

  Future<void> _add() async {
    if (!_formKey.currentState!.validate()) return;
    try {
      await Api.post('/dictionary', {'name': _name.text.trim(), 'sequence': _sequence.text.trim().toUpperCase(), 'category': _category});
      _name.clear();
      _sequence.clear();
      _load();
    } catch (_) {
      if (mounted) showMessage(context, context.tr('common.connectionFailed'), error: true);
    }
  }

  Future<void> _delete(String id) async {
    try {
      await Api.delete('/dictionary/$id');
      _load();
    } catch (_) {}
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(28),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          PageHeader(title: context.tr('motifs.title'), subtitle: context.tr('motifs.subtitle')),
          Expanded(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                SizedBox(
                  width: 400,
                  child: SingleChildScrollView(
                    child: Panel(
                      expand: false,
                      title: context.tr('motifs.new'),
                      child: Form(
                        key: _formKey,
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            SegmentedButton<int>(
                              segments: [
                                ButtonSegment(
                                  value: 0,
                                  icon: const Icon(Icons.coronavirus_outlined, size: 18),
                                  label: Text(context.tr('common.virus')),
                                ),
                                ButtonSegment(
                                  value: 1,
                                  icon: const Icon(Icons.person_outline, size: 18),
                                  label: Text(context.tr('common.human')),
                                ),
                              ],
                              selected: {_category},
                              showSelectedIcon: false,
                              onSelectionChanged: (s) => setState(() => _category = s.first),
                            ),
                            const SizedBox(height: 16),
                            TextFormField(
                              controller: _name,
                              decoration: InputDecoration(labelText: context.tr('motifs.name')),
                              validator: (v) => (v ?? '').trim().isEmpty ? context.tr('rules.nameRequired') : null,
                            ),
                            const SizedBox(height: 14),
                            TextFormField(
                              controller: _sequence,
                              style: monoStyle.copyWith(fontSize: 14, letterSpacing: 1.2),
                              decoration: InputDecoration(labelText: context.tr('motifs.sequence'), errorMaxLines: 2),
                              validator: (v) =>
                                  _aminoAcids.hasMatch((v ?? '').trim().toUpperCase()) ? null : context.tr('motifs.sequenceInvalid'),
                            ),
                            const SizedBox(height: 16),
                            Container(
                              padding: const EdgeInsets.all(12),
                              decoration: BoxDecoration(
                                color: AppColors.warning.withValues(alpha: 0.08),
                                borderRadius: BorderRadius.circular(10),
                                border: Border.all(color: AppColors.warning.withValues(alpha: 0.25)),
                              ),
                              child: Row(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  const Icon(Icons.lightbulb_outline, size: 18, color: AppColors.warning),
                                  const SizedBox(width: 8),
                                  Expanded(
                                    child: Text(context.tr('motifs.caveat'), style: const TextStyle(fontSize: 12.5, color: AppColors.text)),
                                  ),
                                ],
                              ),
                            ),
                            const SizedBox(height: 18),
                            FilledButton.icon(onPressed: _add, icon: const Icon(Icons.add), label: Text(context.tr('motifs.add'))),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
                const SizedBox(width: 20),
                Expanded(
                  child: Panel(
                    title: context.tr('motifs.list'),
                    child: _motifs.isEmpty
                        ? EmptyState(icon: Icons.dns_outlined, text: context.tr('motifs.empty'))
                        : ListView.separated(
                            itemCount: _motifs.length,
                            separatorBuilder: (_, _) => const SizedBox(height: 8),
                            itemBuilder: (context, i) {
                              final motif = _motifs[i];
                              final bool virus = motif['category'] == 0;
                              final color = virus ? AppColors.warning : AppColors.nextclade;
                              return Container(
                                padding: const EdgeInsets.all(14),
                                decoration: BoxDecoration(
                                  color: AppColors.surfaceMuted,
                                  borderRadius: BorderRadius.circular(12),
                                  border: Border.all(color: AppColors.border),
                                ),
                                child: Row(
                                  children: [
                                    Icon(virus ? Icons.coronavirus_outlined : Icons.person_outline, color: color),
                                    const SizedBox(width: 14),
                                    Expanded(
                                      child: Column(
                                        crossAxisAlignment: CrossAxisAlignment.start,
                                        children: [
                                          Text(motif['name'], style: const TextStyle(fontWeight: FontWeight.w600)),
                                          const SizedBox(height: 6),
                                          SelectableText(
                                            motif['sequence'],
                                            style: monoStyle.copyWith(letterSpacing: 1.2, color: AppColors.primary),
                                          ),
                                        ],
                                      ),
                                    ),
                                    IconButton(
                                      tooltip: context.tr('common.delete'),
                                      icon: const Icon(Icons.delete_outline, color: AppColors.textMuted),
                                      onPressed: () => _delete(motif['id']),
                                    ),
                                  ],
                                ),
                              );
                            },
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
