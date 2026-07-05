import * as React from 'react';

export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  /** Visual role. @default 'secondary' */
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger';
  /** @default 'md' */
  size?: 'sm' | 'md' | 'lg';
  iconLeft?: React.ReactNode;
  iconRight?: React.ReactNode;
  /** Stretch to container width. */
  full?: boolean;
  disabled?: boolean;
  children?: React.ReactNode;
}

/**
 * Primary action control. Teal `primary` for the one main action per view;
 * `secondary` for everything else; `ghost` for low-emphasis; `danger` for
 * destructive actions.
 * @startingPoint section="Forms" subtitle="Action buttons — 4 variants, 3 sizes" viewport="700x160"
 */
export function Button(props: ButtonProps): React.ReactElement;
