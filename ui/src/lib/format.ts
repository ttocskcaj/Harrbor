/**
 * Formatting helpers for the machine-data (monospace) readouts. The backend
 * types numeric fields as `number | string` (openapi-typescript int handling),
 * so everything here coerces defensively through `Number(...)`.
 */

/** Coerce the `number | string | null | undefined` the schema emits to a number. */
export function num(v: number | string | null | undefined, fallback = 0): number {
  if (v == null) return fallback
  const n = typeof v === 'number' ? v : Number(v)
  return Number.isFinite(n) ? n : fallback
}

/** Compact duration: "8s" · "6m" · "2h 4m" · "3d 1h". */
export function formatDuration(seconds: number | string | null | undefined): string {
  const s = Math.max(0, Math.floor(num(seconds)))
  if (s < 60) return `${s}s`
  const m = Math.floor(s / 60)
  if (m < 60) return `${m}m`
  const h = Math.floor(m / 60)
  if (h < 24) {
    const rm = m % 60
    return rm ? `${h}h ${rm}m` : `${h}h`
  }
  const d = Math.floor(h / 24)
  const rh = h % 24
  return rh ? `${d}d ${rh}h` : `${d}d`
}

/** Human byte size: "4.2 MB", "21 GB". */
export function formatBytes(bytes: number | string | null | undefined): string {
  let n = num(bytes)
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  let i = 0
  while (n >= 1024 && i < units.length - 1) { n /= 1024; i++ }
  const digits = n >= 100 || i === 0 ? 0 : 1
  return `${n.toFixed(digits)} ${units[i]}`
}

/** Transfer speed: "4.2 MB/s" (0 renders "0 B/s"). */
export function formatSpeed(bytesPerSec: number | string | null | undefined): string {
  return `${formatBytes(bytesPerSec)}/s`
}

/** ETA in the mock's terse form: "6m", "2m", "—" when unknown/zero. */
export function formatEta(seconds: number | string | null | undefined): string {
  const s = num(seconds, -1)
  if (s < 0) return '—'
  return formatDuration(s)
}

/** Relative age from an ISO timestamp: "6m ago", "just now". */
export function formatRelativeTime(iso: string | null | undefined, now: number = Date.now()): string {
  if (!iso) return '—'
  const then = Date.parse(iso)
  if (Number.isNaN(then)) return '—'
  const diff = Math.max(0, Math.floor((now - then) / 1000))
  if (diff < 10) return 'just now'
  return `${formatDuration(diff)} ago`
}
