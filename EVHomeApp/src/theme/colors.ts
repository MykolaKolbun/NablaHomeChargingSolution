// Nabla — Design System Colors

export const Colors = {
  // ── Backgrounds ──────────────────────────────────────────
  bgPrimary:   '#0F1117',
  bgCard:      '#161B27',
  bgInput:     '#1A1F2E',
  bgChip:      '#1E2436',

  // ── Brand ─────────────────────────────────────────────────
  primary:     '#2EE89E',
  primaryDark: '#1BC47D',

  // ── Text ──────────────────────────────────────────────────
  textPrimary:   '#FFFFFF',
  textSecondary: '#9CA3AF',
  textMuted:     '#6B7280',
  textOnPrimary: '#0F1117',

  // ── Borders ───────────────────────────────────────────────
  border:        '#2A2D3E',
  borderFocus:   '#2EE89E',

  // ── Status ────────────────────────────────────────────────
  available:  '#2EE89E',
  inUse:      '#F59E0B',
  error:      '#EF4444',
  offline:    '#6B7280',

  // ── Map pins ──────────────────────────────────────────────
  pinAvailable: '#2EE89E',
  pinBusy:      '#F97316',

  // ── Icon background tints ─────────────────────────────────
  tintGreen:  '#162318',
  tintBlue:   '#161B2E',
  tintPurple: '#1C1628',
  tintOrange: '#231A14',
  tintRed:    '#1F1215',

  // ── Stop / danger button ──────────────────────────────────
  stopBg:     '#3D1515',
  stopBorder: '#7F1D1D',
  stopText:   '#FCA5A5',
};

export const LightColors: Partial<typeof Colors> = {
  bgPrimary:     '#F4F6FA',
  bgCard:        '#FFFFFF',
  bgInput:       '#F3F4F6',
  bgChip:        '#E9ECF2',
  textPrimary:   '#111827',
  textSecondary: '#4B5563',
  textMuted:     '#9CA3AF',
  textOnPrimary: '#0F1117',
  border:        '#E5E7EB',
  borderFocus:   '#2EE89E',
  tintGreen:  '#DCFCE7',
  tintBlue:   '#DBEAFE',
  tintPurple: '#EDE9FE',
  tintOrange: '#FFF7ED',
  tintRed:    '#FEF2F2',
  stopBg:     '#FEE2E2',
  stopBorder: '#EF4444',
  stopText:   '#B91C1C',   // red-700: 5.3:1 on stopBg (#DC2626 was 3.95:1)
};

export type AppColors = typeof Colors;
