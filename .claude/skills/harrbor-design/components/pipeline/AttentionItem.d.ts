import * as React from 'react';

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
  style?: React.CSSProperties;
}

/**
 * One row in the dashboard's attention/failures panel.
 * @startingPoint section="Pipeline" subtitle="Attention/failure row with action" viewport="700x120"
 */
export function AttentionItem(props: AttentionItemProps): React.ReactElement;
