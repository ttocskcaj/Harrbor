import type { CSSProperties, ReactNode } from 'react';
import { PhaseChip, type Phase } from './PhaseChip';

/**
 * FlowRail — the signature Harrbor motif. Shows the whole six-phase
 * pipeline as one connected line (transit-diagram meets signal meter):
 * a node per phase in its hue, connected by a rail whose segments shimmer
 * where releases are actively moving. Stuck phases get an amber ring; a
 * fully clear phase reads dim. Doubles as column-jump nav.
 *
 * Also the home of the "harbor-light": pass `bloom` to place one soft amber
 * glow behind the phase that's the live focus of the screen.
 *
 * Ported verbatim from the harrbor-design skill
 * (components/pipeline/FlowRail.jsx) — inline-styled by design.
 */
export interface FlowRailPhase {
  phase: Phase;
  count: number;
  active?: boolean;
  stuck?: boolean;
  stuckCount?: number;
}

export interface FlowRailProps {
  phases?: FlowRailPhase[];
  /** @default 'full' */
  variant?: 'full' | 'condensed';
  activePhase?: Phase | null;
  /** Phase (or index) to place the single amber harbor-light behind. */
  bloom?: Phase | number | null;
  onSelect?: (phase: Phase) => void;
  style?: CSSProperties;
}

const ORDER: Phase[] = ['discover', 'download', 'transfer', 'import', 'cleanup', 'archival'];

export function FlowRail({
  phases = [],
  variant = 'full',
  activePhase = null,
  bloom = null,
  onSelect,
  style = {},
}: FlowRailProps) {
  // normalize into ordered list
  const byPhase: Record<string, FlowRailPhase> = {};
  phases.forEach((p) => { byPhase[p.phase] = p; });
  const items: FlowRailPhase[] = ORDER.map((ph) => byPhase[ph] || { phase: ph, count: 0 });

  const condensed = variant === 'condensed';
  const nodeSize = condensed ? 22 : 30;
  const labelFs = condensed ? '9px' : 'var(--text-2xs)';
  const meta = PhaseChip.PHASES;

  // build a 3-row grid: labels / node-band / counts
  const cols: string[] = [];
  items.forEach((_, i) => { cols.push('auto'); if (i < items.length - 1) cols.push('1fr'); });

  const cells: ReactNode[] = [];
  items.forEach((it, i) => {
    const p = meta[it.phase];
    const nodeCol = 2 * i + 1;
    const moving = it.active && it.count > 0;
    const clear = !it.count;
    const isSel = activePhase === it.phase;

    // label (row 1)
    if (!condensed) {
      cells.push(
        <div key={`l${i}`} style={{
          gridColumn: nodeCol, gridRow: 1, justifySelf: 'center',
          font: `var(--fw-semibold) ${labelFs} var(--font-mono)`,
          letterSpacing: 'var(--tracking-caps)', textTransform: 'uppercase',
          color: isSel ? 'var(--text-body)' : 'var(--text-faint)', marginBottom: '9px',
          whiteSpace: 'nowrap',
        }}>{p.n}</div>
      );
    }

    // node (row 2)
    cells.push(
      <button key={`n${i}`} type="button"
        onClick={onSelect ? () => onSelect(it.phase) : undefined}
        title={`${p.n} · ${it.count}`}
        style={{
          gridColumn: nodeCol, gridRow: 2, justifySelf: 'center', position: 'relative',
          width: nodeSize, height: nodeSize, padding: 0,
          borderRadius: condensed ? 'var(--radius-xs)' : 'var(--radius-sm)',
          background: clear ? 'var(--surface-inset)' : `color-mix(in oklab, ${p.c}, transparent 78%)`,
          border: `1.5px solid ${it.stuck ? 'var(--amber-500)' : clear ? 'var(--border-strong)' : p.c}`,
          boxShadow: it.stuck ? '0 0 0 3px var(--amber-glow)'
            : moving ? `0 0 0 3px color-mix(in oklab, ${p.c}, transparent 82%)` : 'none',
          cursor: onSelect ? 'pointer' : 'default',
          display: 'grid', placeItems: 'center',
          transition: 'transform var(--dur-fast) var(--ease-out)',
        }}
        onMouseEnter={onSelect ? (e) => { e.currentTarget.style.transform = 'translateY(-2px)'; } : undefined}
        onMouseLeave={onSelect ? (e) => { e.currentTarget.style.transform = 'translateY(0)'; } : undefined}
      >
        <span style={{
          width: condensed ? 6 : 9, height: condensed ? 6 : 9, borderRadius: 2,
          background: clear ? 'var(--slate-600)' : p.c,
          animation: moving ? 'var(--pulse)' : 'none',
        }} />
        {it.stuck && (
          <span style={{
            position: 'absolute', top: -6, right: -6, minWidth: 14, height: 14, padding: '0 3px',
            borderRadius: 'var(--radius-pill)', background: 'var(--amber-500)', color: 'var(--slate-1000)',
            font: `var(--fw-bold) 9px/14px var(--font-mono)`, textAlign: 'center',
          }}>{it.stuckCount || '!'}</span>
        )}
      </button>
    );

    // count (row 3)
    cells.push(
      <div key={`c${i}`} style={{
        gridColumn: nodeCol, gridRow: 3, justifySelf: 'center', marginTop: '8px',
        font: `var(--fw-semibold) ${condensed ? 'var(--text-xs)' : 'var(--text-sm)'} var(--font-mono)`,
        color: clear ? 'var(--text-faint)' : 'var(--text-body)',
        fontVariantNumeric: 'tabular-nums',
      }}>{it.count > 999 ? `${(it.count / 1000).toFixed(1)}k` : it.count}</div>
    );

    // connector (row 2, between nodes)
    if (i < items.length - 1) {
      const next = items[i + 1];
      const segMoving = (it.active && it.count > 0) || (next.active && next.count > 0);
      cells.push(
        <div key={`s${i}`} style={{ gridColumn: nodeCol + 1, gridRow: 2, alignSelf: 'center', height: 2, margin: '0 6px', position: 'relative' }}>
          <div style={{
            position: 'absolute', inset: 0, borderRadius: 'var(--radius-pill)',
            background: segMoving
              ? `repeating-linear-gradient(90deg, ${p.c} 0 7px, transparent 7px 14px)`
              : 'var(--border-strong)',
            backgroundSize: segMoving ? '14px 2px' : 'auto',
            animation: segMoving ? 'flow-dash 0.9s linear infinite' : 'none',
            opacity: segMoving ? 0.9 : 0.5,
          }} />
        </div>
      );
    }
  });

  // bloom target — soft amber light behind one node
  let bloomEl: ReactNode = null;
  if (bloom != null) {
    const bi = typeof bloom === 'number' ? bloom : ORDER.indexOf(bloom);
    if (bi >= 0) {
      const leftPct = ((bi + 0.5) / items.length) * 100;
      bloomEl = (
        <div aria-hidden style={{
          position: 'absolute', top: '50%', left: `${leftPct}%`, width: 200, height: 160,
          transform: 'translate(-50%, -50%)', pointerEvents: 'none',
          background: 'var(--harbor-bloom)', animation: 'bloom-drift 3.2s var(--ease-inout) infinite',
        }} />
      );
    }
  }

  return (
    <div style={{ position: 'relative', ...style }}>
      {bloomEl}
      <div style={{
        position: 'relative', display: 'grid',
        gridTemplateColumns: cols.join(' '),
        alignItems: 'center', padding: condensed ? '4px 6px' : '4px 10px',
      }}>
        {cells}
      </div>
    </div>
  );
}
