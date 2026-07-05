import * as React from 'react';
import { Phase } from '../display/PhaseChip';

export interface FlowRailPhase {
  phase: Phase;
  /** Count in this phase right now. */
  count: number;
  /** Releases actively moving — animates the node + outgoing segment. */
  active?: boolean;
  /** This phase has stalled items — amber ring + badge. */
  stuck?: boolean;
  /** Number shown in the stuck badge. */
  stuckCount?: number;
}

export interface FlowRailProps {
  phases: FlowRailPhase[];
  /** 'full' (dashboard hero) or 'condensed' (pinned strip / nav). @default 'full' */
  variant?: 'full' | 'condensed';
  /** Highlighted phase (e.g. current column in the Pipeline view). */
  activePhase?: Phase | null;
  /** Place the single amber "harbor-light" bloom behind this phase. */
  bloom?: Phase | number | null;
  /** Makes nodes clickable — column-jump nav. */
  onSelect?: (phase: Phase) => void;
  style?: React.CSSProperties;
}

/**
 * The signature Harrbor motif — the whole six-phase pipeline as one
 * connected, flowing line. Use as the Dashboard hero and as a pinned
 * overview/nav strip on the Pipeline view.
 * @startingPoint section="Pipeline" subtitle="Six-phase flow-rail — the signature motif" viewport="900x150"
 */
export function FlowRail(props: FlowRailProps): React.ReactElement;
