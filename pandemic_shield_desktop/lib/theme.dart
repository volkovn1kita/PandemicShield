import 'package:flutter/material.dart';

// ==========================================
// DESIGN TOKENS (див. docs/DESIGN.md)
// Монохромна основа; колір лише для клад, сповіщень і статусів.
// ==========================================
const String sansFont = 'IBM Plex Sans';
const String monoFont = 'IBM Plex Mono';

@immutable
class Palette extends ThemeExtension<Palette> {
  final Color bg;
  final Color sidebar;
  final Color surface;
  final Color border;
  final Color text;
  final Color muted;
  final Color faint;
  final Color hover;
  final Color selected;
  final Color ink;
  final Color onInk;
  final Color danger;
  final Color warning;
  final Color success;

  /// Графіки нагляду: «тривожна» частина (карбапенемази) і решта. Пару перевірено на розрізнення
  /// при дальтонізмі (ΔE ≥ 14); сірий — фон, а не категорія, тож поруч завжди є легенда, підписи й таблиця.
  final Color chartAlert;
  final Color chartRest;
  final Brightness brightness;

  const Palette({
    required this.bg,
    required this.sidebar,
    required this.surface,
    required this.border,
    required this.text,
    required this.muted,
    required this.faint,
    required this.hover,
    required this.selected,
    required this.ink,
    required this.onInk,
    required this.danger,
    required this.warning,
    required this.success,
    required this.chartAlert,
    required this.chartRest,
    required this.brightness,
  });

  static const light = Palette(
    bg: Color(0xFFFBFBFA),
    sidebar: Color(0xFFF4F4F2),
    surface: Color(0xFFFFFFFF),
    border: Color(0xFFE7E6E3),
    text: Color(0xFF1C1B19),
    muted: Color(0xFF6F6D68),
    faint: Color(0xFFA3A19C),
    hover: Color(0xFFF1F0EE),
    selected: Color(0xFFEAE9E6),
    ink: Color(0xFF1C1B19),
    onInk: Color(0xFFFFFFFF),
    danger: Color(0xFFD93636),
    warning: Color(0xFFC27C0E),
    success: Color(0xFF2F8A4C),
    chartAlert: Color(0xFFD93636),
    chartRest: Color(0xFFC9C7C2),
    brightness: Brightness.light,
  );

  static const dark = Palette(
    bg: Color(0xFF141414),
    sidebar: Color(0xFF1A1A1A),
    surface: Color(0xFF1C1C1C),
    border: Color(0xFF2C2C2C),
    text: Color(0xFFEDEDEC),
    muted: Color(0xFFA1A09C),
    faint: Color(0xFF6B6A67),
    hover: Color(0xFF232323),
    selected: Color(0xFF2A2A2A),
    ink: Color(0xFFEDEDEC),
    onInk: Color(0xFF141414),
    danger: Color(0xFFF06A6A),
    warning: Color(0xFFE0A23B),
    success: Color(0xFF5BB57A),
    chartAlert: Color(0xFFE35D5D),
    chartRest: Color(0xFF4A4A48),
    brightness: Brightness.dark,
  );

  Color forQc(String? status) => switch (status) {
    'good' => success,
    'mediocre' => warning,
    'bad' => danger,
    _ => faint,
  };

  @override
  Palette copyWith() => this;

  @override
  Palette lerp(ThemeExtension<Palette>? other, double t) => t < 0.5 ? this : (other as Palette? ?? this);
}

extension PaletteContext on BuildContext {
  Palette get p => Theme.of(this).extension<Palette>()!;
}

/// Колір клади за хронологією, як у Nextstrain: найстаріші (19A) — сині, найновіші — червоні.
/// Коди клад мають вигляд «рік + літера» (21J, 24F); для інших (recombinant, підклади грипу) — сірий.
Color cladeColor(String? clade, Palette p) {
  final match = RegExp(r'^(\d{2})([A-Z])').firstMatch(clade ?? '');
  if (match == null) return p.faint;
  final t = ((int.parse(match[1]!) - 19) + (match[2]!.codeUnitAt(0) - 65) / 26) / 6.5;
  return _cladeRamp(t.clamp(0.0, 1.0));
}

const _cladeStops = [
  Color(0xFF4C6FD1), // 2019
  Color(0xFF3FA0C9),
  Color(0xFF4DB38A),
  Color(0xFFA2B847),
  Color(0xFFE0AE36),
  Color(0xFFE57F3A),
  Color(0xFFD64A3E), // 2025
];

Color _cladeRamp(double t) {
  final scaled = t * (_cladeStops.length - 1);
  final i = scaled.floor().clamp(0, _cladeStops.length - 2);
  return Color.lerp(_cladeStops[i], _cladeStops[i + 1], scaled - i)!;
}

