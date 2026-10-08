/**
 * ThemeContext.tsx — light/dark theme for the whole app
 *
 * Provides a `colors` palette and a `toggleTheme()` function to every screen
 * and component without prop-drilling.
 *
 * How it works:
 *   • The app defaults to dark mode.
 *   • The user's preference is persisted in AsyncStorage under 'nabla_theme'
 *     so it survives app restarts.
 *   • `colors` is a merged AppColors object: in dark mode it's the base Colors
 *     palette; in light mode it's Colors with LightColors overrides merged on top.
 *   • useMemo ensures the colors object is only recreated when isDark changes,
 *     not on every render.
 *
 * Usage in any component:
 *   const { colors, isDark, toggleTheme } = useTheme();
 *   <View style={{ backgroundColor: colors.bgPrimary }} />
 */

import React, { createContext, useContext, useState, useEffect, useMemo } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { Colors, LightColors, AppColors } from '../theme/colors';

// ── Context shape ──────────────────────────────────────────────────────────────
interface ThemeContextValue {
  isDark:      boolean;     // true = dark mode, false = light mode
  colors:      AppColors;   // the full resolved color palette
  toggleTheme: () => void;  // flip between light and dark
}

// Default value used before the Provider mounts (shouldn't be seen in practice)
const ThemeContext = createContext<ThemeContextValue>({
  isDark:      true,
  colors:      Colors,
  toggleTheme: () => {},
});

const STORAGE_KEY = 'nabla_theme';

// ── Provider ───────────────────────────────────────────────────────────────────
export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [isDark, setIsDark] = useState(true); // default: dark mode

  // Restore the saved preference on first mount
  useEffect(() => {
    AsyncStorage.getItem(STORAGE_KEY).then(val => {
      if (val !== null) setIsDark(val === 'dark');
    });
  }, []);

  // Toggle theme and persist the new preference to storage
  const toggleTheme = async () => {
    const next = !isDark;
    setIsDark(next);
    await AsyncStorage.setItem(STORAGE_KEY, next ? 'dark' : 'light');
  };

  // Merge dark base with light overrides — only recomputed when isDark changes
  const colors = useMemo<AppColors>(
    () => isDark ? Colors : { ...Colors, ...LightColors },
    [isDark]
  );

  return (
    <ThemeContext.Provider value={{ isDark, colors, toggleTheme }}>
      {children}
    </ThemeContext.Provider>
  );
}

// ── Hook ───────────────────────────────────────────────────────────────────────
export const useTheme = () => useContext(ThemeContext);
