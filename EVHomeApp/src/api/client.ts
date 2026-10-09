/**
 * api/client.ts — Axios client for EVHomeAPI (docs/api.md).
 *
 * • Attaches the JWT from AsyncStorage to every request.
 * • A 401 on an authenticated request calls the registered logout callback,
 *   so an expired token sends the user back to the login screen.
 * • API_BASE_URL comes from app.json → expo.extra.apiBaseUrl.
 */

import axios, { AxiosError } from 'axios';
import AsyncStorage from '@react-native-async-storage/async-storage';
import Constants from 'expo-constants';
import type { AuthResponse, MeterPoint, Profile, Session, Station } from '../types';

export const API_BASE_URL: string =
  (Constants.expoConfig?.extra?.apiBaseUrl as string | undefined) ?? 'https://home-api.alternatiview.com.ua';

export const TOKEN_KEY = 'auth_token';
export const USER_KEY  = 'auth_user';

let onUnauthorized: (() => void) | null = null;
export function registerUnauthorizedHandler(fn: () => void) { onUnauthorized = fn; }

const api = axios.create({
  baseURL: `${API_BASE_URL}/api`,
  timeout: 15000,
  headers: { 'Content-Type': 'application/json' },
});

api.interceptors.request.use(async config => {
  const token = await AsyncStorage.getItem(TOKEN_KEY);
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use(
  r => r,
  (error: AxiosError) => {
    const hadToken = !!error.config?.headers?.Authorization;
    if (error.response?.status === 401 && hadToken) onUnauthorized?.();
    return Promise.reject(error);
  },
);

/** Human-readable message from an API error (EVHomeAPI returns plain-text bodies). */
export function apiErrorMessage(err: unknown, fallback: string): string {
  const e = err as AxiosError;
  if (!e?.response) return fallback;
  const data = e.response.data as unknown;
  if (typeof data === 'string' && data.length > 0 && data.length < 200) return data;
  if (data && typeof data === 'object' && 'title' in data) return String((data as { title: unknown }).title);
  return fallback;
}

export const authApi = {
  register: (name: string, email: string, password: string) =>
    api.post<AuthResponse>('/auth/register', { name, email, password }),
  login: (email: string, password: string) =>
    api.post<AuthResponse>('/auth/login', { email, password }),
  profile: () => api.get<Profile>('/auth/profile'),
};

export const stationsApi = {
  list:        ()                                   => api.get<Station[]>('/stations'),
  get:         (id: number)                         => api.get<Station>(`/stations/${id}`),
  claim:       (ocppId: string, claimCode: string)  => api.post<Station>('/stations/claim', { ocppId, claimCode }),
  start:       (id: number)                         => api.post<Session>(`/stations/${id}/start`, { connectorId: 1 }),
  stop:        (id: number)                         => api.post<Session>(`/stations/${id}/stop`),
  setLimit:    (id: number, limitA: number | null)  => api.put<Station>(`/stations/${id}/limit`, { limitA }),
  /** 204 (empty body) when idle. */
  openSession: (id: number)                         => api.get<Session | ''>(`/stations/${id}/session`),
  history:     (id: number, limit = 50)             => api.get<Session[]>(`/stations/${id}/sessions`, { params: { limit } }),
};

/** Developer screen (EVHomeAPI DevTools; owner only, 404 when disabled on the server). */
export interface DevCallResult { action: string; status: string; result: unknown; elapsedMs: number }

export const devApi = {
  /** Never throws for charger-side outcomes: a 504 timeout is returned as a result too. */
  call: async (stationId: number, action: string, payload?: object): Promise<DevCallResult> => {
    try {
      const { data } = await api.post<DevCallResult>(`/stations/${stationId}/dev/call`, { action, payload }, { timeout: 45000 });
      return data;
    } catch (e) {
      const res = (e as AxiosError<DevCallResult>).response;
      if (res?.status === 504 && res.data) return res.data;
      throw e;
    }
  },
  charger: (stationId: number) => api.get<unknown>(`/stations/${stationId}/dev/charger`),
};

export const sessionsApi = {
  get:          (id: number) => api.get<Session>(`/sessions/${id}`),
  meterHistory: (id: number) => api.get<MeterPoint[]>(`/sessions/${id}/meter-history`),
};

export const devicesApi = {
  register:   (token: string, platform: string, language: string) => api.post('/devices', { token, platform, language }),
  unregister: (token: string) => api.post('/devices/unregister', { token }),
};
