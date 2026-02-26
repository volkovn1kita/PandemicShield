import 'dart:async';
import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:file_picker/file_picker.dart';
import 'package:http/http.dart' as http;
import 'package:intl/intl.dart';

void main() {
  runApp(const PandemicShieldApp());
}

// ==========================================
// THEME: "VANILLA CLINICAL / MODERN LAB"
// ==========================================
const Color bgLight = Color(0xFFF9FAFB); 
const Color surfaceLight = Color(0xFFFFFFFF); 
const Color borderLight = Color(0xFFE2E8F0); 
const Color primaryTeal = Color(0xFF0D9488); 
const Color textMain = Color(0xFF1E293B); 
const Color textMuted = Color(0xFF64748B); 
const Color dangerRed = Color(0xFFE11D48); 
const Color warningOrange = Color(0xFFD97706); 
const Color successGreen = Color(0xFF059669); 
const Color accentPurple = Color(0xFF7C3AED); 

class PandemicShieldApp extends StatelessWidget {
  const PandemicShieldApp({Key? key}) : super(key: key);

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Pandemic Shield - Bio Security',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        brightness: Brightness.light,
        scaffoldBackgroundColor: bgLight,
        cardColor: surfaceLight,
        dividerColor: borderLight,
        colorScheme: const ColorScheme.light(
          primary: primaryTeal,
          surface: surfaceLight,
          background: bgLight,
        ),
        fontFamily: 'Segoe UI',
        appBarTheme: const AppBarTheme(
          backgroundColor: surfaceLight,
          elevation: 0,
          shadowColor: borderLight,
          iconTheme: IconThemeData(color: primaryTeal),
        ),
      ),
      home: const MainShell(),
    );
  }
}

// ==========================================
// MAIN SHELL (Sidebar & REAL Health Check)
// ==========================================
class MainShell extends StatefulWidget {
  const MainShell({Key? key}) : super(key: key);

  @override
  State<MainShell> createState() => _MainShellState();
}

class _MainShellState extends State<MainShell> {
  int _selectedIndex = 0;
  final PageController _pageController = PageController();
  
  bool _isOnline = false;
  Timer? _healthTimer;
  final String apiUrl = 'http://localhost:5020/api';

  @override
  void initState() {
    super.initState();
    _checkServerHealth();
    // Пінг сервера кожні 3 секунди
    _healthTimer = Timer.periodic(const Duration(seconds: 3), (timer) => _checkServerHealth());
  }

  @override
  void dispose() {
    _healthTimer?.cancel();
    super.dispose();
  }

