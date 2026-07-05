import { useState } from 'react'
import type { CSSProperties } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { $api } from '@/api/query'
import { AppShell } from '@/components/layout/AppShell'
import { ConnectionStrip } from '@/components/ui/ConnectionStrip'
import { Card } from '@/components/ui/Card'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import type { Phase } from '@/components/ui/PhaseChip'
import { RailView } from './operate/RailView'
import { ListView } from './operate/ListView'
import { PHASE_ORDER } from '@/lib/release'
import { num } from '@/lib/format'
import { errorMessage } from '@/lib/errorMessage'

type ViewMode = 'rail' | 'list'

/** Operate — the live pipeline-monitoring dashboard (mock: Operate.dc.html). */
export function Operate() {
  const dashQ = $api.useQuery('get', '/api/dashboard', {}, { refetchInterval: 3000 })
  const releasesQ = $api.useQuery(
    'get', '/api/releases',
    { params: { query: { state: 'active', pageSize: 200 } } },
    { refetchInterval: 3000 },
  )
  const queryClient = useQueryClient()
  const retry = $api.useMutation('post', '/api/releases/{id}/retry')

  const [viewMode, setViewMode] = useState<ViewMode>('rail')
  const [selectedPhase, setSelectedPhase] = useState<Phase | null>(null)

  const dashboard = dashQ.data
  const releases = releasesQ.data?.items ?? []
  const activeTotal = dashboard ? PHASE_ORDER.reduce((sum, p) => sum + num(dashboard.pipeline?.[p]), 0) : 0
  const attentionTotal = num(dashboard?.attention?.totalCount)

  const refreshAll = () => { void queryClient.invalidateQueries() }
  const onRetry = (id: number) => {
    if (!id) return
    retry.mutate({ params: { path: { id } }, body: {} }, { onSuccess: refreshAll })
  }
  const selectPhase = (p: Phase) => setSelectedPhase((cur) => (cur === p ? null : p))

  const subtitle = dashboard ? `${activeTotal} active · ${attentionTotal} need attention` : 'connecting…'

  const topRight = (
    <>
      <div style={{ display: 'flex', gap: '2px', padding: '3px', border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-md)', background: 'var(--surface-inset)' }}>
        <button type="button" onClick={() => setViewMode('rail')} style={seg(viewMode === 'rail')}>Rail</button>
        <button type="button" onClick={() => setViewMode('list')} style={seg(viewMode === 'list')}>List</button>
      </div>
      <Button size="sm" variant="secondary" iconLeft="↻" onClick={refreshAll}>Recheck</Button>
      <Badge tone="active" dot>auto 3s</Badge>
    </>
  )

  return (
    <AppShell current="operate" title="Operate" subtitle={subtitle} topRight={topRight}>
      <div style={{ maxWidth: 1180, display: 'flex', flexDirection: 'column', gap: '16px' }}>
        {dashQ.isError && (
          <Card title="Backend unreachable" actions={<Badge tone="error" dot>Offline</Badge>}>
            <span style={{ font: 'var(--type-mono)', color: 'var(--red-300)' }}>{errorMessage(dashQ.error)}</span>
          </Card>
        )}

        {!dashboard && !dashQ.isError && (
          <Card>
            <span style={{ font: 'var(--type-mono)', color: 'var(--text-muted)' }}>Loading pipeline…</span>
          </Card>
        )}

        {dashboard && (
          <>
            <ConnectionStrip connections={dashboard.connections ?? []} />
            {viewMode === 'rail' ? (
              <RailView
                dashboard={dashboard}
                releases={releases}
                activeTotal={activeTotal}
                selectedPhase={selectedPhase}
                onSelectPhase={selectPhase}
                onClearPhase={() => setSelectedPhase(null)}
                onRetry={onRetry}
              />
            ) : (
              <ListView releases={releases} />
            )}
          </>
        )}
      </div>
    </AppShell>
  )
}

function seg(active: boolean): CSSProperties {
  return {
    padding: '4px 13px', borderRadius: 'var(--radius-xs)', border: 'none', cursor: 'pointer',
    font: 'var(--fw-semibold) var(--text-xs) var(--font-sans)',
    background: active ? 'var(--accent-glow)' : 'transparent',
    color: active ? 'var(--accent-text)' : 'var(--text-muted)',
    boxShadow: active ? 'inset 0 0 0 1px var(--accent)' : 'none',
  }
}
