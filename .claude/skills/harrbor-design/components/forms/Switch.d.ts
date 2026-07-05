import * as React from 'react';

export interface SwitchProps {
  checked?: boolean;
  onChange?: (next: boolean) => void;
  disabled?: boolean;
  label?: string;
  style?: React.CSSProperties;
}

/** On/off toggle. Teal when on; recessed track when off. */
export function Switch(props: SwitchProps): React.ReactElement;
