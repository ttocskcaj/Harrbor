import * as React from 'react';

export interface TextFieldProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  hint?: string;
  error?: string;
  /** Render input value in monospace (hosts, ports, paths). */
  mono?: boolean;
  /** Static prefix inside the well (e.g. "https://"). */
  prefix?: React.ReactNode;
  /** @default 'md' */
  size?: 'sm' | 'md' | 'lg';
}

/**
 * Labelled text input with recessed inset-well styling, monospace mode,
 * prefix slot and error/hint line.
 * @startingPoint section="Forms" subtitle="Text input — label, hint, error, mono" viewport="700x150"
 */
export function TextField(props: TextFieldProps): React.ReactElement;
