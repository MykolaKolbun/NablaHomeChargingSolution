/**
 * DevScreen — developer tools for charger bring-up (hidden; see DevModeContext).
 *
 * Talks to EVHomeAPI /api/stations/{id}/dev/* (owner only, server flag DevTools:Enabled),
 * which relays whitelisted raw OCPP 1.6 calls through EVOCPP and returns the charger's
 * answer as is. Labels are English on purpose: they map 1:1 to OCPP names.
 */

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Platform, ScrollView, StyleSheet, Text, TextInput, TouchableOpacity, View } from 'react-native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { apiErrorMessage, devApi, DevCallResult } from '../../api/client';
import { DebugEvent, stationHub } from '../../api/stationHub';
import { useTheme } from '../../context/ThemeContext';
import { useStationLive } from '../../hooks/useStationLive';
import { registerForPush } from '../../push/pushNotifications';
import ConfirmSheet from '../../components/ui/ConfirmSheet';
import type { AppColors } from '../../theme';
import type { StationsStackParamList } from '../../types';

type Props = NativeStackScreenProps<StationsStackParamList, 'Dev'>;
type ConfigKey = { key: string; readonly: boolean; value?: string };
type Confirm = { title: string; text: string; action: string; payload?: object } | null;

const MONO = Platform.select({ ios: 'Menlo', android: 'monospace', default: 'monospace' });
const json = (v: unknown) => { try { return JSON.stringify(v, null, 2); } catch { return String(v); } };

