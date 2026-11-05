/**
 * StatusBadge — coloured dot + label for a station's live state.
 * Offline wins over connector status; Charging pulses green, Preparing/paused amber.
 */

import React from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../../context/ThemeContext';
import type { AppColors } from '../../theme';
import type { Station } from '../../types';

const KNOWN = ['Available', 'Preparing', 'Charging', 'SuspendedEVSE', 'SuspendedEV', 'Finishing', 'Reserved', 'Unavailable', 'Faulted'];

/**
 * One status for a whole station (station list). Multi-connector chargers: the most "active"
 * connector wins — Charging, then a car plugged in, then the rest; single connector: its status.
 */
export function summaryStatus(station: Pick<Station, 'connectorStatus' | 'connectors'>): string | null {
  const all = station.connectors?.map(c => c.status) ?? [];
  if (all.length < 2) return station.connectorStatus;
  const rank = (s: string | null) =>
    s === 'Charging' ? 0
      : s === 'SuspendedEV' || s === 'SuspendedEVSE' || s === 'Preparing' || s === 'Finishing' ? 1
      : s === 'Faulted' ? 2
      : s === 'Available' ? 3 : 4;
  return [...all].sort((a, b) => rank(a) - rank(b))[0] ?? station.connectorStatus;
}

export function statusColor(station: Pick<Station, 'isOnline' | 'connectorStatus'>, c: AppColors): string {
  if (!station.isOnline) return c.offline;
  switch (station.connectorStatus) {
    case 'Available': case 'Charging':                              return c.available;
    case 'Preparing': case 'SuspendedEV': case 'SuspendedEVSE':
    case 'Finishing': case 'Reserved':                              return c.inUse;
    case 'Faulted':                                                 return c.error;
    default:                                                        return c.offline;
  }
}

export default function StatusBadge({ station }: { station: Pick<Station, 'isOnline' | 'connectorStatus'> }) {
  const { t } = useTranslation();
  const { colors } = useTheme();
  const color = statusColor(station, colors);
  const label = !station.isOnline
    ? t('stations.offline')
    : t(`status.${KNOWN.includes(station.connectorStatus ?? '') ? station.connectorStatus : 'unknown'}`);

  return (
    <View style={[styles.badge, { backgroundColor: colors.bgChip, borderColor: colors.border }]}>
      <View style={[styles.dot, { backgroundColor: color }]} />
      <Text style={[styles.text, { color: colors.textSecondary }]}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  badge: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingHorizontal: 10, paddingVertical: 5, borderRadius: 999, borderWidth: 1, alignSelf: 'flex-start' },
  dot:   { width: 8, height: 8, borderRadius: 4 },
  text:  { fontSize: 12, fontWeight: '600' },
});