TextStyle mono(BuildContext context, {double size = 12.5, Color? color, FontWeight weight = FontWeight.w400}) =>
    TextStyle(fontFamily: monoFont, fontSize: size, color: color ?? context.p.text, fontWeight: weight, height: 1.3);

ThemeData buildTheme(Palette p) {
  final base = ThemeData(
    useMaterial3: true,
    brightness: p.brightness,
    fontFamily: sansFont,
    colorScheme: ColorScheme.fromSeed(
      seedColor: p.ink,
      brightness: p.brightness,
      primary: p.ink,
      onPrimary: p.onInk,
      surface: p.surface,
      error: p.danger,
    ),
  );

  OutlineInputBorder border(Color color, [double width = 1]) => OutlineInputBorder(
    borderRadius: BorderRadius.circular(6),
    borderSide: BorderSide(color: color, width: width),
  );

  final buttonShape = RoundedRectangleBorder(borderRadius: BorderRadius.circular(6));
  const buttonText = TextStyle(fontFamily: sansFont, fontSize: 13, fontWeight: FontWeight.w500);
  const buttonPadding = EdgeInsets.symmetric(horizontal: 12, vertical: 10);

  return base.copyWith(
    extensions: [p],
    scaffoldBackgroundColor: p.bg,
    dividerColor: p.border,
    dividerTheme: DividerThemeData(color: p.border, thickness: 1, space: 1),
    splashFactory: NoSplash.splashFactory,
    hoverColor: p.hover,
    highlightColor: Colors.transparent,
    textTheme: base.textTheme.apply(fontFamily: sansFont, bodyColor: p.text, displayColor: p.text),
    textSelectionTheme: TextSelectionThemeData(cursorColor: p.text, selectionColor: p.selected),
    inputDecorationTheme: InputDecorationTheme(
      isDense: true,
      filled: true,
      fillColor: p.surface,
      contentPadding: const EdgeInsets.symmetric(horizontal: 10, vertical: 11),
      labelStyle: TextStyle(color: p.muted, fontSize: 13),
      floatingLabelStyle: TextStyle(color: p.muted, fontSize: 13),
      hintStyle: TextStyle(color: p.faint, fontSize: 13),
      enabledBorder: border(p.border),
      focusedBorder: border(p.ink, 1.2),
      errorBorder: border(p.danger),
      focusedErrorBorder: border(p.danger, 1.2),
      border: border(p.border),
      errorStyle: TextStyle(color: p.danger, fontSize: 12),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: p.ink,
        foregroundColor: p.onInk,
        disabledBackgroundColor: p.selected,
        disabledForegroundColor: p.faint,
        padding: buttonPadding,
        minimumSize: const Size(0, 34),
        shape: buttonShape,
        textStyle: buttonText,
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: p.text,
        side: BorderSide(color: p.border),
        padding: buttonPadding,
        minimumSize: const Size(0, 34),
        shape: buttonShape,
        textStyle: buttonText,
      ),
    ),
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(foregroundColor: p.muted, shape: buttonShape, textStyle: buttonText, padding: buttonPadding),
    ),
    iconButtonTheme: IconButtonThemeData(
      style: IconButton.styleFrom(
        foregroundColor: p.muted,
        shape: buttonShape,
        minimumSize: const Size(30, 30),
        padding: const EdgeInsets.all(6),
      ),
    ),
    dropdownMenuTheme: DropdownMenuThemeData(textStyle: TextStyle(fontSize: 13, color: p.text)),
    popupMenuTheme: PopupMenuThemeData(
      color: p.surface,
      surfaceTintColor: Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(8),
        side: BorderSide(color: p.border),
      ),
      textStyle: TextStyle(fontSize: 13, color: p.text),
    ),
    tooltipTheme: TooltipThemeData(
      decoration: BoxDecoration(color: p.ink, borderRadius: BorderRadius.circular(6)),
      textStyle: TextStyle(color: p.onInk, fontSize: 12, fontFamily: sansFont),
      waitDuration: const Duration(milliseconds: 350),
    ),
    dialogTheme: DialogThemeData(
      backgroundColor: p.surface,
      surfaceTintColor: Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(10),
        side: BorderSide(color: p.border),
      ),
    ),
    snackBarTheme: SnackBarThemeData(
      behavior: SnackBarBehavior.floating,
      backgroundColor: p.ink,
      contentTextStyle: TextStyle(color: p.onInk, fontFamily: sansFont, fontSize: 13),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(6)),
      width: 480,
    ),
    progressIndicatorTheme: ProgressIndicatorThemeData(color: p.muted),
    scrollbarTheme: ScrollbarThemeData(
      thumbColor: WidgetStatePropertyAll(p.border),
      radius: const Radius.circular(4),
      thickness: const WidgetStatePropertyAll(6),
    ),
  );
}
