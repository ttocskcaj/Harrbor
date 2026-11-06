import { useLayoutEffect, useRef, useState } from 'react'
import { Card } from '@/components/ui/Card'
import { Badge } from '@/components/ui/Badge'
import { FlowRail, type FlowRailPhase } from '@/components/ui/FlowRail'
import { PhaseChip, PHASES, type Phase } from '@/components/ui/PhaseChip'
import { IconButton } from '@/components/ui/IconButton'
import { AttentionItem } from '@/components/ui/AttentionItem'
import { ReleaseRow } from './ReleaseRow'
import type { DashboardResponse, ReleaseListItem, AttentionItem as AttentionItemData } from '@/api/types'
import { PHASE_ORDER, PHASE_LABEL, deriveReleaseView } from '@/lib/release'
import { num, formatDuration, formatRelativeTime } from '@/lib/format'

export interface RailViewProps {
  dashboard: DashboardResponse
  releases: ReleaseListItem[]
  activeTotal: number
  selectedPhase: Phase | null
  onSelectPhase: (p: Phase) => void
  onClearPhase: () => void
  onRetry: (id: number) => void
}

const DRILL_COLS = '2.4fr 1fr 1.4fr 0.8fr 1.2fr'
const headerCellStyle = {
  font: 'var(--fw-semibold) var(--text-2xs) var(--font-sans)', letterSpacing: 'var(--tracking-caps)',
  textTransform: 'uppercase' as const, color: 'var(--text-faint)',
}

export function RailView({ dashboard, releases, activeTotal, selectedPhase, onSelectPhase, onClearPhase, onRetry }: RailViewProps) {
  const railPhases = buildRailPhases(dashboard, releases)
  const drillHue = selectedPhase ? PHASES[selectedPhase].c : 'var(--border-subtle)'
  const panelBorder = selectedPhase ? `color-mix(in oklab, ${drillHue}, transparent 50%)` : 'var(--border-subtle)'

  // The FlowRail lays its nodes out on a CSS grid with auto-width columns, so
  // node centres are not evenly spaced — a formula can't predict them. Measure
  // the selected node's real position and point the drawer caret at it.
  const railRef = useRef<HTMLDivElement>(null)
  const drawerRef = useRef<HTMLDivElement>(null)
  const [caretLeft, setCaretLeft] = useState<number | null>(null)

  useLayoutEffect(() => {
    if (!selectedPhase) { setCaretLeft(null); return }
    const measure = () => {
      const rail = railRef.current
      const drawer = drawerRef.current
      if (!rail || !drawer) return
      const nodes = rail.querySelectorAll<HTMLElement>('button[title]')
      const node = nodes[PHASE_ORDER.indexOf(selectedPhase)]
      if (!node) return
      const nb = node.getBoundingClientRect()
      const db = drawer.getBoundingClientRect()
      setCaretLeft(nb.left + nb.width / 2 - db.left)
    }
    measure()
    const ro = new ResizeObserver(measure)
    if (railRef.current) ro.observe(railRef.current)
    return () => ro.disconnect()
  }, [selectedPhase])

  const drillReleases = selectedPhase
    ? releases
        .filter((r) => r.currentPhase === selectedPhase)
        .sort((a, b) => Number(deriveReleaseView(b).attention) - Number(deriveReleaseView(a).attention))
    : []

  const attention = dashboard.attention?.items ?? []
  const jobs = dashboard.jobs ?? []

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
      {/* flow-rail spine + phase drill = one continuous surface */}
      <div style={{
        background: 'var(--surface-card)', border: `1px solid ${panelBorder}`, borderRadius: 'var(--radius-lg)',
        overflow: 'hidden', boxShadow: 'var(--shadow-md), var(--edge-light)',
        transition: 'border-color var(--dur-med) var(--ease-out)',
      }}>
        <div style={{ padding: '15px 18px 2px' }}>
          <div style={{ font: 'var(--type-eyebrow)', letterSpacing: 'var(--tracking-caps)', textTransform: 'uppercase', color: 'var(--accent-text)' }}>Pipeline</div>
          <div style={{ font: 'var(--type-card)', color: 'var(--text-heading)', marginTop: '6px' }}>Current releases</div>
          <div style={{ font: 'var(--text-sm) var(--font-sans)', color: 'var(--text-muted)', marginTop: '3px' }}>Tap a phase to inspect its manifest</div>
        </div>
        <div ref={railRef} style={{ padding: '14px 14px 18px' }}>
          <FlowRail phases={railPhases} bloom="transfer" activePhase={selectedPhase} onSelect={onSelectPhase} />
        </div>

        {selectedPhase && (
          <div ref={drawerRef} style={{ position: 'relative', background: 'var(--surface-inset)', borderTop: `2px solid ${drillHue}` }}>
            {/* phase-hued caret tying the drawer up to the selected node */}
            {caretLeft != null && (
              <svg width="22" height="10" viewBox="0 0 22 10" style={{
                position: 'absolute', left: caretLeft, top: 0,
                transform: 'translate(-50%,-100%)', overflow: 'visible', pointerEvents: 'none',
              }}>
                <path d="M0 10 L11 0 L22 10 Z" fill={drillHue} />
              </svg>
            )}

            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', padding: '13px 16px 11px' }}>
              <PhaseChip phase={selectedPhase} active />
              <span style={{ font: 'var(--fw-semibold) var(--text-md) var(--font-sans)', color: 'var(--text-heading)' }}>{drillReleases.length} releases</span>
              <span style={{ font: 'var(--text-sm) var(--font-mono)', color: 'var(--text-faint)' }}>of {activeTotal} tracked</span>
              <span style={{ marginLeft: 'auto' }}>
                <IconButton label="Collapse" onClick={onClearPhase}>✕</IconButton>
              </span>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: DRILL_COLS, padding: '8px 16px', borderTop: '1px solid var(--border-subtle)', ...headerCellStyle }}>
              <span>Release</span><span>Job</span><span>Progress</span><span>In phase</span><span>Status</span>
            </div>
            {drillReleases.length === 0 ? (
              <div style={{ padding: '16px', font: 'var(--text-sm) var(--font-mono)', color: 'var(--text-faint)', borderTop: '1px solid var(--border-subtle)' }}>
                No releases in this phase.
              </div>
            ) : (
              drillReleases.map((r) => <ReleaseRow key={String(r.id)} item={r} variant="drill" />)
            )}
          </div>
        )}
      </div>

      {/* attention-first, full width */}
      <Card eyebrow="Needs attention" title="Failures & stalls" subtitle="Newest first"
        actions={<Badge tone="warn" dot>{num(dashboard.attention?.totalCount)} open</Badge>}>
        {attention.length === 0 ? (
          <span style={{ font: 'var(--text-sm) var(--font-mono)', color: 'var(--text-faint)' }}>Nothing needs attention.</span>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '9px' }}>
            {attention.map((a) => (
              <AttentionItem key={String(a.id)} {...toAttention(a)} onAction={() => onRetry(num(a.id))} />
            ))}
          </div>
        )}
      </Card>

      {/* jobs collapsed strip */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: '11px', padding: '12px 16px',
        background: 'var(--surface-panel)', border: '1px solid var(--border-subtle)',
        borderRadius: 'var(--radius-md)', boxShadow: 'var(--edge-light)',
      }}>
        <span style={{ color: 'var(--text-faint)', fontSize: '12px' }}>▸</span>
        <span style={{ font: 'var(--fw-medium) var(--text-base) var(--font-sans)', color: 'var(--text-body)' }}>Jobs</span>
        <Badge tone="ok" dot>{jobs.filter((j) => j.enabled).length} healthy</Badge>
        <span style={{ font: 'var(--text-sm) var(--font-mono)', color: 'var(--text-muted)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
          {jobsSummary(jobs)}
        </span>
      </div>
    </div>
  )
}

