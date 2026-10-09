/**
 * LimitCard — charging current limit (OCPP SetChargingProfile via EVHomeAPI).
 * Preset chips up to the station's maxCurrentA plus "No limit". Owner only.
 * The charger's answer arrives asynchronously (limitStatus Pending → Applied/Rejected/…).
 */

import React, { useMemo, useState } from 'react';
import { ActivityIndicator, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../../context/ThemeContext';
import type { AppColors } from '../../theme';
import type { Station } from '../../types';

const PRESETS = [6, 10, 13, 16, 20, 25, 32];

interface Props {
  station:  Station;
  onChange: (limitA: number | null) => Promise<string | null>;
}

export default function LimitCard({ station, onChange }: Props) {
  const { t } = useTranslation();
  const { colors } = useTheme();
  const styles = useMemo(() => makeStyles(colors), [colors]);
  const [sending, setSending] = useState<number | 'none' | null>(null);
  const [error, setError]     = useState<string | null>(null);

  const options = PRESETS.filter(a => a <= station.maxCurrentA);
  const current = station.currentLimitA;
  const canEdit = station.role === 'Owner' && station.isOnline;
  const pending = station.limitStatus === 'Pending';

  const select = async (limitA: number | null) => {
    if (!canEdit || sending !== null || limitA === current) return;
    setSending(limitA ?? 'none');
    setError(null);
    const err = await onChange(limitA);
    setSending(null);
    if (err !== null) setError(err || t('common.network'));
  };

  const statusLine = (() => {
    switch (station.limitStatus) {
      case 'Pending':      return { text: t('limit.pending'),      color: colors.inUse };
      case 'Applied':      return { text: t('limit.applied'),      color: colors.primary };
      case 'Rejected':     return { text: t('limit.rejected'),     color: colors.error };
      case 'NotSupported': return { text: t('limit.notSupported'), color: colors.error };
      case 'Timeout':
      case 'Error':        return { text: t('limit.failed'),       color: colors.error };
      default:             return null;
    }
  })();

  const Chip = ({ value, label }: { value: number | null; label: string }) => {
    const active = value === current;
    const busy   = sending === (value ?? 'none');
    return (
      <TouchableOpacity
        onPress={() => select(value)}
        disabled={!canEdit || sending !== null}
        style={[styles.chip, active && styles.chipActive, !canEdit && styles.chipDisabled]}
        accessibilityRole="button"
        accessibilityState={{ selected: active }}
      >
        {busy
          ? <ActivityIndicator size="small" color={active ? colors.textOnPrimary : colors.primary} />
          : <Text style={[styles.chipText, active && styles.chipTextActive]}>{label}</Text>}
      </TouchableOpacity>
    );
  };

  return (
    <View style={styles.card}>
      <View style={styles.header}>
        <View style={styles.iconBg}><Ionicons name="options-outline" size={18} color={colors.primary} /></View>
        <View style={styles.headerText}>
          <Text style={styles.title}>{t('limit.title')}</Text>
          <Text style={styles.value}>
            {current == null ? t('limit.none') : `${current} ${t('units.a')}`}
            <Text style={styles.max}>  ·  {t('limit.max', { max: station.maxCurrentA })}</Text>
          </Text>
        </View>
        {pending && <ActivityIndicator size="small" color={colors.inUse} />}
      </View>

      <View style={styles.chips}>
        {options.map(a => <Chip key={a} value={a} label={`${a} ${t('units.a')}`} />)}
        <Chip value={null} label={t('limit.none')} />
      </View>

      {statusLine && <Text style={[styles.status, { color: statusLine.color }]}>{statusLine.text}</Text>}
      {error && <Text style={[styles.status, { color: colors.error }]}>{error}</Text>}
      {station.role !== 'Owner' && <Text style={styles.hint}>{t('limit.ownerOnly')}</Text>}
    </View>
  );
}

function makeStyles(c: AppColors) {
  return StyleSheet.create({
    card:       { width: '100%', backgroundColor: c.bgCard, borderRadius: 16, padding: 16, borderWidth: 1, borderColor: c.border, marginTop: 20 },
    header:     { flexDirection: 'row', alignItems: 'center', gap: 12, marginBottom: 14 },
    iconBg:     { width: 36, height: 36, borderRadius: 10, backgroundColor: c.tintGreen, alignItems: 'center', justifyContent: 'center' },
    headerText: { flex: 1 },
    title:      { fontSize: 12, fontWeight: '600', color: c.textMuted, letterSpacing: 0.5, textTransform: 'uppercase' },
    value:      { fontSize: 18, fontWeight: '800', color: c.textPrimary, marginTop: 2 },
    max:        { fontSize: 13, fontWeight: '400', color: c.textMuted },
    chips:      { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
    chip:       { minWidth: 56, paddingHorizontal: 12, paddingVertical: 9, borderRadius: 999, borderWidth: 1, borderColor: c.border, backgroundColor: c.bgInput, alignItems: 'center' },
    chipActive: { backgroundColor: c.primary, borderColor: c.primary },
    chipDisabled: { opacity: 0.5 },
    chipText:   { fontSize: 14, fontWeight: '700', color: c.textPrimary },
    chipTextActive: { color: c.textOnPrimary },
    status:     { fontSize: 13, marginTop: 12 },
    hint:       { fontSize: 12, color: c.textMuted, marginTop: 8 },
  });
}
