import * as React from 'react';

export interface CardProps {
  title?: React.ReactNode;
  subtitle?: React.ReactNode;
  /** Small monospace uppercase kicker above the title (instrument voice). */
  eyebrow?: React.ReactNode;
  /** Header action slot (buttons, menus). */
  actions?: React.ReactNode;
  footer?: React.ReactNode;
  /** Apply body padding. @default true */
  pad?: boolean;
  /** Hover lift + accent border. */
  interactive?: boolean;
  children?: React.ReactNode;
  style?: React.CSSProperties;
}

/**
 * Base container surface with optional header/footer slots.
 * @startingPoint section="Layout" subtitle="Panel container with header + footer" viewport="700x220"
 */
export function Card(props: CardProps): React.ReactElement;
