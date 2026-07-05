import * as React from 'react';

export interface ConnectionRowProps {
  name: string;
  /** Host:port (mono). */
  host?: string;
  /** @default 'ok' */
  status?: 'ok' | 'warn' | 'error' | 'idle';
  /** Right-aligned detail: latency, version, or error. */
  detail?: string;
  /** Small uppercase kind tag, e.g. "TORRENT" / "SFTP". */
  kind?: string;
  style?: React.CSSProperties;
}

/**
 * One service in the connection-health panel (qBittorrent, Sonarr, …).
 * @startingPoint section="Pipeline" subtitle="Service connection health row" viewport="700x120"
 */
export function ConnectionRow(props: ConnectionRowProps): React.ReactElement;
