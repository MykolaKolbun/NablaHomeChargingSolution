/**
 * typography.ts — Nabla text style presets
 *
 * Spread these into StyleSheet text entries instead of repeating
 * fontSize / fontWeight / lineHeight everywhere.
 *
 * Usage:
 *   import { typography } from '../theme';
 *   title: { ...typography.h2, color: colors.textPrimary }
 *   hint:  { ...typography.caption, color: colors.textMuted }
 */

export const typography = {
  /** 36 / 800 — large live timer */
  display: { fontSize: 36, fontWeight: '800' as const, lineHeight: 44, letterSpacing: 2 },

  /** 28 / 800 — page-level headings */
  h1:      { fontSize: 28, fontWeight: '800' as const, lineHeight: 34 },

  /** 22 / 800 — section headings, screen titles */
  h2:      { fontSize: 22, fontWeight: '800' as const, lineHeight: 28 },

  /** 18 / 700 — card headings, modal titles */
  h3:      { fontSize: 18, fontWeight: '700' as const, lineHeight: 24 },

  /** 20 / 800 — stat card values (energy, cost, power) */
  statValue: { fontSize: 20, fontWeight: '800' as const, lineHeight: 26 },

  /** 17 / 800 — footer button text */
  btnLg:   { fontSize: 17, fontWeight: '700' as const, lineHeight: 22 },

  /** 16 / 700 — medium button text */
  btn:     { fontSize: 16, fontWeight: '700' as const, lineHeight: 20 },

  /** 16 / 400 — standard body copy */
  bodyLg:  { fontSize: 16, fontWeight: '400' as const, lineHeight: 24 },

  /** 15 / 400 — secondary body copy */
  body:    { fontSize: 15, fontWeight: '400' as const, lineHeight: 22 },

  /** 14 / 400 — smaller body, descriptions */
  bodyMd:  { fontSize: 14, fontWeight: '400' as const, lineHeight: 20 },

  /** 13 / 600 — station row text, chips */
  small:   { fontSize: 13, fontWeight: '600' as const, lineHeight: 18 },

  /** 12 / 600 — badges, stat labels */
  label:   { fontSize: 12, fontWeight: '600' as const, lineHeight: 16 },

  /** 11 / 400 — captions, sub-labels */
  caption: { fontSize: 11, fontWeight: '400' as const, lineHeight: 15 },

  /** 11 / 600 — uppercase section tags, screen hints */
  tag:     { fontSize: 11, fontWeight: '600' as const, lineHeight: 15, letterSpacing: 1.5 },
} as const;
