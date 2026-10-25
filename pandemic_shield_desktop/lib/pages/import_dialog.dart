import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';

import '../api.dart';
import '../l10n.dart';
import '../store.dart';
import '../theme.dart';
import '../widgets.dart';
import 'amr_view.dart' show OrganismDropdown;

// ==========================================
// IMPORT FROM NCBI
// ==========================================
/// Масовий імпорт відкритих даних NCBI: список номерів або архів NCBI Datasets.
class NcbiImportDialog extends StatefulWidget {
  final AppStore store;

  const NcbiImportDialog({super.key, required this.store});

  @override
  State<NcbiImportDialog> createState() => _NcbiImportDialogState();
}

class _NcbiImportDialogState extends State<NcbiImportDialog> {
  final _accessions = TextEditingController();
  int _category = 0;
  String? _dataset = autoDataset;
  String? _organism;
  int _source = 0;
  PlatformFile? _archive;
  bool _busy = false;

  @override
  void dispose() {
    _accessions.dispose();
    super.dispose();
  }

  bool get _ready => _source == 0 ? _accessions.text.trim().isNotEmpty : _archive?.path != null;

  Future<void> _pickArchive() async {
    final result = await FilePicker.platform.pickFiles(type: FileType.custom, allowedExtensions: ['zip']);
    if (result != null) setState(() => _archive = result.files.first);
  }

  Future<void> _start() async {
    setState(() => _busy = true);
    final dataset = _category == 0 ? _dataset : null;
    final organism = _category == 2 ? _organism : null;
    try {
      if (_source == 0) {
        await Api.post('/imports/ncbi', {
          'accessions': _accessions.text,
          'category': _category,
          'nextcladeDataset': dataset,
          'amrOrganism': organism,
        });
      } else {
        await Api.importArchive(filePath: _archive!.path!, category: _category, nextcladeDataset: dataset, amrOrganism: organism);
      }
      if (!mounted) return;
      Navigator.pop(context);
      showMessage(context, context.tr('import.started'));
      widget.store.refresh();
    } on ApiException catch (e) {
      if (mounted) showMessage(context, e.message, error: true);
    } catch (_) {
      if (mounted) showMessage(context, context.tr('common.connectionFailed'), error: true);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return AlertDialog(
      titlePadding: const EdgeInsets.fromLTRB(20, 18, 20, 0),
      contentPadding: const EdgeInsets.fromLTRB(20, 16, 20, 4),
      actionsPadding: const EdgeInsets.fromLTRB(20, 12, 20, 16),
      title: Text(context.tr('import.title'), style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600)),
      content: SizedBox(
        width: 480,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            FieldLabel(
              context.tr('upload.type'),
              child: Align(
                alignment: Alignment.centerLeft,
                child: ChoiceTabs<int>(
                  options: [(0, context.tr('common.virus')), (2, context.tr('common.bacteria'))],
                  value: _category,
                  onChanged: (v) => setState(() => _category = v),
                ),
              ),
            ),
            const SizedBox(height: 18),
            if (_category == 0)
              FieldLabel(
                context.tr('upload.dataset'),
                child: DatasetDropdown(
                  datasets: widget.store.datasets,
                  value: _dataset,
                  nullLabel: context.tr('upload.noNextclade'),
                  onChanged: (v) => setState(() => _dataset = v),
                ),
              )
            else
              FieldLabel(
                context.tr('upload.organism'),
                hint: context.tr('import.organismHint'),
                child: OrganismDropdown(
                  organisms: widget.store.organisms,
                  value: _organism,
                  nullLabel: context.tr('import.organismAuto'),
                  onChanged: (v) => setState(() => _organism = v),
                ),
              ),
            const SizedBox(height: 18),
            FieldLabel(
              context.tr('import.source'),
              child: Align(
                alignment: Alignment.centerLeft,
                child: ChoiceTabs<int>(
                  options: [(0, context.tr('import.accessions')), (1, context.tr('import.archive'))],
                  value: _source,
                  onChanged: (v) => setState(() => _source = v),
                ),
              ),
            ),
            const SizedBox(height: 12),
            if (_source == 0)
              FieldLabel(
                context.tr('import.accessionsLabel'),
                hint: context.tr(_category == 0 ? 'import.hintVirus' : 'import.hintBacteria'),
                child: TextField(
                  controller: _accessions,
                  minLines: 4,
                  maxLines: 8,
                  style: mono(context, size: 12.5),
                  onChanged: (_) => setState(() {}),
                  decoration: InputDecoration(
                    hintText: _category == 0 ? 'OQ291490.1\nMN908947.3' : 'GCA_009887825.1\nGCF_000240185.1',
                    hintStyle: mono(context, size: 12.5, color: p.faint),
                  ),
                ),
              )
            else
              FieldLabel(
                context.tr('import.archiveLabel'),
                hint: context.tr('import.archiveHint'),
                child: Row(
                  children: [
                    OutlinedButton(onPressed: _busy ? null : _pickArchive, child: Text(context.tr('import.pickArchive'))),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        _archive == null ? '' : '${_archive!.name} · ${formatBases(_archive!.size).replaceAll('b', 'B')}',
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
          onPressed: _busy || !_ready ? null : _start,
          child: _busy
              ? SizedBox(width: 14, height: 14, child: CircularProgressIndicator(strokeWidth: 1.6, color: p.onInk))
              : Text(context.tr('import.start')),
        ),
      ],
    );
  }
}

// ==========================================
// IMPORT PROGRESS (над таблицею запусків)
// ==========================================
/// Активні імпорти й ті, що завершилися за останні 30 хвилин: прогрес і помилки.
class ImportsStrip extends StatelessWidget {
  final AppStore store;

  const ImportsStrip({super.key, required this.store});

  static const _queued = 0, _running = 1, _failed = 3;

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final now = DateTime.now();
    final visible = store.imports.where((b) {
      final status = b['status'] as int;
      if (status == _queued || status == _running) return true;
      final completed = b['completedAt'] == null ? null : DateTime.parse(b['completedAt']).toLocal();
      return completed != null && now.difference(completed).inMinutes < 30;
    }).toList();
    if (visible.isEmpty) return const SizedBox.shrink();

    return Container(
      decoration: BoxDecoration(border: Border(bottom: BorderSide(color: p.border))),
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 8),
      child: Column(
        children: [for (final batch in visible) _ImportRow(batch: batch)],
      ),
    );
  }
}

