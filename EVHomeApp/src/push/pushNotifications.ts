/**
 * Push notifications (FCM). The server renders the texts and sends them straight to FCM
 * (no Expo push service): the app only registers its native FCM token with EVHomeAPI.
 *
 * Expo Go cannot receive remote notifications (SDK 53+) — registration is skipped there;
 * use the APK.
 */

import { Platform } from 'react-native';
import Constants, { ExecutionEnvironment } from 'expo-constants';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { devicesApi } from '../api/client';
import i18n from '../i18n';

const PUSH_TOKEN_KEY = 'push_token';
export const CHANNEL_ID = 'charging';   // must match the server's android.notification.channel_id

// Show pushes as banners while the app is open too.
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowBanner: true, shouldShowList: true, shouldPlaySound: true, shouldSetBadge: false,
  }),
});

const supported = () =>
  Platform.OS === 'android' && Device.isDevice &&
  Constants.executionEnvironment !== ExecutionEnvironment.StoreClient;   // not Expo Go

/** Ask permission, get the FCM token and register it for the logged-in user. Best effort. */
export async function registerForPush(): Promise<void> {
  if (!supported()) return;
  try {
    await Notifications.setNotificationChannelAsync(CHANNEL_ID, {
      name:       i18n.t('push.channelName'),
      importance: Notifications.AndroidImportance.HIGH,
      sound:      'default',
    });

    const current = await Notifications.getPermissionsAsync();
    const granted = current.granted || (await Notifications.requestPermissionsAsync()).granted;
    if (!granted) return;

    const { data: token } = await Notifications.getDevicePushTokenAsync();
    await devicesApi.register(String(token), Platform.OS, i18n.language === 'en' ? 'en' : 'uk');
    await AsyncStorage.setItem(PUSH_TOKEN_KEY, String(token));
  } catch (e) {
    console.warn('Push registration failed', e);
  }
}

/** Before logout (needs the auth token still set): stop this device getting the account's pushes. */
export async function unregisterPush(): Promise<void> {
  try {
    const token = await AsyncStorage.getItem(PUSH_TOKEN_KEY);
    if (!token) return;
    await AsyncStorage.removeItem(PUSH_TOKEN_KEY);
    await devicesApi.unregister(token);
  } catch {
    // offline / token expired — the server drops it when FCM reports it, or on the next login
  }
}

/** Language changed: re-register so server-rendered texts follow it (only if already registered). */
export async function refreshPushLanguage(): Promise<void> {
  if (await AsyncStorage.getItem(PUSH_TOKEN_KEY)) await registerForPush();
}
