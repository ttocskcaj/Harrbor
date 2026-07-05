import { useMemo, useState } from 'react'
import { Badge } from '@/components/ui/Badge'
import { Select, type SelectOption } from '@/components/ui/Select'
import { ReleaseRow } from './ReleaseRow'
import type { ReleaseListItem } from '@/api/types'
import { PHASE_ORDER, PHASE_LABEL, deriveReleaseView } from '@/lib/release'

export interface ListViewProps {
  releases: ReleaseListItem[]
}

const LIST_COLS = '2.4fr 1fr 1.1fr 0.8fr 1.3fr 1.1fr'
const headerCellStyle = {
  font: 'var(--fw-semibold) var(--text-2xs) var(--font-sans)', letterSpacing: 'var(--tracking-caps)',
  textTransform: 'uppercase' as const, color: 'var(--text-faint)',
}

export function ListView({ releases }: ListViewProps) {
  const [jobFilter, setJobFilter] = useState('all')
  const [phaseFilter, setPhaseFilter] = useState('all')

  const jobOptions: SelectOption[] = useMemo(() => {
    const names = Array.from(new Set(releases.map((r) => r.jobName).filter((n): n is string => !!n)))
    return [{ value: 'all', label: 'All jobs' }, ...names.map((n) => ({ value: n, label: n }))]
  }, [releases])

  const phaseOptions: SelectOption[] = [
    { value: 'all', label: 'All phases' },
    ...PHASE_ORDER.map((p) => ({ value: p, label: PHASE_LABEL[p] })),
  ]

  const filtered = releases.filter(
    (r) => (jobFilter === 'all' || r.jobName === jobFilter) && (phaseFilter === 'all' || r.currentPhase === phaseFilter),
  )

  const stuck = releases.filter((r) => deriveReleaseView(r).attention).length
  const inFlight = releases.filter((r) => deriveReleaseView(r).active).length

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
        <div style={{ width: 200 }}>
          <Select size="sm" options={jobOptions} value={jobFilter} onChange={(e) => setJobFilter(e.target.value)} />
        </div>
        <div style={{ width: 170 }}>
          <Select size="sm" options={phaseOptions} value={phaseFilter} onChange={(e) => setPhaseFilter(e.target.value)} />
        </div>
        {stuck > 0 && <Badge tone="warn" dot>{stuck} stuck</Badge>}
        <Badge tone="active" dot>{inFlight} in flight</Badge>
        <span style={{ marginLeft: 'auto', font: 'var(--text-xs) var(--font-mono)', color: 'var(--text-faint)' }}>
          showing {filtered.length} of {releases.length}
        </span>
      </div>

      <div style={{
        background: 'var(--surface-panel)', border: '1px solid var(--border-subtle)',
        borderRadius: 'var(--radius-lg)', overflow: 'hidden', boxShadow: 'var(--shadow-md), var(--edge-light)',
      }}>
        <div style={{ display: 'grid', gridTemplateColumns: LIST_COLS, padding: '10px 16px', borderBottom: '1px solid var(--border-subtle)', ...headerCellStyle }}>
          <span>Release</span><span>Job</span><span>Phase</span><span>In phase</span><span>Progress</span><span>Status</span>
        </div>
        {filtered.length === 0 ? (
          <div style={{ padding: '20px 16px', font: 'var(--text-sm) var(--font-mono)', color: 'var(--text-faint)' }}>
            No releases match the current filters.
          </div>
        ) : (
          filtered.map((r) => <ReleaseRow key={String(r.id)} item={r} variant="list" />)
        )}
      </div>
    </div>
  )
}
