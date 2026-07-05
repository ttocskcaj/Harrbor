import type { ReactNode } from 'react'
import { $api } from '@/api/query'
import { StatusDot } from '@/components/ui/StatusDot'
import { num, formatSpeed } from '@/lib/format'

/**
 * AppShell — the Harrbor chrome: fixed sidebar (brand mark with beacon halo,
 * nav, live telemetry footer) + topbar (title / subtitle / right slot) + a
 * scrolling content well with the amber harbor-floor wash.
 *
 * Ported from the harrbor-design skill's ui_kits/harrbor-app/AppShell.jsx and
 * wired to live `/api/dashboard` data for the sidebar telemetry.
 */

type NavId = 'operate' | 'jobs' | 'connections' | 'history' | 'logs'

const NAV: { id: NavId; label: string; icon: string }[] = [
  { id: 'operate', label: 'Operate', icon: '◵' },
  { id: 'jobs', label: 'Jobs', icon: '≡' },
  { id: 'connections', label: 'Connections', icon: '⚙' },
  { id: 'history', label: 'History', icon: '◴' },
  { id: 'logs', label: 'Logs', icon: '▤' },
]

// Live ticking telemetry — throughput + in-flight, straight off the dashboard.
function Telemetry() {
  const { data, isError, isPending } = $api.useQuery('get', '/api/dashboard')
  const tp = data?.throughput
  const status = isError ? 'error' : isPending ? 'idle' : 'ok'

  return (
    <>
      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '9px' }}>
        <StatusDot status={status} pulse={status === 'ok'} size={8} />
        <span style={{ font: 'var(--text-xs) var(--font-sans)', color: 'var(--text-muted)' }}>
          {isError ? 'Harbormaster offline' : 'Harbormaster online'}
        </span>
      </div>
      <div style={{ font: 'var(--text-2xs) var(--font-mono)', color: 'var(--text-faint)', lineHeight: 1.6, fontVariantNumeric: 'tabular-nums' }}>
        <div>
          <span style={{ color: 'var(--status-active)' }}>↑</span>{' '}
          {formatSpeed(tp?.aggregateSpeedBytesPerSec)} · {num(tp?.transfersInProgress)} in flight
        </div>
        <div>{num(tp?.completedLast24h)} done · 24h · {num(tp?.completedLast7d)} · 7d</div>
      </div>
    </>
  )
}

function Sidebar({ current }: { current: NavId }) {
  return (
    <aside style={{
      width: 'var(--sidebar-w)', flexShrink: 0, background: 'var(--bg-app)',
      borderRight: '1px solid var(--border-subtle)', display: 'flex', flexDirection: 'column',
      padding: '0 0 12px',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '10px', height: 'var(--topbar-h)', padding: '0 16px', borderBottom: '1px solid var(--border-subtle)' }}>
        <span style={{ position: 'relative', display: 'grid', placeItems: 'center', width: 28, height: 28 }}>
          <span aria-hidden style={{ position: 'absolute', width: 34, height: 34, borderRadius: '50%', background: 'var(--harbor-bloom)', animation: 'beacon-breath 3.6s var(--ease-inout) infinite' }} />
          <img src="/harrbor-mark.svg" width={28} height={28} alt="" style={{ position: 'relative' }} />
        </span>
        <span style={{ font: 'var(--fw-semibold) var(--text-lg) var(--font-sans)', color: 'var(--text-heading)', letterSpacing: 'var(--tracking-tight)' }}>Harrbor</span>
      </div>
      <nav style={{ padding: '12px 10px', display: 'flex', flexDirection: 'column', gap: '2px' }}>
        {NAV.map((n) => {
          const active = current === n.id
          return (
            <button key={n.id} type="button" disabled={!active} style={{
              display: 'flex', alignItems: 'center', gap: '11px', padding: '0 12px', height: 34,
              borderRadius: 'var(--radius-sm)', border: '1px solid transparent',
              background: active ? 'var(--accent-glow)' : 'transparent',
              color: active ? 'var(--accent-text)' : 'var(--text-muted)',
              font: `${active ? 'var(--fw-semibold)' : 'var(--fw-medium)'} var(--text-base) var(--font-sans)`,
              cursor: active ? 'default' : 'not-allowed', textAlign: 'left', width: '100%',
              boxShadow: active ? 'inset 2px 0 0 var(--accent)' : 'none',
              opacity: active ? 1 : 0.55,
            }}
              onMouseEnter={(e) => { if (!active) e.currentTarget.style.background = 'var(--surface-hover)' }}
              onMouseLeave={(e) => { if (!active) e.currentTarget.style.background = 'transparent' }}
            >
              <span style={{ width: 16, textAlign: 'center', fontSize: '14px' }}>{n.icon}</span>{n.label}
            </button>
          )
        })}
      </nav>
      <div style={{ marginTop: 'auto', padding: '12px 16px', borderTop: '1px solid var(--border-subtle)' }}>
        <Telemetry />
      </div>
    </aside>
  )
}

function Topbar({ title, subtitle, right }: { title: string; subtitle?: ReactNode; right?: ReactNode }) {
  return (
    <header style={{
      height: 'var(--topbar-h)', flexShrink: 0, display: 'flex', alignItems: 'center', gap: '14px',
      padding: '0 22px', borderBottom: '1px solid var(--border-subtle)', background: 'var(--bg-page)',
    }}>
      <div>
        <div style={{ font: 'var(--fw-semibold) var(--text-lg) var(--font-sans)', color: 'var(--text-heading)' }}>{title}</div>
        {subtitle && <div style={{ font: 'var(--text-2xs) var(--font-mono)', color: 'var(--text-faint)', marginTop: '1px' }}>{subtitle}</div>}
      </div>
      <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: '10px' }}>{right}</div>
    </header>
  )
}

export interface AppShellProps {
  /** @default 'operate' */
  current?: NavId
  title: string
  subtitle?: ReactNode
  topRight?: ReactNode
  children?: ReactNode
}

export function AppShell({ current = 'operate', title, subtitle, topRight, children }: AppShellProps) {
  return (
    <div style={{ display: 'flex', height: '100vh', overflow: 'hidden', background: 'var(--bg-page)', color: 'var(--text-body)', font: 'var(--type-body)' }}>
      <Sidebar current={current} />
      <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
        <Topbar title={title} subtitle={subtitle} right={topRight} />
        <main style={{ flex: 1, minHeight: 0, overflow: 'auto', padding: '22px', backgroundImage: 'var(--harbor-floor)', backgroundAttachment: 'local' }}>
          {children}
        </main>
      </div>
    </div>
  )
}
