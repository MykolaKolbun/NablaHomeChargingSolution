/**
 * AppNavigator — auth stack when signed out; tabs (Chargers, Profile) when signed in.
 * Header/tab colours follow the active theme.
 */

import React from 'react';
import { ActivityIndicator, View } from 'react-native';
import { createNativeStackNavigator, NativeStackNavigationOptions } from '@react-navigation/native-stack';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext';
import { useTheme } from '../context/ThemeContext';
import LoginScreen from '../screens/auth/LoginScreen';
import RegisterScreen from '../screens/auth/RegisterScreen';
import StationsScreen from '../screens/stations/StationsScreen';
import StationScreen from '../screens/stations/StationScreen';
import ClaimStationScreen from '../screens/stations/ClaimStationScreen';
import HistoryScreen from '../screens/stations/HistoryScreen';
import ProfileScreen from '../screens/profile/ProfileScreen';
import type { AuthStackParamList, MainTabParamList, StationsStackParamList } from '../types';

const AuthStack     = createNativeStackNavigator<AuthStackParamList>();
const StationsStack = createNativeStackNavigator<StationsStackParamList>();
const ProfileStack  = createNativeStackNavigator<{ ProfileHome: undefined }>();
const Tab           = createBottomTabNavigator<MainTabParamList>();

function useHeaderOptions(): NativeStackNavigationOptions {
  const { colors } = useTheme();
  return {
    headerStyle:         { backgroundColor: colors.bgPrimary },
    headerTintColor:     colors.textPrimary,
    headerTitleStyle:    { fontWeight: '700' },
    headerShadowVisible: false,
    contentStyle:        { backgroundColor: colors.bgPrimary },
  };
}

function StationsNavigator() {
  const { t } = useTranslation();
  const header = useHeaderOptions();
  return (
    <StationsStack.Navigator screenOptions={header}>
      <StationsStack.Screen name="Stations"     component={StationsScreen}     options={{ title: t('stations.title') }} />
      <StationsStack.Screen name="Station"      component={StationScreen}      options={({ route }) => ({ title: route.params.name })} />
      <StationsStack.Screen name="ClaimStation" component={ClaimStationScreen} options={{ title: t('claim.title') }} />
      <StationsStack.Screen name="History"      component={HistoryScreen}      options={{ title: t('station.history') }} />
    </StationsStack.Navigator>
  );
}

function ProfileNavigator() {
  const { t } = useTranslation();
  const header = useHeaderOptions();
  return (
    <ProfileStack.Navigator screenOptions={header}>
      <ProfileStack.Screen name="ProfileHome" component={ProfileScreen} options={{ title: t('profile.title') }} />
    </ProfileStack.Navigator>
  );
}

type IoniconName = React.ComponentProps<typeof Ionicons>['name'];
const TAB_ICONS: Record<keyof MainTabParamList, [IoniconName, IoniconName]> = {
  Home:    ['flash', 'flash-outline'],
  Profile: ['person', 'person-outline'],
};

function MainTabs() {
  const { t } = useTranslation();
  const { colors } = useTheme();
  return (
    <Tab.Navigator
      screenOptions={({ route }) => ({
        headerShown: false,
        tabBarIcon: ({ focused, size }) => (
          <Ionicons name={TAB_ICONS[route.name][focused ? 0 : 1]} size={size} color={focused ? colors.primary : colors.textMuted} />
        ),
        tabBarActiveTintColor:   colors.primary,
        tabBarInactiveTintColor: colors.textMuted,
        tabBarStyle: { backgroundColor: colors.bgCard, borderTopColor: colors.border },
      })}
    >
      <Tab.Screen name="Home"    component={StationsNavigator} options={{ title: t('tabs.home') }} />
      <Tab.Screen name="Profile" component={ProfileNavigator}  options={{ title: t('tabs.profile') }} />
    </Tab.Navigator>
  );
}

export default function AppNavigator() {
  const { ready, isAuthenticated } = useAuth();
  const { colors } = useTheme();
  const header = useHeaderOptions();

  if (!ready) {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bgPrimary }}>
        <ActivityIndicator color={colors.primary} />
      </View>
    );
  }

  if (isAuthenticated) return <MainTabs />;

  return (
    <AuthStack.Navigator screenOptions={{ ...header, headerShown: false }}>
      <AuthStack.Screen name="Login"    component={LoginScreen} />
      <AuthStack.Screen name="Register" component={RegisterScreen} />
    </AuthStack.Navigator>
  );
}
