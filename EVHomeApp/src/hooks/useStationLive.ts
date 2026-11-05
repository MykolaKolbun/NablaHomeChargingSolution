/**
 * useStationLive — live state of one station for the station screen.
 *
 *   REST    : station, open sessions, meter-history (on mount, on reconnect,
 *             on app foreground, and every 30 s while the hub is disconnected)
 *   SignalR : status / session / meter events applied incrementally
 *
 * REST is the source of truth; events only make the UI instant. Any time we may
 * have missed events (reconnect, background) we simply reload.
 *
 * A station can have several connectors (Wallbox Supernova: 1 and 2), each with its own
 * session. The screen shows one connector at a time (`connectorId`, chosen with
 * `selectConnector`); `session`, `power` and `connectorStatus` belong to that connector.
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import { AppState } from 'react-native';
import { apiErrorMessage, sessionsApi, stationsApi } from '../api/client';
import { stationHub } from '../api/stationHub';
import type { Connector, PowerPoint, Session, Station } from '../types';
import { secondsBetween } from '../utils/format';

const MAX_POINTS   = 240;
const POLL_MS      = 30_000;

export type Failure  = { kind: 'start' | 'stop'; reason: 'Rejected' | 'Timeout' | 'Request'; message?: string };
export type Finished = { sessionId: number; energyKwh: number; durationSec: number };

/** Connector of a session (1 when an older server omits it). */
export const connectorOf = (s: Session): number => s.connectorId ?? 1;

/** Connectors of a station, connector 1 always present (older server: none listed). */
export function connectorsOf(station: Station | null): Connector[] {
  const list = station?.connectors ?? [];
  if (list.some(c => c.connectorId === 1)) return list;
  return [{ connectorId: 1, status: station?.connectorStatus ?? null, lastStatusAt: station?.lastStatusAt ?? null }, ...list];
}

