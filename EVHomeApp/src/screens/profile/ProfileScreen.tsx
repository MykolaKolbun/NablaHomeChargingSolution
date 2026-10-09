import React, { useMemo, useState } from 'react';
import { ScrollView, StyleSheet, Switch, Text, TouchableOpacity, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import Constants from 'expo-constants';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext';
import { useLanguage } from '../../context/LanguageContext';
import { useTheme } from '../../context/ThemeContext';
import { SUPPORTED_LANGUAGES } from '../../i18n';
import Card from '../../components/ui/Card';
import { useDevMode } from '../../context/DevModeContext';
import ConfirmSheet from '../../components/ui/ConfirmSheet';
import type { AppColors } from '../../theme';

export default function ProfileScreen() {
  const { t } = useTranslation();
  const { colors, isDark, toggleTheme } = useTheme();
  const { language, setLanguage } = useLanguage();
  const { name, email, logout } = useAuth();
  const devMode = useDevMode();
  const [devHint, setDevHint] = useState<string | null>(null);

  const onVersionTap = () => {
    if (!devMode.available) return;
    const left = devMode.registerTap();
    if (left === 0) setDevHint(devMode.enabled ? 'Developer mode off' : 'Developer mode on — 🔧 on the charger screen');
    else if (left <= 3) setDevHint(`${left} more…`);
  };
  const styles = useMemo(() => makeStyles(colors), [colors]);
  const [confirmLogout, setConfirmLogout] = useState(false);

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      <Card style={styles.userCard}>
        <View style={styles.avatar}><Text style={styles.avatarText}>{(name ?? '?').slice(0, 1).toUpperCase()}</Text></View>
        <View>
          <Text style={styles.name}>{name}</Text>
          <Text style={styles.email}>{email}</Text>
        </View>
      </Card>

      <Card style={styles.section}>
        <View style={styles.row}>
          <Ionicons name="moon-outline" size={20} color={colors.textSecondary} />
          <Text style={styles.rowLabel}>{t('profile.darkMode')}</Text>
          <Switch value={isDark} onValueChange={toggleTheme} trackColor={{ true: colors.primary, false: colors.border }} />
        </View>
      </Card>

      <Text style={styles.sectionTitle}>{t('profile.language')}</Text>
      <Card style={styles.section} padding={0}>
        {SUPPORTED_LANGUAGES.map((l, i) => (
          <TouchableOpacity key={l.code} onPress={() => setLanguage(l.code)}
            style={[styles.row, styles.langRow, i > 0 && { borderTopWidth: 1, borderTopColor: colors.border }]}>
            <Text style={styles.flag}>{l.flag}</Text>
            <Text style={styles.rowLabel}>{l.label}</Text>
            {language === l.code && <Ionicons name="checkmark" size={20} color={colors.primary} />}
          </TouchableOpacity>
        ))}
      </Card>

      <TouchableOpacity style={styles.logout} onPress={() => setConfirmLogout(true)}>
        <Ionicons name="log-out-outline" size={20} color={colors.stopText} />
        <Text style={styles.logoutText}>{t('profile.logout')}</Text>
      </TouchableOpacity>

      {/* 7 taps toggle the hidden developer screen (only in builds with extra.devTools) */}
      <TouchableOpacity activeOpacity={1} onPress={onVersionTap}>
        <Text style={styles.version}>
          {t('profile.version', { version: Constants.expoConfig?.version ?? '' })}
          {devMode.enabled ? '  ·  developer' : ''}
        </Text>
        {devHint ? <Text style={styles.devHint}>{devHint}</Text> : null}
      </TouchableOpacity>

      <ConfirmSheet
        visible={confirmLogout}
        title={t('profile.logout')}
        text={t('profile.logoutConfirm')}
        confirmLabel={t('profile.logout')}
        cancelLabel={t('common.cancel')}
        destructive
        onConfirm={() => { setConfirmLogout(false); logout(); }}
        onCancel={() => setConfirmLogout(false)}
      />
    </ScrollView>
  );
}

function makeStyles(c: AppColors) {
  return StyleSheet.create({
    container:    { flex: 1, backgroundColor: c.bgPrimary },
    content:      { padding: 20, paddingBottom: 40 },
    userCard:     { flexDirection: 'row', alignItems: 'center', gap: 14, marginBottom: 20 },
    avatar:       { width: 52, height: 52, borderRadius: 26, backgroundColor: c.tintGreen, alignItems: 'center', justifyContent: 'center' },
    avatarText:   { fontSize: 22, fontWeight: '800', color: c.primary },
    name:         { fontSize: 18, fontWeight: '700', color: c.textPrimary },
    email:        { fontSize: 13, color: c.textMuted, marginTop: 2 },
    section:      { marginBottom: 20 },
    sectionTitle: { fontSize: 11, fontWeight: '600', color: c.textMuted, letterSpacing: 1.5, textTransform: 'uppercase', marginBottom: 8, marginLeft: 4 },
    row:          { flexDirection: 'row', alignItems: 'center', gap: 12 },
    langRow:      { paddingHorizontal: 16, paddingVertical: 14 },
    rowLabel:     { flex: 1, fontSize: 15, color: c.textPrimary },
    flag:         { fontSize: 18 },
    logout:       { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8, backgroundColor: c.stopBg, borderWidth: 1, borderColor: c.stopBorder, borderRadius: 16, paddingVertical: 16, marginTop: 8 },
    logoutText:   { color: c.stopText, fontSize: 16, fontWeight: '700' },
    version:      { textAlign: 'center', fontSize: 12, color: c.textMuted, marginTop: 24 },
    devHint:      { textAlign: 'center', fontSize: 12, color: c.primary, marginTop: 6 },
  });
}
