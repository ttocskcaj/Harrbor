import { PhaseChip } from '@/components/ui/PhaseChip'
import { ProgressBar } from '@/components/ui/ProgressBar'
import { StatusDot } from '@/components/ui/StatusDot'
import type { ReleaseListItem } from '@/api/types'
import { deriveReleaseView, toChipPhase } from '@/lib/release'
import { formatDuration } from '@/lib/format'

/**
 * One release row, shared by the List-mode table and the Rail-mode phase drill.
 * The two differ only in columns: List carries a Phase chip; the drill omits it
 * (the phase is implied by the selected node) and reorders Progress ahead of it.
 */
export interface ReleaseRowProps {
  item: ReleaseListItem
  /** @default 'list' */
  variant?: 'list' | 'drill'
}

const LIST_COLS = '2.4fr 1fr 1.1fr 0.8fr 1.3fr 1.1fr'
const DRILL_COLS = '2.4fr 1fr 1.4fr 0.8fr 1.2fr'

export function ReleaseRow({ item, variant = 'list' }: ReleaseRowProps) {
  const v = deriveReleaseView(item)

  const name = (
    <span style={{
      font: 'var(--fw-medium) var(--text-sm) var(--font-mono)', color: 'var(--text-body)',
      overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
    }}>{item.name}</span>
  )
  const job = <span style={{ font: 'var(--text-sm) var(--font-mono)', color: 'var(--text-muted)' }}>{item.jobName}</span>
  const inPhase = <span style={{ font: 'var(--text-sm) var(--font-mono)', color: 'var(--text-muted)' }}>{formatDuration(item.timeInPhaseSeconds)}</span>
  const phaseChip = <span><PhaseChip phase={toChipPhase(item.currentPhase)} size="sm" /></span>
  const progress = (
    <span style={{ paddingRight: '14px' }}>
      {v.hasProgress
        ? <ProgressBar value={v.progress ?? 0} tone={v.progressTone} striped={v.active} height={6} />
        : <span style={{ font: 'var(--text-sm) var(--font-mono)', color: 'var(--text-faint)' }}>—</span>}
    </span>
  )
  const status = (
    <span style={{ display: 'flex', alignItems: 'center', gap: '7px', font: 'var(--text-sm) var(--font-mono)', color: v.statusColor }}>
      <StatusDot status={v.statusDot} size={7} />
      {v.statusLabel}
    </span>
  )

  return (
    <div style={{
      display: 'grid', gridTemplateColumns: variant === 'drill' ? DRILL_COLS : LIST_COLS,
      alignItems: 'center', padding: '11px 16px', borderTop: '1px solid var(--border-subtle)',
      background: v.attention ? 'color-mix(in srgb, var(--red-500) 7%, transparent)' : 'transparent',
    }}>
      {variant === 'drill' ? (
        <>{name}{job}{progress}{inPhase}{status}</>
      ) : (
        <>{name}{job}{phaseChip}{inPhase}{progress}{status}</>
      )}
    </div>
  )
}
