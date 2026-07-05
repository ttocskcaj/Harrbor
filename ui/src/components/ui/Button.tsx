import type { ButtonHTMLAttributes, CSSProperties, ReactNode } from 'react';

/**
 * Harrbor Button — the primary action control.
 * Variants: primary (teal signal), secondary (outlined), ghost, danger.
 * Mechanical press: no bounce, quick color shift, subtle depress.
 *
 * Ported verbatim from the harrbor-design skill
 * (components/forms/Button.jsx) — inline-styled by design.
 */
export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  /** Visual role. @default 'secondary' */
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger';
  /** @default 'md' */
  size?: 'sm' | 'md' | 'lg';
  iconLeft?: ReactNode;
  iconRight?: ReactNode;
  /** Stretch to container width. */
  full?: boolean;
  disabled?: boolean;
  children?: ReactNode;
  style?: CSSProperties;
}

export function Button({
  variant = 'secondary',
  size = 'md',
  iconLeft = null,
  iconRight = null,
  disabled = false,
  full = false,
  children,
  style = {},
  ...rest
}: ButtonProps) {
  const heights = { sm: 'var(--control-sm)', md: 'var(--control-md)', lg: 'var(--control-lg)' };
  const pads = { sm: '0 10px', md: '0 14px', lg: '0 18px' };
  const fonts = { sm: 'var(--text-xs)', md: 'var(--text-base)', lg: 'var(--text-md)' };

  const palettes: Record<string, CSSProperties> = {
    primary: {
      background: 'var(--accent)', color: 'var(--text-invert)',
      border: '1px solid var(--amber-400)', fontWeight: 'var(--fw-semibold)' as CSSProperties['fontWeight'],
      boxShadow: '0 1px 0 rgba(255,255,255,0.12) inset, var(--shadow-sm)',
    },
    secondary: {
      background: 'var(--surface-inset)', color: 'var(--text-body)',
      border: '1px solid var(--border-strong)', fontWeight: 'var(--fw-medium)' as CSSProperties['fontWeight'],
      boxShadow: 'var(--edge-light)',
    },
    ghost: {
      background: 'transparent', color: 'var(--text-muted)',
      border: '1px solid transparent', fontWeight: 'var(--fw-medium)' as CSSProperties['fontWeight'],
    },
    danger: {
      background: 'transparent', color: 'var(--red-300)',
      border: '1px solid var(--red-500)', fontWeight: 'var(--fw-medium)' as CSSProperties['fontWeight'],
    },
  };

  const p = palettes[variant] || palettes.secondary;

  return (
    <button
      disabled={disabled}
      style={{
        display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        gap: '7px', height: heights[size], padding: pads[size],
        width: full ? '100%' : 'auto',
        font: `${fonts[size]} var(--font-sans)`,
        letterSpacing: 'var(--tracking-normal)', borderRadius: 'var(--radius-sm)',
        cursor: disabled ? 'not-allowed' : 'pointer', whiteSpace: 'nowrap',
        opacity: disabled ? 0.45 : 1,
        transition: 'background var(--dur-fast) var(--ease-out), transform var(--dur-fast) var(--ease-out), border-color var(--dur-fast)',
        ...p, ...style,
      }}
      onMouseDown={(e) => { if (!disabled) e.currentTarget.style.transform = 'translateY(1px)'; }}
      onMouseUp={(e) => { e.currentTarget.style.transform = 'translateY(0)'; }}
      onMouseLeave={(e) => { e.currentTarget.style.transform = 'translateY(0)'; }}
      {...rest}
    >
      {iconLeft && <span style={{ display: 'inline-flex', fontSize: '1.1em' }}>{iconLeft}</span>}
      {children}
      {iconRight && <span style={{ display: 'inline-flex', fontSize: '1.1em' }}>{iconRight}</span>}
    </button>
  );
}
