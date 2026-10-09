/**
 * StationScreen — the main screen: live status, start/stop, and while charging
 * a Nabla-style live view (timer, stat cards, power chart).
 *
 * All state comes from useStationLive (REST + SignalR). Outcomes that arrive
 * asynchronously (start/stop failed, session finished) are shown as banners.
 */

import React, { useEffect, useLayoutEffect, useMemo, useState } from 'react';
import { ActivityIndicator, RefreshControl, ScrollView, StyleSheet, Text, TouchableOpacity, View } from 'react-native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../../context/ThemeContext';
import { Failure, useStationLive } from '../../hooks/useStationLive';
import Button from '../../components/ui/Button';
import Card from '../../components/ui/Card';
import ConfirmSheet from '../../components/ui/ConfirmSheet';
import PowerChart from '../../components/domain/PowerChart';
import StatusBadge from '../../components/domain/StatusBadge';
import LimitCard from '../../components/domain/LimitCard';
import { formatDurationShort, formatTimer, kw, kwh, secondsBetween } from '../../utils/format';
import type { AppColors } from '../../theme';
import type { StationsStackParamList } from '../../types';

type Props = NativeStackScreenProps<StationsStackParamList, 'Station'>;
type IoniconName = React.ComponentProps<typeof Ionicons>['name'];

