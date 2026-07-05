import type { CSSProperties } from 'react';

/**
 * PhaseChip — labels one of the six pipeline phases with its assigned hue.
 * Used as column headers and inline release-position markers.
 *
 * Ported verbatim from the harrbor-design skill
 * (components/display/PhaseChip.jsx) — inline-styled by design.
 */
export type Phase = 'discover' | 'download' | 'transfer' | 'import' | 'cleanup' | 'archival';

export interface PhaseChipProps {
  /** @default 'discover' */
  phase?: Phase;
  /** Optional count shown after the label. */
  count?: number;
  /** @default 'md' */
  size?: 'sm' | 'md';
  /** Filled/active treatment (tinted bg + glow). */
  active?: boolean;
  style?: CSSProperties;
}

export const PHASES: Record<Phase, { c: string; n: string }> = {
  discover: { c: 'var(--phase-discover)', n: 'Discover' },
  download: { c: 'var(--phase-download)', n: 'Download' },
  transfer: { c: 'var(--phase-transfer)', n: 'Transfer' },
  import: { c: 'var(--phase-import)', n: 'Import' },
  cleanup: { c: 'var(--phase-cleanup)', n: 'Cleanup' },
  archival: { c: 'var(--phase-archival)', n: 'Archival' },
};

export function PhaseChip({ phase = 'discover', count, size = 'md', active = false, style = {} }: PhaseChipProps) {
  const p = PHASES[phase] || PHASES.discover;
  const pad = size === 'sm' ? '2px 8px' : '4px 10px';
  const fs = size === 'sm' ? 'var(--text-2xs)' : 'var(--text-xs)';
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: '7px', padding: pad,
      borderRadius: 'var(--radius-xs)',
      background: active ? `color-mix(in oklab, ${p.c}, transparent 86%)` : 'transparent',
      border: `1px solid ${active ? p.c : 'var(--border-strong)'}`,
      color: active ? 'var(--text-heading)' : 'var(--text-body)', ...style,
    }}>
      <span style={{ width: 7, height: 7, borderRadius: 2, background: p.c, boxShadow: active ? `0 0 0 3px color-mix(in oklab, ${p.c}, transparent 82%)` : 'none' }} />
      <span style={{ font: `var(--fw-medium) ${fs} var(--font-sans)`, letterSpacing: 'var(--tracking-wide)' }}>{p.n}</span>
      {count != null && (
        <span style={{
          font: `var(--fw-semibold) ${fs} var(--font-mono)`, color: 'var(--text-muted)',
          fontVariantNumeric: 'tabular-nums',
        }}>{count}</span>
      )}
    </span>
  );
}

PhaseChip.PHASES = PHASES;