function buildRailPhases(dashboard: DashboardResponse, releases: ReleaseListItem[]): FlowRailPhase[] {
  const pipeline = dashboard.pipeline
  const perPhase = new Map<Phase, { active: number; stuck: number }>()
  PHASE_ORDER.forEach((p) => perPhase.set(p, { active: 0, stuck: 0 }))
  for (const r of releases) {
    const p = r.currentPhase
    if (!p || p === 'done' || !perPhase.has(p)) continue
    const s = perPhase.get(p)!
    const v = deriveReleaseView(r)
    if (v.attention) s.stuck++
    else if (r.status === 'flowing') s.active++
  }
  return PHASE_ORDER.map((p) => {
    const s = perPhase.get(p)!
    return {
      phase: p,
      count: num(pipeline?.[p]),
      active: s.active > 0,
      stuck: s.stuck > 0,
      stuckCount: s.stuck || undefined,
    }
  })
}

function toAttention(a: AttentionItemData) {
  const phaseName = a.currentPhase && a.currentPhase !== 'done' ? PHASE_LABEL[a.currentPhase as Phase] : 'Pipeline'
  const severity = a.status === 'retrying' ? 'warn' : 'error'
  const word = a.status === 'failed' ? 'failed' : a.status === 'stuck' ? 'stalled' : 'retrying'
  const message = a.lastError ? `${phaseName} ${word} — ${a.lastError}` : `${phaseName} ${word}`
  return {
    severity: severity as 'warn' | 'error',
    message,
    releaseId: a.name ?? `rel_${a.id}`,
    phase: phaseName.toLowerCase(),
    time: formatRelativeTime(a.lastErrorAtUtc),
    action: 'Retry',
  }
}

function jobsSummary(jobs: DashboardResponse['jobs']): string {
  if (!jobs || jobs.length === 0) return 'No jobs configured'
  const names = jobs.map((j) => j.name).filter(Boolean).join(' · ')
  const nextRuns = jobs
    .filter((j) => j.enabled && j.nextRunAtUtc)
    .map((j) => Date.parse(j.nextRunAtUtc as string))
    .filter((t) => !Number.isNaN(t))
  if (nextRuns.length === 0) return names
  const next = Math.max(0, Math.floor((Math.min(...nextRuns) - Date.now()) / 1000))
  return `${names} · next poll ${formatDuration(next)}`
}
