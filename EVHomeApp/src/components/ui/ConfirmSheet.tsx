/** Bottom-sheet confirmation (Nabla stop-modal style). Works on native and web. */

import React, { useMemo } from 'react';
import { Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import { useTheme } from '../../context/ThemeContext';
import type { AppColors } from '../../theme';
import Button from './Button';

interface Props {
  visible:      boolean;
  title:        string;
  text:         string;
  confirmLabel: string;
  cancelLabel:  string;
  destructive?: boolean;
  onConfirm:    () => void;
  onCancel:     () => void;
}

export default function ConfirmSheet({ visible, title, text, confirmLabel, cancelLabel, destructive, onConfirm, onCancel }: Props) {
  const { colors } = useTheme();
  const styles = useMemo(() => makeStyles(colors), [colors]);

  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={onCancel}>
      <Pressable style={styles.backdrop} onPress={onCancel}>
        <Pressable style={styles.sheet} onPress={() => {}}>
          <View style={styles.handle} />
          <Text style={styles.title}>{title}</Text>
          <Text style={styles.text}>{text}</Text>
          <Button label={confirmLabel} onPress={onConfirm} variant={destructive ? 'destructive' : 'primary'} size="lg" />
          <Button label={cancelLabel} onPress={onCancel} variant="ghost" size="lg" style={styles.cancel} />
        </Pressable>
      </Pressable>
    </Modal>
  );
}

function makeStyles(c: AppColors) {
  return StyleSheet.create({
    backdrop: { flex: 1, backgroundColor: 'rgba(0,0,0,0.6)', justifyContent: 'flex-end' },
    sheet:    { backgroundColor: c.bgCard, borderTopLeftRadius: 24, borderTopRightRadius: 24, padding: 28, paddingBottom: 40, borderWidth: 1, borderColor: c.border },
    handle:   { width: 40, height: 4, borderRadius: 2, backgroundColor: c.border, alignSelf: 'center', marginBottom: 20 },
    title:    { fontSize: 22, fontWeight: '800', color: c.textPrimary, marginBottom: 10 },
    text:     { fontSize: 15, color: c.textSecondary, lineHeight: 22, marginBottom: 28 },
    cancel:   { marginTop: 12 },
  });
}
