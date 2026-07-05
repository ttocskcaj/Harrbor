import * as React from 'react';

export interface ProgressBarProps {
  /** 0–100. */
  value?: number;
  /** @default 'accent' */
  tone?: 'accent' | 'active' | 'ok' | 'warn' | 'error';
  /** Animated flowing dash for in-flight transfers. */
  striped?: boolean;
  /** Track height in px. @default 6 */
  height?: number;
  /** Optional caption row with value readout. */
  label?: string;
  style?: React.CSSProperties;
}

/** Transfer/import progress track — recessed well, teal fill. */
export function ProgressBar(props: ProgressBarProps): React.ReactElement;
