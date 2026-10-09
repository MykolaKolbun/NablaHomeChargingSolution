/**
 * useStationLive — live state of one station for the station screen.
 *
 *   REST    : station, open session, meter-history (on mount, on reconnect,
 *             on app foreground, and every 30 s while the hub is disconnected)
 *   SignalR : status / session / meter events applied incrementally
 *
 * REST is the source of truth; events only make the UI instant. Any time we may
 * have missed events (reconnect, background) we simply reload.
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import { AppState } from 'react-native';
import { apiErrorMessage, sessionsApi, stationsApi } from '../api/client';
import { stationHub } from '../api/stationHub';
import type { PowerPoint, Session, Station } from '../types';
import { secondsBetween } from '../utils/format';

const MAX_POINTS   = 240;
const POLL_MS      = 30_000;

export type Failure  = { kind: 'start' | 'stop'; reason: 'Rejected' | 'Timeout' | 'Request'; message?: string };
export type Finished = { sessionId: number; energyKwh: number; durationSec: number };

export function useStationLive(stationId: number) {
  const [station,  setStation]  = useState<Station | null>(null);
  const [session,  setSession]  = useState<Session | null>(null);
  const [power,    setPower]    = useState<PowerPoint[]>([]);
  const [loading,  setLoading]  = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [hubConnected, setHubConnected] = useState(stationHub.isConnected);
  const [busy,     setBusy]     = useState(false);         // start/stop request in flight
  const [failure,  setFailure]  = useState<Failure | null>(null);
  const [finished, setFinished] = useState<Finished | null>(null);

  const sessionRef = useRef<Session | null>(null);
  sessionRef.current = session;

  // ── REST load ───────────────────────────────────────────────────────────────

  const loadSession = useCallback(async () => {
    const { data } = await stationsApi.openSession(stationId);
    const open = data && typeof data === 'object' ? data : null;
    setSession(open);
    if (open && open.status !== 'Pending') {
      const history = await sessionsApi.meterHistory(open.id);
      setPower(history.data.map(p => ({ t: p.elapsedSec, kw: p.currentPowerKw ?? 0, soc: p.soc })).slice(-MAX_POINTS));
    } else {
      setPower([]);
    }
  }, [stationId]);

  const reload = useCallback(async () => {
    try {
      const { data } = await stationsApi.get(stationId);
      setStation(data);
      await loadSession();
      setLoadError(null);
    } catch (e) {
      setLoadError(apiErrorMessage(e, 'network'));
    } finally {
      setLoading(false);
    }
  }, [stationId, loadSession]);

  useEffect(() => { reload(); }, [reload]);

  // ── SignalR ─────────────────────────────────────────────────────────────────

  useEffect(() => stationHub.subscribe(stationId, {
    onConnectionChange: setHubConnected,
    onReconnected: reload,

    onStatus: m => setStation(s => s && ({
      ...s,
      isOnline:        m.isConnected,
      connectorStatus: m.ocppConnectorId === 1 ? m.status : s.connectorStatus,
      lastStatusAt:    new Date().toISOString(),
    })),

    // Also fires when a Paused session resumes (same session id, new transaction).
    onSessionStarted: () => { loadSession().catch(() => {}); },

    onSessionPaused: m => setSession(s => (s && s.id === m.sessionId ? { ...s, status: 'Paused', currentPowerKw: 0 } : s)),

    onSessionStartFailed: m => {
      if (sessionRef.current && sessionRef.current.id !== m.sessionId) return;
      setSession(null);
      setFailure({ kind: 'start', reason: m.reason });
    },

    onMeter: m => {
      const cur = sessionRef.current;
      if (!cur || cur.id !== m.sessionId) { loadSession().catch(() => {}); return; }
      setSession({ ...cur, energyKwh: m.energyKwh, currentPowerKw: m.currentPowerKw, soc: m.soc ?? cur.soc });
      const t = secondsBetween(cur.startedAt ?? cur.createdAt);
      setPower(p => [...p, { t, kw: m.currentPowerKw ?? 0, soc: m.soc }].slice(-MAX_POINTS));
    },

    onSessionFinalized: m => {
      const cur = sessionRef.current;
      setFinished(prev => prev?.sessionId === m.sessionId ? prev : {   // stop() may have set it already
        sessionId:   m.sessionId,
        energyKwh:   m.energyKwh,
        durationSec: cur ? secondsBetween(cur.startedAt ?? cur.createdAt) : 0,
      });
      setSession(null);
      setPower([]);
    },

    onLimitUpdated: m => setStation(s => s && ({ ...s, currentLimitA: m.limitA, limitStatus: m.status })),

    onSessionStopFailed: m => {
      setSession(s => (s && s.id === m.sessionId ? { ...s, status: 'Active' } : s));
      setFailure({ kind: 'stop', reason: m.reason });
    },
  }), [stationId, reload, loadSession]);

  // Fallback poll while the hub is down.
  useEffect(() => {
    if (hubConnected) return;
    const timer = setInterval(reload, POLL_MS);
    return () => clearInterval(timer);
  }, [hubConnected, reload]);

  // Re-sync when the app returns to the foreground.
  useEffect(() => {
    const sub = AppState.addEventListener('change', s => { if (s === 'active') reload(); });
    return () => sub.remove();
  }, [reload]);

  // ── Actions ─────────────────────────────────────────────────────────────────

  const start = useCallback(async () => {
    setBusy(true);
    setFinished(null);
    try {
      const { data } = await stationsApi.start(stationId);
      setSession(data);
      setPower([]);
    } catch (e) {
      setFailure({ kind: 'start', reason: 'Request', message: apiErrorMessage(e, '') });
      reload();
    } finally {
      setBusy(false);
    }
  }, [stationId, reload]);

  const stop = useCallback(async () => {
    setBusy(true);
    try {
      const { data } = await stationsApi.stop(stationId);
      if (data.status === 'Completed') {
        // Paused session ended on the server (nothing was charging) — no charger round-trip.
        setFinished({ sessionId: data.id, energyKwh: data.energyKwh, durationSec: secondsBetween(data.startedAt ?? data.createdAt) });
        setSession(null);
        setPower([]);
      } else {
        setSession(data);
      }
    } catch (e) {
      setFailure({ kind: 'stop', reason: 'Request', message: apiErrorMessage(e, '') });
      reload();
    } finally {
      setBusy(false);
    }
  }, [stationId, reload]);

  /** Set (amps) or remove (null) the current limit. Resolves to an error message or null. */
  const setLimit = useCallback(async (limitA: number | null): Promise<string | null> => {
    try {
      const { data } = await stationsApi.setLimit(stationId, limitA);
      setStation(data);   // limitStatus = Pending until ChargingLimitUpdated arrives
      return null;
    } catch (e) {
      reload();
      return apiErrorMessage(e, '');
    }
  }, [stationId, reload]);

  return {
    station, session, power, loading, loadError, hubConnected, busy,
    failure, clearFailure: () => setFailure(null),
    finished, clearFinished: () => setFinished(null),
    start, stop, reload, setLimit,
  };
}
