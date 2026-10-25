import 'package:flutter/material.dart';

import '../api.dart';
import '../l10n.dart';
import '../store.dart';
import '../theme.dart';
import '../widgets.dart';

// ==========================================
// ALERTS
// ==========================================
class AlertsPage extends StatefulWidget {
  final AppStore store;
  final ValueChanged<String> onOpenRun;

  const AlertsPage({super.key, required this.store, required this.onOpenRun});

  @override
  State<AlertsPage> createState() => _AlertsPageState();
}

class _AlertsPageState extends State<AlertsPage> {
  String _source = 'all';

  Future<void> _delete(String id) async {
    try {
      await Api.delete('/threats/$id');
      widget.store.refresh();
    } catch (_) {}
  }

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final all = widget.store.alerts;
    int count(String source) => all.where((a) => a['source'] == source).length;
    final alerts = _source == 'all' ? all : all.where((a) => a['source'] == _source).toList();

    final columns = [
      Col(context.tr('alerts.rule'), flex: 4),
      Col(context.tr('col.genome'), flex: 3),
      Col(context.tr('alerts.variant'), flex: 2),
      Col(context.tr('alerts.position'), width: 140),
      Col(context.tr('alerts.source'), width: 120),
      Col(context.tr('alerts.when'), width: 100, alignRight: true),
      const Col('', width: 36),
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TopBar(
          title: Text(context.tr('nav.alerts')),
          actions: [
            ChoiceTabs<String>(
              options: [
                ('all', '${context.tr('common.all')}  ${all.length}'),
                ('nextclade', '${context.tr('source.nextclade')}  ${count('nextclade')}'),
                ('amrfinder', '${context.tr('source.amrfinder')}  ${count('amrfinder')}'),
                ('mlst', '${context.tr('source.mlst')}  ${count('mlst')}'),
                ('orf-motif', '${context.tr('source.orf')}  ${count('orf-motif')}'),
              ],
              value: _source,
              onChanged: (v) => setState(() => _source = v),
            ),
          ],
        ),
        if (alerts.isEmpty)
          Expanded(child: EmptyHint(context.tr('alerts.empty')))
        else ...[
          TableHeader(columns),
          Expanded(
            child: ListView.builder(
              itemCount: alerts.length,
              itemBuilder: (context, i) {
                final alert = alerts[i];
                final String source = alert['source'];
                final bool fromNextclade = source == 'nextclade';
                // MLST описує весь геном (сім генів одразу), тож позиції в нього немає.
                final String position = source == 'mlst'
                    ? ''
                    : fromNextclade
                    ? ((alert['ntStart'] ?? 0) > 0 ? 'ref ${alert['ntStart']}' : '')
                    : '${alert['ntStart']}–${alert['ntEnd']} ${alert['strand']}';
                final String variant = source == 'orf-motif' ? '' : shortVariant(alert['variant']);
                final (sourceKey, positionHint) = switch (source) {
                  'nextclade' => ('source.nextclade', 'alerts.refHint'),
                  'amrfinder' => ('source.amrfinder', 'alerts.contigHint'),
                  'mlst' => ('source.mlst', 'alerts.mlstHint'),
                  _ => ('source.orf', 'alerts.recordHint'),
                };

                return TableRowItem(
                  columns: columns,
                  onTap: alert['runId'] == null ? null : () => widget.onOpenRun(alert['runId']),
                  cells: [
                    Text(alert['threatName'], overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 13)),
                    Tooltip(
                      message: alert['recordId'],
                      child: Text(
                        alert['recordId'],
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(fontSize: 13, color: p.muted),
                      ),
                    ),
                    Text(
                      variant,
                      overflow: TextOverflow.ellipsis,
                      style: mono(context, size: 12.5, color: p.danger),
                    ),
                    Tooltip(
                      message: context.tr(positionHint),
                      child: Text(position, style: mono(context, size: 12, color: p.muted)),
                    ),
                    Text(context.tr(sourceKey), style: TextStyle(fontSize: 13, color: p.muted)),
                    Text(relativeTime(context, alert['detectedAt']), style: TextStyle(fontSize: 13, color: p.muted)),
                    IconButton(
                      tooltip: context.tr('common.delete'),
                      icon: Icon(Icons.close, size: 15, color: p.faint),
                      onPressed: () => _delete(alert['id']),
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
