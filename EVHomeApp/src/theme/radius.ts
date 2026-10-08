/**
 * radius.ts — Nabla border-radius scale
 *
 * Usage:
 *   import { radius } from '../theme';
 *   borderRadius: radius.md   // buttons
 *   borderRadius: radius.lg   // cards
 *   borderRadius: radius.full // pills, avatar circles
 */

export const radius = {
  /** 8  — small badges, tight chips */
  sm:   8,

  /** 12 — inner cards, small interactive elements */
  md:   12,

  /** 14 — buttons */
  btn:  14,

  /** 16 — standard cards, list items */
  lg:   16,

  /** 20 — bottom sheets, large overlays */
  xl:   20,

  /** 24 — summary cards, prominent modals */
  xxl:  24,

  /** 999 — full pill / circle */
  full: 999,
} as const;
