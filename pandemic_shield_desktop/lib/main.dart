import 'dart:async';

import 'package:flutter/material.dart';

import 'api.dart';
import 'l10n.dart';
import 'pages/motifs_page.dart';
import 'pages/overview_page.dart';
import 'pages/rules_page.dart';
import 'pages/samples_page.dart';
import 'theme.dart';

void main() {
  runApp(PandemicShieldApp(language: LanguageController()));
}

class PandemicShieldApp extends StatelessWidget {
  final LanguageController language;
  const PandemicShieldApp({super.key, required this.language});

  @override
  Widget build(BuildContext context) {
    return L10n(
      controller: language,
      child: MaterialApp(title: 'PandemicShield', debugShowCheckedModeBanner: false, theme: buildTheme(), home: const AppShell()),
    );
  }
}

// ==========================================
// SHELL: бічна панель + сторінки
// ==========================================
class AppShell extends StatefulWidget {
  const AppShell({super.key});

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  int _page = 0;
  String? _selectedRunId;
  bool _online = false;
  Timer? _healthTimer;

  @override
  void initState() {
    super.initState();
    _checkHealth();
    _healthTimer = Timer.periodic(const Duration(seconds: 5), (_) => _checkHealth());
  }

  @override
  void dispose() {
    _healthTimer?.cancel();
    super.dispose();
  }

  Future<void> _checkHealth() async {
    bool online;
    try {
      await Api.get('/nextclade/datasets');
      online = true;
    } catch (_) {
      online = false;
    }
    if (mounted && online != _online) setState(() => _online = online);
  }

  void _openRun(String runId) => setState(() {
    _selectedRunId = runId;
    _page = 1;
  });

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Row(
        children: [
          _Sidebar(page: _page, online: _online, onSelect: (page) => setState(() => _page = page)),
          Expanded(
            child: IndexedStack(
              index: _page,
              children: [
                OverviewPage(active: _page == 0, onOpenRun: _openRun),
                SamplesPage(active: _page == 1, selectedRunId: _selectedRunId, onSelectRun: (id) => setState(() => _selectedRunId = id)),
                RulesPage(active: _page == 2),
                MotifsPage(active: _page == 3),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Sidebar extends StatelessWidget {
  final int page;
  final bool online;
  final ValueChanged<int> onSelect;

  const _Sidebar({required this.page, required this.online, required this.onSelect});

  @override
  Widget build(BuildContext context) {
    final items = [
      (Icons.space_dashboard_outlined, 'nav.overview'),
      (Icons.biotech_outlined, 'nav.samples'),
      (Icons.rule, 'nav.rules'),
      (Icons.dns_outlined, 'nav.motifs'),
    ];

    return Container(
      width: 236,
      decoration: const BoxDecoration(
        gradient: LinearGradient(begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [AppColors.sidebar, Color(0xFF0E1A2B)]),
      ),
      padding: const EdgeInsets.fromLTRB(16, 24, 16, 18),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Container(
                width: 38,
                height: 38,
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(11),
                  gradient: const LinearGradient(colors: [AppColors.primary, AppColors.primaryBright]),
                  boxShadow: [BoxShadow(color: AppColors.primaryBright.withValues(alpha: 0.35), blurRadius: 14)],
                ),
                child: const Icon(Icons.shield_outlined, color: Colors.white, size: 22),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'PandemicShield',
                      style: TextStyle(color: Colors.white, fontWeight: FontWeight.w600, fontSize: 16),
                    ),
                    Text(context.tr('app.tagline'), style: const TextStyle(color: AppColors.sidebarText, fontSize: 11.5)),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 32),
          for (int i = 0; i < items.length; i++)
            _NavItem(icon: items[i].$1, label: context.tr(items[i].$2), selected: page == i, onTap: () => onSelect(i)),
          const Spacer(),
          const _LanguageSwitch(),
          const SizedBox(height: 14),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            decoration: BoxDecoration(color: AppColors.sidebarRaised, borderRadius: BorderRadius.circular(10)),
            child: Row(
              children: [
                AnimatedContainer(
                  duration: const Duration(milliseconds: 300),
                  width: 8,
                  height: 8,
                  decoration: BoxDecoration(
                    color: online ? AppColors.primaryBright : AppColors.danger,
                    shape: BoxShape.circle,
                    boxShadow: [
                      BoxShadow(color: (online ? AppColors.primaryBright : AppColors.danger).withValues(alpha: 0.6), blurRadius: 8),
                    ],
                  ),
                ),
                const SizedBox(width: 10),
                Text(context.tr(online ? 'status.online' : 'status.offline'), style: const TextStyle(color: Colors.white, fontSize: 12.5)),
              ],
            ),
          ),
          const SizedBox(height: 10),
          Text(context.tr('disclaimer'), style: const TextStyle(color: AppColors.sidebarText, fontSize: 11)),
        ],
      ),
    );
  }
}

class _NavItem extends StatelessWidget {
  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback onTap;

  const _NavItem({required this.icon, required this.label, required this.selected, required this.onTap});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: Material(
        color: selected ? AppColors.primaryBright.withValues(alpha: 0.12) : Colors.transparent,
        borderRadius: BorderRadius.circular(10),
        child: InkWell(
          borderRadius: BorderRadius.circular(10),
          hoverColor: Colors.white.withValues(alpha: 0.05),
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 11),
            child: Row(
              children: [
                Icon(icon, size: 20, color: selected ? AppColors.primaryBright : AppColors.sidebarText),
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    label,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      color: selected ? Colors.white : AppColors.sidebarText,
                      fontWeight: selected ? FontWeight.w600 : FontWeight.w400,
                      fontSize: 14,
                    ),
                  ),
                ),
                if (selected)
                  Container(
                    width: 6,
                    height: 6,
                    decoration: const BoxDecoration(color: AppColors.primaryBright, shape: BoxShape.circle),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _LanguageSwitch extends StatelessWidget {
  const _LanguageSwitch();

  @override
  Widget build(BuildContext context) {
    final controller = L10n.controllerOf(context);
    Widget option(AppLanguage language, String label) {
      final selected = controller.value == language;
      return Expanded(
        child: GestureDetector(
          onTap: () => controller.value = language,
          child: MouseRegion(
            cursor: SystemMouseCursors.click,
            child: AnimatedContainer(
              duration: const Duration(milliseconds: 200),
              padding: const EdgeInsets.symmetric(vertical: 7),
              decoration: BoxDecoration(
                color: selected ? AppColors.primaryBright.withValues(alpha: 0.18) : Colors.transparent,
                borderRadius: BorderRadius.circular(8),
              ),
              alignment: Alignment.center,
              child: Text(
                label,
                style: TextStyle(
                  color: selected ? Colors.white : AppColors.sidebarText,
                  fontWeight: FontWeight.w600,
                  fontSize: 12.5,
                  letterSpacing: 0.6,
                ),
              ),
            ),
          ),
        ),
      );
    }

    return Container(
      padding: const EdgeInsets.all(3),
      decoration: BoxDecoration(color: AppColors.sidebarRaised, borderRadius: BorderRadius.circular(10)),
      child: Row(children: [option(AppLanguage.uk, 'УКР'), option(AppLanguage.en, 'EN')]),
    );
  }
}
