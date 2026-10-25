import 'dart:io';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';

import 'api.dart';
import 'l10n.dart';
import 'widgets.dart';

/// Звіт по зразку: PDF — передати результат далі (мова — як в інтерфейсі), CSV — для Excel, R чи pandas.
class ReportButton extends StatelessWidget {
  final dynamic run;

  const ReportButton({super.key, required this.run});

  Future<void> _save(BuildContext context, String format) async {
    final language = L10n.controllerOf(context).value.name;
    final suggested = 'pandemicshield-${(run['fileName'] as String).replaceAll(RegExp(r'[^\w.\-]+'), '_').replaceAll(RegExp(r'\.fasta$'), '')}.$format';
    final messenger = ScaffoldMessenger.of(context);
    final savedText = context.tr('report.saved');
    final failedText = context.tr('common.connectionFailed');

    final path = await FilePicker.platform.saveFile(
      dialogTitle: context.tr('report.saveTitle'),
      fileName: suggested,
      type: FileType.custom,
      allowedExtensions: [format],
    );
    if (path == null) return;

    try {
      final bytes = await Api.download('/runs/${run['id']}/report.$format', format == 'pdf' ? {'lang': language} : null);
      final target = path.toLowerCase().endsWith('.$format') ? path : '$path.$format';
      await File(target).writeAsBytes(bytes);
      messenger.showSnackBar(SnackBar(content: Text('$savedText: $target')));
    } on ApiException catch (e) {
      if (context.mounted) showMessage(context, e.message, error: true);
    } catch (_) {
      messenger.showSnackBar(SnackBar(content: Text(failedText)));
    }
  }

  @override
  Widget build(BuildContext context) {
    return PopupMenuButton<String>(
      tooltip: '',
      position: PopupMenuPosition.under,
      onSelected: (format) => _save(context, format),
      itemBuilder: (context) => [
        PopupMenuItem(value: 'pdf', height: 36, child: Text(context.tr('report.pdf'))),
        PopupMenuItem(value: 'csv', height: 36, child: Text(context.tr('report.csv'))),
      ],
      child: IgnorePointer(
        child: OutlinedButton.icon(
          onPressed: () {},
          icon: const Icon(Icons.description_outlined, size: 15),
          label: Text(context.tr('report.button')),
        ),
      ),
    );
  }
}