  Future<void> _checkServerHealth() async {
    try {
      var response = await http.get(Uri.parse('$apiUrl/dictionary')).timeout(const Duration(seconds: 2));
      if (response.statusCode == 200 && !_isOnline) {
        setState(() => _isOnline = true);
      } else if (response.statusCode != 200 && _isOnline) {
        setState(() => _isOnline = false);
      }
    } catch (e) {
      if (_isOnline) setState(() => _isOnline = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Row(
          children: [
            Icon(Icons.biotech, color: primaryTeal, size: 28),
            SizedBox(width: 12),
            Text('PANDEMIC SHIELD', style: TextStyle(fontSize: 18, letterSpacing: 1.5, fontWeight: FontWeight.bold, color: textMain)),
            Text(' // CLINICAL', style: TextStyle(fontSize: 18, letterSpacing: 1.2, fontWeight: FontWeight.w300, color: textMuted)),
          ],
        ),
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(1.0),
          child: Container(color: borderLight, height: 1.0),
        ),
        actions: [
          Center(
            child: AnimatedContainer(
              duration: const Duration(milliseconds: 300),
              margin: const EdgeInsets.only(right: 24),
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
              decoration: BoxDecoration(
                color: _isOnline ? successGreen.withOpacity(0.1) : dangerRed.withOpacity(0.1),
                borderRadius: BorderRadius.circular(20),
                border: Border.all(color: _isOnline ? successGreen.withOpacity(0.3) : dangerRed.withOpacity(0.3)),
              ),
              child: Row(
                children: [
                  Icon(Icons.circle, color: _isOnline ? successGreen : dangerRed, size: 10),
                  const SizedBox(width: 6),
                  Text(_isOnline ? 'SYSTEM ONLINE' : 'SYSTEM OFFLINE', 
                    style: TextStyle(fontSize: 12, color: _isOnline ? successGreen : dangerRed, fontWeight: FontWeight.bold, letterSpacing: 0.5)),
                ],
              ),
            ),
          )
        ],
      ),
      body: Row(
        children: [
          NavigationRail(
            backgroundColor: bgLight,
            selectedIndex: _selectedIndex,
            indicatorColor: primaryTeal.withOpacity(0.15),
            selectedIconTheme: const IconThemeData(color: primaryTeal, size: 28),
            unselectedIconTheme: const IconThemeData(color: textMuted, size: 24),
            selectedLabelTextStyle: const TextStyle(color: primaryTeal, fontWeight: FontWeight.bold, letterSpacing: 1),
            unselectedLabelTextStyle: const TextStyle(color: textMuted, letterSpacing: 1),
            onDestinationSelected: (int index) {
              setState(() => _selectedIndex = index);
              _pageController.jumpToPage(index);
            },
            labelType: NavigationRailLabelType.all,
            destinations: const [
              NavigationRailDestination(icon: Icon(Icons.dashboard_outlined), selectedIcon: Icon(Icons.dashboard), label: Text('Dashboard')),
              NavigationRailDestination(icon: Icon(Icons.dns_outlined), selectedIcon: Icon(Icons.dns), label: Text('Dictionary')),
            ],
          ),
          const VerticalDivider(thickness: 1, width: 1, color: borderLight),
          Expanded(
            child: PageView(
              controller: _pageController,
              physics: const NeverScrollableScrollPhysics(),
              children: const [
                AnalysisDashboard(),
                DictionaryView(),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

// ==========================================
// SHARED STYLES
// ==========================================
BoxDecoration _cardDecoration() {
  return BoxDecoration(
    color: surfaceLight,
    borderRadius: BorderRadius.circular(20), // ЗБІЛЬШЕНО РАДІУС
    boxShadow: [
      BoxShadow(color: Colors.black.withOpacity(0.02), blurRadius: 15, offset: const Offset(0, 5)),
    ],
    border: Border.all(color: borderLight),
  );
}

InputDecoration _customInputDecoration(String label) {
  return InputDecoration(
    labelText: label,
    labelStyle: const TextStyle(color: textMuted),
    isDense: true,
    enabledBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(12), // М'які кути
      borderSide: const BorderSide(color: borderLight, width: 1.5), // М'який сірий колір
    ),
    focusedBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(12),
      borderSide: const BorderSide(color: primaryTeal, width: 2), // Бірюзовий при кліку
    ),
    border: OutlineInputBorder(
      borderRadius: BorderRadius.circular(12),
    ),
  );
}

// ==========================================
// VIEW 1: ANALYSIS DASHBOARD
// ==========================================
class AnalysisDashboard extends StatefulWidget {
  const AnalysisDashboard({Key? key}) : super(key: key);

  @override
  State<AnalysisDashboard> createState() => _AnalysisDashboardState();
}

class _AnalysisDashboardState extends State<AnalysisDashboard> {
  PlatformFile? _selectedFile;
  int _selectedCategory = 0;
  bool _isUploading = false;
  bool _isLoadingThreats = false;
  List<dynamic> _threats = [];
  int _dictionaryCount = 0;
  
  Timer? _pollingTimer;
  final String apiUrl = 'http://localhost:5020/api';

  @override
  void initState() {
    super.initState();
    _fetchThreats();
    _fetchDictionaryStats();
    
    _pollingTimer = Timer.periodic(const Duration(seconds: 3), (timer) {
      _fetchThreats(silent: true);
      _fetchDictionaryStats();
    });
  }

  @override
  void dispose() {
    _pollingTimer?.cancel();
    super.dispose();
  }

  Future<void> _fetchDictionaryStats() async {
    try {
      var response = await http.get(Uri.parse('$apiUrl/dictionary'));
      if (response.statusCode == 200) {
        setState(() => _dictionaryCount = jsonDecode(response.body).length);
      }
    } catch (_) {}
  }

  Future<void> _fetchThreats({bool silent = false}) async {
    if (!silent) setState(() => _isLoadingThreats = true);
    try {
      var response = await http.get(Uri.parse('$apiUrl/threats'));
      if (response.statusCode == 200) {
        setState(() => _threats = jsonDecode(response.body));
      }
    } catch (_) {
    } finally {
      if (!silent) setState(() => _isLoadingThreats = false);
    }
  }

  Future<void> _pickFile() async {
    FilePickerResult? result = await FilePicker.platform.pickFiles(type: FileType.custom, allowedExtensions: ['fasta']);
    if (result != null) setState(() => _selectedFile = result.files.first);
  }

  Future<void> _uploadDna() async {
    if (_selectedFile == null || _selectedFile!.path == null) return;
    setState(() => _isUploading = true);

    try {
      var request = http.MultipartRequest('POST', Uri.parse('$apiUrl/analyze'));
      request.fields['category'] = _selectedCategory.toString();
      request.files.add(await http.MultipartFile.fromPath('file', _selectedFile!.path!));
      var response = await request.send();

      if (response.statusCode == 200) {
        _showSnackBar('Sequence stream sent to processing queue.', successGreen);
        setState(() => _selectedFile = null);
      } else {
        _showSnackBar('API Error: ${response.statusCode}', dangerRed);
      }
    } catch (e) {
      _showSnackBar('Connection failed. Is the API running?', dangerRed);
    } finally {
      setState(() => _isUploading = false);
    }
  }

  Future<void> _deleteThreat(String id) async {
    try {
      var response = await http.delete(Uri.parse('$apiUrl/threats/$id'));
      if (response.statusCode == 200) _fetchThreats(silent: true);
    } catch (_) {}
  }

  void _showSnackBar(String msg, Color color) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(msg, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w500)), 
      backgroundColor: color,
      behavior: SnackBarBehavior.floating,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
    ));
  }

  @override
  Widget build(BuildContext context) {
    int viralThreats = _threats.where((t) => t['category'] == 0).length;
    int humanThreats = _threats.where((t) => t['category'] == 1).length;

    return Padding(
      padding: const EdgeInsets.all(32.0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              _buildMetricCard('Total Anomalies', _threats.length.toString(), Icons.warning_amber_rounded, dangerRed),
              const SizedBox(width: 24),
              _buildMetricCard('Viral Signatures', viralThreats.toString(), Icons.coronavirus, warningOrange),
              const SizedBox(width: 24),
              _buildMetricCard('Human Markers', humanThreats.toString(), Icons.person, accentPurple),
              const SizedBox(width: 24),
              _buildMetricCard('Monitored Patterns', _dictionaryCount.toString(), Icons.library_books, primaryTeal),
            ],
          ),
          const SizedBox(height: 32),
          Expanded(
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  flex: 1,
                  child: Container(
                    padding: const EdgeInsets.all(24),
                    decoration: _cardDecoration(),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text('DATA INGESTION', style: TextStyle(color: primaryTeal, fontWeight: FontWeight.bold, letterSpacing: 1.2)),
                        const SizedBox(height: 24),
                        const Text('ANALYSIS TARGET', style: TextStyle(fontSize: 11, color: textMuted, letterSpacing: 1, fontWeight: FontWeight.bold)),
                        const SizedBox(height: 8),
                        SegmentedButton<int>(
                          segments: const [
                            ButtonSegment(value: 0, label: Text('Virus Pathogen')),
                            ButtonSegment(value: 1, label: Text('Human Genetics')),
                          ],
                          selected: {_selectedCategory},
                          onSelectionChanged: (set) => setState(() => _selectedCategory = set.first),
                          style: SegmentedButton.styleFrom(
                            backgroundColor: surfaceLight,
                            selectedBackgroundColor: primaryTeal.withOpacity(0.1),
                            selectedForegroundColor: primaryTeal,
                            foregroundColor: textMuted,
                            side: const BorderSide(color: borderLight),
                          ),
                        ),
                        const SizedBox(height: 32),
                        const Text('SEQUENCE FILE (.fasta)', style: TextStyle(fontSize: 11, color: textMuted, letterSpacing: 1, fontWeight: FontWeight.bold)),
                        const SizedBox(height: 8),
                        InkWell(
                          onTap: _pickFile,
                          child: Container(
                            width: double.infinity,
                            padding: const EdgeInsets.symmetric(vertical: 40),
                            decoration: BoxDecoration(
                              color: bgLight,
                              border: Border.all(color: _selectedFile == null ? borderLight : primaryTeal, width: 2),
                              borderRadius: BorderRadius.circular(16),
                            ),
                            child: Column(
                              children: [
                                Icon(_selectedFile == null ? Icons.cloud_upload_outlined : Icons.check_circle, 
                                     color: _selectedFile == null ? textMuted : primaryTeal, size: 48),
                                const SizedBox(height: 16),
                                Text(_selectedFile == null ? 'Drop sequence file here' : _selectedFile!.name, 
                                     style: TextStyle(color: _selectedFile == null ? textMuted : textMain, fontWeight: FontWeight.bold)),
                              ],
                            ),
                          ),
                        ),
                        const Spacer(),
                        SizedBox(
                          width: double.infinity,
                          height: 54,
                          child: ElevatedButton(
                            onPressed: _isUploading || _selectedFile == null ? null : _uploadDna,
                            style: ElevatedButton.styleFrom(
                              backgroundColor: primaryTeal,
                              foregroundColor: Colors.white,
                              elevation: 0,
                              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                            ),
                            child: _isUploading 
                                ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(color: Colors.white, strokeWidth: 3))
                                : const Text('START DISTRIBUTED ANALYSIS', style: TextStyle(fontWeight: FontWeight.bold, letterSpacing: 1)),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(width: 24),
                Expanded(
                  flex: 2,
                  child: Container(
                    padding: const EdgeInsets.all(24),
                    decoration: _cardDecoration(),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            const Text('REAL-TIME THREAT LOGS', style: TextStyle(color: textMain, fontWeight: FontWeight.w900, letterSpacing: 1.2)),
                            Row(
                              children: [
                                const SizedBox(width: 12, height: 12, child: CircularProgressIndicator(strokeWidth: 2, color: primaryTeal)),
                                const SizedBox(width: 8),
                                const Text('LIVE SYNC', style: TextStyle(color: primaryTeal, fontSize: 12, fontWeight: FontWeight.bold, letterSpacing: 1)),
                              ],
                            )
                          ],
                        ),
                        const SizedBox(height: 16),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                          decoration: BoxDecoration(color: bgLight, borderRadius: BorderRadius.circular(12), border: Border.all(color: borderLight)),
                          child: const Row(
                            children: [
                              Expanded(flex: 1, child: Text('TYPE', style: TextStyle(color: textMuted, fontSize: 11, fontWeight: FontWeight.bold, letterSpacing: 1))),
                              Expanded(flex: 3, child: Text('THREAT IDENTIFIER', style: TextStyle(color: textMuted, fontSize: 11, fontWeight: FontWeight.bold, letterSpacing: 1))),
                              Expanded(flex: 2, child: Text('GLOBAL OFFSET', style: TextStyle(color: textMuted, fontSize: 11, fontWeight: FontWeight.bold, letterSpacing: 1))),
                              Expanded(flex: 2, child: Text('TIME', style: TextStyle(color: textMuted, fontSize: 11, fontWeight: FontWeight.bold, letterSpacing: 1))),
                              SizedBox(width: 40), 
                            ],
                          ),
                        ),
                        const SizedBox(height: 8),
                        Expanded(
                          child: _threats.isEmpty
                            ? Center(
                                child: Column(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  children: [
                                    Icon(Icons.verified_user_outlined, size: 64, color: borderLight),
                                    const SizedBox(height: 16),
                                    const Text('No anomalies detected. System clear.', style: TextStyle(color: textMuted, fontSize: 16)),
                                  ],
                                ),
                              )
                            : ListView.builder(
                                itemCount: _threats.length,
                                itemBuilder: (context, index) {
                                  var threat = _threats[index];
                                  bool isVirus = threat['category'] == 0;
                                  String time = DateFormat('HH:mm:ss.SSS').format(DateTime.parse(threat['detectedAt']).toLocal());

                                  return Container(
                                    margin: const EdgeInsets.only(bottom: 8),
                                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                                    decoration: BoxDecoration(
                                      color: surfaceLight,
                                      borderRadius: BorderRadius.circular(12),
                                      border: Border.all(color: borderLight),
                                    ),
                                    child: Row(
                                      children: [
                                        Expanded(
                                          flex: 1, 
                                          child: Align(
                                            alignment: Alignment.centerLeft,
                                            child: Container(
                                              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                                              decoration: BoxDecoration(
                                                color: isVirus ? dangerRed.withOpacity(0.1) : accentPurple.withOpacity(0.1),
                                                borderRadius: BorderRadius.circular(6),
                                              ),
                                              child: Text(isVirus ? 'VIRAL' : 'HUMAN', style: TextStyle(color: isVirus ? dangerRed : accentPurple, fontSize: 10, fontWeight: FontWeight.bold)),
                                            ),
                                          )
                                        ),
                                        Expanded(flex: 3, child: Text(threat['threatName'], style: const TextStyle(fontWeight: FontWeight.w600, color: textMain))),
                                        Expanded(flex: 2, child: Text(threat['globalPosition'].toString(), style: const TextStyle(fontFamily: 'Consolas', color: primaryTeal, fontWeight: FontWeight.w600))),
                                        Expanded(flex: 2, child: Text(time, style: const TextStyle(fontFamily: 'Consolas', color: textMuted, fontSize: 12))),
                                        SizedBox(
                                          width: 40, 
                                          child: IconButton(
                                            icon: const Icon(Icons.delete_outline, color: textMuted, size: 20),
                                            hoverColor: dangerRed.withOpacity(0.1),
                                            onPressed: () => _deleteThreat(threat['id']),
                                            tooltip: 'Purge log',
                                          )
                                        ),
                                      ],
                                    ),
                                  );
                                },
                              ),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildMetricCard(String title, String value, IconData icon, Color color) {
    return Expanded(
      child: Container(
        padding: const EdgeInsets.all(20),
        decoration: _cardDecoration(),
        child: Row(
          children: [
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(color: color.withOpacity(0.1), shape: BoxShape.circle),
              child: Icon(icon, color: color, size: 28),
            ),
            const SizedBox(width: 16),
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: const TextStyle(color: textMuted, fontSize: 12, fontWeight: FontWeight.bold, letterSpacing: 0.5)),
                const SizedBox(height: 4),
                Text(value, style: const TextStyle(color: textMain, fontSize: 24, fontWeight: FontWeight.w900)),
              ],
            )
          ],
        ),
      ),
    );
  }
}

// ==========================================
// VIEW 2: DICTIONARY MANAGEMENT
// ==========================================
class DictionaryView extends StatefulWidget {
  const DictionaryView({Key? key}) : super(key: key);

