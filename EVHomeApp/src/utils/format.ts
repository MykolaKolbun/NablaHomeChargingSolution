/** "1:05:09" / "05:09" */
export function formatTimer(totalSec: number): string {
  const s = Math.max(0, Math.floor(totalSec));
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  const ss = s % 60;
  const mm = m.toString().padStart(2, '0');
  const sss = ss.toString().padStart(2, '0');
  return h > 0 ? `${h}:${mm}:${sss}` : `${mm}:${sss}`;
}

/** "2 h 15 min" style, compact: "2:15" hours/minutes or "15 min". */
export function formatDurationShort(totalSec: number): string {
  const m = Math.round(Math.max(0, totalSec) / 60);
  if (m < 60) return `${m} min`;
  return `${Math.floor(m / 60)}:${(m % 60).toString().padStart(2, '0')} h`;
}

export function secondsBetween(fromIso: string | null | undefined, to: number = Date.now()): number {
  if (!fromIso) return 0;
  return Math.max(0, (to - new Date(fromIso).getTime()) / 1000);
}

export const kwh = (v: number | null | undefined) => (v ?? 0).toFixed(2);
export const kw  = (v: number | null | undefined) => (v == null ? '—' : v.toFixed(1));
