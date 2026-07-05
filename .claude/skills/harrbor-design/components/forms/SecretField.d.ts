import * as React from 'react';

export interface SecretFieldProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  hint?: string;
  /** Current value (usually left empty when already stored). */
  value?: string;
  /** Marks the secret as already stored — shows a "● stored" flag. */
  isSet?: boolean;
  /** @default 'md' */
  size?: 'sm' | 'md' | 'lg';
}

/**
 * Masked secret input (API keys, passwords) with a reveal toggle and a
 * "stored" indicator. Always monospace so keys can be eyeballed.
 * @startingPoint section="Forms" subtitle="Masked secret field with reveal" viewport="700x150"
 */
export function SecretField(props: SecretFieldProps): React.ReactElement;