  @override
  State<DictionaryView> createState() => _DictionaryViewState();
}

class _DictionaryViewState extends State<DictionaryView> {
  final String apiUrl = 'http://localhost:5020/api';
  List<dynamic> _dictionary = [];
  bool _isLoading = false;

  // 1. Додаємо ключ для управління формою та валідацією
  final _formKey = GlobalKey<FormState>(); 
  final TextEditingController _nameCtrl = TextEditingController();
  final TextEditingController _seqCtrl = TextEditingController();
  int _addCategory = 0;

  @override
  void initState() {
    super.initState();
    _fetchDictionary();
  }

  Future<void> _fetchDictionary() async {
    setState(() => _isLoading = true);
    try {
      var response = await http.get(Uri.parse('$apiUrl/dictionary'));
      if (response.statusCode == 200) setState(() => _dictionary = jsonDecode(response.body));
    } catch (_) {} 
    finally { setState(() => _isLoading = false); }
  }

  Future<void> _addMutation() async {
    // 2. Перевіряємо, чи всі поля пройшли валідацію
    if (!_formKey.currentState!.validate()) {
      return; // Якщо є помилки, зупиняємо відправку
    }
    
    // .trim() видаляє випадкові пробіли на початку чи в кінці
    var body = jsonEncode({
      "name": _nameCtrl.text.trim(),
      "sequence": _seqCtrl.text.trim().toUpperCase(),
      "category": _addCategory
    });

    try {
      var response = await http.post(Uri.parse('$apiUrl/dictionary'), headers: {"Content-Type": "application/json"}, body: body);
      if (response.statusCode == 200) {
        _nameCtrl.clear();
        _seqCtrl.clear();
        _fetchDictionary();
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
          content: Text('Маркер успішно додано!', style: TextStyle(color: Colors.white)), 
          backgroundColor: successGreen,
          behavior: SnackBarBehavior.floating,
        ));
      }
    } catch (_) {}
  }

  Future<void> _deleteMutation(String id) async {
    try {
      var response = await http.delete(Uri.parse('$apiUrl/dictionary/$id'));
      if (response.statusCode == 200) _fetchDictionary();
    } catch (_) {}
  }

  BoxDecoration _cardDecoration() {
    return BoxDecoration(
      color: surfaceLight,
      borderRadius: BorderRadius.circular(16),
      boxShadow: [
        BoxShadow(color: Colors.black.withOpacity(0.03), blurRadius: 10, offset: const Offset(0, 4)),
      ],
      border: Border.all(color: borderLight),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(32.0),
      child: Column(
        children: [
          // ADD FORM
          Container(
            padding: const EdgeInsets.all(24),
            decoration: _cardDecoration(),
            child: Form( // 3. Огортаємо форму у віджет Form
              key: _formKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('NEW PATTERN ENTRY', style: TextStyle(color: textMain, fontWeight: FontWeight.w900, letterSpacing: 1.2)),
                  const SizedBox(height: 16),
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start, // Щоб текст помилки не ламав верстку
                    children: [
                      Expanded(
                        flex: 2, 
                        child: TextFormField( // Змінили TextField на TextFormField
                          controller: _nameCtrl, 
                          style: const TextStyle(color: textMain), 
                          decoration: _customInputDecoration('Threat Name'),
                          validator: (value) {
                            if (value == null || value.trim().isEmpty) {
                              return 'Назва не може бути порожньою';
                            }
                            if (value.trim().length < 3) {
                              return 'Мінімум 3 символи';
                            }
                            return null;
                          },
                        )
                      ),
                      const SizedBox(width: 16),
                      Expanded(
                        flex: 2, 
                        child: TextFormField(
                          controller: _seqCtrl, 
                          style: const TextStyle(color: textMain, fontFamily: 'Consolas'), // Моноширинний шрифт для ДНК
                          textCapitalization: TextCapitalization.characters, // Автоматично робить великі літери
                          decoration: _customInputDecoration('Amino Acid Marker (e.g. MVHLTPVEKS*)'),
                          validator: (value) {
                            if (value == null || value.trim().isEmpty) {
                              return 'Маркер не може бути порожнім';
                            }
                            // 4. Найголовніше: Регулярний вираз для валідних амінокислот
                            // Дозволяє тільки літери ACDEFGHIKLMNPQRSTVWY та зірочку *
                            if (!RegExp(r'^[ACDEFGHIKLMNPQRSTVWY\*]+$').hasMatch(value.toUpperCase().trim())) {
                              return 'Містить невалідні символи (тільки амінокислоти)';
                            }
                            return null;
                          },
                        )
                      ),
                      const SizedBox(width: 16),
                      Expanded(
                        flex: 1, 
                        child: DropdownButtonFormField<int>(
                          value: _addCategory,
                          dropdownColor: surfaceLight,
                          style: const TextStyle(color: textMain),
                          decoration: _customInputDecoration('Class'),
                          items: const [DropdownMenuItem(value: 0, child: Text('Viral')), DropdownMenuItem(value: 1, child: Text('Human'))],
                          onChanged: (val) => setState(() => _addCategory = val!),
                        )
                      ),
                      const SizedBox(width: 16),
                      SizedBox(
                        height: 48, 
                        child: ElevatedButton.icon(
                          onPressed: _addMutation, 
                          icon: const Icon(Icons.add), 
                          label: const Text('SAVE PATTERN', style: TextStyle(fontWeight: FontWeight.bold)), 
                          style: ElevatedButton.styleFrom(backgroundColor: primaryTeal, foregroundColor: Colors.white, elevation: 0, shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)))
                        )
                      )
                    ],
                  )
                ],
              ),
            ),
          ),
          const SizedBox(height: 24),
          // CUSTOM LIST
          Expanded(
            child: Container(
              padding: const EdgeInsets.all(24),
              decoration: _cardDecoration(),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      const Text('ACTIVE DATABASE SIGNATURES', style: TextStyle(color: textMain, fontWeight: FontWeight.w900, letterSpacing: 1.2)),
                      IconButton(onPressed: _fetchDictionary, icon: const Icon(Icons.refresh, size: 20, color: textMuted))
                    ],
                  ),
                  const SizedBox(height: 16),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                    decoration: BoxDecoration(color: bgLight, borderRadius: BorderRadius.circular(12), border: Border.all(color: borderLight)),
                    child: const Row(
                      children: [
                        Expanded(flex: 1, child: Text('CLASS', style: TextStyle(color: textMuted, fontSize: 11, fontWeight: FontWeight.bold, letterSpacing: 1))),
                        Expanded(flex: 3, child: Text('IDENTIFIER', style: TextStyle(color: textMuted, fontSize: 11, fontWeight: FontWeight.bold, letterSpacing: 1))),
                        Expanded(flex: 3, child: Text('SEQUENCE MARKER', style: TextStyle(color: textMuted, fontSize: 11, fontWeight: FontWeight.bold, letterSpacing: 1))),
                        SizedBox(width: 40),
                      ],
                    ),
                  ),
                  const SizedBox(height: 8),
                  Expanded(
                    child: _isLoading 
                      ? const Center(child: CircularProgressIndicator(color: primaryTeal))
                      : ListView.builder(
                          itemCount: _dictionary.length,
                          itemBuilder: (context, index) {
                            var item = _dictionary[index];
                            bool isVirus = item['category'] == 0;
                            return Container(
                              margin: const EdgeInsets.only(bottom: 8),
                              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                              decoration: BoxDecoration(color: surfaceLight, borderRadius: BorderRadius.circular(12), border: Border.all(color: borderLight)),
                              child: Row(
                                children: [
                                  Expanded(
                                    flex: 1, 
                                    child: Align(
                                      alignment: Alignment.centerLeft, 
                                      child: Container(
                                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                                        decoration: BoxDecoration(color: isVirus ? warningOrange.withOpacity(0.1) : accentPurple.withOpacity(0.1), borderRadius: BorderRadius.circular(6)),
                                        child: Text(isVirus ? 'VIRAL' : 'HUMAN', style: TextStyle(color: isVirus ? warningOrange : accentPurple, fontSize: 10, fontWeight: FontWeight.bold))
                                      )
                                    )
                                  ),
                                  Expanded(flex: 3, child: Text(item['name'], style: const TextStyle(fontWeight: FontWeight.w600, color: textMain))),
                                  Expanded(flex: 3, child: SelectableText(item['sequence'], style: const TextStyle(fontFamily: 'Consolas', color: primaryTeal, fontWeight: FontWeight.w600))),
                                  SizedBox(width: 40, child: IconButton(icon: const Icon(Icons.delete_outline, color: textMuted, size: 20), hoverColor: dangerRed.withOpacity(0.1), onPressed: () => _deleteMutation(item['id']))),
                                ],
                              ),
                            );
                          },
                        ),
                  )
                ],
              ),
            ),
          )
        ],
      ),
    );
  }
}