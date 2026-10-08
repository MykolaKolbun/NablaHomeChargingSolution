/**
 * ui/Button.tsx — generic button atom
 *
 * Variants
 * ─────────
 *  primary     — filled green  (main CTAs: Start Charging, Confirm, Save)
 *  secondary   — outlined      (Cancel, Back, secondary actions)
 *  destructive — red/stop      (Stop Charging, Delete)
 *  ghost       — muted fill    (Keep Charging, dismiss)
 *
 * Props
 * ──────
 *  label       — button text
 *  onPress     — tap handler
 *  variant     — style variant (default: 'primary')
 *  disabled    — greys out and blocks interaction
 *  loading     — shows spinner, blocks interaction
 *  fullWidth   — stretches to parent width (default: true)
 *  size        — 'md' (default) or 'lg' (footer CTAs)
 *
 * Usage
 * ──────
 *  <Button label="Start Charging" onPress={handleStart} />
 *  <Button label="Cancel" variant="secondary" onPress={onClose} />
 *  <Button label="Stop" variant="destructive" onPress={confirmStop} size="lg" />
 */

import React from 'react';
import {
  ActivityIndicator, StyleSheet, Text, TouchableOpacity, ViewStyle,
} from 'react-native';
import { useTheme } from '../../context/ThemeContext';
import { radius, typography } from '../../theme';

// ── Types ──────────────────────────────────────────────────────────────────────

export type ButtonVariant = 'primary' | 'secondary' | 'destructive' | 'ghost';
export type ButtonSize    = 'md' | 'lg';

interface Props {
  label:      string;
  onPress:    () => void;
  variant?:   ButtonVariant;
  size?:      ButtonSize;
  disabled?:  boolean;
  loading?:   boolean;
  fullWidth?: boolean;
  style?:     ViewStyle;
}

// ── Component ──────────────────────────────────────────────────────────────────

export default function Button({
  label,
  onPress,
  variant   = 'primary',
  size      = 'md',
  disabled  = false,
  loading   = false,
  fullWidth = true,
  style,
}: Props) {
  const { colors } = useTheme();

  const bg: Record<ButtonVariant, string> = {
    primary:     colors.primary,
    secondary:   'transparent',
    destructive: colors.stopBorder,
    ghost:       colors.bgInput,
  };

  const textColor: Record<ButtonVariant, string> = {
    primary:     colors.textOnPrimary,
    secondary:   colors.textSecondary,
    destructive: colors.stopText,
    ghost:       colors.textPrimary,
  };

  const borderColor: Record<ButtonVariant, string | undefined> = {
    primary:     undefined,
    secondary:   colors.border,
    destructive: undefined,
    ghost:       colors.border,
  };

  const paddingVertical = size === 'lg' ? 16 : 13;
  const textStyle       = size === 'lg' ? typography.btnLg : typography.btn;

  return (
    <TouchableOpacity
      style={[
        styles.base,
        {
          backgroundColor: bg[variant],
          borderColor:     borderColor[variant],
          borderWidth:     borderColor[variant] ? 1 : 0,
          borderRadius:    radius.btn,
          paddingVertical,
          opacity: (disabled || loading) ? 0.45 : 1,
        },
        fullWidth && styles.fullWidth,
        style,
      ]}
      onPress={onPress}
      disabled={disabled || loading}
      activeOpacity={0.85}
    >
      {loading ? (
        <ActivityIndicator color={textColor[variant]} />
      ) : (
        <Text style={[textStyle, { color: textColor[variant] }]}>{label}</Text>
      )}
    </TouchableOpacity>
  );
}

// ── Styles ─────────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  base:      { alignItems: 'center', justifyContent: 'center' },
  fullWidth: { width: '100%' },
});
