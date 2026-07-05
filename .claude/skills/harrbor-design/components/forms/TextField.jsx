import React from 'react';

/**
 * TextField — labelled text input with optional mono mode (for paths,
 * hosts, ports) and error / hint states. Recessed inset well styling.
 */
export function TextField({
  label,
  hint,
  error,
  mono = false,
  prefix = null,
  size = 'md',
  style = {},
  id,
  ...rest
}) {
  const heights = { sm: 'var(--control-sm)', md: 'var(--control-md)', lg: 'var(--control-lg)' };
  const fid = id || (label ? `tf-${label.replace(/\s+/g, '-').toLowerCase()}` : undefined);
  return (
    <label htmlFor={fid} style={{ display: 'block', ...style }}>
      {label && (
        <span style={{
          display: 'block', font: 'var(--type-label)', color: 'var(--text-muted)',
          marginBottom: '6px', letterSpacing: 'var(--tracking-wide)',
        }}>{label}</span>
      )}
      <div style={{
        display: 'flex', alignItems: 'center', height: heights[size],
        background: 'var(--surface-inset)',
        border: `1px solid ${error ? 'var(--red-500)' : 'var(--border-strong)'}`,
        borderRadius: 'var(--radius-sm)', boxShadow: 'var(--inset-well)',
        transition: 'border-color var(--dur-fast), box-shadow var(--dur-fast)',
        overflow: 'hidden',
      }}>
        {prefix && (
          <span style={{
            padding: '0 8px 0 10px', color: 'var(--text-faint)',
            font: 'var(--type-mono)', borderRight: '1px solid var(--border-subtle)',
            alignSelf: 'stretch', display: 'flex', alignItems: 'center',
          }}>{prefix}</span>
        )}
        <input
          id={fid}
          style={{
            flex: 1, minWidth: 0, height: '100%', padding: '0 11px',
            background: 'transparent', border: 'none', outline: 'none',
            color: 'var(--text-heading)',
            font: mono ? 'var(--type-mono)' : `var(--text-base) var(--font-sans)`,
          }}
          onFocus={(e) => {
            const box = e.currentTarget.parentElement;
            box.style.borderColor = 'var(--border-focus)';
            box.style.boxShadow = 'var(--inset-well), 0 0 0 3px var(--accent-glow)';
          }}
          onBlur={(e) => {
            const box = e.currentTarget.parentElement;
            box.style.borderColor = error ? 'var(--red-500)' : 'var(--border-strong)';
            box.style.boxShadow = 'var(--inset-well)';
          }}
          {...rest}
        />
      </div>
      {(hint || error) && (
        <span style={{
          display: 'block', marginTop: '6px', font: 'var(--text-xs) var(--font-sans)',
          color: error ? 'var(--red-300)' : 'var(--text-faint)',
        }}>{error || hint}</span>
      )}
    </label>
  );
}
