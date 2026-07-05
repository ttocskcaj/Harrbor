import type { Phase } from '@/components/ui/PhaseChip'
import type { ReleaseListItem, ReleasePhase } from '@/api/types'
import { num, formatSpeed } from './format'

/** The six pipeline phases, in flow order (excludes the terminal `done`). */
export const PHASE_ORDER: Phase[] = ['discover', 'download', 'transfer', 'import', 'cleanup', 'archival']

export const PHASE_LABEL: Record<Phase, string> = {
  discover: 'Discover', download: 'Download', transfer: 'Transfer',
  import: 'Import', cleanup: 'Cleanup', archival: 'Archival',
}

/** The mock's status-dot kinds. */
export type StatusDotKind = 'ok' | 'active' | 'warn' | 'error' | 'idle'

const STATUS_COLOR: Record<StatusDotKind, string> = {
  error: 'var(--red-300)', warn: 'var(--amber-300)', active: 'var(--teal-300)',
  ok: 'var(--green-300)', idle: 'var(--text-muted)',
}

const PROGRESS_TONE: Record<StatusDotKind, 'accent' | 'active' | 'ok' | 'warn' | 'error'> = {
  error: 'error', warn: 'warn', active: 'active', ok: 'ok', idle: 'accent',
}

/** A `PhaseChip`-safe phase (maps the terminal `done` onto `archival`). */
export function toChipPhase(phase: ReleasePhase | null | undefined): Phase {
  if (phase && phase !== 'done' && PHASE_ORDER.includes(phase)) return phase
  return 'archival'
}

export interface ReleaseView {
  statusDot: StatusDotKind
  statusLabel: string
  statusColor: string
  progressTone: 'accent' | 'active' | 'ok' | 'warn' | 'error'
  /** 0–100, or null when the phase exposes no progress. */
  progress: number | null
  hasProgress: boolean
  /** Animate the progress bar (in-flight transfer). */
  active: boolean
  /** Red-tint the row (needs attention). */
  attention: boolean
}

/** Map a raw release row to the mock's presentation vocabulary. */
export function deriveReleaseView(item: ReleaseListItem): ReleaseView {
  const status = item.status
  const phase = item.currentPhase

  const progress = item.transferProgress != null ? Math.round(num(item.transferProgress) * 100) : null

  const statusDot = deriveDot(status, phase)
  return {
    statusDot,
    statusLabel: deriveLabel(item),
    statusColor: STATUS_COLOR[statusDot],
    progressTone: PROGRESS_TONE[statusDot],
    progress,
    hasProgress: progress != null,
    active: statusDot === 'active',
    attention: status === 'stuck' || status === 'failed',
  }
}

function deriveDot(status: ReleaseListItem['status'], phase: ReleaseListItem['currentPhase']): StatusDotKind {
  switch (status) {
    case 'failed':
    case 'stuck':
      return 'error'
    case 'retrying':
      return 'warn'
    case 'cancelled':
      return 'idle'
    case 'done':
      return 'ok'
    case 'flowing':
    default:
      switch (phase) {
        case 'transfer':
        case 'import':
          return 'active'
        case 'cleanup':
        case 'archival':
        case 'done':
          return 'ok'
        default: // discover, download — waiting/pulling
          return 'idle'
      }
  }
}

function deriveLabel(item: ReleaseListItem): string {
  const { status, currentPhase: phase } = item
  const phaseName = phase && phase !== 'done' ? PHASE_LABEL[phase as Phase] : 'Pipeline'

  if (status === 'failed') return `${phaseName} failed`
  if (status === 'stuck') {
    return phase === 'transfer'
      ? `Becalmed · ${formatSpeed(item.transferSpeedBytesPerSec ?? 0)}`
      : `Stuck in ${phaseName.toLowerCase()}`
  }
  if (status === 'retrying') {
    const n = num(item.errorCount)
    const max = item.maxRetries != null ? num(item.maxRetries) : null
    return max ? `Retrying ${Math.min(n + 1, max)}/${max}` : 'Retrying'
  }
  if (status === 'cancelled') return 'Cancelled'
  if (status === 'done') return 'Done'

  // flowing
  switch (phase) {
    case 'discover': return 'Queued'
    case 'download': return 'Downloading'
    case 'transfer': {
      const pct = item.transferProgress != null ? Math.round(num(item.transferProgress) * 100) : null
      const spd = item.transferSpeedBytesPerSec != null ? formatSpeed(item.transferSpeedBytesPerSec) : null
      if (pct != null && spd) return `${pct}% · ${spd}`
      if (pct != null) return `${pct}%`
      return 'Transferring'
    }
    case 'import': return 'Importing'
    case 'cleanup': return 'Cleaning'
    case 'archival': return 'Seeding'
    default: return 'Flowing'
  }
}
