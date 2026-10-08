import React, { useRef, useState } from 'react';
import { StyleSheet, Text, TextInput, TouchableOpacity, View } from 'react-native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useTranslation } from 'react-i18next';
import { apiErrorMessage } from '../../api/client';
import { useAuth } from '../../context/AuthContext';
import { useTheme } from '../../context/ThemeContext';
import Button from '../../components/ui/Button';
import TextField from '../../components/ui/TextField';
import AuthLayout from './AuthLayout';
import type { AuthStackParamList } from '../../types';

type Props = NativeStackScreenProps<AuthStackParamList, 'Register'>;

export default function RegisterScreen({ navigation }: Props) {
  const { t } = useTranslation();
  const { colors } = useTheme();
  const { register } = useAuth();
  const emailRef    = useRef<TextInput>(null);
  const passwordRef = useRef<TextInput>(null);

  const [name, setName]         = useState('');
  const [email, setEmail]       = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading]   = useState(false);
  const [error, setError]       = useState<string | null>(null);

  const submit = async () => {
    if (!name.trim() || !email.trim() || !password) { setError(t('auth.fillAll')); return; }
    if (password.length < 8) { setError(t('auth.passwordMin')); return; }
    setLoading(true);
    setError(null);
    try {
      await register(name, email, password);
    } catch (e) {
      setError(apiErrorMessage(e, t('common.network')));
    } finally {
      setLoading(false);
    }
  };

  return (
    <AuthLayout title={t('auth.signUp')} subtitle={t('auth.tagline')} error={error}>
      <TextField
        label={t('auth.name')} icon="person-outline" value={name} onChangeText={setName}
        autoComplete="name" returnKeyType="next" onSubmitEditing={() => emailRef.current?.focus()} submitBehavior="submit"
      />
      <TextField
        ref={emailRef} label={t('auth.email')} icon="mail-outline" value={email} onChangeText={setEmail}
        keyboardType="email-address" autoCapitalize="none" autoComplete="email"
        returnKeyType="next" onSubmitEditing={() => passwordRef.current?.focus()} submitBehavior="submit"
      />
      <TextField
        ref={passwordRef} label={t('auth.password')} icon="lock-closed-outline" secure
        value={password} onChangeText={setPassword} autoComplete="new-password"
        returnKeyType="go" onSubmitEditing={submit}
      />
      <View style={styles.actions}>
        <Button label={t('auth.signUp')} onPress={submit} loading={loading} size="lg" />
      </View>
      <TouchableOpacity onPress={() => navigation.goBack()} style={styles.link}>
        <Text style={[styles.linkText, { color: colors.primary }]}>{t('auth.haveAccount')}</Text>
      </TouchableOpacity>
    </AuthLayout>
  );
}

const styles = StyleSheet.create({
  actions:  { marginTop: 28 },
  link:     { alignItems: 'center', marginTop: 24 },
  linkText: { fontSize: 14, fontWeight: '600' },
});
