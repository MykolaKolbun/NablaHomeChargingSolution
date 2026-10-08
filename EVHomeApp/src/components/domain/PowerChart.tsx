/**
 * PowerChart — dual-line area chart drawn with react-native-svg.
 *
 * Renders the charging session power curve and, for DC fast chargers,
 * an SoC curve on the same chart — two metrics, two Y-axes:
 *
 *   Left  axis  — Power in kW  (green fill + solid line)
 *   Right axis  — SoC in %     (blue solid line, no fill)
 *
 * If `showSoc` is false, or no sample has SoC data, only the power
 * line and left axis are rendered (AC charger / no SoC reporting).
 *
 * Layout:
 *   ┌ kW ──────────────────────────── 100% ┐
 *   │  ~~~~green power~~~~                  │
 *   │  ────blue SoC────                     │
 *   └ 0 ──────────────────────────────  0% ┘
 *   │ ▬ Power (kW)   — Battery (%)         │  ← legend row
 *
 * Features:
 *   • Fully responsive — measures container width via onLayout.
 *   • Right axis labels only rendered when SoC data is present.
 *   • "Collecting data…" placeholder while < 2 power samples exist.
 *   • No new npm dependencies — react-native-svg already installed.
 */

import React, { useState } from 'react';
import { View, Text, StyleSheet, LayoutChangeEvent } from 'react-native';
import Svg, { Path, Line, Circle, Text as SvgText } from 'react-native-svg';
import type { PowerPoint } from '../../types';
import { useTheme } from '../../context/ThemeContext';
import { useTranslation } from 'react-i18next';

// ── Constants ──────────────────────────────────────────────────────────────────
const SOC_COLOR   = '#60A5FA';   // blue-400

/**
 * Format elapsed seconds as M:SS (< 1 h) or H:MM:SS (≥ 1 h).
 * totalSeconds decides which format to use so all labels on one chart are consistent.
 */
function formatTime(seconds: number, totalSeconds: number): string {
  const s = Math.round(seconds);
  if (totalSeconds < 3600) {
    const m = Math.floor(s / 60);
    const ss = s % 60;
    return `${m}:${ss.toString().padStart(2, '0')}`;
  }
  const h  = Math.floor(s / 3600);
  const m  = Math.floor((s % 3600) / 60);
  const ss = s % 60;
  return `${h}:${m.toString().padStart(2, '0')}:${ss.toString().padStart(2, '0')}`;
}

interface Props {
  data:      PowerPoint[];
  /** Show SoC line — pass true when the charger reports SoC (DC fast chargers). */
  showSoc?:  boolean;
  /** Total SVG height in dp. Does not include the legend row. Default 130. */
  height?:   number;
}

