// Shapes mirror EVHomeAPI DTOs — see docs/api.md.

export interface AuthResponse { token: string; name: string; email: string }
export interface Profile      { name: string; email: string }

/** OCPP 1.6 connector status as reported by the charger. */
export type ConnectorStatus =
  | 'Available' | 'Preparing' | 'Charging' | 'SuspendedEVSE' | 'SuspendedEV'
  | 'Finishing' | 'Reserved' | 'Unavailable' | 'Faulted';

export interface Station {
  id:              number;
  ocppId:          string;
  name:            string;
  role:            'Owner' | 'Member';
  claimedAt:       string | null;
  isOnline:        boolean;
  connectorStatus: ConnectorStatus | string | null;
  lastStatusAt:    string | null;
  maxCurrentA:     number;
  currentLimitA:   number | null;   // null = at maxCurrentA (never unlimited)
  limitStatus:     LimitStatus | null;
}

export type LimitStatus = 'Pending' | 'Applied' | 'Rejected' | 'NotSupported' | 'Timeout' | 'Error';

export type SessionStatus = 'Pending' | 'Active' | 'Stopping' | 'Completed' | 'Cancelled';

export interface Session {
  id:             number;
  stationId:      number;
  status:         SessionStatus;
  initiatedBy:    'App' | 'Charger';
  stopReason:     'UserInitiated' | 'ChargerInitiated' | null;
  createdAt:      string;
  startedAt:      string | null;
  endedAt:        string | null;
  energyKwh:      number;
  currentPowerKw: number | null;
  soc:            number | null;
  transactionId:  number | null;
}

export interface MeterPoint { elapsedSec: number; currentPowerKw: number | null; soc: number | null }

// ── SignalR events (/hubs/charger) ────────────────────────────────────────────

export interface StatusUpdatedMsg      { stationId: number; ocppConnectorId: number; status: string; isConnected: boolean; carId: string | null }
export interface SessionStartedMsg     { stationId: number; sessionId: number; transactionId: number; meterStartWh: number | null }
export interface SessionStartFailedMsg { stationId: number; sessionId: number; reason: 'Rejected' | 'Timeout' }
export interface MeterUpdatedMsg       { stationId: number; sessionId: number; energyKwh: number; currentPowerKw: number | null; totalCost: null; soc: number | null }
export interface SessionFinalizedMsg   { stationId: number; sessionId: number; energyKwh: number; totalCost: null; stopReason: string }
export interface SessionStopFailedMsg  { stationId: number; sessionId: number; reason: 'Rejected' | 'Timeout' }
export interface ChargingLimitUpdatedMsg { stationId: number; limitA: number | null; status: LimitStatus }

/** One chart sample — same shape PowerChart expects (t = elapsed seconds). */
export interface PowerPoint { t: number; kw: number; soc: number | null }

// ── Navigation ────────────────────────────────────────────────────────────────

export type AuthStackParamList = {
  Login:    undefined;
  Register: undefined;
};

export type StationsStackParamList = {
  Stations:     undefined;
  Station:      { stationId: number; name: string };
  ClaimStation: undefined;
  History:      { stationId: number; name: string };
  Dev:          { stationId: number; name: string };
};

export type MainTabParamList = {
  Home:    undefined;
  Profile: undefined;
};
