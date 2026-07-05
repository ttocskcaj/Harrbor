import { Fragment } from 'react'
import type { CSSProperties } from 'react'
import { StatusDot } from './StatusDot'
import type { ConnectionHealth, ConnectionHealthStatus } from '@/api/types'
import { num } from '@/lib/format'

/**
 * ConnectionStrip — the persistent, non-phase-scoped health bar that sits above
 * the pipeline. Horizontal equal-flex segments (one per configured connection)
 * with hairline dividers; a down segment gets a soft red wash. This is the
 * mock's inline strip promoted to a component; rows use `ConnectionRow` instead.
 */
export interface ConnectionStripProps {
  connections: ConnectionHealth[]
  style?: CSSProperties
}

type Kind = 'ok' | 'warn' | 'error' | 'idle'

const STATUS_KIND: Record<ConnectionHealthStatus, Kind> = {
  up: 'ok', degraded: 'warn', down: 'error', disabled: 'idle', unknown: 'idle',
}

const DETAIL_COLOR: Record<Kind, string> = {
  ok: 'var(--text-muted)', warn: 'var(--amber-300)', error: 'var(--red-300)', idle: 'var(--text-faint)',
}

function detailText(c: ConnectionHealth, kind: Kind): string {
  if (kind === 'error') return c.error ?? 'offline'
  if (kind === 'warn') {
    const latency = c.latencyMs != null ? ` · ${num(c.latencyMs)}ms` : ''
    return `degraded${latency}`
  }
  if (kind === 'idle') return c.status === 'disabled' ? 'disabled' : 'not checked'
  return c.latencyMs != null ? `${num(c.latencyMs)}ms` : 'connected'
}

export function ConnectionStrip({ connections, style = {} }: ConnectionStripProps) {
  return (
    <div style={{
      display: 'flex', alignItems: 'stretch', background: 'var(--surface-panel)',
      border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-md)',
      overflow: 'hidden', boxShadow: 'var(--edge-light)', ...style,
    }}>
      {connections.length === 0 && (
        <div style={{ flex: 1, padding: '10px 16px', font: 'var(--text-sm) var(--font-mono)', color: 'var(--text-faint)' }}>
          No connections configured
        </div>
      )}
      {connections.map((c, i) => {
        const kind = STATUS_KIND[c.status ?? 'unknown'] ?? 'idle'
        return (
          <Fragment key={String(c.id ?? i)}>
            {i > 0 && <div style={{ width: 1, background: 'var(--border-subtle)' }} />}
            <div style={{
              flex: 1, minWidth: 0, display: 'flex', alignItems: 'center', gap: '9px', padding: '10px 16px',
              background: kind === 'error' ? 'color-mix(in srgb, var(--red-500) 6%, transparent)' : 'transparent',
            }}>
              <StatusDot status={kind} size={9} />
              <span style={{
                font: 'var(--fw-medium) var(--text-base) var(--font-sans)', color: 'var(--text-heading)',
                overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
              }}>{c.name || c.providerKey || 'Unknown'}</span>
              <span style={{
                marginLeft: 'auto', font: 'var(--text-sm) var(--font-mono)', color: DETAIL_COLOR[kind],
                overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', paddingLeft: '8px',
              }}>{detailText(c, kind)}</span>
            </div>
          </Fragment>
        )
      })}
    </div>
  )
}
