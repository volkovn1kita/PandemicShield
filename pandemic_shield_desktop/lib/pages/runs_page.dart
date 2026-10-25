import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';

import '../api.dart';
import '../l10n.dart';
import '../store.dart';
import '../theme.dart';
import '../widgets.dart';

// ==========================================
// RUNS
// ==========================================
class RunsPage extends StatelessWidget {
  final AppStore store;
  final ValueChanged<String> onOpenRun;

  const RunsPage({super.key, required this.store, required this.onOpenRun});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final columns = [
      Col(context.tr('col.file'), flex: 4),
      Col(context.tr('col.dataset'), flex: 2),
      Col(context.tr('col.records'), width: 80, alignRight: true),
      Col(context.tr('col.size'), width: 90, alignRight: true),
      const Col('', width: 24),
      Col(context.tr('col.nextclade'), flex: 2),
      Col(context.tr('col.alerts'), width: 90, alignRight: true),
      Col(context.tr('col.started'), width: 110, alignRight: true),
    ];
    final numbers = TextStyle(fontSize: 13, color: p.muted, fontFeatures: const [FontFeature.tabularFigures()]);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TopBar(
          title: Text(context.tr('nav.runs')),
          actions: [
            FilledButton.icon(
              onPressed: () => showDialog(
                context: context,
                builder: (_) => NewAnalysisDialog(store: store, onStarted: onOpenRun),
              ),
              icon: const Icon(Icons.add, size: 16),
              label: Text(context.tr('runs.new')),
            ),
          ],
        ),
        if (store.runs.isEmpty)
          Expanded(child: EmptyHint(context.tr('runs.empty')))
        else ...[
          TableHeader(columns),
          Expanded(
            child: ListView.builder(
              itemCount: store.runs.length,
              itemBuilder: (context, i) {
                final run = store.runs[i];
                final alerts = store.alertCountForRun(run['id']);
                return TableRowItem(
                  columns: columns,
                  onTap: () => onOpenRun(run['id']),
                  cells: [
                    Row(
                      children: [
                        Icon(Icons.description_outlined, size: 16, color: p.faint),
                        const SizedBox(width: 10),
                        Flexible(
                          child: Text(
                            run['fileName'],
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w500),
                          ),
                        ),
                      ],
                    ),
                    Text(
                      datasetName(run['nextcladeDataset']),
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(fontSize: 13, color: p.muted),
                    ),
                    Text('${run['recordCount']}', style: numbers),
                    Text(formatBases(run['baseCount'] ?? 0), style: numbers),
                    const SizedBox(),
                    RunStatusLabel(status: run['nextcladeStatus'] ?? 0, message: run['nextcladeMessage']),
                    Text(alerts == 0 ? '—' : '$alerts', style: numbers.copyWith(color: alerts == 0 ? p.faint : p.danger)),
                    Text(relativeTime(context, run['startedAt']), style: numbers),
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

// ==========================================
// NEW ANALYSIS
// ==========================================
class NewAnalysisDialog extends StatefulWidget {
  final AppStore store;
  final ValueChanged<String> onStarted;

  const NewAnalysisDialog({super.key, required this.store, required this.onStarted});

  @override
  State<NewAnalysisDialog> createState() => _NewAnalysisDialogState();
}

class _NewAnalysisDialogState extends State<NewAnalysisDialog> {
  int _category = 0;
  late String? _dataset = widget.store.datasets.where((d) => d.contains('sars-cov-2')).firstOrNull;
  PlatformFile? _file;
  bool _uploading = false;

  Future<void> _pick() async {
    final result = await FilePicker.platform.pickFiles(type: FileType.custom, allowedExtensions: ['fasta', 'fa', 'fna', 'txt']);
    if (result != null) setState(() => _file = result.files.first);
  }

  Future<void> _start() async {
    if (_file?.path == null) return;
    setState(() => _uploading = true);
    final dataset = _category == 0 ? _dataset : null;
    try {
      final response = await Api.analyze(filePath: _file!.path!, category: _category, nextcladeDataset: dataset);
      if (!mounted) return;
      Navigator.pop(context);
      showMessage(
        context,
        dataset != null
            ? context.tr('upload.sentNextclade')
            : context.tr('upload.sent', {'records': response['recordCount'], 'bases': formatBases(response['baseCount'] ?? 0)}),
      );
      widget.store.refresh();
      if (dataset != null) widget.onStarted(response['runId']);
    } on ApiException catch (e) {
      if (mounted) showMessage(context, e.message, error: true);
    } catch (_) {
      if (mounted) showMessage(context, context.tr('common.connectionFailed'), error: true);
    } finally {
      if (mounted) setState(() => _uploading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return AlertDialog(
      titlePadding: const EdgeInsets.fromLTRB(20, 18, 20, 0),
      contentPadding: const EdgeInsets.fromLTRB(20, 16, 20, 4),
      actionsPadding: const EdgeInsets.fromLTRB(20, 12, 20, 16),
      title: Text(context.tr('runs.new'), style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600)),
      content: SizedBox(
        width: 440,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            FieldLabel(
              context.tr('upload.type'),
              child: Align(
                alignment: Alignment.centerLeft,
                child: ChoiceTabs<int>(
                  options: [(0, context.tr('common.virus')), (1, context.tr('common.human'))],
                  value: _category,
                  onChanged: (v) => setState(() => _category = v),
                ),
              ),
            ),
            const SizedBox(height: 18),
            if (_category == 0)
              FieldLabel(
                context.tr('upload.dataset'),
                hint: context.tr('upload.datasetHint'),
                child: DatasetDropdown(
                  datasets: widget.store.datasets,
                  value: _dataset,
                  nullLabel: context.tr('upload.noNextclade'),
                  onChanged: (v) => setState(() => _dataset = v),
                ),
              )
            else
              Text(context.tr('upload.humanHint'), style: TextStyle(fontSize: 12.5, color: p.muted)),
            const SizedBox(height: 18),
            FieldLabel(
              context.tr('upload.file'),
              child: Row(
                children: [
                  OutlinedButton(onPressed: _uploading ? null : _pick, child: Text(context.tr('upload.pick'))),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      _file == null ? '' : '${_file!.name} · ${formatBases(_file!.size).replaceAll('b', 'B')}',
                      overflow: TextOverflow.ellipsis,
                      style: mono(context, size: 12.5, color: p.muted),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: Text(context.tr('common.cancel'))),
        FilledButton(
          onPressed: _uploading || _file == null ? null : _start,
          child: _uploading
              ? SizedBox(width: 14, height: 14, child: CircularProgressIndicator(strokeWidth: 1.6, color: p.onInk))
              : Text(context.tr('upload.start')),
        ),
      ],
    );
  }
}
