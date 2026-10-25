import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'l10n.dart';
import 'pages/alerts_page.dart';
import 'pages/explorer_page.dart';
import 'pages/genomes_page.dart';
import 'pages/rules_page.dart';
import 'pages/runs_page.dart';
import 'pages/surveillance_page.dart';
import 'store.dart';
import 'theme.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Мова й тема зберігаються між запусками застосунку.
  final prefs = await SharedPreferences.getInstance();
  final language = LanguageController()..value = prefs.getString('language') == 'en' ? AppLanguage.en : AppLanguage.uk;
  language.addListener(() => prefs.setString('language', language.value.name));

  runApp(PandemicShieldApp(language: language, store: AppStore()..start(), prefs: prefs));
}

class PandemicShieldApp extends StatefulWidget {
  final LanguageController language;
  final AppStore store;
  final SharedPreferences prefs;

  const PandemicShieldApp({super.key, required this.language, required this.store, required this.prefs});

  @override
  State<PandemicShieldApp> createState() => _PandemicShieldAppState();
}

class _PandemicShieldAppState extends State<PandemicShieldApp> {
  late bool _dark = widget.prefs.getBool('dark') ?? false;

  void _toggleTheme() {
    setState(() => _dark = !_dark);
    widget.prefs.setBool('dark', _dark);
  }

  @override
  Widget build(BuildContext context) {
    return L10n(
      controller: widget.language,
      child: MaterialApp(
        title: 'PandemicShield',
        debugShowCheckedModeBanner: false,
        theme: buildTheme(_dark ? Palette.dark : Palette.light),
        home: AppShell(store: widget.store, onToggleTheme: _toggleTheme),
      ),
    );
  }
}

// ==========================================
// SHELL
// ==========================================
class AppShell extends StatefulWidget {
  final AppStore store;
  final VoidCallback onToggleTheme;

  const AppShell({super.key, required this.store, required this.onToggleTheme});

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  int _page = 0;
  String? _selectedRunId;

