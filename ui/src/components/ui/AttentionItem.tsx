import type { CSSProperties } from 'react';
import { StatusDot } from './StatusDot';
import { Button } from './Button';

/**
 * AttentionItem — one row in the dashboard's attention/failures panel.
 * A severity dot, a plain-language message, the offending release id in
 * mono, and an optional resolve action (Retry / Skip / Inspect).
 *
 * Ported verbatim from the harrbor-design skill
 * (components/pipeline/AttentionItem.jsx) — inline-styled by design.
 */
export interface AttentionItemProps {
  /** @default 'error' */
  severity?: 'error' | 'warn' | 'info';
  message: string;
  /** Offending release id (mono). */
  releaseId?: string;
  phase?: string;
  time?: string;
  /** Action label, e.g. "Retry" / "Skip". */
  action?: string;
  onAction?: () => void;
  style?: CSSProperties;
}

export function AttentionItem({ severity = 'error', message, releaseId, phase, time, action, onAction, style = {} }: AttentionItemProps) {
  const sev = ({ error: 'error', warn: 'warn', info: 'active' } as const)[severity] || 'error';
  return (
    <div style={{
      display: 'flex', alignItems: 'flex-start', gap: '11px',
      padding: '11px 13px', borderRadius: 'var(--radius-sm)',
      background: 'var(--surface-panel)', border: '1px solid var(--border-subtle)', ...style,
    }}>
      <span style={{ marginTop: '3px' }}><StatusDot status={sev} size={8} /></span>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ font: `var(--fw-medium) var(--text-base) var(--font-sans)`, color: 'var(--text-heading)', marginBottom: '3px' }}>{message}</div>
        <div style={{ display: 'flex', gap: '10px', font: 'var(--text-2xs) var(--font-mono)', color: 'var(--text-faint)' }}>
          {releaseId && <span style={{ color: 'var(--text-muted)' }}>{releaseId}</span>}
          {phase && <span>· {phase}</span>}
          {time && <span>· {time}</span>}
        </div>
      </div>
      {action && (
        <Button size="sm" variant={severity === 'error' ? 'primary' : 'secondary'} onClick={onAction}>{action}</Button>
      )}
    </div>
  );
}
