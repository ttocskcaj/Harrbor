import type { CSSProperties } from 'react';

/**
 * StatusDot — the connection/health atom. A filled dot with an optional
 * soft halo and pulse for "live" states.
 *
 * Ported verbatim from the harrbor-design skill
 * (components/display/StatusDot.jsx) — inline-styled by design.
 */
export interface StatusDotProps {
  /** @default 'idle' */
  status?: 'ok' | 'active' | 'warn' | 'error' | 'idle';
  /** Slow opacity pulse for live states. */
  pulse?: boolean;
  /** Diameter in px. @default 9 */
  size?: number;
  /** Soft colored halo ring. @default true */
  halo?: boolean;
  style?: CSSProperties;
}

export function StatusDot({ status = 'idle', pulse = false, size = 9, halo = true, style = {} }: StatusDotProps) {
  const map: Record<string, { c: string; g: string }> = {
    ok: { c: 'var(--status-ok)', g: 'var(--green-glow)' },
    active: { c: 'var(--status-active)', g: 'var(--accent-glow)' },
    warn: { c: 'var(--status-warn)', g: 'var(--amber-glow)' },
    error: { c: 'var(--status-error)', g: 'var(--red-glow)' },
    idle: { c: 'var(--status-idle)', g: 'transparent' },
  };
  const s = map[status] ?? map.idle;
  return (
    <span
      aria-label={status}
      style={{
        display: 'inline-block', width: size, height: size, flexShrink: 0,
        borderRadius: '50%', background: s.c,
        boxShadow: halo ? `0 0 0 3px ${s.g}` : 'none',
        animation: pulse ? 'var(--pulse)' : 'none', ...style,
      }}
    />
  );
}
