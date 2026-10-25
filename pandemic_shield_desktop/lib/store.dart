import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:signalr_netcore/signalr_client.dart';

import 'api.dart';

/// Подія зміни з API (SignalR /hubs/events): тема (runs, alerts, imports, surveillance, rules) і, можливо, Id запуску.
class ChangeEvent {
  final String topic;
  final String? runId;

  const ChangeEvent(this.topic, this.runId);

  /// Стосується цього запуску (або подія без конкретного запуску — тоді стосується всіх).
  bool concernsRun(String? id) => runId == null || runId == id;
}

/// Спільний стан застосунку: запуски, сповіщення, правила.
/// API повідомляє про зміни через SignalR, і дані перечитуються лише тоді, коли щось змінилося.
/// Якщо з'єднання з хабом немає, застосунок повертається до опитування кожні 3 секунди.
class AppStore extends ChangeNotifier {
  List<dynamic> runs = [];
  List<dynamic> alerts = [];
  List<dynamic> variantRules = [];
  List<dynamic> motifs = [];
  List<dynamic> imports = [];
  List<String> datasets = [];
  List<String> organisms = [];
  bool online = false;

  /// Події надходять через SignalR (інакше — опитування).
  bool live = false;

  final _events = StreamController<ChangeEvent>.broadcast();

  /// Сторінки з власними даними (геноми запуску, нагляд) підписуються й перечитують лише своє.
  Stream<ChangeEvent> get events => _events.stream;

  HubConnection? _hub;
  Timer? _timer;
  Timer? _debounce;
  int _tick = 0;

  void start() {
    refresh();
    _connect();
    _timer = Timer.periodic(const Duration(seconds: 3), (_) {
      _tick++;
      // Наживо — лише страховка раз на 30 с; без хаба — опитування кожні 3 с і спроба підключитися кожні 15 с.
      if (!live || _tick % 10 == 0) refresh();
      if (!live && _tick % 5 == 0) _connect();
    });
  }

  Future<void> _connect() async {
    final state = _hub?.state;
    if (state == HubConnectionState.Connected || state == HubConnectionState.Connecting || state == HubConnectionState.Reconnecting) {
      return;
    }
    final hub = HubConnectionBuilder().withUrl(Api.hubUrl).withAutomaticReconnect().build();
    hub.on('changed', (arguments) {
      final data = arguments?.firstOrNull;
      if (data is Map) {
        _events.add(ChangeEvent(data['topic'] as String? ?? '', data['runId'] as String?));
        _scheduleRefresh();
      }
    });
    hub.onclose(({error}) => _setLive(false));
    hub.onreconnecting(({error}) => _setLive(false));
    hub.onreconnected(({connectionId}) {
      _setLive(true);
      refresh();
    });
    _hub = hub;
    try {
      await hub.start();
      _setLive(true);
      // Зміни, що сталися до підключення, підтягуємо одразу.
      refresh();
    } catch (_) {
      _setLive(false);
    }
  }

  void _setLive(bool value) {
    if (live == value) return;
    live = value;
    notifyListeners();
  }

  // Події йдуть пачками (сповіщення ORF по одному): перечитуємо не частіше ніж раз на 300 мс.
  void _scheduleRefresh() {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 300), refresh);
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
    _debounce?.cancel();
    _hub?.stop();
    _events.close();
    super.dispose();
  }
}
