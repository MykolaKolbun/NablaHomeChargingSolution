/**
 * StationsScreen — the user's chargers. With exactly one charger, opens it
 * straight away (the common home case); the list stays reachable via Back.
 */

import React, { useCallback, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, FlatList, RefreshControl, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import { useFocusEffect } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { stationsApi } from '../../api/client';
import { useTheme } from '../../context/ThemeContext';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import StatusBadge from '../../components/domain/StatusBadge';
import type { AppColors } from '../../theme';
import type { Station, StationsStackParamList } from '../../types';

type Props = NativeStackScreenProps<StationsStackParamList, 'Stations'>;

export default function StationsScreen({ navigation }: Props) {
  const { t } = useTranslation();
  const { colors } = useTheme();
  const styles = useMemo(() => makeStyles(colors), [colors]);

  const [stations, setStations] = useState<Station[] | null>(null);
  const [error, setError]       = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const autoOpened = useRef(false);

  const load = useCallback(async () => {
    try {
      const { data } = await stationsApi.list();
      setStations(data);
      setError(false);
      if (!autoOpened.current && data.length === 1) {
        autoOpened.current = true;
        navigation.navigate('Station', { stationId: data[0].id, name: data[0].name });
      }
    } catch {
      setError(true);
    }
  }, [navigation]);

  useFocusEffect(useCallback(() => { load(); }, [load]));

  const onRefresh = async () => { setRefreshing(true); await load(); setRefreshing(false); };

  if (stations === null && !error) {
    return <View style={styles.center}><ActivityIndicator color={colors.primary} /></View>;
  }

  if (error && stations === null) {
    return (
      <View style={styles.center}>
        <Text style={styles.hint}>{t('common.network')}</Text>
        <Button label={t('common.retry')} onPress={load} variant="secondary" fullWidth={false} style={styles.retry} />
      </View>
    );
  }

  return (
    <FlatList
      style={styles.container}
      contentContainerStyle={styles.content}
      data={stations ?? []}
      keyExtractor={s => String(s.id)}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
      ListEmptyComponent={
        <View style={styles.empty}>
          <View style={styles.emptyIcon}><Ionicons name="flash-outline" size={36} color={colors.primary} /></View>
          <Text style={styles.emptyTitle}>{t('stations.emptyTitle')}</Text>
          <Text style={styles.hint}>{t('stations.emptyHint')}</Text>
        </View>
      }
      renderItem={({ item }) => (
        <TouchableOpacity activeOpacity={0.85} onPress={() => navigation.navigate('Station', { stationId: item.id, name: item.name })}>
          <Card style={styles.card}>
            <View style={styles.row}>
              <View style={styles.iconBg}><Ionicons name="flash" size={20} color={colors.primary} /></View>
              <View style={styles.info}>
                <Text style={styles.name}>{item.name}</Text>
                <Text style={styles.ocppId}>{item.ocppId}</Text>
              </View>
              <Ionicons name="chevron-forward" size={18} color={colors.textMuted} />
            </View>
            <View style={styles.badgeRow}><StatusBadge station={item} /></View>
          </Card>
        </TouchableOpacity>
      )}
      ListFooterComponent={
        <Button label={t('stations.add')} variant="secondary" onPress={() => navigation.navigate('ClaimStation')} style={styles.add} />
      }
    />
  );
}

function makeStyles(c: AppColors) {
  return StyleSheet.create({
    container:  { flex: 1, backgroundColor: c.bgPrimary },
    content:    { padding: 20, paddingBottom: 40 },
    center:     { flex: 1, alignItems: 'center', justifyContent: 'center', padding: 28, backgroundColor: c.bgPrimary },
    card:       { marginBottom: 12 },
    row:        { flexDirection: 'row', alignItems: 'center', gap: 12 },
    iconBg:     { width: 40, height: 40, borderRadius: 12, backgroundColor: c.tintGreen, alignItems: 'center', justifyContent: 'center' },
    info:       { flex: 1 },
    name:       { fontSize: 16, fontWeight: '700', color: c.textPrimary },
    ocppId:     { fontSize: 12, color: c.textMuted, marginTop: 2 },
    badgeRow:   { marginTop: 12 },
    empty:      { alignItems: 'center', paddingVertical: 48 },
    emptyIcon:  { width: 72, height: 72, borderRadius: 24, backgroundColor: c.tintGreen, alignItems: 'center', justifyContent: 'center', marginBottom: 16 },
    emptyTitle: { fontSize: 18, fontWeight: '700', color: c.textPrimary, marginBottom: 8 },
    hint:       { fontSize: 14, color: c.textSecondary, textAlign: 'center', lineHeight: 20 },
    retry:      { marginTop: 16, paddingHorizontal: 24 },
    add:        { marginTop: 8 },
  });
}
