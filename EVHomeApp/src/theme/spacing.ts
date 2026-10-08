/**
 * spacing.ts — Nabla spacing scale
 *
 * Use these instead of hardcoded numbers in StyleSheet definitions.
 * Based on a 4-point grid — all values are multiples of 4.
 *
 * Usage:
 *   import { spacing } from '../theme';
 *   paddingHorizontal: spacing.md   // 16
 *   gap: spacing.sm                 // 8
 */

export const spacing = {
  xs:  4,
  sm:  8,
  md:  16,
  lg:  24,
  xl:  32,
  xxl: 48,
} as const;
