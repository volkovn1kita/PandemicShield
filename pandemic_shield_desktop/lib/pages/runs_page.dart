import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';

import '../api.dart';
import '../l10n.dart';
import '../store.dart';
import '../theme.dart';
import '../widgets.dart';
import 'amr_view.dart' show OrganismDropdown, isSequenceType, organismName;
import 'import_dialog.dart';

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
      Col(context.tr('col.datasetOrOrganism'), flex: 2),
      Col(context.tr('col.records'), width: 80, alignRight: true),
      Col(context.tr('col.size'), width: 90, alignRight: true),
      const Col('', width: 24),
      Col(context.tr('col.analysis'), flex: 3),
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
            OutlinedButton.icon(
              onPressed: () => showDialog(
                context: context,
                builder: (_) => NcbiImportDialog(store: store),
              ),
              icon: const Icon(Icons.cloud_download_outlined, size: 15),
              label: Text(context.tr('import.button')),
            ),
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
        ImportsStrip(store: store),
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
                final bool bacterial = run['category'] == 2;
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
                      bacterial
                          ? // ST спершу: назва виду довга й обрізається, а тип штаму — головна відмінність між ізолятами.
                            [if (isSequenceType(run['mlstSequenceType'])) 'ST${run['mlstSequenceType']}', organismName(run['amrOrganism'])].join(' · ')
                          : datasetLabel(context, run['nextcladeDataset']),
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontSize: 13,
                        color: p.muted,
                        fontStyle: bacterial && run['amrOrganism'] != null ? FontStyle.italic : null,
                      ),
                    ),
                    Text('${run['recordCount']}', style: numbers),
                    Text(formatBases(run['baseCount'] ?? 0), style: numbers),
                    const SizedBox(),
                    Row(
                      children: [
                        if (bacterial || (run['nextcladeStatus'] ?? 0) != 0)
                          SizedBox(
                            width: 108,
                            child: Text(bacterial ? 'AMRFinderPlus' : 'Nextclade', style: TextStyle(fontSize: 13, color: p.muted)),
                          ),
                        Flexible(
                          child: bacterial
                              ? RunStatusLabel(status: run['amrStatus'] ?? 0, message: run['amrMessage'])
                              : RunStatusLabel(status: run['nextcladeStatus'] ?? 0, message: run['nextcladeMessage']),
                        ),
                      ],
                    ),
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
  // За замовчуванням — автовибір: користувачу не треба знати, який вірус у файлі.
  String? _dataset = autoDataset;
  String? _organism;
  List<PlatformFile> _files = [];
  String _platform = 'illumina';
  bool _uploading = false;

  static final _fastqName = RegExp(r'\.(fastq|fq)(\.gz)?$', caseSensitive: false);

  PlatformFile? get _file => _files.firstOrNull;

  /// Сирі прочитання (FASTQ) замість готового геному: консенсус або збірку зробить воркер Reads.
  bool get _isFastq => _files.isNotEmpty && _files.every((f) => _fastqName.hasMatch(f.name));

  Future<void> _pick() async {
    // Кілька файлів — лише для FASTQ: пара R1/R2 Illumina або частини одного зразка Nanopore.
    final result = await FilePicker.platform.pickFiles(
      type: FileType.custom,
      allowMultiple: true,
      allowedExtensions: ['fasta', 'fa', 'fna', 'txt', 'fastq', 'fq', 'gz'],
    );
    if (result == null) return;
    final files = result.files;
    final fastq = files.every((f) => _fastqName.hasMatch(f.name));
    if (!fastq && files.length > 1) {
      if (mounted) showMessage(context, context.tr('upload.oneFasta'), error: true);
      return;
    }
    setState(() {
      _files = files..sort((a, b) => a.name.compareTo(b.name));
      if (fastq && _category == 1) _category = 0;
    });
  }

  Future<void> _start() async {
    if (_file?.path == null) return;
    if (_isFastq) return _startReads();
    setState(() => _uploading = true);
    final dataset = _category == 0 ? _dataset : null;
    final bacterial = _category == 2;
    try {
      final response = await Api.analyze(
        filePath: _file!.path!,
        category: _category,
        nextcladeDataset: dataset,
        amrOrganism: bacterial ? _organism : null,
      );
      if (!mounted) return;
      Navigator.pop(context);
      showMessage(
        context,
        bacterial
            ? context.tr('upload.sentAmr')
            : dataset != null
            ? context.tr('upload.sentNextclade')
            : context.tr('upload.sent', {'records': response['recordCount'], 'bases': formatBases(response['baseCount'] ?? 0)}),
      );
      widget.store.refresh();
      if (dataset != null || bacterial) widget.onStarted(response['runId']);
    } on ApiException catch (e) {
      if (mounted) showMessage(context, e.message, error: true);
    } catch (_) {
      if (mounted) showMessage(context, context.tr('common.connectionFailed'), error: true);
    } finally {
      if (mounted) setState(() => _uploading = false);
    }
  }

  Future<void> _startReads() async {
    if (_platform == 'illumina' && _files.length > 2) {
      showMessage(context, context.tr('upload.illuminaPairs'), error: true);
      return;
    }
    setState(() => _uploading = true);
    try {
      await Api.uploadReads(
        filePaths: [for (final f in _files) f.path!],
        category: _category,
        platform: _platform,
        nextcladeDataset: _category == 0 ? _dataset : null,
        amrOrganism: _category == 2 ? _organism : null,
      );
      if (!mounted) return;
      Navigator.pop(context);
      showMessage(context, context.tr('upload.sentReads'));
      widget.store.refresh();
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
                  options: [
                    (0, context.tr('common.virus')),
                    (2, context.tr('common.bacteria')),
                    // Прочитання людини не збираються й не картуються: для навчального режиму потрібна готова послідовність.
                    if (!_isFastq) (1, context.tr('common.human')),
                  ],
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
            else if (_category == 2)
              FieldLabel(
                context.tr('upload.organism'),
                hint: context.tr('upload.organismHint'),
                child: OrganismDropdown(
                  organisms: widget.store.organisms,
                  value: _organism,
                  onChanged: (v) => setState(() => _organism = v),
                ),
              )
            else
              Text(context.tr('upload.humanHint'), style: TextStyle(fontSize: 12.5, color: p.muted)),
            const SizedBox(height: 18),
            FieldLabel(
              context.tr(_category == 2 ? 'upload.bacteriaFile' : 'upload.file'),
              hint: context.tr('upload.fastqHint'),
              child: Row(
                children: [
                  OutlinedButton(onPressed: _uploading ? null : _pick, child: Text(context.tr('upload.pick'))),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      _files.map((f) => '${f.name} · ${formatBases(f.size).replaceAll('b', 'B')}').join('\n'),
                      overflow: TextOverflow.ellipsis,
                      maxLines: 3,
                      style: mono(context, size: 12.5, color: p.muted),
                    ),
                  ),
                ],
              ),
            ),
            if (_isFastq) ...[
              const SizedBox(height: 18),
              FieldLabel(
                context.tr('upload.platform'),
                hint: context.tr(_category == 2
                    ? (_platform == 'nanopore' ? 'upload.readsBacteriaOnt' : 'upload.readsBacteriaIllumina')
                    : 'upload.readsVirus'),
                child: Align(
                  alignment: Alignment.centerLeft,
                  child: ChoiceTabs<String>(
                    options: const [('illumina', 'Illumina'), ('nanopore', 'Nanopore')],
                    value: _platform,
                    onChanged: (v) => setState(() => _platform = v),
                  ),
                ),
              ),
            ],
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
