import type { CSSProperties, ReactNode } from 'react';

/**
 * Badge — compact status/label pill. `tone` drives color; `soft` uses a
 * tinted-glow background, otherwise a hairline outline. `dot` prepends a dot.
 *
 * Ported verbatim from the harrbor-design skill (components/display/Badge.jsx).
 */
export interface BadgeProps {
  /** @default 'neutral' */
  tone?: 'neutral' | 'accent' | 'active' | 'ok' | 'warn' | 'error' | 'info';
  /** Tinted-glow background vs hairline outline. @default true */
  soft?: boolean;
  /** Prepend a small status dot. */
  dot?: boolean;
  uppercase?: boolean;
  children?: ReactNode;
  style?: CSSProperties;
}

export function Badge({ tone = 'neutral', soft = true, dot = false, uppercase = false, children, style = {} }: BadgeProps) {
  const tones: Record<string, { fg: string; bg: string; bd: string }> = {
    neutral: { fg: 'var(--text-muted)', bg: 'var(--surface-inset)', bd: 'var(--border-strong)' },
    accent: { fg: 'var(--accent-text)', bg: 'var(--accent-glow)', bd: 'var(--amber-600)' },
    // `active` (teal = in-progress) added to match the Operate mock's live badges.
    active: { fg: 'var(--teal-300)', bg: 'var(--teal-glow)', bd: 'var(--status-active)' },
    ok: { fg: 'var(--green-300)', bg: 'var(--green-glow)', bd: 'var(--green-500)' },
    warn: { fg: 'var(--amber-300)', bg: 'var(--amber-glow)', bd: 'var(--amber-500)' },
    error: { fg: 'var(--red-300)', bg: 'var(--red-glow)', bd: 'var(--red-500)' },
    info: { fg: 'var(--blue-300)', bg: 'rgba(77,142,240,0.12)', bd: 'var(--blue-500)' },
  };
  const t = tones[tone] ?? tones.neutral;
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: '5px',
      height: 20, padding: '0 8px', borderRadius: 'var(--radius-xs)',
      background: soft ? t.bg : 'transparent',
      border: `1px solid ${soft ? 'transparent' : t.bd}`,
      color: t.fg, font: `var(--fw-medium) var(--text-2xs)/1 var(--font-sans)`,
      letterSpacing: uppercase ? 'var(--tracking-caps)' : 'var(--tracking-wide)',
      textTransform: uppercase ? 'uppercase' : 'none', whiteSpace: 'nowrap', ...style,
    }}>
      {dot && <span style={{ width: 6, height: 6, borderRadius: '50%', background: t.fg }} />}
      {children}
    </span>
  );
}
