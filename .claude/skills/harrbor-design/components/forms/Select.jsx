import React from 'react';

/**
 * Select — native dropdown styled to match the inset-well controls.
 * Pass options as [{value, label}] or children <option>s.
 */
export function Select({
  label,
  hint,
  options = [],
  size = 'md',
  style = {},
  id,
  children,
  ...rest
}) {
  const heights = { sm: 'var(--control-sm)', md: 'var(--control-md)', lg: 'var(--control-lg)' };
  const fid = id || (label ? `sel-${label.replace(/\s+/g, '-').toLowerCase()}` : undefined);
  return (
    <label htmlFor={fid} style={{ display: 'block', ...style }}>
      {label && (
        <span style={{
          display: 'block', font: 'var(--type-label)', color: 'var(--text-muted)',
          marginBottom: '6px', letterSpacing: 'var(--tracking-wide)',
        }}>{label}</span>
      )}
      <div style={{ position: 'relative' }}>
        <select
          id={fid}
          style={{
            width: '100%', height: heights[size], padding: '0 32px 0 11px',
            background: 'var(--surface-inset)', border: '1px solid var(--border-strong)',
            borderRadius: 'var(--radius-sm)', boxShadow: 'var(--inset-well)',
            color: 'var(--text-heading)', font: `var(--text-base) var(--font-sans)`,
            outline: 'none', appearance: 'none', cursor: 'pointer',
          }}
          onFocus={(e) => { e.currentTarget.style.borderColor = 'var(--border-focus)'; e.currentTarget.style.boxShadow = 'var(--inset-well), 0 0 0 3px var(--accent-glow)'; }}
          onBlur={(e) => { e.currentTarget.style.borderColor = 'var(--border-strong)'; e.currentTarget.style.boxShadow = 'var(--inset-well)'; }}
          {...rest}
        >
          {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
          {children}
        </select>
        <span aria-hidden style={{
          position: 'absolute', right: '11px', top: '50%', transform: 'translateY(-50%)',
          pointerEvents: 'none', color: 'var(--text-faint)', fontSize: '10px',
        }}>▾</span>
      </div>
      {hint && (
        <span style={{ display: 'block', marginTop: '6px', font: 'var(--text-xs) var(--font-sans)', color: 'var(--text-faint)' }}>{hint}</span>
      )}
    </label>
  );
}
