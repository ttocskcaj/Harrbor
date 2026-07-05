import React from 'react';
import { StatusDot } from '../display/StatusDot.jsx';

/**
 * ConnectionRow — one service in the connection-health panel
 * (qBittorrent, Sonarr, Radarr, SFTP). Status dot, name, host in mono,
 * and a right-aligned detail (latency, version, or error).
 * status: ok | warn | error | idle
 */
export function ConnectionRow({ name, host, status = 'ok', detail, kind, style = {} }) {
  const statusLabel = { ok: 'Connected', warn: 'Degraded', error: 'Offline', idle: 'Not set' }[status];
  const detailColor = { ok: 'var(--text-muted)', warn: 'var(--amber-300)', error: 'var(--red-300)', idle: 'var(--text-faint)' }[status];
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: '11px',
      padding: '11px 13px', borderRadius: 'var(--radius-sm)',
      background: 'var(--surface-panel)', border: '1px solid var(--border-subtle)', ...style,
    }}>
      <StatusDot status={status} pulse={status === 'ok'} size={9} />
      <div style={{ minWidth: 0, flex: 1 }}>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px' }}>
          <span style={{ font: `var(--fw-semibold) var(--text-base) var(--font-sans)`, color: 'var(--text-heading)' }}>{name}</span>
          {kind && <span style={{ font: 'var(--text-2xs) var(--font-sans)', color: 'var(--text-faint)', letterSpacing: 'var(--tracking-caps)', textTransform: 'uppercase' }}>{kind}</span>}
        </div>
        {host && <div style={{ font: 'var(--text-xs) var(--font-mono)', color: 'var(--text-faint)', marginTop: '2px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{host}</div>}
      </div>
      <div style={{ textAlign: 'right', flexShrink: 0 }}>
        <div style={{ font: `var(--fw-medium) var(--text-xs) var(--font-sans)`, color: detailColor }}>{statusLabel}</div>
        {detail && <div style={{ font: 'var(--text-2xs) var(--font-mono)', color: 'var(--text-faint)', marginTop: '2px' }}>{detail}</div>}
      </div>
    </div>
  );
}