export function useStationLive(stationId: number) {
  const [station,  setStation]  = useState<Station | null>(null);
  const [sessions, setSessions] = useState<Session[]>([]);
  const [connectorId, setConnectorId] = useState(1);
  const [power,    setPower]    = useState<PowerPoint[]>([]);
  const [loading,  setLoading]  = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [hubConnected, setHubConnected] = useState(stationHub.isConnected);
  const [busy,     setBusy]     = useState(false);         // start/stop request in flight
  const [failure,  setFailure]  = useState<Failure | null>(null);
  const [finished, setFinished] = useState<Finished | null>(null);

  const sessionsRef  = useRef<Session[]>([]);
  sessionsRef.current = sessions;
  const connectorRef = useRef(1);
  connectorRef.current = connectorId;

  const session = sessions.find(s => connectorOf(s) === connectorId) ?? null;

  // ── REST load ───────────────────────────────────────────────────────────────

  const loadPower = useCallback(async (s: Session | null | undefined) => {
    if (s && s.status !== 'Pending') {
      const history = await sessionsApi.meterHistory(s.id);
      setPower(history.data.map(p => ({ t: p.elapsedSec, kw: p.currentPowerKw ?? 0, soc: p.soc })).slice(-MAX_POINTS));
    } else {
      setPower([]);
    }
  }, []);

  const loadSessions = useCallback(async () => {
    let open: Session[];
    try {
      open = (await stationsApi.openSessions(stationId)).data;
    } catch (e) {
      // Older server without /sessions/open: the single open session.
      if ((e as { response?: { status?: number } })?.response?.status !== 404) throw e;
      const { data } = await stationsApi.openSession(stationId);
      open = data && typeof data === 'object' ? [data] : [];
    }
    setSessions(open);
    await loadPower(open.find(s => connectorOf(s) === connectorRef.current));
  }, [stationId, loadPower]);

  const reload = useCallback(async () => {
    try {
      const { data } = await stationsApi.get(stationId);
      setStation(data);
      await loadSessions();
      setLoadError(null);
    } catch (e) {
      setLoadError(apiErrorMessage(e, 'network'));
    } finally {
      setLoading(false);
    }
  }, [stationId, loadSessions]);

  useEffect(() => { reload(); }, [reload]);

  /** Show another connector of the station (its session and chart). */
  const selectConnector = useCallback((id: number) => {
    if (id === connectorRef.current) return;
    setConnectorId(id);
    connectorRef.current = id;
    setFinished(null);
    setPower([]);
    loadPower(sessionsRef.current.find(s => connectorOf(s) === id)).catch(() => {});
  }, [loadPower]);

  const updateSession = (id: number, patch: (s: Session) => Session) =>
    setSessions(list => list.map(s => (s.id === id ? patch(s) : s)));

  // ── SignalR ─────────────────────────────────────────────────────────────────

  useEffect(() => stationHub.subscribe(stationId, {
    onConnectionChange: setHubConnected,
    onReconnected: reload,

    onStatus: m => setStation(s => {
      if (!s) return s;
      const now = new Date().toISOString();
      const connectors = m.ocppConnectorId > 0
        ? [...connectorsOf(s).filter(c => c.connectorId !== m.ocppConnectorId),
           { connectorId: m.ocppConnectorId, status: m.status, lastStatusAt: now }]
            .sort((a, b) => a.connectorId - b.connectorId)
        : s.connectors;
      return {
        ...s,
        isOnline:        m.isConnected,
        connectorStatus: m.ocppConnectorId === 1 ? m.status : s.connectorStatus,
        lastStatusAt:    now,
        connectors,
      };
    }),

    // New session (e.g. started at the charger on any connector) or a Paused one resuming.
    onSessionStarted: () => { loadSessions().catch(() => {}); },

    onSessionPaused: m => updateSession(m.sessionId, s => ({ ...s, status: 'Paused', currentPowerKw: 0 })),

    onSessionStartFailed: m => {
      const failed = sessionsRef.current.find(s => s.id === m.sessionId);
      setSessions(list => list.filter(s => s.id !== m.sessionId));
      if (!failed || connectorOf(failed) === connectorRef.current)
        setFailure({ kind: 'start', reason: m.reason });
    },

    onMeter: m => {
      const cur = sessionsRef.current.find(s => s.id === m.sessionId);
      if (!cur) { loadSessions().catch(() => {}); return; }
      updateSession(m.sessionId, s => ({ ...s, energyKwh: m.energyKwh, currentPowerKw: m.currentPowerKw, soc: m.soc ?? s.soc }));
      if (connectorOf(cur) !== connectorRef.current) return;   // chart shows the selected connector only
      const t = secondsBetween(cur.startedAt ?? cur.createdAt);
      setPower(p => [...p, { t, kw: m.currentPowerKw ?? 0, soc: m.soc }].slice(-MAX_POINTS));
    },

    onSessionFinalized: m => {
      const cur = sessionsRef.current.find(s => s.id === m.sessionId);
      setSessions(list => list.filter(s => s.id !== m.sessionId));
      if (cur && connectorOf(cur) !== connectorRef.current) return;   // another connector finished
      setFinished(prev => prev?.sessionId === m.sessionId ? prev : {   // stop() may have set it already
        sessionId:   m.sessionId,
        energyKwh:   m.energyKwh,
        durationSec: cur ? secondsBetween(cur.startedAt ?? cur.createdAt) : 0,
      });
      setPower([]);
    },

    onLimitUpdated: m => setStation(s => s && ({ ...s, currentLimitA: m.limitA, limitStatus: m.status })),

    onSessionStopFailed: m => {
      updateSession(m.sessionId, s => ({ ...s, status: 'Active' }));
      setFailure({ kind: 'stop', reason: m.reason });
    },
  }), [stationId, reload, loadSessions]);

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

  // ── Actions (on the selected connector) ─────────────────────────────────────

  const start = useCallback(async () => {
    setBusy(true);
    setFinished(null);
    try {
      const { data } = await stationsApi.start(stationId, connectorRef.current);
      setSessions(list => [...list.filter(s => connectorOf(s) !== connectorOf(data)), data]);
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
      const { data } = await stationsApi.stop(stationId, connectorRef.current);
      if (data.status === 'Completed') {
        // Paused session ended on the server (nothing was charging) — no charger round-trip.
        setFinished({ sessionId: data.id, energyKwh: data.energyKwh, durationSec: secondsBetween(data.startedAt ?? data.createdAt) });
        setSessions(list => list.filter(s => s.id !== data.id));
        setPower([]);
      } else {
        updateSession(data.id, () => data);
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

  const connectors      = connectorsOf(station);
  const connectorStatus = connectors.find(c => c.connectorId === connectorId)?.status ?? null;

  return {
    station, session, sessions, power, loading, loadError, hubConnected, busy,
    connectors, connectorId, connectorStatus, selectConnector,
    failure, clearFailure: () => setFailure(null),
    finished, clearFinished: () => setFinished(null),
    start, stop, reload, setLimit,
  };
}
