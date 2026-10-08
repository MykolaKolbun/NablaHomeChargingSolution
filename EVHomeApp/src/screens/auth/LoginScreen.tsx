import React, { useRef, useState } from 'react';
import { StyleSheet, Text, TextInput, TouchableOpacity, View } from 'react-native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useTranslation } from 'react-i18next';
import { AxiosError } from 'axios';
import { useAuth } from '../../context/AuthContext';
import { useTheme } from '../../context/ThemeContext';
import Button from '../../components/ui/Button';
import TextField from '../../components/ui/TextField';
import AuthLayout from './AuthLayout';
import type { AuthStackParamList } from '../../types';

type Props = NativeStackScreenProps<AuthStackParamList, 'Login'>;

export default function LoginScreen({ navigation }: Props) {
  const { t } = useTranslation();
  const { colors } = useTheme();
  const { login } = useAuth();
  const passwordRef = useRef<TextInput>(null);

  const [email, setEmail]       = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading]   = useState(false);
  const [error, setError]       = useState<string | null>(null);

  const submit = async () => {
    if (!email.trim() || !password) { setError(t('auth.fillAll')); return; }
    setLoading(true);
    setError(null);
    try {
      await login(email, password);
    } catch (e) {
      const status = (e as AxiosError).response?.status;
      setError(status === 401 ? t('auth.loginFailed') : t('common.network'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <AuthLayout title={t('auth.signIn')} subtitle={t('auth.tagline')} error={error}>
      <TextField
        label={t('auth.email')} icon="mail-outline" value={email} onChangeText={setEmail}
        keyboardType="email-address" autoCapitalize="none" autoComplete="email"
        returnKeyType="next" onSubmitEditing={() => passwordRef.current?.focus()} submitBehavior="submit"
      />
      <TextField
        ref={passwordRef} label={t('auth.password')} icon="lock-closed-outline" secure
        value={password} onChangeText={setPassword} autoComplete="password"
        returnKeyType="go" onSubmitEditing={submit}
      />
      <View style={styles.actions}>
        <Button label={t('auth.signIn')} onPress={submit} loading={loading} size="lg" />
      </View>
      <TouchableOpacity onPress={() => navigation.navigate('Register')} style={styles.link}>
        <Text style={[styles.linkText, { color: colors.primary }]}>{t('auth.noAccount')}</Text>
      </TouchableOpacity>
    </AuthLayout>
  );
}

const styles = StyleSheet.create({
  actions:  { marginTop: 28 },
  link:     { alignItems: 'center', marginTop: 24 },
  linkText: { fontSize: 14, fontWeight: '600' },
});
