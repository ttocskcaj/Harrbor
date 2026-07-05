import * as React from 'react';

export interface SelectOption { value: string; label: string; }

export interface SelectProps extends React.SelectHTMLAttributes<HTMLSelectElement> {
  label?: string;
  hint?: string;
  options?: SelectOption[];
  /** @default 'md' */
  size?: 'sm' | 'md' | 'lg';
}

/** Native dropdown styled to match the inset-well controls. */
export function Select(props: SelectProps): React.ReactElement;
