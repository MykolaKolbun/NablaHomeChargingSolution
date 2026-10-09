/**
 * stationHub.ts — single SignalR connection to EVHomeAPI /hubs/charger.
 *
 * • start() after login, stop() on logout.
 * • subscribe(stationId, listener) joins the station group (ref-counted) and
 *   returns an unsubscribe function.
 * • After an automatic reconnect every joined station is re-joined and listeners
 *   get onReconnected(), so screens can re-sync over REST (events may have been
 *   missed while disconnected).
 */

import * as signalR from '@microsoft/signalr';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { API_BASE_URL, TOKEN_KEY } from './client';
import type {
  MeterUpdatedMsg, SessionFinalizedMsg, SessionStartedMsg, SessionStartFailedMsg,
  SessionStopFailedMsg, StatusUpdatedMsg, ChargingLimitUpdatedMsg,
} from '../types';

export interface StationListener {
  onStatus?:           (m: StatusUpdatedMsg) => void;
  onSessionStarted?:   (m: SessionStartedMsg) => void;
  onSessionStartFailed?: (m: SessionStartFailedMsg) => void;
  onMeter?:            (m: MeterUpdatedMsg) => void;
  onSessionFinalized?: (m: SessionFinalizedMsg) => void;
  onSessionStopFailed?: (m: SessionStopFailedMsg) => void;
  onLimitUpdated?:     (m: ChargingLimitUpdatedMsg) => void;
  onReconnected?:      () => void;
  onConnectionChange?: (connected: boolean) => void;
}

type Entry = { listeners: Set<StationListener> };

export interface DebugEvent { at: number; name: string; payload: unknown }
const DEBUG_LOG_SIZE = 100;

class StationHub {
  private connection: signalR.HubConnection | null = null;
  private stations = new Map<number, Entry>();

  // ── Developer screen: ring buffer of everything received ────────────────────
  private debugLog: DebugEvent[] = [];
  private debugListeners = new Set<() => void>();

  /** Newest first, at most DEBUG_LOG_SIZE entries. */
  getDebugLog(): DebugEvent[] { return this.debugLog; }
  clearDebugLog() { this.debugLog = []; this.debugListeners.forEach(fn => fn()); }
  onDebugLog(fn: () => void): () => void {
    this.debugListeners.add(fn);
    return () => { this.debugListeners.delete(fn); };
  }

  private log(name: string, payload: unknown) {
    this.debugLog = [{ at: Date.now(), name, payload }, ...this.debugLog].slice(0, DEBUG_LOG_SIZE);
    this.debugListeners.forEach(fn => fn());
  }

  get isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }

  async start(): Promise<void> {
    if (this.connection) return;

    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/charger`, {
        accessTokenFactory: async () => (await AsyncStorage.getItem(TOKEN_KEY)) ?? '',
      })
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: () => 5000 })   // retry forever
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    const forward = <T extends { stationId: number }>(name: string, pick: (l: StationListener) => ((m: T) => void) | undefined) =>
      (m: T) => {
        this.log(name, m);
        this.stations.get(m.stationId)?.listeners.forEach(l => pick(l)?.(m));
      };

    conn.on('StatusUpdated',        forward<StatusUpdatedMsg>('StatusUpdated', l => l.onStatus));
    conn.on('SessionStarted',       forward<SessionStartedMsg>('SessionStarted', l => l.onSessionStarted));
    conn.on('SessionStartFailed',   forward<SessionStartFailedMsg>('SessionStartFailed', l => l.onSessionStartFailed));
    conn.on('MeterUpdated',         forward<MeterUpdatedMsg>('MeterUpdated', l => l.onMeter));
    conn.on('SessionFinalized',     forward<SessionFinalizedMsg>('SessionFinalized', l => l.onSessionFinalized));
    conn.on('SessionStopFailed',    forward<SessionStopFailedMsg>('SessionStopFailed', l => l.onSessionStopFailed));
    conn.on('ChargingLimitUpdated', forward<ChargingLimitUpdatedMsg>('ChargingLimitUpdated', l => l.onLimitUpdated));

    conn.onreconnecting(() => { this.log('hub:reconnecting', null); this.broadcast(l => l.onConnectionChange?.(false)); });
    conn.onreconnected(async () => {
      this.log('hub:reconnected', null);
      await this.rejoinAll();
      this.broadcast(l => { l.onConnectionChange?.(true); l.onReconnected?.(); });
    });
    conn.onclose(() => { this.log('hub:closed', null); this.broadcast(l => l.onConnectionChange?.(false)); });

    this.connection = conn;
    await this.connectWithRetry(conn);
  }

  async stop(): Promise<void> {
    const conn = this.connection;
    this.connection = null;
    this.stations.clear();
    await conn?.stop().catch(() => {});
  }

  subscribe(stationId: number, listener: StationListener): () => void {
    let entry = this.stations.get(stationId);
    if (!entry) {
      entry = { listeners: new Set() };
      this.stations.set(stationId, entry);
      if (this.isConnected) this.join(stationId);
    }
    entry.listeners.add(listener);
    listener.onConnectionChange?.(this.isConnected);

    return () => {
      const e = this.stations.get(stationId);
      if (!e) return;
      e.listeners.delete(listener);
      if (e.listeners.size === 0) {
        this.stations.delete(stationId);
        if (this.isConnected) this.connection?.invoke('LeaveStation', stationId).catch(() => {});
      }
    };
  }

  // ── internals ───────────────────────────────────────────────────────────────

  /** First connect: withAutomaticReconnect only covers drops after a successful start. */
  private async connectWithRetry(conn: signalR.HubConnection): Promise<void> {
    while (this.connection === conn) {
      try {
        await conn.start();
        await this.rejoinAll();
        this.broadcast(l => { l.onConnectionChange?.(true); l.onReconnected?.(); });
        return;
      } catch {
        await new Promise(r => setTimeout(r, 5000));
      }
    }
  }

  private async rejoinAll() {
    await Promise.all([...this.stations.keys()].map(id => this.join(id)));
  }

  private join(stationId: number) {
    return this.connection?.invoke('JoinStation', stationId).catch(() => {});
  }

  private broadcast(fn: (l: StationListener) => void) {
    this.stations.forEach(e => e.listeners.forEach(fn));
  }
}

export const stationHub = new StationHub();
