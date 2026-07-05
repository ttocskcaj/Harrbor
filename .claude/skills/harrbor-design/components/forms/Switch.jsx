import React from 'react';

/**
 * Switch — on/off toggle. Teal when on, recessed track when off.
 * Controlled via `checked` + `onChange`.
 */
export function Switch({ checked = false, onChange, disabled = false, label, style = {} }) {
  const toggle = () => { if (!disabled && onChange) onChange(!checked); };
  return (
    <span
      role="switch"
      aria-checked={checked}
      aria-label={label}
      onClick={toggle}
      style={{
        display: 'inline-flex', alignItems: 'center', gap: '9px',
        cursor: disabled ? 'not-allowed' : 'pointer', opacity: disabled ? 0.5 : 1,
        userSelect: 'none', ...style,
      }}
    >
      <span style={{
        position: 'relative', width: 34, height: 19, flexShrink: 0,
        borderRadius: 'var(--radius-pill)',
        background: checked ? 'var(--accent)' : 'var(--surface-inset)',
        border: `1px solid ${checked ? 'var(--amber-400)' : 'var(--border-strong)'}`,
        boxShadow: checked ? '0 0 0 3px var(--accent-glow)' : 'var(--inset-well)',
        transition: 'background var(--dur-med) var(--ease-out), box-shadow var(--dur-med)',
      }}>
        <span style={{
          position: 'absolute', top: 2, left: checked ? 16 : 2, width: 13, height: 13,
          borderRadius: '50%', background: checked ? 'var(--text-invert)' : 'var(--slate-400)',
          transition: 'left var(--dur-med) var(--ease-out), background var(--dur-med)',
          boxShadow: 'var(--shadow-sm)',
        }} />
      </span>
      {label && <span style={{ font: 'var(--text-base) var(--font-sans)', color: 'var(--text-body)' }}>{label}</span>}
    </span>
  );
}
