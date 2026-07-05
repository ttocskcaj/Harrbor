import React from 'react';

/**
 * Card — the base container surface. header/footer slots optional.
 * `pad` controls body padding; `interactive` adds hover lift.
 */
export function Card({ title, subtitle, eyebrow, actions, footer, pad = true, interactive = false, children, style = {} }) {
  return (
    <section
      style={{
        display: 'flex', flexDirection: 'column',
        background: 'var(--surface-card)', border: '1px solid var(--border-subtle)',
        borderRadius: 'var(--radius-lg)', boxShadow: 'var(--shadow-md), var(--edge-light)',
        overflow: 'hidden', transition: 'border-color var(--dur-fast), transform var(--dur-fast)',
        ...style,
      }}
      onMouseEnter={interactive ? (e) => { e.currentTarget.style.borderColor = 'var(--border-focus)'; } : undefined}
      onMouseLeave={interactive ? (e) => { e.currentTarget.style.borderColor = 'var(--border-subtle)'; } : undefined}
    >
      {(title || actions) && (
        <header style={{
          display: 'flex', alignItems: 'center', gap: '12px',
          padding: '13px 16px', borderBottom: '1px solid var(--border-subtle)',
        }}>
          <div style={{ minWidth: 0 }}>
            {eyebrow && <div style={{ font: 'var(--fw-medium) var(--text-2xs) var(--font-mono)', color: 'var(--text-faint)', letterSpacing: 'var(--tracking-caps)', textTransform: 'uppercase', marginBottom: '4px' }}>{eyebrow}</div>}
            {title && <div style={{ font: 'var(--type-card)', color: 'var(--text-heading)' }}>{title}</div>}
            {subtitle && <div style={{ font: 'var(--text-xs) var(--font-sans)', color: 'var(--text-muted)', marginTop: '2px' }}>{subtitle}</div>}
          </div>
          {actions && <div style={{ marginLeft: 'auto', display: 'flex', gap: '6px' }}>{actions}</div>}
        </header>
      )}
      <div style={{ padding: pad ? '16px' : 0, flex: 1, minHeight: 0 }}>{children}</div>
      {footer && (
        <footer style={{ padding: '11px 16px', borderTop: '1px solid var(--border-subtle)', background: 'var(--surface-panel)' }}>{footer}</footer>
      )}
    </section>
  );
}
