import 'package:flutter/material.dart';

// ==========================================
// DESIGN TOKENS: "LAB CONSOLE"
// Темна бічна панель, світла робоча зона, бірюзовий акцент.
// ==========================================
class AppColors {
  static const sidebar = Color(0xFF0B1220);
  static const sidebarRaised = Color(0xFF152033);
  static const sidebarText = Color(0xFF94A3B8);

  static const background = Color(0xFFF3F5F8);
  static const surface = Color(0xFFFFFFFF);
  static const surfaceMuted = Color(0xFFF8FAFC);
  static const border = Color(0xFFE3E8EF);

  static const text = Color(0xFF0F172A);
  static const textMuted = Color(0xFF64748B);
  static const textFaint = Color(0xFF94A3B8);

  static const primary = Color(0xFF0D9488);
  static const primaryBright = Color(0xFF2DD4BF);
  static const nextclade = Color(0xFF6366F1);
  static const danger = Color(0xFFE11D48);
  static const warning = Color(0xFFD97706);
  static const success = Color(0xFF059669);

  /// Кольори для клад: однакова клада завжди має однаковий колір.
  static const cladePalette = [
    Color(0xFF0EA5E9),
    Color(0xFF8B5CF6),
    Color(0xFFF59E0B),
    Color(0xFF10B981),
    Color(0xFFEC4899),
    Color(0xFF6366F1),
    Color(0xFFEF4444),
    Color(0xFF14B8A6),
  ];

  static Color forClade(String? clade) {
    if (clade == null || clade.isEmpty) return textFaint;
    int hash = clade.codeUnits.fold(0, (h, c) => (h * 31 + c) & 0x7fffffff);
    return cladePalette[hash % cladePalette.length];
  }

  static Color forQc(String? status) => switch (status) {
    'good' => success,
    'mediocre' => warning,
    'bad' => danger,
    _ => textFaint,
  };
}

const String sansFont = 'IBM Plex Sans';
const String monoFont = 'IBM Plex Mono';

const TextStyle monoStyle = TextStyle(fontFamily: monoFont, fontSize: 12.5, color: AppColors.text);

const TextStyle labelStyle = TextStyle(fontSize: 11, fontWeight: FontWeight.w600, letterSpacing: 0.9, color: AppColors.textMuted);

ThemeData buildTheme() {
  final scheme = ColorScheme.fromSeed(
    seedColor: AppColors.primary,
    primary: AppColors.primary,
    surface: AppColors.surface,
    error: AppColors.danger,
  );

  OutlineInputBorder border(Color color, [double width = 1.2]) => OutlineInputBorder(
    borderRadius: BorderRadius.circular(10),
    borderSide: BorderSide(color: color, width: width),
  );

  return ThemeData(
    useMaterial3: true,
    colorScheme: scheme,
    fontFamily: sansFont,
    scaffoldBackgroundColor: AppColors.background,
    dividerColor: AppColors.border,
    textTheme: const TextTheme(
      headlineSmall: TextStyle(fontSize: 24, fontWeight: FontWeight.w600, color: AppColors.text, letterSpacing: -0.3),
      titleMedium: TextStyle(fontSize: 15, fontWeight: FontWeight.w600, color: AppColors.text),
      bodyMedium: TextStyle(fontSize: 13.5, color: AppColors.text),
      bodySmall: TextStyle(fontSize: 12, color: AppColors.textMuted),
    ),
    inputDecorationTheme: InputDecorationTheme(
      isDense: true,
      filled: true,
      fillColor: AppColors.surfaceMuted,
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
      labelStyle: const TextStyle(color: AppColors.textMuted),
      hintStyle: const TextStyle(color: AppColors.textFaint),
      enabledBorder: border(AppColors.border),
      focusedBorder: border(AppColors.primary, 1.6),
      errorBorder: border(AppColors.danger),
      focusedErrorBorder: border(AppColors.danger, 1.6),
      border: border(AppColors.border),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: AppColors.primary,
        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 18),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        textStyle: const TextStyle(fontFamily: sansFont, fontWeight: FontWeight.w600, fontSize: 14),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: AppColors.text,
        side: const BorderSide(color: AppColors.border),
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      ),
    ),
    chipTheme: const ChipThemeData(
      side: BorderSide(color: AppColors.border),
      backgroundColor: AppColors.surfaceMuted,
      labelStyle: TextStyle(fontSize: 12, color: AppColors.text),
    ),
    tooltipTheme: TooltipThemeData(
      decoration: BoxDecoration(color: AppColors.sidebar, borderRadius: BorderRadius.circular(8)),
      textStyle: const TextStyle(color: Colors.white, fontSize: 12),
      waitDuration: const Duration(milliseconds: 400),
    ),
    dialogTheme: DialogThemeData(
      backgroundColor: AppColors.surface,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
    ),
    snackBarTheme: SnackBarThemeData(
      behavior: SnackBarBehavior.floating,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
    ),
  );
}
