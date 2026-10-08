import React, { useMemo } from 'react';
import { Image, KeyboardAvoidingView, Platform, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useTheme } from '../../context/ThemeContext';
import type { AppColors } from '../../theme';

/** Shared shell for Login/Register: logo, title, error line, scrollable form. */
export default function AuthLayout({ title, subtitle, error, children }: {
  title: string; subtitle: string; error?: string | null; children: React.ReactNode;
}) {
  const { colors } = useTheme();
  const styles = useMemo(() => makeStyles(colors), [colors]);

  return (
    <KeyboardAvoidingView style={styles.container} behavior={Platform.OS === 'ios' ? 'padding' : 'height'}>
      <ScrollView contentContainerStyle={styles.inner} keyboardShouldPersistTaps="handled" showsVerticalScrollIndicator={false}>
        <View style={styles.logoContainer}>
          <Image source={require('../../../assets/nabla-logo.png')} style={styles.logo} resizeMode="contain" />
          <Text style={styles.appName}>Nabla Home</Text>
        </View>
        <Text style={styles.heading}>{title}</Text>
        <Text style={styles.subheading}>{subtitle}</Text>
        {error ? <Text style={styles.error}>{error}</Text> : null}
        {children}
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

function makeStyles(c: AppColors) {
  return StyleSheet.create({
    container:     { flex: 1, backgroundColor: c.bgPrimary },
    inner:         { flexGrow: 1, paddingHorizontal: 28, paddingTop: 60, paddingBottom: 40 },
    logoContainer: { alignItems: 'center', marginBottom: 36 },
    logo:          { width: 90, height: 90 },
    appName:       { fontSize: 26, fontWeight: '800', color: c.primary, marginTop: 10, letterSpacing: 2 },
    heading:       { fontSize: 28, fontWeight: '800', color: c.textPrimary, marginBottom: 6 },
    subheading:    { fontSize: 15, color: c.textSecondary, marginBottom: 16 },
    error:         { fontSize: 14, color: c.error, marginBottom: 4 },
  });
}
