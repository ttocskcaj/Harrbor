import React from 'react';
import { PhaseChip } from '../display/PhaseChip.jsx';
import { ProgressBar } from '../display/ProgressBar.jsx';
import { StatusDot } from '../display/StatusDot.jsx';

/**
 * ReleaseCard — one release as it sits in a pipeline column. Mono title
 * (the actual release name), job lane, size, and — when transferring —
 * a live progress track. `stuck` flags a stalled item with an amber edge.
 */
export function ReleaseCard({
  name, job, phase = 'transfer', size, progress = null,
  stuck = false, active = false, eta, style = {},
}) {
  return (
    <article style={{
      position: 'relative', padding: '10px 12px',
      background: 'var(--surface-panel)',
      border: `1px solid ${stuck ? 'var(--amber-500)' : 'var(--border-subtle)'}`,
      borderRadius: 'var(--radius-md)',
      boxShadow: stuck ? '0 0 0 3px var(--amber-glow)' : 'var(--edge-light)',
      cursor: 'pointer', transition: 'border-color var(--dur-fast), transform var(--dur-fast)',
      ...style,
    }}
      onMouseEnter={(e) => { if (!stuck) e.currentTarget.style.borderColor = 'var(--border-focus)'; e.currentTarget.style.transform = 'translateY(-1px)'; }}
      onMouseLeave={(e) => { if (!stuck) e.currentTarget.style.borderColor = 'var(--border-subtle)'; e.currentTarget.style.transform = 'translateY(0)'; }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: '7px', marginBottom: '8px' }}>
        {active && <StatusDot status="active" pulse size={7} />}
        <span style={{
          font: 'var(--type-mono)', color: 'var(--text-heading)', whiteSpace: 'nowrap',
          overflow: 'hidden', textOverflow: 'ellipsis', flex: 1,
        }}>{name}</span>
        {stuck && <StatusDot status="warn" size={7} />}
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: progress != null ? '9px' : 0 }}>
        <PhaseChip phase={phase} size="sm" />
        <span style={{ marginLeft: 'auto', font: 'var(--text-2xs) var(--font-mono)', color: 'var(--text-faint)' }}>
          {job}{size ? ` · ${size}` : ''}
        </span>
      </div>
      {progress != null && (
        <ProgressBar value={progress} striped={active && !stuck} tone={stuck ? 'warn' : 'active'} height={5} />
      )}
      {eta && (
        <div style={{ marginTop: '6px', font: 'var(--text-2xs) var(--font-mono)', color: stuck ? 'var(--amber-300)' : 'var(--text-faint)' }}>
          {stuck ? '⚠ stalled' : `eta ${eta}`}
        </div>
      )}
    </article>
  );
}
