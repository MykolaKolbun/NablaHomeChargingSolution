/**
 * ui/Card.tsx — generic card container atom
 *
 * The standard background + border + border-radius container used
 * throughout the app. Accepts optional padding override and any
 * extra ViewStyle via the `style` prop.
 *
 * Usage
 * ──────
 *  <Card>
 *    <Text>Content</Text>
 *  </Card>
 *
 *  <Card padding={spacing.lg} style={{ marginBottom: spacing.md }}>
 *    <Text>Padded card</Text>
 *  </Card>
 */

import React from 'react';
import { StyleSheet, View, ViewStyle } from 'react-native';
import { useTheme } from '../../context/ThemeContext';
import { radius, spacing } from '../../theme';

// ── Types ──────────────────────────────────────────────────────────────────────

interface Props {
  children: React.ReactNode;
  /** Inner padding — defaults to spacing.md (16) */
  padding?: number;
  /** Extra styles applied to the wrapping View */
  style?:   ViewStyle;
}

// ── Component ──────────────────────────────────────────────────────────────────

export default function Card({ children, padding = spacing.md, style }: Props) {
  const { colors } = useTheme();

  return (
    <View
      style={[
        styles.base,
        {
          backgroundColor: colors.bgCard,
          borderColor:     colors.border,
          padding,
        },
        style,
      ]}
    >
      {children}
    </View>
  );
}

// ── Styles ─────────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  base: {
    borderRadius: radius.lg,
    borderWidth:  1,
  },
});
