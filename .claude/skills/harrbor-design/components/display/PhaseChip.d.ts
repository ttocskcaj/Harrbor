import * as React from 'react';

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
  style?: React.CSSProperties;
}

/** Labels one of the six pipeline phases with its assigned hue. */
export function PhaseChip(props: PhaseChipProps): React.ReactElement;
