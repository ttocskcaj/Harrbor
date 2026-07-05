import React from 'react';

/**
 * MetricTile — a single number on the dashboard: a phase count, a
 * queue depth, a throughput figure. Big mono-ish number, small label,
 * optional trend/delta and accent bar.
 */
export function MetricTile({ label, value, unit, delta, tone = 'neutral', accentBar = false, style = {} }) {
  const tones = {
    neutral: 'var(--text-heading)', accent: 'var(--accent-text)',
    ok: 'var(--green-300)', warn: 'var(--amber-300)', error: 'var(--red-300)',
  };
  const bar = {
    neutral: 'var(--border-strong)', accent: 'var(--accent)',
    ok: 'var(--green-500)', warn: 'var(--amber-500)', error: 'var(--red-500)',
  };
  const deltaUp = delta && delta.startsWith('+');
  return (
    <div style={{
      position: 'relative', padding: '14px 16px', background: 'var(--surface-card)',
      border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-md)',
      boxShadow: 'var(--edge-light)', overflow: 'hidden', ...style,
    }}>
      {accentBar && <span style={{ position: 'absolute', left: 0, top: 0, bottom: 0, width: 3, background: bar[tone] }} />}
      <div style={{
        font: 'var(--type-eyebrow)', color: 'var(--text-muted)',
        letterSpacing: 'var(--tracking-caps)', textTransform: 'uppercase', marginBottom: '8px',
      }}>{label}</div>
      <div style={{ display: 'flex', alignItems: 'baseline', gap: '6px' }}>
        <span style={{ font: `var(--fw-semibold) var(--text-3xl)/1 var(--font-sans)`, color: tones[tone], letterSpacing: 'var(--tracking-tight)', fontVariantNumeric: 'tabular-nums' }}>{value}</span>
        {unit && <span style={{ font: 'var(--text-sm) var(--font-mono)', color: 'var(--text-faint)' }}>{unit}</span>}
        {delta && (
          <span style={{
            marginLeft: 'auto', font: 'var(--text-xs) var(--font-mono)',
            color: deltaUp ? 'var(--green-300)' : 'var(--text-muted)',
          }}>{delta}</span>
        )}
      </div>
    </div>
  );
}
