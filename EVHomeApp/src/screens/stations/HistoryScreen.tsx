import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, FlatList, RefreshControl, StyleSheet, Text, View } from 'react-native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { stationsApi } from '../../api/client';
import { useTheme } from '../../context/ThemeContext';
import { useLanguage } from '../../context/LanguageContext';
import Card from '../../components/ui/Card';
import { formatDurationShort, kwh, secondsBetween } from '../../utils/format';
import type { AppColors } from '../../theme';
import type { Session, StationsStackParamList } from '../../types';

type Props = NativeStackScreenProps<StationsStackParamList, 'History'>;

export default function HistoryScreen({ route }: Props) {
  const { stationId } = route.params;
  const { t } = useTranslation();
  const { colors } = useTheme();
  const { language } = useLanguage();
  const styles = useMemo(() => makeStyles(colors), [colors]);

  const [sessions, setSessions] = useState<Session[] | null>(null);
  const [error, setError]       = useState(false);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    try {
      const { data } = await stationsApi.history(stationId, 100);
      setSessions(data);
      setError(false);
    } catch {
      setError(true);
    }
  }, [stationId]);

  useEffect(() => { load(); }, [load]);

  const dateFmt = useMemo(() => new Intl.DateTimeFormat(language === 'uk' ? 'uk-UA' : 'en-GB',
    { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' }), [language]);

  if (sessions === null) {
    return (
      <View style={styles.center}>
        {error ? <Text style={styles.muted}>{t('common.network')}</Text> : <ActivityIndicator color={colors.primary} />}
      </View>
    );
  }

  return (
    <FlatList
      style={styles.container}
      contentContainerStyle={styles.content}
      data={sessions}
      keyExtractor={s => String(s.id)}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={async () => { setRefreshing(true); await load(); setRefreshing(false); }} tintColor={colors.primary} />}
      ListEmptyComponent={<Text style={[styles.muted, styles.empty]}>{t('history.empty')}</Text>}
      renderItem={({ item }) => {
        const cancelled = item.status === 'Cancelled';
        const open      = item.status === 'Pending' || item.status === 'Active' || item.status === 'Stopping' || item.status === 'Paused';
        const duration  = item.startedAt ? secondsBetween(item.startedAt, item.endedAt ? new Date(item.endedAt).getTime() : Date.now()) : 0;
        return (
          <Card style={styles.card}>
            <View style={styles.row}>
              <View style={[styles.iconBg, { backgroundColor: cancelled ? colors.tintRed : colors.tintGreen }]}>
                <Ionicons name={cancelled ? 'close' : 'flash'} size={18} color={cancelled ? colors.error : colors.primary} />
              </View>
              <View style={styles.info}>
                <Text style={styles.date}>{dateFmt.format(new Date(item.startedAt ?? item.createdAt))}</Text>
                <Text style={styles.muted}>
                  {item.initiatedBy === 'App' ? t('history.byApp') : t('history.byCharger')}
                  {open ? ` · ${t('history.inProgress')}` : cancelled ? ` · ${t('history.cancelled')}` : ` · ${formatDurationShort(duration)}`}
                  {item.stopReason === 'PowerLoss' ? ` · ${t('history.powerLoss')}` : ''}
                </Text>
              </View>
              {!cancelled && <Text style={styles.energy}>{kwh(item.energyKwh)} {t('units.kwh')}</Text>}
            </View>
          </Card>
        );
      }}
    />
  );
}

function makeStyles(c: AppColors) {
  return StyleSheet.create({
    container: { flex: 1, backgroundColor: c.bgPrimary },
    content:   { padding: 20, paddingBottom: 40 },
    center:    { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: c.bgPrimary, padding: 28 },
    card:      { marginBottom: 10 },
    row:       { flexDirection: 'row', alignItems: 'center', gap: 12 },
    iconBg:    { width: 36, height: 36, borderRadius: 10, alignItems: 'center', justifyContent: 'center' },
    info:      { flex: 1 },
    date:      { fontSize: 15, fontWeight: '700', color: c.textPrimary, marginBottom: 2 },
    muted:     { fontSize: 13, color: c.textMuted },
    energy:    { fontSize: 16, fontWeight: '800', color: c.textPrimary },
    empty:     { textAlign: 'center', marginTop: 48 },
  });
}
