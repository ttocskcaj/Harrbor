import React, { useState } from 'react';

/**
 * SecretField — masked input for API keys / passwords with a reveal
 * toggle. Shows a dotted mask by default; monospace when revealed so
 * the operator can eyeball the key. Optional "set" indicator.
 */
export function SecretField({
  label,
  hint,
  value,
  isSet = false,
  size = 'md',
  style = {},
  id,
  ...rest
}) {
  const [revealed, setRevealed] = useState(false);
  const heights = { sm: 'var(--control-sm)', md: 'var(--control-md)', lg: 'var(--control-lg)' };
  const fid = id || (label ? `sf-${label.replace(/\s+/g, '-').toLowerCase()}` : undefined);
  return (
    <label htmlFor={fid} style={{ display: 'block', ...style }}>
      {label && (
        <span style={{
          display: 'flex', justifyContent: 'space-between', alignItems: 'baseline',
          font: 'var(--type-label)', color: 'var(--text-muted)', marginBottom: '6px',
          letterSpacing: 'var(--tracking-wide)',
        }}>
          {label}
          {isSet && (
            <span style={{
              font: 'var(--text-2xs) var(--font-sans)', color: 'var(--green-300)',
              letterSpacing: 'var(--tracking-caps)', textTransform: 'uppercase',
            }}>● stored</span>
          )}
        </span>
      )}
      <div style={{
        display: 'flex', alignItems: 'center', height: heights[size],
        background: 'var(--surface-inset)', border: '1px solid var(--border-strong)',
        borderRadius: 'var(--radius-sm)', boxShadow: 'var(--inset-well)', overflow: 'hidden',
      }}>
        <input
          id={fid}
          type={revealed ? 'text' : 'password'}
          defaultValue={value}
          placeholder={isSet ? '••••••••••••••••' : 'Enter secret'}
          style={{
            flex: 1, minWidth: 0, height: '100%', padding: '0 11px',
            background: 'transparent', border: 'none', outline: 'none',
            color: 'var(--text-heading)',
            font: 'var(--type-mono)', letterSpacing: revealed ? 0 : '0.15em',
          }}
          onFocus={(e) => {
            const box = e.currentTarget.parentElement;
            box.style.borderColor = 'var(--border-focus)';
            box.style.boxShadow = 'var(--inset-well), 0 0 0 3px var(--accent-glow)';
          }}
          onBlur={(e) => {
            const box = e.currentTarget.parentElement;
            box.style.borderColor = 'var(--border-strong)';
            box.style.boxShadow = 'var(--inset-well)';
          }}
          {...rest}
        />
        <button
          type="button"
          aria-label={revealed ? 'Hide secret' : 'Reveal secret'}
          onClick={() => setRevealed((v) => !v)}
          style={{
            alignSelf: 'stretch', padding: '0 11px', background: 'transparent',
            border: 'none', borderLeft: '1px solid var(--border-subtle)',
            color: 'var(--text-faint)', cursor: 'pointer',
            font: 'var(--text-2xs) var(--font-sans)', textTransform: 'uppercase',
            letterSpacing: 'var(--tracking-caps)',
          }}
        >{revealed ? 'Hide' : 'Show'}</button>
      </div>
      {hint && (
        <span style={{
          display: 'block', marginTop: '6px', font: 'var(--text-xs) var(--font-sans)',
          color: 'var(--text-faint)',
        }}>{hint}</span>
      )}
    </label>
  );
}