class _ImportRow extends StatelessWidget {
  final dynamic batch;

  const _ImportRow({required this.batch});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final status = batch['status'] as int;
    final total = (batch['total'] ?? 0) as int;
    final done = (batch['done'] ?? 0) as int;
    final failed = (batch['failedCount'] ?? 0) as int;
    final active = status == ImportsStrip._queued || status == ImportsStrip._running;
    final source = batch['archiveName'] ?? _accessionsPreview(batch['accessions'] as String? ?? '');
    final bool reads = batch['kind'] == 'fastq';
    final errors = batch['errors'] as String?;

    final parts = <String>[
      context.tr(reads && status == 1 ? 'import.status.processing' : 'import.status.$status'),
      if (total > 0) context.tr('import.progress', {'done': done + failed, 'total': total}),
      if (failed > 0) context.tr('import.failedSome', {'n': failed}),
      if (batch['message'] != null) batch['message'],
    ];

    return Tooltip(
      message: errors ?? '',
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Row(
          children: [
            Icon(Icons.cloud_download_outlined, size: 15, color: status == ImportsStrip._failed ? p.danger : p.faint),
            const SizedBox(width: 10),
            SizedBox(
              width: 260,
              child: Text(
                '${context.tr(reads ? 'import.rowReads' : 'import.row')} · ${batch['category'] == 2 ? context.tr('common.bacteria') : context.tr('common.virus')} · $source',
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(fontSize: 12.5, fontWeight: FontWeight.w500),
              ),
            ),
            const SizedBox(width: 12),
            SizedBox(
              width: 120,
              child: active && total > 0
                  ? ClipRRect(
                      borderRadius: BorderRadius.circular(2),
                      child: LinearProgressIndicator(
                        value: (done + failed) / total,
                        minHeight: 3,
                        backgroundColor: p.hover,
                        color: p.muted,
                      ),
                    )
                  : null,
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                parts.join('  ·  '),
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontSize: 12.5, color: status == ImportsStrip._failed || failed > 0 ? p.danger : p.muted),
              ),
            ),
          ],
        ),
      ),
    );
  }

  static String _accessionsPreview(String accessions) {
    final list = accessions.split(' ').where((a) => a.isNotEmpty).toList();
    return list.length <= 2 ? list.join(', ') : '${list.take(2).join(', ')} +${list.length - 2}';
  }
}