export default function DevScreen({ route }: Props) {
  const { stationId } = route.params;
  const { colors } = useTheme();
  const s = useMemo(() => makeStyles(colors), [colors]);
  const live = useStationLive(stationId);

  const [busy, setBusy]       = useState<string | null>(null);
  const [last, setLast]       = useState<DevCallResult | { action: string; status: string; result: unknown } | null>(null);
  const [charger, setCharger] = useState<unknown>(null);
  const [config, setConfig]   = useState<ConfigKey[] | null>(null);
  const [filter, setFilter]   = useState('');
  const [editing, setEditing] = useState<{ key: string; value: string } | null>(null);
  const [limit, setLimit]     = useState('');
  const [dt, setDt]           = useState({ vendorId: 'Nabla', messageId: '', data: '' });
  const [confirm, setConfirm] = useState<Confirm>(null);
  const [log, setLog]         = useState<DebugEvent[]>(stationHub.getDebugLog());

  useEffect(() => stationHub.onDebugLog(() => setLog(stationHub.getDebugLog())), []);

  // ── calls ───────────────────────────────────────────────────────────────────

  const call = useCallback(async (action: string, payload?: object) => {
    setBusy(action);
    try {
      const r = await devApi.call(stationId, action, payload);
      setLast(r);
      return r;
    } catch (e) {
      const r = { action, status: 'RequestFailed', result: apiErrorMessage(e, 'network error') };
      setLast(r);
      return null;
    } finally {
      setBusy(null);
    }
  }, [stationId]);

  const loadCharger = useCallback(async () => {
    try { setCharger((await devApi.charger(stationId)).data); }
    catch (e) { setCharger({ error: apiErrorMessage(e, 'network error') }); }
  }, [stationId]);

  useEffect(() => { loadCharger(); }, [loadCharger]);

  const loadConfig = async () => {
    const r = await call('GetConfiguration');
    const keys = (r?.result as { configurationKey?: ConfigKey[] } | null)?.configurationKey;
    if (keys) setConfig([...keys].sort((a, b) => a.key.localeCompare(b.key)));
  };

  const saveConfig = async () => {
    if (!editing) return;
    const r = await call('ChangeConfiguration', { key: editing.key, value: editing.value });
    if ((r?.result as { status?: string } | null)?.status === 'Accepted') {
      setConfig(c => c?.map(k => (k.key === editing.key ? { ...k, value: editing.value } : k)) ?? c);
      setEditing(null);
    }
  };

  const applyLimit = async () => {
    const a = Number(limit.replace(',', '.'));
    if (!Number.isFinite(a)) return;
    setBusy('limit');
    const err = await live.setLimit(a);
    setBusy(null);
    setLast({ action: `PUT limit ${a} A`, status: err ? 'RequestFailed' : 'Accepted (Pending → see ChargingLimitUpdated in log)', result: err });
  };

  const testPush = async (kind: string) => {
    setBusy(`push ${kind}`);
    try {
      await registerForPush();   // make sure this device is registered first
      const { data } = await devApi.push(stationId, kind);
      setLast({ action: `push ${kind}`, status: data.enabled ? `sent ${data.sent}/${data.devices}` : 'FCM not configured on server', result: data });
    } catch (e) {
      setLast({ action: `push ${kind}`, status: 'RequestFailed', result: apiErrorMessage(e, 'network error') });
    } finally {
      setBusy(null);
    }
  };

  const sendDataTransfer = (messageId: string, data?: string) =>
    call('DataTransfer', { vendorId: dt.vendorId || 'Nabla', ...(messageId ? { messageId } : {}), ...(data ? { data } : {}) });

  // ── UI ──────────────────────────────────────────────────────────────────────

  // Bound once per render; Btn/Section are stable top-level components (inputs keep focus).
  const Btn = (p: { label: string; onPress: () => void; danger?: boolean }) => <DevButton {...p} s={s} busy={busy} />;

  const shown = (config ?? []).filter(k => !filter || k.key.toLowerCase().includes(filter.toLowerCase()));

  return (
    <ScrollView style={s.container} contentContainerStyle={s.content} keyboardShouldPersistTaps="handled">
      <Section s={s} title="Last response">
        {last
          ? <Text style={s.mono}>{`${last.action} → ${last.status}${'elapsedMs' in last ? ` (${last.elapsedMs} ms)` : ''}\n${json(last.result)}`}</Text>
          : <Text style={s.muted}>No call yet.</Text>}
      </Section>

      <Section s={s} title="TriggerMessage">
        <View style={s.row}>
          {['StatusNotification', 'MeterValues', 'BootNotification', 'Heartbeat'].map(m => (
            <Btn key={m} label={m} onPress={() => call('TriggerMessage', m === 'MeterValues' ? { requestedMessage: m, connectorId: 1 } : { requestedMessage: m })} />
          ))}
        </View>
      </Section>

      <Section s={s} title={`Configuration${config ? ` (${config.length})` : ''}`} right={<Btn label="GetConfiguration" onPress={loadConfig} />}>
        {config && (
          <TextInput style={s.input} placeholder="filter keys…" placeholderTextColor={colors.textMuted}
            value={filter} onChangeText={setFilter} autoCapitalize="none" autoCorrect={false} />
        )}
        {shown.map(k => (
          <View key={k.key} style={s.cfgRow}>
            {editing?.key === k.key ? (
              <View style={s.cfgEdit}>
                <Text style={s.cfgKey}>{k.key}</Text>
                <TextInput style={s.input} value={editing.value} onChangeText={v => setEditing({ key: k.key, value: v })}
                  autoCapitalize="none" autoCorrect={false} />
                <View style={s.row}><Btn label="ChangeConfiguration" onPress={saveConfig} /><Btn label="Cancel" onPress={() => setEditing(null)} /></View>
              </View>
            ) : (
              <TouchableOpacity disabled={k.readonly} onPress={() => setEditing({ key: k.key, value: k.value ?? '' })} style={s.cfgLine}>
                <Text style={s.cfgKey}>{k.key}{k.readonly ? '  (ro)' : ''}</Text>
                <Text style={s.cfgValue} numberOfLines={3}>{k.value ?? '—'}</Text>
              </TouchableOpacity>
            )}
          </View>
        ))}
      </Section>

      <Section s={s} title={`Current limit (max ${live.station?.maxCurrentA ?? '?'} A)`}>
        <Text style={s.muted}>Now: {live.station?.currentLimitA ?? 'max'} A · {live.station?.limitStatus ?? '—'}</Text>
        <View style={s.row}>
          <TextInput style={[s.input, s.flex]} keyboardType="decimal-pad" placeholder="amps, e.g. 7.5" placeholderTextColor={colors.textMuted}
            value={limit} onChangeText={setLimit} />
          <Btn label="limit" onPress={applyLimit} />
        </View>
      </Section>

      <Section s={s} title="DataTransfer">
        <TextInput style={s.input} value={dt.vendorId} onChangeText={v => setDt({ ...dt, vendorId: v })} placeholder="vendorId" placeholderTextColor={colors.textMuted} autoCapitalize="none" />
        <TextInput style={s.input} value={dt.messageId} onChangeText={v => setDt({ ...dt, messageId: v })} placeholder="messageId" placeholderTextColor={colors.textMuted} autoCapitalize="none" />
        <TextInput style={[s.input, s.multiline]} value={dt.data} onChangeText={v => setDt({ ...dt, data: v })} placeholder="data (string / JSON)" placeholderTextColor={colors.textMuted} autoCapitalize="none" multiline />
        <View style={s.row}>
          <Btn label="DataTransfer" onPress={() => sendDataTransfer(dt.messageId, dt.data)} />
          <Btn label="CP reset (DIY)" onPress={() => setConfirm({
            title: 'CP reset', text: 'DataTransfer Nabla/CpReset: the DIY controller pauses PWM (B1→B2) for 2–3 s. Wallbox answers UnknownVendorId.',
            action: 'DataTransfer', payload: { vendorId: 'Nabla', messageId: 'CpReset' } })} />
        </View>
      </Section>

      <Section s={s} title="Service">
        <View style={s.row}>
          <Btn label="Reset Soft" danger onPress={() => setConfirm({ title: 'Reset Soft', text: 'The charger restarts its software; a running session may be stopped.', action: 'Reset', payload: { type: 'Soft' } })} />
          <Btn label="Reset Hard" danger onPress={() => setConfirm({ title: 'Reset Hard', text: 'The charger reboots; a running session is stopped.', action: 'Reset', payload: { type: 'Hard' } })} />
          <Btn label="UnlockConnector" danger onPress={() => setConfirm({ title: 'UnlockConnector 1', text: 'Releases the cable lock; a running session is stopped.', action: 'UnlockConnector', payload: { connectorId: 1 } })} />
          <Btn label="ClearCache" onPress={() => call('ClearCache')} />
          <Btn label="GetCompositeSchedule" onPress={() => call('GetCompositeSchedule', { connectorId: 1, duration: 3600, chargingRateUnit: 'A' })} />
        </View>
      </Section>

      <Section s={s} title="Push notifications">
        <View style={s.row}>
          {['Paused', 'Resumed', 'ResumeFailed', 'Completed'].map(k => (
            <Btn key={k} label={k} onPress={() => testPush(k)} />
          ))}
        </View>
      </Section>

      <Section s={s} title="State" right={<Btn label="refresh" onPress={() => { live.reload(); loadCharger(); }} />}>
        <Text style={s.muted}>SignalR: {live.hubConnected ? 'connected' : 'disconnected'}</Text>
        <Text style={s.label}>EVOCPP plug</Text>
        <Text style={s.mono}>{json(charger)}</Text>
        <Text style={s.label}>Station</Text>
        <Text style={s.mono}>{json(live.station)}</Text>
        <Text style={s.label}>Open session</Text>
        <Text style={s.mono}>{json(live.session)}</Text>
      </Section>

      <Section s={s} title={`SignalR events (${log.length})`} right={<Btn label="clear" onPress={() => stationHub.clearDebugLog()} />}>
        {log.length === 0 && <Text style={s.muted}>Nothing received yet.</Text>}
        {log.map((e, i) => (
          <Text key={`${e.at}-${i}`} style={s.logLine}>
            <Text style={s.logTime}>{new Date(e.at).toLocaleTimeString()} </Text>
            <Text style={s.logName}>{e.name} </Text>
            {e.payload ? JSON.stringify(e.payload) : ''}
          </Text>
        ))}
      </Section>

      <ConfirmSheet
        visible={confirm !== null}
        title={confirm?.title ?? ''}
        text={confirm?.text ?? ''}
        confirmLabel="Send"
        cancelLabel="Cancel"
        destructive
        onConfirm={() => { const c = confirm!; setConfirm(null); call(c.action, c.payload); }}
        onCancel={() => setConfirm(null)}
      />
    </ScrollView>
  );
}

