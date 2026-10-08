import React, { useMemo, useRef, useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useTranslation } from 'react-i18next';
import { apiErrorMessage, stationsApi } from '../../api/client';
import { useTheme } from '../../context/ThemeContext';
import Button from '../../components/ui/Button';
import TextField from '../../components/ui/TextField';
import type { AppColors } from '../../theme';
import type { StationsStackParamList } from '../../types';

type Props = NativeStackScreenProps<StationsStackParamList, 'ClaimStation'>;

export default function ClaimStationScreen({ navigation }: Props) {
  const { t } = useTranslation();
  const { colors } = useTheme();
  const styles = useMemo(() => makeStyles(colors), [colors]);
  const codeRef = useRef<TextInput>(null);

  const [ocppId, setOcppId]   = useState('');
  const [code, setCode]       = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError]     = useState<string | null>(null);

  const submit = async () => {
    if (!ocppId.trim() || !code.trim()) return;
    setLoading(true);
    setError(null);
    try {
      const { data } = await stationsApi.claim(ocppId.trim(), code.trim());
      navigation.replace('Station', { stationId: data.id, name: data.name });
    } catch (e) {
      setError(apiErrorMessage(e, t('common.network')));
    } finally {
      setLoading(false);
    }
  };

  return (
    <KeyboardAvoidingView style={styles.container} behavior={Platform.OS === 'ios' ? 'padding' : 'height'}>
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <Text style={styles.hint}>{t('claim.hint')}</Text>
        {error ? <Text style={styles.error}>{error}</Text> : null}
        <TextField
          label={t('claim.ocppId')} icon="hardware-chip-outline" value={ocppId} onChangeText={setOcppId}
          autoCapitalize="none" returnKeyType="next" onSubmitEditing={() => codeRef.current?.focus()} submitBehavior="submit"
        />
        <TextField
          ref={codeRef} label={t('claim.code')} icon="key-outline" value={code} onChangeText={setCode}
          autoCapitalize="characters" placeholder="XXXX-XXXX" returnKeyType="go" onSubmitEditing={submit}
        />
        <View style={styles.actions}>
          <Button label={t('claim.submit')} onPress={submit} loading={loading} disabled={!ocppId.trim() || !code.trim()} size="lg" />
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

function makeStyles(c: AppColors) {
  return StyleSheet.create({
    container: { flex: 1, backgroundColor: c.bgPrimary },
    content:   { padding: 24 },
    hint:      { fontSize: 15, color: c.textSecondary, lineHeight: 22 },
    error:     { fontSize: 14, color: c.error, marginTop: 12 },
    actions:   { marginTop: 28 },
  });
}
