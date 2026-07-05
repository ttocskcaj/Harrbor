import * as React from 'react';
import { Phase } from '../display/PhaseChip';

export interface ReleaseCardProps {
  /** Release name (rendered monospace). */
  name: string;
  /** Job lane, e.g. "tv-shows" / "movies". */
  job?: string;
  /** @default 'transfer' */
  phase?: Phase;
  /** Human size, e.g. "4.3 GB". */
  size?: string;
  /** 0–100 transfer progress; omit to hide the bar. */
  progress?: number | null;
  /** Stalled item — amber edge + glow. */
  stuck?: boolean;
  /** Currently moving — pulse dot + striped bar. */
  active?: boolean;
  /** ETA string shown at the bottom. */
  eta?: string;
  style?: React.CSSProperties;
}

/**
 * One release as it sits in a pipeline column.
 * @startingPoint section="Pipeline" subtitle="Release card with phase + transfer progress" viewport="700x150"
 */
export function ReleaseCard(props: ReleaseCardProps): React.ReactElement;