export default function PowerChart({ data, showSoc = false, height = 130 }: Props) {
  const { colors } = useTheme();
  const { t }      = useTranslation();
  const [width, setWidth] = useState(0);

  const onLayout = (e: LayoutChangeEvent) => {
    const w = e.nativeEvent.layout.width;
    if (w > 0) setWidth(w);
  };

  // ── Decide whether to actually draw SoC ───────────────────────────────────
  // showSoc=true is necessary but not sufficient: we also need ≥2 samples
  // with actual soc data in the history.
  const socSamples = showSoc ? data.filter(p => p.soc != null) : [];
  const hasSoc     = socSamples.length >= 2;

  // ── Empty / loading state ─────────────────────────────────────────────────
  const isEmpty = data.length < 2 || width === 0;

  // ── Chart geometry ────────────────────────────────────────────────────────
  // Right padding is wider when SoC axis labels are present.
  const pad = {
    top:    10,
    left:   38,  // room for kW labels
    right:  hasSoc ? 36 : 12,  // room for % labels when SoC visible
    bottom: 22,
  };
  const chartW = width  - pad.left - pad.right;
  const chartH = height - pad.top  - pad.bottom;
  const baseY  = pad.top + chartH;

  // ── Path / label computation (only when we have data + measured width) ────
  let powerArea    = '';
  let powerLine    = '';
  let socPath      = '';
  let lastPx       = 0;
  let lastPy       = baseY;
  let maxKwLabel   = '';
  let endTimeLabel = '';
  // Intermediate axis labels/positions (filled when !isEmpty)
  let midKwY1      = 0;
  let midKwY2      = 0;
  let midKwLabel1  = '';
  let midKwLabel2  = '';
  let midTx1       = 0;
  let midTx2       = 0;
  let midTLabel1   = '';
  let midTLabel2   = '';

  if (!isEmpty) {
    const maxKw = Math.max(...data.map(p => p.kw), 1);
    const maxT  = Math.max(...data.map(p => p.t),  1);

    // Map data coords → SVG pixel coords
    const px   = (t: number)  => pad.left + (t / maxT) * chartW;
    const pyKw = (kw: number) => pad.top + chartH - (kw / maxKw) * chartH;
    // SoC always 0-100 scale; chart height maps 0→baseY, 100→pad.top
    const pySoc = (soc: number) => pad.top + chartH - (soc / 100) * chartH;

    const last = data[data.length - 1];
    lastPx = px(last.t);
    lastPy = pyKw(last.kw);

    // Power line + closed area path
    const pts = data
      .map((p, i) => `${i === 0 ? 'M' : 'L'}${px(p.t).toFixed(1)},${pyKw(p.kw).toFixed(1)}`)
      .join(' ');
    powerLine = pts;
    powerArea = `${pts} L${px(last.t).toFixed(1)},${baseY.toFixed(1)} L${px(data[0].t).toFixed(1)},${baseY.toFixed(1)} Z`;

    // SoC solid line (no fill)
    if (hasSoc) {
      socPath = socSamples
        .map((p, i) =>
          `${i === 0 ? 'M' : 'L'}${px(p.t).toFixed(1)},${pySoc(p.soc ?? 0).toFixed(1)}`
        )
        .join(' ');
    }

    maxKwLabel = `${maxKw.toFixed(1)}`;
    endTimeLabel = formatTime(last.t, maxT);

    // Intermediate Y-axis positions (1/3 and 2/3 of maxKw)
    midKwY1     = pyKw(maxKw / 3);
    midKwY2     = pyKw(maxKw * 2 / 3);
    midKwLabel1 = (maxKw / 3).toFixed(1);
    midKwLabel2 = (maxKw * 2 / 3).toFixed(1);

    // Intermediate X-axis positions (1/3 and 2/3 of maxT)
    midTx1     = px(maxT / 3);
    midTx2     = px(maxT * 2 / 3);
    midTLabel1 = formatTime(maxT / 3, maxT);
    midTLabel2 = formatTime(maxT * 2 / 3, maxT);
  }

  // ── Render ────────────────────────────────────────────────────────────────
  return (
    <View style={styles.root} onLayout={onLayout}>

      {/* ── SVG chart area ────────────────────────────────────────────────── */}
      <View style={{ height }}>
        {isEmpty ? (
          <View style={styles.placeholder}>
            <Text style={[styles.placeholderText, { color: colors.textMuted }]}>
              {t('activeSession.collectingData')}
            </Text>
          </View>
        ) : (
          <Svg width={width} height={height}>
            {/* Left Y-axis (power) */}
            <Line
              x1={pad.left} y1={pad.top}
              x2={pad.left} y2={baseY}
              stroke={colors.border} strokeWidth={1}
            />
            {/* X-axis baseline */}
            <Line
              x1={pad.left} y1={baseY}
              x2={width - pad.right} y2={baseY}
              stroke={colors.border} strokeWidth={1}
            />
            {/* Right Y-axis (SoC) — only when SoC data present */}
            {hasSoc && (
              <Line
                x1={width - pad.right} y1={pad.top}
                x2={width - pad.right} y2={baseY}
                stroke={colors.border} strokeWidth={1}
              />
            )}

            {/* Intermediate horizontal grid lines (behind data) */}
            <Line
              x1={pad.left} y1={midKwY1}
              x2={width - pad.right} y2={midKwY1}
              stroke={colors.border} strokeWidth={0.5} strokeDasharray="3,4"
            />
            <Line
              x1={pad.left} y1={midKwY2}
              x2={width - pad.right} y2={midKwY2}
              stroke={colors.border} strokeWidth={0.5} strokeDasharray="3,4"
            />

            {/* Power: filled area */}
            <Path d={powerArea} fill={colors.primary} opacity={0.12} />
            {/* Power: solid green line */}
            <Path
              d={powerLine}
              stroke={colors.primary} strokeWidth={2}
              fill="none" strokeLinecap="round" strokeLinejoin="round"
            />

            {/* SoC: solid blue line */}
            {hasSoc && (
              <Path
                d={socPath}
                stroke={SOC_COLOR} strokeWidth={2}
                fill="none" strokeLinecap="round" strokeLinejoin="round"
              />
            )}

            {/* Last-point dot (power) */}
            <Circle cx={lastPx} cy={lastPy} r={3.5} fill={colors.primary} />

            {/* Left Y-axis labels (kW): max, 2/3, 1/3, 0 */}
            <SvgText
              x={pad.left - 4} y={pad.top + 6}
              fontSize={9} fill={colors.textMuted} textAnchor="end"
            >{maxKwLabel}</SvgText>
            <SvgText
              x={pad.left - 4} y={midKwY2 + 4}
              fontSize={9} fill={colors.textMuted} textAnchor="end"
            >{midKwLabel2}</SvgText>
            <SvgText
              x={pad.left - 4} y={midKwY1 + 4}
              fontSize={9} fill={colors.textMuted} textAnchor="end"
            >{midKwLabel1}</SvgText>
            <SvgText
              x={pad.left - 4} y={baseY}
              fontSize={9} fill={colors.textMuted} textAnchor="end"
            >0</SvgText>

            {/* Right Y-axis labels (%) — only when SoC data present */}
            {hasSoc && <>
              <SvgText
                x={width - pad.right + 4} y={pad.top + 6}
                fontSize={9} fill={SOC_COLOR} textAnchor="start"
              >100%</SvgText>
              <SvgText
                x={width - pad.right + 4} y={baseY}
                fontSize={9} fill={SOC_COLOR} textAnchor="start"
              >0%</SvgText>
            </>}

            {/* X-axis labels: start, 1/3, 2/3, end */}
            <SvgText
              x={pad.left} y={height - 4}
              fontSize={9} fill={colors.textMuted} textAnchor="middle"
            >0:00</SvgText>
            <SvgText
              x={midTx1} y={height - 4}
              fontSize={9} fill={colors.textMuted} textAnchor="middle"
            >{midTLabel1}</SvgText>
            <SvgText
              x={midTx2} y={height - 4}
              fontSize={9} fill={colors.textMuted} textAnchor="middle"
            >{midTLabel2}</SvgText>
            <SvgText
              x={width - pad.right} y={height - 4}
              fontSize={9} fill={colors.textMuted} textAnchor="end"
            >{endTimeLabel}</SvgText>
          </Svg>
        )}
      </View>

      {/* ── Legend row ────────────────────────────────────────────────────── */}
      {!isEmpty && (
        <View style={styles.legend}>
          <View style={styles.legendItem}>
            <View style={[styles.legendSwatch, { backgroundColor: colors.primary }]} />
            <Text style={[styles.legendLabel, { color: colors.textMuted }]}>
              {t('activeSession.legendPower')}
            </Text>
          </View>
          {hasSoc && (
            <View style={styles.legendItem}>
              <View style={[styles.legendSwatch, { backgroundColor: SOC_COLOR }]} />
              <Text style={[styles.legendLabel, { color: colors.textMuted }]}>
                {t('activeSession.legendSoc')}
              </Text>
            </View>
          )}
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  root:           { width: '100%' },
  placeholder:    { flex: 1, justifyContent: 'center', alignItems: 'center' },
  placeholderText:{ fontSize: 13 },
  legend:         { flexDirection: 'row', gap: 16, marginTop: 8 },
  legendItem:     { flexDirection: 'row', alignItems: 'center', gap: 5 },
  legendSwatch:   { width: 10, height: 10, borderRadius: 2 },
  legendLabel:    { fontSize: 11 },
});
