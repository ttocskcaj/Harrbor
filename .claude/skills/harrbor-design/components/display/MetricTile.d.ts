import * as React from 'react';

export interface MetricTileProps {
  label: string;
  value: React.ReactNode;
  unit?: string;
  /** Delta readout, e.g. "+12" / "-3". */
  delta?: string;
  /** @default 'neutral' */
  tone?: 'neutral' | 'accent' | 'ok' | 'warn' | 'error';
  /** Left accent bar in the tone color. */
  accentBar?: boolean;
  style?: React.CSSProperties;
}

/**
 * A single dashboard number: phase count, queue depth, throughput.
 * @startingPoint section="Data" subtitle="Dashboard metric tile" viewport="700x140"
 */
export function MetricTile(props: MetricTileProps): React.ReactElement;