  void _openRun(String runId) => setState(() {
    _selectedRunId = runId;
    _page = 1;
  });

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: widget.store,
      builder: (context, _) => Scaffold(
        body: Row(
          children: [
            _Sidebar(
              page: _page,
              store: widget.store,
              onSelect: (page) => setState(() => _page = page),
              onToggleTheme: widget.onToggleTheme,
            ),
            Expanded(
              child: IndexedStack(
                index: _page,
                children: [
                  RunsPage(store: widget.store, onOpenRun: _openRun),
                  GenomesPage(
                    store: widget.store,
                    active: _page == 1,
                    selectedRunId: _selectedRunId ?? widget.store.runs.where((r) => r['nextcladeStatus'] == 3).firstOrNull?['id'],
                    onSelectRun: (id) => setState(() => _selectedRunId = id),
                  ),
                  AlertsPage(store: widget.store, onOpenRun: _openRun),
                  RulesPage(store: widget.store),
                  SurveillancePage(store: widget.store, active: _page == 4, onOpenRun: _openRun),
                  const ExplorerPage(),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Sidebar extends StatelessWidget {
  final int page;
  final AppStore store;
  final ValueChanged<int> onSelect;
  final VoidCallback onToggleTheme;

  const _Sidebar({required this.page, required this.store, required this.onSelect, required this.onToggleTheme});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    final language = L10n.controllerOf(context);
    final items = [
      (Icons.folder_outlined, 'nav.runs', store.runs.length),
      (
        Icons.biotech_outlined,
        'nav.genomes',
        store.runs.fold<int>(0, (n, r) => n + ((r['sequencesAnalyzed'] ?? 0) as int) + (r['amrStatus'] == 3 ? 1 : 0)),
      ),
      (Icons.notifications_none, 'nav.alerts', store.alerts.length),
      (Icons.rule, 'nav.rules', store.variantRules.length + store.motifs.length),
      // Нагляд за даними NCBI: лічильника немає, це не список об'єктів, а зведення.
      (Icons.public, 'nav.surveillance', 0),
      // Навчальний режим: файл відкривається в пам'яті, нічого не зберігається.
      (Icons.science_outlined, 'nav.explorer', 0),
    ];

    return Container(
      width: 224,
      decoration: BoxDecoration(
        color: p.sidebar,
        border: Border(right: BorderSide(color: p.border)),
      ),
      padding: const EdgeInsets.fromLTRB(10, 14, 10, 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(8, 2, 8, 18),
            child: Row(
              children: [
                Container(
                  width: 22,
                  height: 22,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(color: p.ink, borderRadius: BorderRadius.circular(5)),
                  child: Text(
                    'PS',
                    style: TextStyle(color: p.onInk, fontSize: 10, fontWeight: FontWeight.w700, letterSpacing: -0.2),
                  ),
                ),
                const SizedBox(width: 10),
                Text(
                  'PandemicShield',
                  style: TextStyle(fontSize: 13.5, fontWeight: FontWeight.w600, color: p.text),
                ),
              ],
            ),
          ),
          for (int i = 0; i < items.length; i++)
            _NavItem(icon: items[i].$1, label: context.tr(items[i].$2), count: items[i].$3, selected: page == i, onTap: () => onSelect(i)),
          const Spacer(),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 8),
            child: Row(
              children: [
                Container(
                  width: 6,
                  height: 6,
                  decoration: BoxDecoration(color: store.online ? p.success : p.danger, shape: BoxShape.circle),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    context.tr(store.online ? (store.live ? 'status.live' : 'status.polling') : 'status.offline'),
                    style: TextStyle(fontSize: 12, color: p.muted),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: ChoiceTabsCompact(
                  options: const [(AppLanguage.uk, 'УКР'), (AppLanguage.en, 'EN')],
                  value: language.value,
                  onChanged: (v) => language.value = v,
                ),
              ),
              const SizedBox(width: 6),
              IconButton(
                tooltip: context.tr('theme.toggle'),
                onPressed: onToggleTheme,
                icon: Icon(p.brightness == Brightness.dark ? Icons.light_mode_outlined : Icons.dark_mode_outlined, size: 17),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 8),
            child: Text(context.tr('disclaimer'), style: TextStyle(fontSize: 11, color: p.faint, height: 1.4)),
          ),
        ],
      ),
    );
  }
}

class _NavItem extends StatelessWidget {
  final IconData icon;
  final String label;
  final int count;
  final bool selected;
  final VoidCallback onTap;

  const _NavItem({required this.icon, required this.label, required this.count, required this.selected, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return Padding(
      padding: const EdgeInsets.only(bottom: 1),
      child: Material(
        color: selected ? p.selected : Colors.transparent,
        borderRadius: BorderRadius.circular(6),
        child: InkWell(
          borderRadius: BorderRadius.circular(6),
          hoverColor: p.hover,
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 7),
            child: Row(
              children: [
                Icon(icon, size: 17, color: selected ? p.text : p.muted),
                const SizedBox(width: 10),
                Expanded(
                  child: Text(
                    label,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontSize: 13,
                      color: selected ? p.text : p.muted,
                      fontWeight: selected ? FontWeight.w500 : FontWeight.w400,
                    ),
                  ),
                ),
                if (count > 0)
                  Text(
                    '$count',
                    style: TextStyle(fontSize: 12, color: p.faint, fontFeatures: const [FontFeature.tabularFigures()]),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Перемикач мови в бічній панелі (компактніший за ChoiceTabs і на всю ширину).
class ChoiceTabsCompact<T> extends StatelessWidget {
  final List<(T, String)> options;
  final T value;
  final ValueChanged<T> onChanged;

  const ChoiceTabsCompact({super.key, required this.options, required this.value, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    final p = context.p;
    return Container(
      height: 30,
      padding: const EdgeInsets.all(2),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(6),
        border: Border.all(color: p.border),
      ),
      child: Row(
        children: [
          for (final (option, label) in options)
            Expanded(
              child: GestureDetector(
                onTap: () => onChanged(option),
                child: MouseRegion(
                  cursor: SystemMouseCursors.click,
                  child: Container(
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      color: option == value ? p.selected : Colors.transparent,
                      borderRadius: BorderRadius.circular(4),
                    ),
                    child: Text(
                      label,
                      style: TextStyle(
                        fontSize: 11.5,
                        fontWeight: FontWeight.w600,
                        color: option == value ? p.text : p.faint,
                        letterSpacing: 0.3,
                      ),
                    ),
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}
