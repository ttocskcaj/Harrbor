import type { ButtonHTMLAttributes, CSSProperties, ReactNode } from 'react';

/**
 * IconButton — square control for a single glyph. Used in toolbars,
 * table rows, card headers. Pass an icon node as children.
 *
 * Ported verbatim from the harrbor-design skill
 * (components/forms/IconButton.jsx) — inline-styled by design.
 */
export interface IconButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  /** @default 'md' */
  size?: 'sm' | 'md' | 'lg';
  /** @default 'ghost' */
  variant?: 'ghost' | 'solid' | 'accent';
  /** Accessible label (also the tooltip). */
  label?: string;
  disabled?: boolean;
  children?: ReactNode;
  style?: CSSProperties;
}

export function IconButton({
  size = 'md',
  variant = 'ghost',
  disabled = false,
  label,
  children,
  style = {},
  ...rest
}: IconButtonProps) {
  const dims = { sm: 26, md: 32, lg: 38 };
  const d = dims[size];
  const palettes: Record<string, CSSProperties> = {
    ghost: { background: 'transparent', color: 'var(--text-muted)', border: '1px solid transparent' },
    solid: { background: 'var(--surface-inset)', color: 'var(--text-body)', border: '1px solid var(--border-strong)' },
    accent: { background: 'var(--accent-glow)', color: 'var(--accent-text)', border: '1px solid var(--amber-600)' },
  };
  const p = palettes[variant] || palettes.ghost;
  return (
    <button
      aria-label={label}
      title={label}
      disabled={disabled}
      style={{
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        width: d, height: d, borderRadius: 'var(--radius-sm)',
        cursor: disabled ? 'not-allowed' : 'pointer', opacity: disabled ? 0.4 : 1,
        transition: 'background var(--dur-fast) var(--ease-out), color var(--dur-fast)',
        ...p, ...style,
      }}
      onMouseEnter={(e) => { if (!disabled && variant === 'ghost') { e.currentTarget.style.background = 'var(--surface-hover)'; e.currentTarget.style.color = 'var(--text-body)'; } }}
      onMouseLeave={(e) => { if (variant === 'ghost') { e.currentTarget.style.background = 'transparent'; e.currentTarget.style.color = 'var(--text-muted)'; } }}
      {...rest}
    >
      {children}
    </button>
  );
}
