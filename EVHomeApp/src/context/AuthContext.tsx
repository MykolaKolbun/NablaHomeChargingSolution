/**
 * AuthContext.tsx — login state for the whole app.
 *
 * Token + user are persisted in AsyncStorage; on startup a saved token restores
 * the session. The SignalR hub is started on login and stopped on logout.
 * A 401 from the API (expired token) logs the user out automatically.
 */

import React, { createContext, ReactNode, useCallback, useContext, useEffect, useState } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { authApi, registerUnauthorizedHandler, TOKEN_KEY, USER_KEY } from '../api/client';
import { stationHub } from '../api/stationHub';

interface AuthState {
  ready:           boolean;   // false until the saved token has been checked
  isAuthenticated: boolean;
  name:            string | null;
  email:           string | null;
}

interface AuthContextType extends AuthState {
  login:    (email: string, password: string) => Promise<void>;
  register: (name: string, email: string, password: string) => Promise<void>;
  logout:   () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | null>(null);

const signedOut: AuthState = { ready: true, isAuthenticated: false, name: null, email: null };

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({ ...signedOut, ready: false });

  const logout = useCallback(async () => {
    await stationHub.stop();
    await AsyncStorage.multiRemove([TOKEN_KEY, USER_KEY]);
    setState(signedOut);
  }, []);

  useEffect(() => {
    registerUnauthorizedHandler(() => { logout(); });

    (async () => {
      const [[, token], [, userJson]] = await AsyncStorage.multiGet([TOKEN_KEY, USER_KEY]);
      if (!token) { setState(signedOut); return; }
      const user = userJson ? JSON.parse(userJson) : {};
      setState({ ready: true, isAuthenticated: true, name: user.name ?? null, email: user.email ?? null });
      stationHub.start();
    })();
  }, [logout]);

  const signIn = async (token: string, name: string, email: string) => {
    await AsyncStorage.multiSet([[TOKEN_KEY, token], [USER_KEY, JSON.stringify({ name, email })]]);
    setState({ ready: true, isAuthenticated: true, name, email });
    stationHub.start();
  };

  const login = async (email: string, password: string) => {
    const { data } = await authApi.login(email.trim(), password);
    await signIn(data.token, data.name, data.email);
  };

  const register = async (name: string, email: string, password: string) => {
    const { data } = await authApi.register(name.trim(), email.trim(), password);
    await signIn(data.token, data.name, data.email);
  };

  return (
    <AuthContext.Provider value={{ ...state, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextType {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}