export default function StationScreen({ route, navigation }: Props) {
  const { stationId, name } = route.params;
  const { t } = useTranslation();
  const { colors } = useTheme();
  const styles = useMemo(() => makeStyles(colors), [colors]);
  const live = useStationLive(stationId);
  const { station, session } = live;

  const [confirmStop, setConfirmStop] = useState(false);
  const [refreshing, setRefreshing]   = useState(false);
  const [, tick] = useState(0);

  useLayoutEffect(() => {
    navigation.setOptions({
      title: station?.name ?? name,
      headerRight: () => (
        <TouchableOpacity onPress={() => navigation.navigate('History', { stationId, name: station?.name ?? name })} accessibilityLabel={t('station.history')}>
          <Ionicons name="time-outline" size={22} color={colors.textPrimary} />
        </TouchableOpacity>
      ),
    });
  }, [navigation, station?.name, name, stationId, colors, t]);

  // 1 s re-render for the live timer while a session runs.
  const running = session?.status === 'Active' || session?.status === 'Stopping';
  useEffect(() => {
    if (!running) return;
    const id = setInterval(() => tick(n => n + 1), 1000);
    return () => clearInterval(id);
  }, [running]);

  const onRefresh = async () => { setRefreshing(true); await live.reload(); setRefreshing(false); };

  if (live.loading) {
    return <View style={styles.center}><ActivityIndicator color={colors.primary} /></View>;
  }
  if (!station) {
    return (
      <View style={styles.center}>
        <Text style={styles.hint}>{t('common.network')}</Text>
        <Button label={t('common.retry')} onPress={live.reload} variant="secondary" fullWidth={false} style={styles.retry} />
      </View>
    );
  }

  const elapsed = secondsBetween(session?.startedAt ?? session?.createdAt);
  const showSoc = live.power.some(p => p.soc != null) || session?.soc != null;

  const stats: { icon: IoniconName; tint: string; color: string; value: string; label: string; muted?: boolean }[] = [
    { icon: 'flash',          tint: colors.tintGreen,  color: colors.primary, value: `${kwh(session?.energyKwh)} ${t('units.kwh')}`, label: t('station.energy') },
    { icon: 'speedometer',    tint: colors.tintBlue,   color: '#60A5FA',      value: `${kw(session?.currentPowerKw)} ${t('units.kw')}`, label: t('station.power') },
    { icon: 'battery-half',   tint: colors.tintPurple, color: '#A78BFA',      value: session?.soc != null ? `${Math.round(session.soc)}%` : '—', label: t('station.soc'), muted: session?.soc == null },
  ];

  return (
    <View style={styles.container}>
      <ScrollView
        contentContainerStyle={styles.content}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary} />}
      >
        {/* ── Station status ── */}
        <Card style={styles.statusCard}>
          <View style={styles.statusRow}>
            <StatusBadge station={station} />
            {!live.hubConnected && <Ionicons name="cloud-offline-outline" size={16} color={colors.textMuted} />}
          </View>
          <Text style={styles.ocppId}>{station.ocppId}</Text>
        </Card>

        {/* ── Async outcomes ── */}
        {live.failure && <FailureBanner failure={live.failure} onClose={live.clearFailure} styles={styles} colors={colors} />}
        {live.finished && (
          <Banner icon="checkmark-circle" color={colors.primary} styles={styles} onClose={live.clearFinished}
            title={t('station.doneTitle')}
            text={t('station.doneText', { energy: kwh(live.finished.energyKwh), duration: formatDurationShort(live.finished.durationSec) })} />
        )}

        {/* ── Idle ── */}
        {!session && (
          <View style={styles.idle}>
            <Text style={styles.hint}>{station.isOnline ? t('station.plugHint') : t('station.offlineHint')}</Text>
            <Button label={t('station.start')} onPress={live.start} loading={live.busy} disabled={!station.isOnline} size="lg" style={styles.mainBtn} />
          </View>
        )}

        {/* ── Starting ── */}
        {session?.status === 'Pending' && (
          <Card style={styles.pending}>
            <ActivityIndicator color={colors.primary} />
            <Text style={styles.pendingTitle}>{t('station.starting')}</Text>
            <Text style={styles.hint}>{t('station.waitingCharger')}</Text>
          </Card>
        )}

        {/* ── Charging ── */}
        {running && session && (
          <>
            <Text style={styles.timer}>{formatTimer(elapsed)}</Text>
            <Text style={styles.timerLabel}>{t('station.duration')}</Text>
            {session.initiatedBy === 'Charger' && <Text style={styles.origin}>{t('station.startedAtCharger')}</Text>}

            <View style={styles.statsGrid}>
              {stats.map(s => (
                <View key={s.label} style={[styles.statCard, s === stats[0] && styles.statCardFull]}>
                  <View style={[styles.statIconBg, { backgroundColor: s.tint }]}>
                    <Ionicons name={s.icon} size={18} color={s.muted ? colors.textMuted : s.color} />
                  </View>
                  <Text style={[styles.statValue, s.muted && { color: colors.textMuted }]}>{s.value}</Text>
                  <Text style={styles.statLabel}>{s.label}</Text>
                </View>
              ))}
            </View>

            <View style={styles.chartCard}>
              <Text style={styles.chartTitle}>{t('station.chart')}</Text>
              <PowerChart data={live.power} showSoc={showSoc} />
            </View>

            <TouchableOpacity
              style={[styles.stopBtn, (live.busy || session.status === 'Stopping') && styles.stopBtnDisabled]}
              onPress={() => setConfirmStop(true)}
              disabled={live.busy || session.status === 'Stopping'}
              activeOpacity={0.85}
            >
              {session.status === 'Stopping'
                ? <ActivityIndicator color={colors.stopText} />
                : <Ionicons name="stop-circle-outline" size={20} color={colors.stopText} />}
              <Text style={styles.stopBtnText}>{session.status === 'Stopping' ? t('station.stopping') : t('station.stop')}</Text>
            </TouchableOpacity>
          </>
        )}

        {/* ── Current limit (applies to the running session too) ── */}
        <LimitCard station={station} onChange={live.setLimit} />
      </ScrollView>

      <ConfirmSheet
        visible={confirmStop}
        title={t('station.stopConfirmTitle')}
        text={t('station.stopConfirmText')}
        confirmLabel={t('station.stop')}
        cancelLabel={t('station.keepCharging')}
        destructive
        onConfirm={() => { setConfirmStop(false); live.stop(); }}
        onCancel={() => setConfirmStop(false)}
      />
    </View>
  );
}

// ── Banners ───────────────────────────────────────────────────────────────────

type Styles = ReturnType<typeof makeStyles>;

function Banner({ icon, color, title, text, onClose, styles }: {
  icon: IoniconName; color: string; title: string; text: string; onClose: () => void; styles: Styles;
}) {
  return (
    <View style={[styles.banner, { borderColor: color }]}>
      <Ionicons name={icon} size={22} color={color} />
      <View style={styles.bannerBody}>
        <Text style={styles.bannerTitle}>{title}</Text>
        <Text style={styles.bannerText}>{text}</Text>
      </View>
      <TouchableOpacity onPress={onClose} hitSlop={12}><Ionicons name="close" size={18} color={color} /></TouchableOpacity>
    </View>
  );
}

