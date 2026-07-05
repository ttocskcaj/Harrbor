import * as React from 'react';

export interface IconButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  /** @default 'md' */
  size?: 'sm' | 'md' | 'lg';
  /** @default 'ghost' */
  variant?: 'ghost' | 'solid' | 'accent';
  /** Accessible label (also the tooltip). */
  label?: string;
  disabled?: boolean;
  children?: React.ReactNode;
}

/** Square single-glyph control for toolbars, table rows and card headers. */
export function IconButton(props: IconButtonProps): React.ReactElement;
