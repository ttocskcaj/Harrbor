import * as React from 'react';

export interface StatusDotProps {
  /** @default 'idle' */
  status?: 'ok' | 'active' | 'warn' | 'error' | 'idle';
  /** Slow opacity pulse for live states. */
  pulse?: boolean;
  /** Diameter in px. @default 9 */
  size?: number;
  /** Soft colored halo ring. @default true */
  halo?: boolean;
  style?: React.CSSProperties;
}

/** The connection/health atom: a filled dot with optional halo + pulse. */
export function StatusDot(props: StatusDotProps): React.ReactElement;
