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
  List<String> datasets = [];
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
        if (datasets.isEmpty) Api.list('/nextclade/datasets'),
      ]);
      runs = results[0];
      alerts = results[1];
      variantRules = results[2];
      motifs = results[3];
      if (results.length > 4) datasets = List<String>.from(results[4]);
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
