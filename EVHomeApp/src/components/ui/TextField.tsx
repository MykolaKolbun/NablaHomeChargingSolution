/**
 * ui/TextField.tsx — labelled input with a leading icon (Nabla login style).
 * Pass `secure` for passwords: adds an eye toggle.
 */

import React, { forwardRef, useMemo, useState } from 'react';
import { StyleSheet, Text, TextInput, TextInputProps, TouchableOpacity, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTheme } from '../../context/ThemeContext';
import type { AppColors } from '../../theme';

type IoniconName = React.ComponentProps<typeof Ionicons>['name'];

interface Props extends TextInputProps {
  label:   string;
  icon:    IoniconName;
  secure?: boolean;
}

const TextField = forwardRef<TextInput, Props>(({ label, icon, secure, ...input }, ref) => {
  const { colors } = useTheme();
  const styles = useMemo(() => makeStyles(colors), [colors]);
  const [hidden, setHidden] = useState(true);

  return (
    <View>
      <Text style={styles.label}>{label}</Text>
      <View style={styles.wrapper}>
        <Ionicons name={icon} size={18} color={colors.textMuted} style={styles.icon} />
        <TextInput
          ref={ref}
          style={styles.input}
          placeholderTextColor={colors.textMuted}
          secureTextEntry={secure && hidden}
          autoCorrect={false}
          {...input}
        />
        {secure && (
          <TouchableOpacity onPress={() => setHidden(h => !h)} style={styles.eye} accessibilityRole="button">
            <Ionicons name={hidden ? 'eye-off-outline' : 'eye-outline'} size={18} color={colors.textMuted} />
          </TouchableOpacity>
        )}
      </View>
    </View>
  );
});

export default TextField;

function makeStyles(c: AppColors) {
  return StyleSheet.create({
    label: {
      fontSize: 13, fontWeight: '600', color: c.textSecondary,
      marginBottom: 8, marginTop: 16, textTransform: 'uppercase', letterSpacing: 0.5,
    },
    wrapper: {
      flexDirection: 'row', alignItems: 'center',
      backgroundColor: c.bgInput, borderWidth: 1, borderColor: c.border,
      borderRadius: 12, paddingHorizontal: 14,
    },
    icon:  { marginRight: 10 },
    input: { flex: 1, paddingVertical: 15, fontSize: 15, color: c.textPrimary },
    eye:   { padding: 4 },
  });
}
