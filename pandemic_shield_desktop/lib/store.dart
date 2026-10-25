import 'dart:async';

import 'package:flutter/foundation.dart';

import 'api.dart';

/// Спільний стан застосунку: запуски, сповіщення, правила. Опитує API кожні 3 секунди
/// (SignalR — у планах), сторінки підписуються на зміни.
class AppStore extends ChangeNotifier {
  List<dynamic> runs = [];
  List<dynamic> alerts = [];
  List<dynamic> variantRules = [];
  List<dynamic> motifs = [];
  List<dynamic> imports = [];
  List<String> datasets = [];
  List<String> organisms = [];
  bool online = false;

  Timer? _timer;

  void start() {
    refresh();
    _timer = Timer.periodic(const Duration(seconds: 3), (_) => refresh());
  }

  Future<void> refresh() async {
    try {
      final results = await Future.wait([
        Api.list('/runs'),
        Api.list('/threats'),
        Api.list('/variant-markers'),
        Api.list('/dictionary'),
        Api.list('/imports'),
        if (datasets.isEmpty) Api.list('/nextclade/datasets'),
        if (organisms.isEmpty) Api.list('/amr/organisms'),
      ]);
      runs = results[0];
      alerts = results[1];
      variantRules = results[2];
      motifs = results[3];
      imports = results[4];
      int next = 5;
      if (datasets.isEmpty) datasets = List<String>.from(results[next++]);
      if (organisms.isEmpty) organisms = List<String>.from(results[next++]);
      online = true;
    } catch (_) {
      online = false;
    }
    notifyListeners();
  }

  int alertCountForRun(String runId) => alerts.where((a) => a['runId'] == runId).length;

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }
}
