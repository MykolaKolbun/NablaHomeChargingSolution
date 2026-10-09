import React from 'react';
import { DarkTheme, DefaultTheme, NavigationContainer } from '@react-navigation/native';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { AuthProvider } from './src/context/AuthContext';
import { LanguageProvider } from './src/context/LanguageContext';
import { ThemeProvider, useTheme } from './src/context/ThemeContext';
import { DevModeProvider } from './src/context/DevModeContext';
import AppNavigator from './src/navigation/AppNavigator';
import './src/i18n'; // initialise i18next before any screen renders

function Root() {
  const { isDark, colors } = useTheme();
  const base = isDark ? DarkTheme : DefaultTheme;
  const navTheme = {
    ...base,
    colors: { ...base.colors, primary: colors.primary, background: colors.bgPrimary, card: colors.bgCard, text: colors.textPrimary, border: colors.border },
  };

  return (
    <NavigationContainer theme={navTheme}>
      <StatusBar style={isDark ? 'light' : 'dark'} />
      <AuthProvider>
        <DevModeProvider>
          <AppNavigator />
        </DevModeProvider>
      </AuthProvider>
    </NavigationContainer>
  );
}

export default function App() {
  return (
    <SafeAreaProvider>
      <LanguageProvider>
        <ThemeProvider>
          <Root />
        </ThemeProvider>
      </LanguageProvider>
    </SafeAreaProvider>
  );
}
