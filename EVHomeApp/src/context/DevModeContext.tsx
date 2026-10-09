/**
 * DevModeContext — hidden developer screen switch.
 *
 * • available: app.json → expo.extra.devTools === true. A production build sets it to false
 *   and the developer screen disappears completely (the server can also disable its
 *   endpoints with DevTools:Enabled = false).
 * • enabled:   per device, toggled by tapping "Version" in Profile 7 times (Android-style).
 */

import React, { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import Constants from 'expo-constants';

const STORAGE_KEY  = 'dev_mode';
const TAPS_NEEDED  = 7;
const TAP_WINDOW   = 3000;   // ms between the first and the last tap

export const DEV_TOOLS_AVAILABLE = Constants.expoConfig?.extra?.devTools === true;

interface DevModeContextType {
  available: boolean;
  enabled:   boolean;
  /** Call on each tap of the version row; returns taps left (0 = just toggled). */
  registerTap: () => number;
  setEnabled:  (on: boolean) => void;
}

const DevModeContext = createContext<DevModeContextType>({
  available: false, enabled: false, registerTap: () => TAPS_NEEDED, setEnabled: () => {},
});

export function DevModeProvider({ children }: { children: React.ReactNode }) {
  const [enabled, setEnabledState] = useState(false);
  const taps = useRef<number[]>([]);

  useEffect(() => {
    if (!DEV_TOOLS_AVAILABLE) return;
    AsyncStorage.getItem(STORAGE_KEY).then(v => setEnabledState(v === '1'));
  }, []);

  const setEnabled = useCallback((on: boolean) => {
    setEnabledState(on);
    AsyncStorage.setItem(STORAGE_KEY, on ? '1' : '0');
  }, []);

  const registerTap = useCallback(() => {
    if (!DEV_TOOLS_AVAILABLE) return TAPS_NEEDED;
    const now = Date.now();
    taps.current = [...taps.current.filter(t => now - t < TAP_WINDOW), now];
    const left = TAPS_NEEDED - taps.current.length;
    if (left <= 0) {
      taps.current = [];
      setEnabled(!enabled);
      return 0;
    }
    return left;
  }, [enabled, setEnabled]);

  return (
    <DevModeContext.Provider value={{ available: DEV_TOOLS_AVAILABLE, enabled: DEV_TOOLS_AVAILABLE && enabled, registerTap, setEnabled }}>
      {children}
    </DevModeContext.Provider>
  );
}

export const useDevMode = () => useContext(DevModeContext);
