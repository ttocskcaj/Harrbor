import * as React from 'react';

export interface BadgeProps {
  /** @default 'neutral' */
  tone?: 'neutral' | 'accent' | 'ok' | 'warn' | 'error' | 'info';
  /** Tinted-glow background vs hairline outline. @default true */
  soft?: boolean;
  /** Prepend a small status dot. */
  dot?: boolean;
  uppercase?: boolean;
  children?: React.ReactNode;
  style?: React.CSSProperties;
}

/** Compact status/label pill. */
export function Badge(props: BadgeProps): React.ReactElement;