// ── Stable building blocks (top level, so children keep their state across renders) ──

type Styles = ReturnType<typeof makeStyles>;

function DevButton({ label, onPress, danger, s, busy }: { label: string; onPress: () => void; danger?: boolean; s: Styles; busy: string | null }) {
  return (
    <TouchableOpacity onPress={onPress} disabled={busy !== null}
      style={[s.btn, danger && s.btnDanger, busy !== null && s.btnBusy]}>
      <Text style={[s.btnText, danger && s.btnDangerText]}>{busy === label ? '…' : label}</Text>
    </TouchableOpacity>
  );
}

function Section({ title, children, right, s }: { title: string; children: React.ReactNode; right?: React.ReactNode; s: Styles }) {
  return (
    <View style={s.card}>
      <View style={s.cardHead}><Text style={s.cardTitle}>{title}</Text>{right}</View>
      {children}
    </View>
  );
}

function makeStyles(c: AppColors) {
  return StyleSheet.create({
    container: { flex: 1, backgroundColor: c.bgPrimary },
    content:   { padding: 16, paddingBottom: 40, gap: 12 },
    card:      { backgroundColor: c.bgCard, borderRadius: 16, padding: 14, borderWidth: 1, borderColor: c.border, gap: 8 },
    cardHead:  { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8 },
    cardTitle: { fontSize: 12, fontWeight: '700', color: c.textMuted, letterSpacing: 0.5, textTransform: 'uppercase' },
    row:       { flexDirection: 'row', flexWrap: 'wrap', gap: 8, alignItems: 'center' },
    flex:      { flex: 1 },
    btn:       { paddingHorizontal: 12, paddingVertical: 8, borderRadius: 10, backgroundColor: c.bgInput, borderWidth: 1, borderColor: c.border },
    btnBusy:   { opacity: 0.5 },
    btnText:   { fontSize: 13, fontWeight: '700', color: c.textPrimary },
    btnDanger: { backgroundColor: c.stopBg, borderColor: c.stopBorder },
    btnDangerText: { color: c.stopText },
    input:     { backgroundColor: c.bgInput, borderWidth: 1, borderColor: c.border, borderRadius: 10, paddingHorizontal: 10, paddingVertical: 8, color: c.textPrimary, fontFamily: MONO, fontSize: 13 },
    multiline: { minHeight: 60, textAlignVertical: 'top' },
    mono:      { fontFamily: MONO, fontSize: 12, color: c.textPrimary },
    muted:     { fontSize: 13, color: c.textMuted },
    label:     { fontSize: 11, fontWeight: '700', color: c.textMuted, marginTop: 6 },
    cfgRow:    { borderTopWidth: 1, borderTopColor: c.border, paddingTop: 6 },
    cfgLine:   { gap: 2 },
    cfgEdit:   { gap: 6 },
    cfgKey:    { fontFamily: MONO, fontSize: 12, fontWeight: '700', color: c.textSecondary },
    cfgValue:  { fontFamily: MONO, fontSize: 12, color: c.textPrimary },
    logLine:   { fontFamily: MONO, fontSize: 11, color: c.textPrimary },
    logTime:   { color: c.textMuted },
    logName:   { color: c.primary, fontWeight: '700' },
  });
}
