import 'dart:convert';

import 'package:http/http.dart' as http;

/// Клієнт REST API PandemicShield (docker-compose відкриває його на порту 5020).
class Api {
  static const String baseUrl = 'http://localhost:5020/api';
  static const Duration timeout = Duration(seconds: 5);

  static Future<dynamic> get(String path, [Map<String, String>? query]) async {
    final uri = Uri.parse('$baseUrl$path').replace(queryParameters: query);
    final response = await http.get(uri).timeout(timeout);
    _ensureSuccess(response);
    return jsonDecode(utf8.decode(response.bodyBytes));
  }

  static Future<List<dynamic>> list(String path, [Map<String, String>? query]) async => (await get(path, query)) as List<dynamic>;

  static Future<dynamic> post(String path, Object body) async {
    final response = await http
        .post(Uri.parse('$baseUrl$path'), headers: {'Content-Type': 'application/json'}, body: jsonEncode(body))
        .timeout(timeout);
    _ensureSuccess(response);
    return response.body.isEmpty ? null : jsonDecode(utf8.decode(response.bodyBytes));
  }

  static Future<void> delete(String path) async {
    final response = await http.delete(Uri.parse('$baseUrl$path')).timeout(timeout);
    _ensureSuccess(response);
  }

  static Future<Map<String, dynamic>> analyze({required String filePath, required int category, String? nextcladeDataset}) async {
    final request = http.MultipartRequest('POST', Uri.parse('$baseUrl/analyze'))
      ..fields['category'] = '$category'
      ..files.add(await http.MultipartFile.fromPath('file', filePath));
    if (nextcladeDataset != null) request.fields['nextcladeDataset'] = nextcladeDataset;

    final response = await http.Response.fromStream(await request.send());
    _ensureSuccess(response);
    return jsonDecode(utf8.decode(response.bodyBytes)) as Map<String, dynamic>;
  }

  static void _ensureSuccess(http.Response response) {
    if (response.statusCode >= 200 && response.statusCode < 300) return;
    String message = utf8.decode(response.bodyBytes);
    // API повертає помилки валідації як JSON-рядок: "Невідомий набір даних…".
    if (message.startsWith('"')) {
      try {
        message = jsonDecode(message) as String;
      } catch (_) {}
    }
    throw ApiException(response.statusCode, message.isEmpty ? 'HTTP ${response.statusCode}' : message);
  }
}

class ApiException implements Exception {
  final int statusCode;
  final String message;

  ApiException(this.statusCode, this.message);

  @override
  String toString() => message;
}
