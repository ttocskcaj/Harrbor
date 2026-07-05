import React from 'react';

/**
 * ProgressBar — transfer/import progress track. Recessed well with a
 * teal fill; `striped` animates a flowing dash for in-flight transfers;
 * `tone` recolors (e.g. amber when stalled). Optional right-aligned value.
 */
export function ProgressBar({ value = 0, tone = 'accent', striped = false, height = 6, label, style = {} }) {
  const v = Math.max(0, Math.min(100, value));
  const fills = {
    accent: 'var(--accent)', ok: 'var(--green-500)', active: 'var(--status-active)',
    warn: 'var(--amber-500)', error: 'var(--red-500)',
  };
  const fill = fills[tone] || fills.accent;
  return (
    <div style={{ ...style }}>
      {label && (
        <div style={{
          display: 'flex', justifyContent: 'space-between', marginBottom: '5px',
          font: 'var(--text-xs) var(--font-mono)', color: 'var(--text-muted)',
        }}>
          <span>{label}</span><span style={{ color: 'var(--text-body)' }}>{v}%</span>
        </div>
      )}
      <div style={{
        height, background: 'var(--surface-inset)', borderRadius: 'var(--radius-pill)',
        boxShadow: 'var(--inset-well)', overflow: 'hidden',
      }}>
        <div style={{
          width: `${v}%`, height: '100%', borderRadius: 'var(--radius-pill)',
          background: striped
            ? `repeating-linear-gradient(-45deg, ${fill} 0 8px, color-mix(in oklab, ${fill}, black 18%) 8px 16px)`
            : fill,
          backgroundSize: striped ? '24px 24px' : 'auto',
          animation: striped ? 'flow-dash 0.7s linear infinite' : 'none',
          transition: 'width var(--dur-slow) var(--ease-out)',
        }} />
      </div>
    </div>
  );
}