function FailureBanner({ failure, onClose, styles, colors }: { failure: Failure; onClose: () => void; styles: Styles; colors: AppColors }) {
  const { t } = useTranslation();
  const prefix = failure.kind === 'start' ? 'station.startFailed' : 'station.stopFailed';
  const text = failure.reason === 'Request'
    ? (failure.message || t('common.network'))
    : t(`${prefix}${failure.reason}`);
  return <Banner icon="alert-circle" color={colors.error} title={t(`${prefix}Title`)} text={text} onClose={onClose} styles={styles} />;
}

// ── Styles (Nabla ActiveSession look) ─────────────────────────────────────────

function makeStyles(c: AppColors) {
  return StyleSheet.create({
    container:  { flex: 1, backgroundColor: c.bgPrimary },
    content:    { paddingHorizontal: 20, paddingTop: 16, paddingBottom: 40, alignItems: 'center' },
    center:     { flex: 1, alignItems: 'center', justifyContent: 'center', padding: 28, backgroundColor: c.bgPrimary },
    retry:      { marginTop: 16, paddingHorizontal: 24 },
    hint:       { fontSize: 14, color: c.textSecondary, textAlign: 'center', lineHeight: 20 },

    statusCard: { width: '100%', marginBottom: 16 },
    statusRow:  { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
    ocppId:     { fontSize: 12, color: c.textMuted, marginTop: 10 },

    idle:       { width: '100%', alignItems: 'center', marginTop: 32 },
    mainBtn:    { marginTop: 24 },

    pending:      { width: '100%', alignItems: 'center', paddingVertical: 32, gap: 10, marginTop: 16 },
    pendingTitle: { fontSize: 18, fontWeight: '700', color: c.textPrimary },

    timer:      { fontSize: 36, fontWeight: '800', color: c.textPrimary, letterSpacing: 2, fontVariant: ['tabular-nums'], marginTop: 16 },
    timerLabel: { fontSize: 11, color: c.textMuted, letterSpacing: 1.5, fontWeight: '600', marginTop: 4, marginBottom: 8 },
    origin:     { fontSize: 12, color: c.textSecondary, marginBottom: 8 },

    statsGrid:    { flexDirection: 'row', flexWrap: 'wrap', gap: 12, width: '100%', marginTop: 12, marginBottom: 20 },
    statCard:     { width: '47%', flexGrow: 1, backgroundColor: c.bgCard, borderRadius: 16, padding: 16, borderWidth: 1, borderColor: c.border },
    statCardFull: { width: '100%' },
    statIconBg:   { width: 36, height: 36, borderRadius: 10, alignItems: 'center', justifyContent: 'center', marginBottom: 12 },
    statValue:    { fontSize: 20, fontWeight: '800', color: c.textPrimary, marginBottom: 4 },
    statLabel:    { fontSize: 12, color: c.textMuted },

    chartCard:  { width: '100%', backgroundColor: c.bgCard, borderRadius: 16, padding: 16, borderWidth: 1, borderColor: c.border, marginBottom: 20 },
    chartTitle: { fontSize: 12, fontWeight: '600', color: c.textMuted, marginBottom: 10, letterSpacing: 0.5, textTransform: 'uppercase' },

    stopBtn:         { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8, width: '100%', backgroundColor: c.stopBg, borderWidth: 1, borderColor: c.stopBorder, borderRadius: 16, paddingVertical: 18 },
    stopBtnDisabled: { opacity: 0.5 },
    stopBtnText:     { color: c.stopText, fontSize: 16, fontWeight: '700' },

    banner:      { width: '100%', flexDirection: 'row', alignItems: 'flex-start', gap: 12, backgroundColor: c.bgCard, borderWidth: 1, borderRadius: 16, padding: 16, marginBottom: 16 },
    bannerBody:  { flex: 1 },
    bannerTitle: { fontSize: 15, fontWeight: '700', color: c.textPrimary, marginBottom: 4 },
    bannerText:  { fontSize: 14, color: c.textSecondary, lineHeight: 20 },
  });
}
