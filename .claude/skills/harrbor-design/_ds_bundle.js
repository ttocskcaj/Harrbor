/* @ds-bundle: {"format":4,"namespace":"HarrborDesignSystem_2a1653","components":[{"name":"Badge","sourcePath":"components/display/Badge.jsx"},{"name":"Card","sourcePath":"components/display/Card.jsx"},{"name":"MetricTile","sourcePath":"components/display/MetricTile.jsx"},{"name":"PhaseChip","sourcePath":"components/display/PhaseChip.jsx"},{"name":"ProgressBar","sourcePath":"components/display/ProgressBar.jsx"},{"name":"StatusDot","sourcePath":"components/display/StatusDot.jsx"},{"name":"Button","sourcePath":"components/forms/Button.jsx"},{"name":"IconButton","sourcePath":"components/forms/IconButton.jsx"},{"name":"SecretField","sourcePath":"components/forms/SecretField.jsx"},{"name":"Select","sourcePath":"components/forms/Select.jsx"},{"name":"Switch","sourcePath":"components/forms/Switch.jsx"},{"name":"TextField","sourcePath":"components/forms/TextField.jsx"},{"name":"AttentionItem","sourcePath":"components/pipeline/AttentionItem.jsx"},{"name":"ConnectionRow","sourcePath":"components/pipeline/ConnectionRow.jsx"},{"name":"FlowRail","sourcePath":"components/pipeline/FlowRail.jsx"},{"name":"ReleaseCard","sourcePath":"components/pipeline/ReleaseCard.jsx"}],"sourceHashes":{"components/display/Badge.jsx":"eb7e7e2c666f","components/display/Card.jsx":"523b4f24d686","components/display/MetricTile.jsx":"2639fdcf7996","components/display/PhaseChip.jsx":"885926474c7b","components/display/ProgressBar.jsx":"bf652ed5867c","components/display/StatusDot.jsx":"f74d3f25624a","components/forms/Button.jsx":"e9d13ed018f7","components/forms/IconButton.jsx":"2b9575b98f84","components/forms/SecretField.jsx":"3318224c242d","components/forms/Select.jsx":"40de33656343","components/forms/Switch.jsx":"52486f057c92","components/forms/TextField.jsx":"ea8364b5ce46","components/pipeline/AttentionItem.jsx":"64fb4bb28ae4","components/pipeline/ConnectionRow.jsx":"6d0c719b0e66","components/pipeline/FlowRail.jsx":"483bbd2cea25","components/pipeline/ReleaseCard.jsx":"b26809b76a16","ui_kits/harrbor-app/AppShell.jsx":"5786fe37ac0d","ui_kits/harrbor-app/Dashboard.jsx":"ac36cf3c42bd","ui_kits/harrbor-app/Pipeline.jsx":"1ddb14be2467","ui_kits/harrbor-app/Settings.jsx":"d9fd592b4852"},"inlinedExternals":[],"unexposedExports":[]} */

(() => {

const __ds_ns = (window.HarrborDesignSystem_2a1653 = window.HarrborDesignSystem_2a1653 || {});

const __ds_scope = {};

(__ds_ns.__errors = __ds_ns.__errors || []);

// components/display/Badge.jsx
try { (() => {
/**
 * Badge — compact status/label pill. tone drives color; `soft` uses a
 * tinted-glow background, otherwise a hairline outline. `dot` prepends
 * a status dot.
 * tone: neutral | accent | ok | warn | error | info
 */
function Badge({
  tone = 'neutral',
  soft = true,
  dot = false,
  uppercase = false,
  children,
  style = {}
}) {
  const tones = {
    neutral: {
      fg: 'var(--text-muted)',
      bg: 'var(--surface-inset)',
      bd: 'var(--border-strong)'
    },
    accent: {
      fg: 'var(--accent-text)',
      bg: 'var(--accent-glow)',
      bd: 'var(--amber-600)'
    },
    ok: {
      fg: 'var(--green-300)',
      bg: 'var(--green-glow)',
      bd: 'var(--green-500)'
    },
    warn: {
      fg: 'var(--amber-300)',
      bg: 'var(--amber-glow)',
      bd: 'var(--amber-500)'
    },
    error: {
      fg: 'var(--red-300)',
      bg: 'var(--red-glow)',
      bd: 'var(--red-500)'
    },
    info: {
      fg: 'var(--blue-300)',
      bg: 'rgba(77,142,240,0.12)',
      bd: 'var(--blue-500)'
    }
  };
  const t = tones[tone] || tones.neutral;
  const dotColors = {
    neutral: 'idle',
    accent: 'active',
    ok: 'ok',
    warn: 'warn',
    error: 'error',
    info: 'active'
  };
  return /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      gap: '5px',
      height: 20,
      padding: '0 8px',
      borderRadius: 'var(--radius-xs)',
      background: soft ? t.bg : 'transparent',
      border: `1px solid ${soft ? 'transparent' : t.bd}`,
      color: t.fg,
      font: `var(--fw-medium) var(--text-2xs)/1 var(--font-sans)`,
      letterSpacing: uppercase ? 'var(--tracking-caps)' : 'var(--tracking-wide)',
      textTransform: uppercase ? 'uppercase' : 'none',
      whiteSpace: 'nowrap',
      ...style
    }
  }, dot && /*#__PURE__*/React.createElement("span", {
    style: {
      width: 6,
      height: 6,
      borderRadius: '50%',
      background: t.fg
    }
  }), children);
}
Object.assign(__ds_scope, { Badge });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/display/Badge.jsx", error: String((e && e.message) || e) }); }

// components/display/Card.jsx
try { (() => {
/**
 * Card — the base container surface. header/footer slots optional.
 * `pad` controls body padding; `interactive` adds hover lift.
 */
function Card({
  title,
  subtitle,
  eyebrow,
  actions,
  footer,
  pad = true,
  interactive = false,
  children,
  style = {}
}) {
  return /*#__PURE__*/React.createElement("section", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      background: 'var(--surface-card)',
      border: '1px solid var(--border-subtle)',
      borderRadius: 'var(--radius-lg)',
      boxShadow: 'var(--shadow-md), var(--edge-light)',
      overflow: 'hidden',
      transition: 'border-color var(--dur-fast), transform var(--dur-fast)',
      ...style
    },
    onMouseEnter: interactive ? e => {
      e.currentTarget.style.borderColor = 'var(--border-focus)';
    } : undefined,
    onMouseLeave: interactive ? e => {
      e.currentTarget.style.borderColor = 'var(--border-subtle)';
    } : undefined
  }, (title || actions) && /*#__PURE__*/React.createElement("header", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: '12px',
      padding: '13px 16px',
      borderBottom: '1px solid var(--border-subtle)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      minWidth: 0
    }
  }, eyebrow && /*#__PURE__*/React.createElement("div", {
    style: {
      font: 'var(--fw-medium) var(--text-2xs) var(--font-mono)',
      color: 'var(--text-faint)',
      letterSpacing: 'var(--tracking-caps)',
      textTransform: 'uppercase',
      marginBottom: '4px'
    }
  }, eyebrow), title && /*#__PURE__*/React.createElement("div", {
    style: {
      font: 'var(--type-card)',
      color: 'var(--text-heading)'
    }
  }, title), subtitle && /*#__PURE__*/React.createElement("div", {
    style: {
      font: 'var(--text-xs) var(--font-sans)',
      color: 'var(--text-muted)',
      marginTop: '2px'
    }
  }, subtitle)), actions && /*#__PURE__*/React.createElement("div", {
    style: {
      marginLeft: 'auto',
      display: 'flex',
      gap: '6px'
    }
  }, actions)), /*#__PURE__*/React.createElement("div", {
    style: {
      padding: pad ? '16px' : 0,
      flex: 1,
      minHeight: 0
    }
  }, children), footer && /*#__PURE__*/React.createElement("footer", {
    style: {
      padding: '11px 16px',
      borderTop: '1px solid var(--border-subtle)',
      background: 'var(--surface-panel)'
    }
  }, footer));
}
Object.assign(__ds_scope, { Card });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/display/Card.jsx", error: String((e && e.message) || e) }); }

// components/display/MetricTile.jsx
try { (() => {
/**
 * MetricTile — a single number on the dashboard: a phase count, a
 * queue depth, a throughput figure. Big mono-ish number, small label,
 * optional trend/delta and accent bar.
 */
function MetricTile({
  label,
  value,
  unit,
  delta,
  tone = 'neutral',
  accentBar = false,
  style = {}
}) {
  const tones = {
    neutral: 'var(--text-heading)',
    accent: 'var(--accent-text)',
    ok: 'var(--green-300)',
    warn: 'var(--amber-300)',
    error: 'var(--red-300)'
  };
  const bar = {
    neutral: 'var(--border-strong)',
    accent: 'var(--accent)',
    ok: 'var(--green-500)',
    warn: 'var(--amber-500)',
    error: 'var(--red-500)'
  };
  const deltaUp = delta && delta.startsWith('+');
  return /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative',
      padding: '14px 16px',
      background: 'var(--surface-card)',
      border: '1px solid var(--border-subtle)',
      borderRadius: 'var(--radius-md)',
      boxShadow: 'var(--edge-light)',
      overflow: 'hidden',
      ...style
    }
  }, accentBar && /*#__PURE__*/React.createElement("span", {
    style: {
      position: 'absolute',
      left: 0,
      top: 0,
      bottom: 0,
      width: 3,
      background: bar[tone]
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      font: 'var(--type-eyebrow)',
      color: 'var(--text-muted)',
      letterSpacing: 'var(--tracking-caps)',
      textTransform: 'uppercase',
      marginBottom: '8px'
    }
  }, label), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'baseline',
      gap: '6px'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      font: `var(--fw-semibold) var(--text-3xl)/1 var(--font-sans)`,
      color: tones[tone],
      letterSpacing: 'var(--tracking-tight)',
      fontVariantNumeric: 'tabular-nums'
    }
  }, value), unit && /*#__PURE__*/React.createElement("span", {
    style: {
      font: 'var(--text-sm) var(--font-mono)',
      color: 'var(--text-faint)'
    }
  }, unit), delta && /*#__PURE__*/React.createElement("span", {
    style: {
      marginLeft: 'auto',
      font: 'var(--text-xs) var(--font-mono)',
      color: deltaUp ? 'var(--green-300)' : 'var(--text-muted)'
    }
  }, delta)));
}
Object.assign(__ds_scope, { MetricTile });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/display/MetricTile.jsx", error: String((e && e.message) || e) }); }

// components/display/PhaseChip.jsx
try { (() => {
/**
 * PhaseChip — labels one of the six pipeline phases with its assigned
 * hue. Used as column headers and inline release-position markers.
 * phase: discover | download | transfer | import | cleanup | archival
 */
const PHASES = {
  discover: {
    c: 'var(--phase-discover)',
    n: 'Discover'
  },
  download: {
    c: 'var(--phase-download)',
    n: 'Download'
  },
  transfer: {
    c: 'var(--phase-transfer)',
    n: 'Transfer'
  },
  import: {
    c: 'var(--phase-import)',
    n: 'Import'
  },
  cleanup: {
    c: 'var(--phase-cleanup)',
    n: 'Cleanup'
  },
  archival: {
    c: 'var(--phase-archival)',
    n: 'Archival'
  }
};
function PhaseChip({
  phase = 'discover',
  count,
  size = 'md',
  active = false,
  style = {}
}) {
  const p = PHASES[phase] || PHASES.discover;
  const pad = size === 'sm' ? '2px 8px' : '4px 10px';
  const fs = size === 'sm' ? 'var(--text-2xs)' : 'var(--text-xs)';
  return /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      gap: '7px',
      padding: pad,
      borderRadius: 'var(--radius-xs)',
      background: active ? `color-mix(in oklab, ${p.c}, transparent 86%)` : 'transparent',
      border: `1px solid ${active ? p.c : 'var(--border-strong)'}`,
      color: active ? 'var(--text-heading)' : 'var(--text-body)',
      ...style
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      width: 7,
      height: 7,
      borderRadius: 2,
      background: p.c,
      boxShadow: active ? `0 0 0 3px color-mix(in oklab, ${p.c}, transparent 82%)` : 'none'
    }
  }), /*#__PURE__*/React.createElement("span", {
    style: {
      font: `var(--fw-medium) ${fs} var(--font-sans)`,
      letterSpacing: 'var(--tracking-wide)'
    }
  }, p.n), count != null && /*#__PURE__*/React.createElement("span", {
    style: {
      font: `var(--fw-semibold) ${fs} var(--font-mono)`,
      color: 'var(--text-muted)',
      fontVariantNumeric: 'tabular-nums'
    }
  }, count));
}
PhaseChip.PHASES = PHASES;
Object.assign(__ds_scope, { PhaseChip });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/display/PhaseChip.jsx", error: String((e && e.message) || e) }); }

// components/display/ProgressBar.jsx
try { (() => {
/**
 * ProgressBar — transfer/import progress track. Recessed well with a
 * teal fill; `striped` animates a flowing dash for in-flight transfers;
 * `tone` recolors (e.g. amber when stalled). Optional right-aligned value.
 */
function ProgressBar({
  value = 0,
  tone = 'accent',
  striped = false,
  height = 6,
  label,
  style = {}
}) {
  const v = Math.max(0, Math.min(100, value));
  const fills = {
    accent: 'var(--accent)',
    ok: 'var(--green-500)',
    active: 'var(--status-active)',
    warn: 'var(--amber-500)',
    error: 'var(--red-500)'
  };
  const fill = fills[tone] || fills.accent;
  return /*#__PURE__*/React.createElement("div", {
    style: {
      ...style
    }
  }, label && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'space-between',
      marginBottom: '5px',
      font: 'var(--text-xs) var(--font-mono)',
      color: 'var(--text-muted)'
    }
  }, /*#__PURE__*/React.createElement("span", null, label), /*#__PURE__*/React.createElement("span", {
    style: {
      color: 'var(--text-body)'
    }
  }, v, "%")), /*#__PURE__*/React.createElement("div", {
    style: {
      height,
      background: 'var(--surface-inset)',
      borderRadius: 'var(--radius-pill)',
      boxShadow: 'var(--inset-well)',
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: `${v}%`,
      height: '100%',
      borderRadius: 'var(--radius-pill)',
      background: striped ? `repeating-linear-gradient(-45deg, ${fill} 0 8px, color-mix(in oklab, ${fill}, black 18%) 8px 16px)` : fill,
      backgroundSize: striped ? '24px 24px' : 'auto',
      animation: striped ? 'flow-dash 0.7s linear infinite' : 'none',
      transition: 'width var(--dur-slow) var(--ease-out)'
    }
  })));
}
Object.assign(__ds_scope, { ProgressBar });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/display/ProgressBar.jsx", error: String((e && e.message) || e) }); }

// components/display/StatusDot.jsx
try { (() => {
/**
 * StatusDot — the connection/health atom. A filled dot with an
 * optional soft halo and pulse for "live" states.
 * status: ok | active | warn | error | idle
 */
function StatusDot({
  status = 'idle',
  pulse = false,
  size = 9,
  halo = true,
  style = {}
}) {
  const map = {
    ok: {
      c: 'var(--status-ok)',
      g: 'var(--green-glow)'
    },
    active: {
      c: 'var(--status-active)',
      g: 'var(--accent-glow)'
    },
    warn: {
      c: 'var(--status-warn)',
      g: 'var(--amber-glow)'
    },
    error: {
      c: 'var(--status-error)',
      g: 'var(--red-glow)'
    },
    idle: {
      c: 'var(--status-idle)',
      g: 'transparent'
    }
  };
  const s = map[status] || map.idle;
  return /*#__PURE__*/React.createElement("span", {
    "aria-label": status,
    style: {
      display: 'inline-block',
      width: size,
      height: size,
      flexShrink: 0,
      borderRadius: '50%',
      background: s.c,
      boxShadow: halo ? `0 0 0 3px ${s.g}` : 'none',
      animation: pulse ? 'var(--pulse)' : 'none',
      ...style
    }
  });
}
Object.assign(__ds_scope, { StatusDot });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/display/StatusDot.jsx", error: String((e && e.message) || e) }); }

// components/forms/Button.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/**
 * Harrbor Button — the primary action control.
 * Variants: primary (teal signal), secondary (outlined), ghost, danger.
 * Mechanical press: no bounce, quick color shift, subtle depress.
 */
function Button({
  variant = 'secondary',
  size = 'md',
  iconLeft = null,
  iconRight = null,
  disabled = false,
  full = false,
  children,
  style = {},
  ...rest
}) {
  const heights = {
    sm: 'var(--control-sm)',
    md: 'var(--control-md)',
    lg: 'var(--control-lg)'
  };
  const pads = {
    sm: '0 10px',
    md: '0 14px',
    lg: '0 18px'
  };
  const fonts = {
    sm: 'var(--text-xs)',
    md: 'var(--text-base)',
    lg: 'var(--text-md)'
  };
  const palettes = {
    primary: {
      background: 'var(--accent)',
      color: 'var(--text-invert)',
      border: '1px solid var(--amber-400)',
      fontWeight: 'var(--fw-semibold)',
      boxShadow: '0 1px 0 rgba(255,255,255,0.12) inset, var(--shadow-sm)'
    },
    secondary: {
      background: 'var(--surface-inset)',
      color: 'var(--text-body)',
      border: '1px solid var(--border-strong)',
      fontWeight: 'var(--fw-medium)',
      boxShadow: 'var(--edge-light)'
    },
    ghost: {
      background: 'transparent',
      color: 'var(--text-muted)',
      border: '1px solid transparent',
      fontWeight: 'var(--fw-medium)'
    },
    danger: {
      background: 'transparent',
      color: 'var(--red-300)',
      border: '1px solid var(--red-500)',
      fontWeight: 'var(--fw-medium)'
    }
  };
  const p = palettes[variant] || palettes.secondary;
  return /*#__PURE__*/React.createElement("button", _extends({
    disabled: disabled,
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      gap: '7px',
      height: heights[size],
      padding: pads[size],
      width: full ? '100%' : 'auto',
      font: `${fonts[size]} var(--font-sans)`,
      letterSpacing: 'var(--tracking-normal)',
      borderRadius: 'var(--radius-sm)',
      cursor: disabled ? 'not-allowed' : 'pointer',
      whiteSpace: 'nowrap',
      opacity: disabled ? 0.45 : 1,
      transition: 'background var(--dur-fast) var(--ease-out), transform var(--dur-fast) var(--ease-out), border-color var(--dur-fast)',
      ...p,
      ...style
    },
    onMouseDown: e => {
      if (!disabled) e.currentTarget.style.transform = 'translateY(1px)';
    },
    onMouseUp: e => {
      e.currentTarget.style.transform = 'translateY(0)';
    },
    onMouseLeave: e => {
      e.currentTarget.style.transform = 'translateY(0)';
    }
  }, rest), iconLeft && /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'inline-flex',
      fontSize: '1.1em'
    }
  }, iconLeft), children, iconRight && /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'inline-flex',
      fontSize: '1.1em'
    }
  }, iconRight));
}
Object.assign(__ds_scope, { Button });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Button.jsx", error: String((e && e.message) || e) }); }

// components/forms/IconButton.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/**
 * IconButton — square control for a single glyph. Used in toolbars,
 * table rows, card headers. Pass an icon node as children.
 */
function IconButton({
  size = 'md',
  variant = 'ghost',
  disabled = false,
  label,
  children,
  style = {},
  ...rest
}) {
  const dims = {
    sm: 26,
    md: 32,
    lg: 38
  };
  const d = dims[size];
  const palettes = {
    ghost: {
      background: 'transparent',
      color: 'var(--text-muted)',
      border: '1px solid transparent'
    },
    solid: {
      background: 'var(--surface-inset)',
      color: 'var(--text-body)',
      border: '1px solid var(--border-strong)'
    },
    accent: {
      background: 'var(--accent-glow)',
      color: 'var(--accent-text)',
      border: '1px solid var(--amber-600)'
    }
  };
  const p = palettes[variant] || palettes.ghost;
  return /*#__PURE__*/React.createElement("button", _extends({
    "aria-label": label,
    title: label,
    disabled: disabled,
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      width: d,
      height: d,
      borderRadius: 'var(--radius-sm)',
      cursor: disabled ? 'not-allowed' : 'pointer',
      opacity: disabled ? 0.4 : 1,
      transition: 'background var(--dur-fast) var(--ease-out), color var(--dur-fast)',
      ...p,
      ...style
    },
    onMouseEnter: e => {
      if (!disabled && variant === 'ghost') {
        e.currentTarget.style.background = 'var(--surface-hover)';
        e.currentTarget.style.color = 'var(--text-body)';
      }
    },
    onMouseLeave: e => {
      if (variant === 'ghost') {
        e.currentTarget.style.background = 'transparent';
        e.currentTarget.style.color = 'var(--text-muted)';
      }
    }
  }, rest), children);
}
Object.assign(__ds_scope, { IconButton });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/IconButton.jsx", error: String((e && e.message) || e) }); }

// components/forms/SecretField.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
const {
  useState
} = React;
/**
 * SecretField — masked input for API keys / passwords with a reveal
 * toggle. Shows a dotted mask by default; monospace when revealed so
 * the operator can eyeball the key. Optional "set" indicator.
 */
function SecretField({
  label,
  hint,
  value,
  isSet = false,
  size = 'md',
  style = {},
  id,
  ...rest
}) {
  const [revealed, setRevealed] = useState(false);
  const heights = {
    sm: 'var(--control-sm)',
    md: 'var(--control-md)',
    lg: 'var(--control-lg)'
  };
  const fid = id || (label ? `sf-${label.replace(/\s+/g, '-').toLowerCase()}` : undefined);
  return /*#__PURE__*/React.createElement("label", {
    htmlFor: fid,
    style: {
      display: 'block',
      ...style
    }
  }, label && /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'flex',
      justifyContent: 'space-between',
      alignItems: 'baseline',
      font: 'var(--type-label)',
      color: 'var(--text-muted)',
      marginBottom: '6px',
      letterSpacing: 'var(--tracking-wide)'
    }
  }, label, isSet && /*#__PURE__*/React.createElement("span", {
    style: {
      font: 'var(--text-2xs) var(--font-sans)',
      color: 'var(--green-300)',
      letterSpacing: 'var(--tracking-caps)',
      textTransform: 'uppercase'
    }
  }, "\u25CF stored")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      height: heights[size],
      background: 'var(--surface-inset)',
      border: '1px solid var(--border-strong)',
      borderRadius: 'var(--radius-sm)',
      boxShadow: 'var(--inset-well)',
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("input", _extends({
    id: fid,
    type: revealed ? 'text' : 'password',
    defaultValue: value,
    placeholder: isSet ? '••••••••••••••••' : 'Enter secret',
    style: {
      flex: 1,
      minWidth: 0,
      height: '100%',
      padding: '0 11px',
      background: 'transparent',
      border: 'none',
      outline: 'none',
      color: 'var(--text-heading)',
      font: 'var(--type-mono)',
      letterSpacing: revealed ? 0 : '0.15em'
    },
    onFocus: e => {
      const box = e.currentTarget.parentElement;
      box.style.borderColor = 'var(--border-focus)';
      box.style.boxShadow = 'var(--inset-well), 0 0 0 3px var(--accent-glow)';
    },
    onBlur: e => {
      const box = e.currentTarget.parentElement;
      box.style.borderColor = 'var(--border-strong)';
      box.style.boxShadow = 'var(--inset-well)';
    }
  }, rest)), /*#__PURE__*/React.createElement("button", {
    type: "button",
    "aria-label": revealed ? 'Hide secret' : 'Reveal secret',
    onClick: () => setRevealed(v => !v),
    style: {
      alignSelf: 'stretch',
      padding: '0 11px',
      background: 'transparent',
      border: 'none',
      borderLeft: '1px solid var(--border-subtle)',
      color: 'var(--text-faint)',
      cursor: 'pointer',
      font: 'var(--text-2xs) var(--font-sans)',
      textTransform: 'uppercase',
      letterSpacing: 'var(--tracking-caps)'
    }
  }, revealed ? 'Hide' : 'Show')), hint && /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'block',
      marginTop: '6px',
      font: 'var(--text-xs) var(--font-sans)',
      color: 'var(--text-faint)'
    }
  }, hint));
}
Object.assign(__ds_scope, { SecretField });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/SecretField.jsx", error: String((e && e.message) || e) }); }

// components/forms/Select.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/**
 * Select — native dropdown styled to match the inset-well controls.
 * Pass options as [{value, label}] or children <option>s.
 */
function Select({
  label,
  hint,
  options = [],
  size = 'md',
  style = {},
  id,
  children,
  ...rest
}) {
  const heights = {
    sm: 'var(--control-sm)',
    md: 'var(--control-md)',
    lg: 'var(--control-lg)'
  };
  const fid = id || (label ? `sel-${label.replace(/\s+/g, '-').toLowerCase()}` : undefined);
  return /*#__PURE__*/React.createElement("label", {
    htmlFor: fid,
    style: {
      display: 'block',
      ...style
    }
  }, label && /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'block',
      font: 'var(--type-label)',
      color: 'var(--text-muted)',
      marginBottom: '6px',
      letterSpacing: 'var(--tracking-wide)'
    }
  }, label), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative'
    }
  }, /*#__PURE__*/React.createElement("select", _extends({
    id: fid,
    style: {
      width: '100%',
      height: heights[size],
      padding: '0 32px 0 11px',
      background: 'var(--surface-inset)',
      border: '1px solid var(--border-strong)',
      borderRadius: 'var(--radius-sm)',
      boxShadow: 'var(--inset-well)',
      color: 'var(--text-heading)',
      font: `var(--text-base) var(--font-sans)`,
      outline: 'none',
      appearance: 'none',
      cursor: 'pointer'
    },
    onFocus: e => {
      e.currentTarget.style.borderColor = 'var(--border-focus)';
      e.currentTarget.style.boxShadow = 'var(--inset-well), 0 0 0 3px var(--accent-glow)';
    },
    onBlur: e => {
      e.currentTarget.style.borderColor = 'var(--border-strong)';
      e.currentTarget.style.boxShadow = 'var(--inset-well)';
    }
  }, rest), options.map(o => /*#__PURE__*/React.createElement("option", {
    key: o.value,
    value: o.value
  }, o.label)), children), /*#__PURE__*/React.createElement("span", {
    "aria-hidden": true,
    style: {
      position: 'absolute',
      right: '11px',
      top: '50%',
      transform: 'translateY(-50%)',
      pointerEvents: 'none',
      color: 'var(--text-faint)',
      fontSize: '10px'
    }
  }, "\u25BE")), hint && /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'block',
      marginTop: '6px',
      font: 'var(--text-xs) var(--font-sans)',
      color: 'var(--text-faint)'
    }
  }, hint));
}
Object.assign(__ds_scope, { Select });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Select.jsx", error: String((e && e.message) || e) }); }

// components/forms/Switch.jsx
try { (() => {
/**
 * Switch — on/off toggle. Teal when on, recessed track when off.
 * Controlled via `checked` + `onChange`.
 */
function Switch({
  checked = false,
  onChange,
  disabled = false,
  label,
  style = {}
}) {
  const toggle = () => {
    if (!disabled && onChange) onChange(!checked);
  };
  return /*#__PURE__*/React.createElement("span", {
    role: "switch",
    "aria-checked": checked,
    "aria-label": label,
    onClick: toggle,
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      gap: '9px',
      cursor: disabled ? 'not-allowed' : 'pointer',
      opacity: disabled ? 0.5 : 1,
      userSelect: 'none',
      ...style
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      position: 'relative',
      width: 34,
      height: 19,
      flexShrink: 0,
      borderRadius: 'var(--radius-pill)',
      background: checked ? 'var(--accent)' : 'var(--surface-inset)',
      border: `1px solid ${checked ? 'var(--amber-400)' : 'var(--border-strong)'}`,
      boxShadow: checked ? '0 0 0 3px var(--accent-glow)' : 'var(--inset-well)',
      transition: 'background var(--dur-med) var(--ease-out), box-shadow var(--dur-med)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      position: 'absolute',
      top: 2,
      left: checked ? 16 : 2,
      width: 13,
      height: 13,
      borderRadius: '50%',
      background: checked ? 'var(--text-invert)' : 'var(--slate-400)',
      transition: 'left var(--dur-med) var(--ease-out), background var(--dur-med)',
      boxShadow: 'var(--shadow-sm)'
    }
  })), label && /*#__PURE__*/React.createElement("span", {
    style: {
      font: 'var(--text-base) var(--font-sans)',
      color: 'var(--text-body)'
    }
  }, label));
}
Object.assign(__ds_scope, { Switch });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Switch.jsx", error: String((e && e.message) || e) }); }

// components/forms/TextField.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/**
 * TextField — labelled text input with optional mono mode (for paths,
 * hosts, ports) and error / hint states. Recessed inset well styling.
 */
function TextField({
  label,
  hint,
  error,
  mono = false,
  prefix = null,
  size = 'md',
  style = {},
  id,
  ...rest
}) {
  const heights = {
    sm: 'var(--control-sm)',
    md: 'var(--control-md)',
    lg: 'var(--control-lg)'
  };
  const fid = id || (label ? `tf-${label.replace(/\s+/g, '-').toLowerCase()}` : undefined);
  return /*#__PURE__*/React.createElement("label", {
    htmlFor: fid,
    style: {
      display: 'block',
      ...style
    }
  }, label && /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'block',
      font: 'var(--type-label)',
      color: 'var(--text-muted)',
      marginBottom: '6px',
      letterSpacing: 'var(--tracking-wide)'
    }
  }, label), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      height: heights[size],
      background: 'var(--surface-inset)',
      border: `1px solid ${error ? 'var(--red-500)' : 'var(--border-strong)'}`,
      borderRadius: 'var(--radius-sm)',
      boxShadow: 'var(--inset-well)',
      transition: 'border-color var(--dur-fast), box-shadow var(--dur-fast)',
      overflow: 'hidden'
    }
  }, prefix && /*#__PURE__*/React.createElement("span", {
    style: {
      padding: '0 8px 0 10px',
      color: 'var(--text-faint)',
      font: 'var(--type-mono)',
      borderRight: '1px solid var(--border-subtle)',
      alignSelf: 'stretch',
      display: 'flex',
      alignItems: 'center'
    }
  }, prefix), /*#__PURE__*/React.createElement("input", _extends({
    id: fid,
    style: {
      flex: 1,
      minWidth: 0,
      height: '100%',
      padding: '0 11px',
      background: 'transparent',
      border: 'none',
      outline: 'none',
      color: 'var(--text-heading)',
      font: mono ? 'var(--type-mono)' : `var(--text-base) var(--font-sans)`
    },
    onFocus: e => {
      const box = e.currentTarget.parentElement;
      box.style.borderColor = 'var(--border-focus)';
      box.style.boxShadow = 'var(--inset-well), 0 0 0 3px var(--accent-glow)';
    },
    onBlur: e => {
      const box = e.currentTarget.parentElement;
      box.style.borderColor = error ? 'var(--red-500)' : 'var(--border-strong)';
      box.style.boxShadow = 'var(--inset-well)';
    }
  }, rest))), (hint || error) && /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'block',
      marginTop: '6px',
      font: 'var(--text-xs) var(--font-sans)',
      color: error ? 'var(--red-300)' : 'var(--text-faint)'
    }
  }, error || hint));
}
Object.assign(__ds_scope, { TextField });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/TextField.jsx", error: String((e && e.message) || e) }); }

// components/pipeline/AttentionItem.jsx
try { (() => {
/**
 * AttentionItem — one row in the dashboard's attention/failures panel.
 * A severity dot, a plain-language message, the offending release id in
 * mono, and an optional resolve action (Retry / Skip / Inspect).
 * severity: error | warn | info
 */
function AttentionItem({
  severity = 'error',
  message,
  releaseId,
  phase,
  time,
  action,
  onAction,
  style = {}
}) {
  const sev = {
    error: 'error',
    warn: 'warn',
    info: 'active'
  }[severity] || 'error';
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'flex-start',
      gap: '11px',
      padding: '11px 13px',
      borderRadius: 'var(--radius-sm)',
      background: 'var(--surface-panel)',
      border: '1px solid var(--border-subtle)',
      ...style
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      marginTop: '3px'
    }
  }, /*#__PURE__*/React.createElement(__ds_scope.StatusDot, {
    status: sev,
    size: 8
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      minWidth: 0
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      font: `var(--fw-medium) var(--text-base) var(--font-sans)`,
      color: 'var(--text-heading)',
      marginBottom: '3px'
    }
  }, message), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: '10px',
      font: 'var(--text-2xs) var(--font-mono)',
      color: 'var(--text-faint)'
    }
  }, releaseId && /*#__PURE__*/React.createElement("span", {
    style: {
      color: 'var(--text-muted)'
    }
  }, releaseId), phase && /*#__PURE__*/React.createElement("span", null, "\xB7 ", phase), time && /*#__PURE__*/React.createElement("span", null, "\xB7 ", time))), action && /*#__PURE__*/React.createElement(__ds_scope.Button, {
    size: "sm",
    variant: severity === 'error' ? 'primary' : 'secondary',
    onClick: onAction
  }, action));
}
Object.assign(__ds_scope, { AttentionItem });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/pipeline/AttentionItem.jsx", error: String((e && e.message) || e) }); }

// components/pipeline/ConnectionRow.jsx
try { (() => {
/**
 * ConnectionRow — one service in the connection-health panel
 * (qBittorrent, Sonarr, Radarr, SFTP). Status dot, name, host in mono,
 * and a right-aligned detail (latency, version, or error).
 * status: ok | warn | error | idle
 */
function ConnectionRow({
  name,
  host,
  status = 'ok',
  detail,
  kind,
  style = {}
}) {
  const statusLabel = {
    ok: 'Connected',
    warn: 'Degraded',
    error: 'Offline',
    idle: 'Not set'
  }[status];
  const detailColor = {
    ok: 'var(--text-muted)',
    warn: 'var(--amber-300)',
    error: 'var(--red-300)',
    idle: 'var(--text-faint)'
  }[status];
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: '11px',
      padding: '11px 13px',
      borderRadius: 'var(--radius-sm)',
      background: 'var(--surface-panel)',
      border: '1px solid var(--border-subtle)',
      ...style
    }
  }, /*#__PURE__*/React.createElement(__ds_scope.StatusDot, {
    status: status,
    pulse: status === 'ok',
    size: 9
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      minWidth: 0,
      flex: 1
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'baseline',
      gap: '8px'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      font: `var(--fw-semibold) var(--text-base) var(--font-sans)`,
      color: 'var(--text-heading)'
    }
  }, name), kind && /*#__PURE__*/React.createElement("span", {
    style: {
      font: 'var(--text-2xs) var(--font-sans)',
      color: 'var(--text-faint)',
      letterSpacing: 'var(--tracking-caps)',
      textTransform: 'uppercase'
    }
  }, kind)), host && /*#__PURE__*/React.createElement("div", {
    style: {
      font: 'var(--text-xs) var(--font-mono)',
      color: 'var(--text-faint)',
      marginTop: '2px',
      overflow: 'hidden',
      textOverflow: 'ellipsis',
      whiteSpace: 'nowrap'
    }
  }, host)), /*#__PURE__*/React.createElement("div", {
    style: {
      textAlign: 'right',
      flexShrink: 0
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      font: `var(--fw-medium) var(--text-xs) var(--font-sans)`,
      color: detailColor
    }
  }, statusLabel), detail && /*#__PURE__*/React.createElement("div", {
    style: {
      font: 'var(--text-2xs) var(--font-mono)',
      color: 'var(--text-faint)',
      marginTop: '2px'
    }
  }, detail)));
}
Object.assign(__ds_scope, { ConnectionRow });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/pipeline/ConnectionRow.jsx", error: String((e && e.message) || e) }); }

// components/pipeline/FlowRail.jsx
try { (() => {
/**
 * FlowRail — the signature Harrbor motif. Shows the whole six-phase
 * pipeline as one connected line (transit-diagram meets signal meter):
 * a node per phase in its hue, connected by a rail whose segments
 * shimmer where releases are actively moving. Stuck phases get an amber
 * ring; a fully clear phase reads dim. Doubles as column-jump nav.
 *
 * Also the home of the "harbor-light": pass `bloom` to place one soft
 * amber glow behind the phase that's the live focus of the screen.
 */
const ORDER = ['discover', 'download', 'transfer', 'import', 'cleanup', 'archival'];
function FlowRail({
  phases = [],
  variant = 'full',
  activePhase = null,
  bloom = null,
  onSelect,
  style = {}
}) {
  // normalize into ordered list
  const byPhase = {};
  phases.forEach(p => {
    byPhase[p.phase] = p;
  });
  const items = ORDER.map(ph => byPhase[ph] || {
    phase: ph,
    count: 0
  });
  const condensed = variant === 'condensed';
  const nodeSize = condensed ? 22 : 30;
  const labelFs = condensed ? '9px' : 'var(--text-2xs)';
  const meta = __ds_scope.PhaseChip.PHASES;

  // build a 3-row grid: labels / node-band / counts
  const cols = [];
  items.forEach((_, i) => {
    cols.push('auto');
    if (i < items.length - 1) cols.push('1fr');
  });
  const cells = [];
  items.forEach((it, i) => {
    const p = meta[it.phase];
    const nodeCol = 2 * i + 1;
    const moving = it.active && it.count > 0;
    const clear = !it.count;
    const isSel = activePhase === it.phase;

    // label (row 1)
    if (!condensed) {
      cells.push(/*#__PURE__*/React.createElement("div", {
        key: `l${i}`,
        style: {
          gridColumn: nodeCol,
          gridRow: 1,
          justifySelf: 'center',
          font: `var(--fw-semibold) ${labelFs} var(--font-mono)`,
          letterSpacing: 'var(--tracking-caps)',
          textTransform: 'uppercase',
          color: isSel ? 'var(--text-body)' : 'var(--text-faint)',
          marginBottom: '9px',
          whiteSpace: 'nowrap'
        }
      }, p.n));
    }

    // node (row 2)
    cells.push(/*#__PURE__*/React.createElement("button", {
      key: `n${i}`,
      type: "button",
      onClick: onSelect ? () => onSelect(it.phase) : undefined,
      title: `${p.n} · ${it.count}`,
      style: {
        gridColumn: nodeCol,
        gridRow: 2,
        justifySelf: 'center',
        position: 'relative',
        width: nodeSize,
        height: nodeSize,
        padding: 0,
        borderRadius: condensed ? 'var(--radius-xs)' : 'var(--radius-sm)',
        background: clear ? 'var(--surface-inset)' : `color-mix(in oklab, ${p.c}, transparent 78%)`,
        border: `1.5px solid ${it.stuck ? 'var(--amber-500)' : clear ? 'var(--border-strong)' : p.c}`,
        boxShadow: it.stuck ? '0 0 0 3px var(--amber-glow)' : moving ? `0 0 0 3px color-mix(in oklab, ${p.c}, transparent 82%)` : 'none',
        cursor: onSelect ? 'pointer' : 'default',
        display: 'grid',
        placeItems: 'center',
        transition: 'transform var(--dur-fast) var(--ease-out)'
      },
      onMouseEnter: onSelect ? e => {
        e.currentTarget.style.transform = 'translateY(-2px)';
      } : undefined,
      onMouseLeave: onSelect ? e => {
        e.currentTarget.style.transform = 'translateY(0)';
      } : undefined
    }, /*#__PURE__*/React.createElement("span", {
      style: {
        width: condensed ? 6 : 9,
        height: condensed ? 6 : 9,
        borderRadius: 2,
        background: clear ? 'var(--slate-600)' : p.c,
        animation: moving ? 'var(--pulse)' : 'none'
      }
    }), it.stuck && /*#__PURE__*/React.createElement("span", {
      style: {
        position: 'absolute',
        top: -6,
        right: -6,
        minWidth: 14,
        height: 14,
        padding: '0 3px',
        borderRadius: 'var(--radius-pill)',
        background: 'var(--amber-500)',
        color: 'var(--slate-1000)',
        font: `var(--fw-bold) 9px/14px var(--font-mono)`,
        textAlign: 'center'
      }
    }, it.stuckCount || '!')));

    // count (row 3)
    cells.push(/*#__PURE__*/React.createElement("div", {
      key: `c${i}`,
      style: {
        gridColumn: nodeCol,
        gridRow: 3,
        justifySelf: 'center',
        marginTop: '8px',
        font: `var(--fw-semibold) ${condensed ? 'var(--text-xs)' : 'var(--text-sm)'} var(--font-mono)`,
        color: clear ? 'var(--text-faint)' : 'var(--text-body)',
        fontVariantNumeric: 'tabular-nums'
      }
    }, it.count > 999 ? `${(it.count / 1000).toFixed(1)}k` : it.count));

    // connector (row 2, between nodes)
    if (i < items.length - 1) {
      const next = items[i + 1];
      const segMoving = it.active && it.count > 0 || next.active && next.count > 0;
      cells.push(/*#__PURE__*/React.createElement("div", {
        key: `s${i}`,
        style: {
          gridColumn: nodeCol + 1,
          gridRow: 2,
          alignSelf: 'center',
          height: 2,
          margin: '0 6px',
          position: 'relative'
        }
      }, /*#__PURE__*/React.createElement("div", {
        style: {
          position: 'absolute',
          inset: 0,
          borderRadius: 'var(--radius-pill)',
          background: segMoving ? `repeating-linear-gradient(90deg, ${p.c} 0 7px, transparent 7px 14px)` : 'var(--border-strong)',
          backgroundSize: segMoving ? '14px 2px' : 'auto',
          animation: segMoving ? 'flow-dash 0.9s linear infinite' : 'none',
          opacity: segMoving ? 0.9 : 0.5
        }
      })));
    }
  });

  // bloom target — soft amber light behind one node
  let bloomEl = null;
  if (bloom != null) {
    const bi = typeof bloom === 'number' ? bloom : ORDER.indexOf(bloom);
    if (bi >= 0) {
      const leftPct = (bi + 0.5) / items.length * 100;
      bloomEl = /*#__PURE__*/React.createElement("div", {
        "aria-hidden": true,
        style: {
          position: 'absolute',
          top: '50%',
          left: `${leftPct}%`,
          width: 200,
          height: 160,
          transform: 'translate(-50%, -50%)',
          pointerEvents: 'none',
          background: 'var(--harbor-bloom)',
          animation: 'bloom-drift 3.2s var(--ease-inout) infinite'
        }
      });
    }
  }
  return /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative',
      ...style
    }
  }, bloomEl, /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative',
      display: 'grid',
      gridTemplateColumns: cols.join(' '),
      alignItems: 'center',
      padding: condensed ? '4px 6px' : '4px 10px'
    }
  }, cells));
}
Object.assign(__ds_scope, { FlowRail });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/pipeline/FlowRail.jsx", error: String((e && e.message) || e) }); }

// components/pipeline/ReleaseCard.jsx
try { (() => {
/**
 * ReleaseCard — one release as it sits in a pipeline column. Mono title
 * (the actual release name), job lane, size, and — when transferring —
 * a live progress track. `stuck` flags a stalled item with an amber edge.
 */
function ReleaseCard({
  name,
  job,
  phase = 'transfer',
  size,
  progress = null,
  stuck = false,
  active = false,
  eta,
  style = {}
}) {
  return /*#__PURE__*/React.createElement("article", {
    style: {
      position: 'relative',
      padding: '10px 12px',
      background: 'var(--surface-panel)',
      border: `1px solid ${stuck ? 'var(--amber-500)' : 'var(--border-subtle)'}`,
      borderRadius: 'var(--radius-md)',
      boxShadow: stuck ? '0 0 0 3px var(--amber-glow)' : 'var(--edge-light)',
      cursor: 'pointer',
      transition: 'border-color var(--dur-fast), transform var(--dur-fast)',
      ...style
    },
    onMouseEnter: e => {
      if (!stuck) e.currentTarget.style.borderColor = 'var(--border-focus)';
      e.currentTarget.style.transform = 'translateY(-1px)';
    },
    onMouseLeave: e => {
      if (!stuck) e.currentTarget.style.borderColor = 'var(--border-subtle)';
      e.currentTarget.style.transform = 'translateY(0)';
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: '7px',
      marginBottom: '8px'
    }
  }, active && /*#__PURE__*/React.createElement(__ds_scope.StatusDot, {
    status: "active",
    pulse: true,
    size: 7
  }), /*#__PURE__*/React.createElement("span", {
    style: {
      font: 'var(--type-mono)',
      color: 'var(--text-heading)',
      whiteSpace: 'nowrap',
      overflow: 'hidden',
      textOverflow: 'ellipsis',
      flex: 1
    }
  }, name), stuck && /*#__PURE__*/React.createElement(__ds_scope.StatusDot, {
    status: "warn",
    size: 7
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: '8px',
      marginBottom: progress != null ? '9px' : 0
    }
  }, /*#__PURE__*/React.createElement(__ds_scope.PhaseChip, {
    phase: phase,
    size: "sm"
  }), /*#__PURE__*/React.createElement("span", {
    style: {
      marginLeft: 'auto',
      font: 'var(--text-2xs) var(--font-mono)',
      color: 'var(--text-faint)'
    }
  }, job, size ? ` · ${size}` : '')), progress != null && /*#__PURE__*/React.createElement(__ds_scope.ProgressBar, {
    value: progress,
    striped: active && !stuck,
    tone: stuck ? 'warn' : 'active',
    height: 5
  }), eta && /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: '6px',
      font: 'var(--text-2xs) var(--font-mono)',
      color: stuck ? 'var(--amber-300)' : 'var(--text-faint)'
    }
  }, stuck ? '⚠ stalled' : `eta ${eta}`));
}
Object.assign(__ds_scope, { ReleaseCard });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/pipeline/ReleaseCard.jsx", error: String((e && e.message) || e) }); }

// ui_kits/harrbor-app/AppShell.jsx
try { (() => {
// Harrbor app shell — sidebar nav + topbar. Shared chrome for all screens.
(() => {
  const {
    StatusDot,
    Badge
  } = window.HarrborDesignSystem_2a1653;
  const NAV = [{
    id: 'dashboard',
    label: 'Dashboard',
    icon: '◵'
  }, {
    id: 'pipeline',
    label: 'Pipeline',
    icon: '⇥'
  }, {
    id: 'jobs',
    label: 'Jobs',
    icon: '≡'
  }, {
    id: 'settings',
    label: 'Connections',
    icon: '⚙'
  }];

  // Ticking mono telemetry — makes the whole console feel live/monitored.
  function Telemetry() {
    const [t, setT] = React.useState({
      rate: 48.2,
      flight: 6,
      sync: 12
    });
    React.useEffect(() => {
      const id = setInterval(() => {
        setT(prev => ({
          rate: Math.max(0, +(prev.rate + (Math.random() - 0.5) * 6).toFixed(1)),
          flight: Math.max(0, prev.flight + (Math.random() < 0.3 ? Math.random() < 0.5 ? -1 : 1 : 0)),
          sync: prev.sync >= 30 ? 0 : prev.sync + 2
        }));
      }, 2000);
      return () => clearInterval(id);
    }, []);
    return /*#__PURE__*/React.createElement("div", {
      style: {
        font: 'var(--text-2xs) var(--font-mono)',
        color: 'var(--text-faint)',
        lineHeight: 1.6,
        fontVariantNumeric: 'tabular-nums'
      }
    }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("span", {
      style: {
        color: 'var(--status-active)'
      }
    }, "\u2191"), " ", t.rate.toFixed(1), " MB/s \xB7 ", t.flight, " in flight"), /*#__PURE__*/React.createElement("div", null, "sync ", t.sync, "s ago \xB7 up 6d 04h \xB7 v0.9.4"));
  }
  function Sidebar({
    current,
    onNav
  }) {
    return /*#__PURE__*/React.createElement("aside", {
      style: {
        width: 'var(--sidebar-w)',
        flexShrink: 0,
        background: 'var(--bg-app)',
        borderRight: '1px solid var(--border-subtle)',
        display: 'flex',
        flexDirection: 'column',
        padding: '0 0 12px'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: '10px',
        height: 'var(--topbar-h)',
        padding: '0 16px',
        borderBottom: '1px solid var(--border-subtle)'
      }
    }, /*#__PURE__*/React.createElement("span", {
      style: {
        position: 'relative',
        display: 'grid',
        placeItems: 'center',
        width: 28,
        height: 28
      }
    }, /*#__PURE__*/React.createElement("span", {
      "aria-hidden": true,
      style: {
        position: 'absolute',
        width: 34,
        height: 34,
        borderRadius: '50%',
        background: 'var(--harbor-bloom)',
        animation: 'beacon-breath 3.6s var(--ease-inout) infinite'
      }
    }), /*#__PURE__*/React.createElement("img", {
      src: "../../assets/harrbor-mark.svg",
      width: "28",
      height: "28",
      alt: "",
      style: {
        position: 'relative'
      }
    })), /*#__PURE__*/React.createElement("span", {
      style: {
        font: 'var(--fw-semibold) var(--text-lg) var(--font-sans)',
        color: 'var(--text-heading)',
        letterSpacing: 'var(--tracking-tight)'
      }
    }, "Harrbor")), /*#__PURE__*/React.createElement("nav", {
      style: {
        padding: '12px 10px',
        display: 'flex',
        flexDirection: 'column',
        gap: '2px'
      }
    }, NAV.map(n => {
      const active = current === n.id;
      return /*#__PURE__*/React.createElement("button", {
        key: n.id,
        onClick: () => onNav(n.id),
        style: {
          display: 'flex',
          alignItems: 'center',
          gap: '11px',
          padding: '0 12px',
          height: 34,
          borderRadius: 'var(--radius-sm)',
          border: '1px solid transparent',
          background: active ? 'var(--accent-glow)' : 'transparent',
          color: active ? 'var(--accent-text)' : 'var(--text-muted)',
          font: `${active ? 'var(--fw-semibold)' : 'var(--fw-medium)'} var(--text-base) var(--font-sans)`,
          cursor: 'pointer',
          textAlign: 'left',
          width: '100%',
          boxShadow: active ? 'inset 2px 0 0 var(--accent)' : 'none'
        },
        onMouseEnter: e => {
          if (!active) e.currentTarget.style.background = 'var(--surface-hover)';
        },
        onMouseLeave: e => {
          if (!active) e.currentTarget.style.background = 'transparent';
        }
      }, /*#__PURE__*/React.createElement("span", {
        style: {
          width: 16,
          textAlign: 'center',
          fontSize: '14px'
        }
      }, n.icon), n.label);
    })), /*#__PURE__*/React.createElement("div", {
      style: {
        marginTop: 'auto',
        padding: '12px 16px',
        borderTop: '1px solid var(--border-subtle)'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        marginBottom: '9px'
      }
    }, /*#__PURE__*/React.createElement(StatusDot, {
      status: "ok",
      pulse: true,
      size: 8
    }), /*#__PURE__*/React.createElement("span", {
      style: {
        font: 'var(--text-xs) var(--font-sans)',
        color: 'var(--text-muted)'
      }
    }, "Orchestrator online")), /*#__PURE__*/React.createElement(Telemetry, null)));
  }
  function Topbar({
    title,
    subtitle,
    right
  }) {
    return /*#__PURE__*/React.createElement("header", {
      style: {
        height: 'var(--topbar-h)',
        flexShrink: 0,
        display: 'flex',
        alignItems: 'center',
        gap: '14px',
        padding: '0 22px',
        borderBottom: '1px solid var(--border-subtle)',
        background: 'var(--bg-page)'
      }
    }, /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
      style: {
        font: 'var(--fw-semibold) var(--text-lg) var(--font-sans)',
        color: 'var(--text-heading)'
      }
    }, title), subtitle && /*#__PURE__*/React.createElement("div", {
      style: {
        font: 'var(--text-2xs) var(--font-mono)',
        color: 'var(--text-faint)',
        marginTop: '1px'
      }
    }, subtitle)), /*#__PURE__*/React.createElement("div", {
      style: {
        marginLeft: 'auto',
        display: 'flex',
        alignItems: 'center',
        gap: '10px'
      }
    }, right));
  }
  function AppShell({
    current,
    onNav,
    title,
    subtitle,
    topRight,
    children
  }) {
    return /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        height: '100vh',
        background: 'var(--bg-page)',
        color: 'var(--text-body)',
        font: 'var(--type-body)'
      }
    }, /*#__PURE__*/React.createElement(Sidebar, {
      current: current,
      onNav: onNav
    }), /*#__PURE__*/React.createElement("div", {
      style: {
        flex: 1,
        minWidth: 0,
        display: 'flex',
        flexDirection: 'column'
      }
    }, /*#__PURE__*/React.createElement(Topbar, {
      title: title,
      subtitle: subtitle,
      right: topRight
    }), /*#__PURE__*/React.createElement("main", {
      style: {
        flex: 1,
        minHeight: 0,
        overflow: 'auto',
        padding: '22px',
        position: 'relative',
        backgroundImage: 'var(--harbor-floor)',
        backgroundAttachment: 'local'
      }
    }, children)));
  }
  Object.assign(window, {
    AppShell,
    Sidebar,
    Topbar
  });
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/harrbor-app/AppShell.jsx", error: String((e && e.message) || e) }); }

// ui_kits/harrbor-app/Dashboard.jsx
try { (() => {
// Dashboard — connection health, per-phase counts, attention panel.
(() => {
  const {
    Card,
    MetricTile,
    FlowRail,
    ConnectionRow,
    AttentionItem,
    Badge,
    Button
  } = window.HarrborDesignSystem_2a1653;
  function Dashboard({
    onNav
  }) {
    const phases = [{
      phase: 'discover',
      count: 3
    }, {
      phase: 'download',
      count: 7,
      active: true
    }, {
      phase: 'transfer',
      count: 12,
      active: true
    }, {
      phase: 'import',
      count: 4,
      stuck: true,
      stuckCount: 2
    }, {
      phase: 'cleanup',
      count: 2
    }, {
      phase: 'archival',
      count: 1256
    }];
    return /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column',
        gap: '18px',
        maxWidth: 'var(--content-max)'
      }
    }, /*#__PURE__*/React.createElement(Card, {
      eyebrow: "Pipeline flow",
      title: "Six-phase throughput",
      subtitle: "Live \xB7 tap a phase to inspect its lane",
      actions: /*#__PURE__*/React.createElement(Button, {
        size: "sm",
        variant: "ghost",
        onClick: () => onNav('pipeline')
      }, "Open board \u2192")
    }, /*#__PURE__*/React.createElement(FlowRail, {
      phases: phases,
      bloom: "transfer",
      onSelect: () => onNav('pipeline'),
      style: {
        padding: '6px 4px 2px'
      }
    })), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: 'repeat(4, 1fr)',
        gap: '12px'
      }
    }, /*#__PURE__*/React.createElement(MetricTile, {
      label: "In pipeline",
      value: "1,284",
      accentBar: true,
      tone: "accent"
    }), /*#__PURE__*/React.createElement(MetricTile, {
      label: "Transferring",
      value: "12",
      unit: "active",
      tone: "accent",
      delta: "+3"
    }), /*#__PURE__*/React.createElement(MetricTile, {
      label: "Stuck",
      value: "3",
      tone: "warn",
      accentBar: true,
      delta: "+2"
    }), /*#__PURE__*/React.createElement(MetricTile, {
      label: "Failed \xB7 24h",
      value: "1",
      tone: "error",
      accentBar: true
    })), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: '18px',
        alignItems: 'start'
      }
    }, /*#__PURE__*/React.createElement(Card, {
      eyebrow: "Services",
      title: "Connection health",
      subtitle: "4 services \xB7 last checked 12s ago",
      actions: /*#__PURE__*/React.createElement(Button, {
        size: "sm",
        variant: "secondary",
        iconLeft: "\u21BB"
      }, "Recheck")
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column',
        gap: '9px'
      }
    }, /*#__PURE__*/React.createElement(ConnectionRow, {
      name: "qBittorrent",
      kind: "torrent",
      host: "10.0.0.4:8080",
      status: "ok",
      detail: "24ms \xB7 v4.6.4"
    }), /*#__PURE__*/React.createElement(ConnectionRow, {
      name: "Sonarr",
      kind: "tv",
      host: "sonarr.local:8989",
      status: "ok",
      detail: "31ms \xB7 v4.0.9"
    }), /*#__PURE__*/React.createElement(ConnectionRow, {
      name: "SFTP",
      kind: "transfer",
      host: "seed.hetzner.net:22",
      status: "warn",
      detail: "latency 410ms"
    }), /*#__PURE__*/React.createElement(ConnectionRow, {
      name: "Radarr",
      kind: "movies",
      host: "radarr.local:7878",
      status: "error",
      detail: "ECONNREFUSED"
    }))), /*#__PURE__*/React.createElement(Card, {
      eyebrow: "Needs attention",
      title: "Failures & stalls",
      subtitle: "Newest first",
      actions: /*#__PURE__*/React.createElement(Badge, {
        tone: "warn",
        dot: true
      }, "4 open")
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column',
        gap: '9px'
      }
    }, /*#__PURE__*/React.createElement(AttentionItem, {
      severity: "error",
      message: "Import failed \u2014 no matching series in Sonarr",
      releaseId: "rel_8fa3c19d",
      phase: "import",
      time: "4m ago",
      action: "Retry"
    }), /*#__PURE__*/React.createElement(AttentionItem, {
      severity: "warn",
      message: "Transfer stalled over SFTP (0 B/s for 6m)",
      releaseId: "rel_1c92aa0",
      phase: "transfer",
      time: "12m ago",
      action: "Inspect"
    }), /*#__PURE__*/React.createElement(AttentionItem, {
      severity: "warn",
      message: "Radarr unreachable \u2014 release held before import",
      releaseId: "rel_77de210",
      phase: "import",
      time: "18m ago",
      action: "Inspect"
    }), /*#__PURE__*/React.createElement(AttentionItem, {
      severity: "info",
      message: "Cleanup skipped: seed ratio below 1.0",
      releaseId: "rel_0b4c8e1",
      phase: "cleanup",
      time: "34m ago",
      action: "Override"
    })))));
  }
  Object.assign(window, {
    Dashboard
  });
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/harrbor-app/Dashboard.jsx", error: String((e && e.message) || e) }); }

// ui_kits/harrbor-app/Pipeline.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
// Pipeline — six-phase board; releases flow left→right, stuck items surfaced.
(() => {
  const {
    ReleaseCard,
    PhaseChip,
    FlowRail,
    Badge,
    Button,
    Select
  } = window.HarrborDesignSystem_2a1653;
  const COLUMNS = [{
    phase: 'discover',
    items: [{
      name: 'The.Bear.S03E02.1080p',
      job: 'tv-shows',
      size: '2.8 GB'
    }, {
      name: 'Furiosa.2024.2160p',
      job: 'movies',
      size: '24 GB'
    }]
  }, {
    phase: 'download',
    items: [{
      name: 'Shogun.S01E09.2160p',
      job: 'tv-shows',
      size: '6.1 GB',
      progress: 88,
      active: true,
      eta: '1m02s'
    }, {
      name: 'Fallout.S01E05.1080p',
      job: 'tv-shows',
      size: '3.4 GB',
      progress: 41,
      active: true,
      eta: '5m40s'
    }]
  }, {
    phase: 'transfer',
    items: [{
      name: 'Severance.S02E07.1080p',
      job: 'tv-shows',
      size: '3.9 GB',
      progress: 72,
      active: true,
      eta: '2m14s'
    }, {
      name: 'Dune.Part.Two.2160p',
      job: 'movies',
      size: '21 GB',
      progress: 38,
      stuck: true,
      eta: '—'
    }, {
      name: 'Ripley.S01E04.1080p',
      job: 'tv-shows',
      size: '2.2 GB',
      progress: 95,
      active: true,
      eta: '18s'
    }]
  }, {
    phase: 'import',
    items: [{
      name: 'Poor.Things.2023.2160p',
      job: 'movies',
      size: '19 GB',
      stuck: true,
      eta: '—'
    }, {
      name: 'True.Detective.S04E06',
      job: 'tv-shows',
      size: '3.1 GB'
    }]
  }, {
    phase: 'cleanup',
    items: [{
      name: 'Oppenheimer.2023.2160p',
      job: 'movies',
      size: '28 GB'
    }]
  }, {
    phase: 'archival',
    items: [{
      name: 'Succession.S04E10.1080p',
      job: 'tv-shows',
      size: '4.0 GB'
    }]
  }];
  function Pipeline() {
    const [activePhase, setActivePhase] = React.useState(null);
    const railPhases = COLUMNS.map(c => ({
      phase: c.phase,
      count: c.items.length,
      active: c.items.some(it => it.active),
      stuck: c.items.some(it => it.stuck),
      stuckCount: c.items.filter(it => it.stuck).length
    }));
    return /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column',
        gap: '14px',
        height: '100%'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        alignItems: 'center',
        gap: '12px'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        width: 200
      }
    }, /*#__PURE__*/React.createElement(Select, {
      options: [{
        value: 'all',
        label: 'All jobs'
      }, {
        value: 'tv',
        label: 'tv-shows'
      }, {
        value: 'mov',
        label: 'movies'
      }],
      size: "sm"
    })), /*#__PURE__*/React.createElement(Badge, {
      tone: "warn",
      dot: true
    }, "2 stuck"), /*#__PURE__*/React.createElement(Badge, {
      tone: "active",
      dot: true
    }, "6 in flight"), /*#__PURE__*/React.createElement("div", {
      style: {
        marginLeft: 'auto'
      }
    }, /*#__PURE__*/React.createElement(Button, {
      size: "sm",
      variant: "secondary",
      iconLeft: "\u21BB"
    }, "Refresh"))), /*#__PURE__*/React.createElement("div", {
      style: {
        background: 'var(--surface-panel)',
        border: '1px solid var(--border-subtle)',
        borderRadius: 'var(--radius-lg)',
        padding: '10px 14px'
      }
    }, /*#__PURE__*/React.createElement(FlowRail, {
      variant: "condensed",
      phases: railPhases,
      activePhase: activePhase,
      onSelect: p => setActivePhase(cur => cur === p ? null : p)
    })), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: 'repeat(6, minmax(200px, 1fr))',
        gap: '12px',
        flex: 1,
        minHeight: 0
      }
    }, COLUMNS.map(col => {
      const dim = activePhase && activePhase !== col.phase;
      const sel = activePhase === col.phase;
      return /*#__PURE__*/React.createElement("div", {
        key: col.phase,
        style: {
          display: 'flex',
          flexDirection: 'column',
          minHeight: 0,
          background: 'var(--surface-panel)',
          border: `1px solid ${sel ? 'var(--border-focus)' : 'var(--border-subtle)'}`,
          boxShadow: sel ? '0 0 0 3px var(--accent-glow)' : 'none',
          borderRadius: 'var(--radius-lg)',
          overflow: 'hidden',
          opacity: dim ? 0.5 : 1,
          transition: 'opacity var(--dur-med), border-color var(--dur-fast)'
        }
      }, /*#__PURE__*/React.createElement("div", {
        style: {
          padding: '11px 12px',
          borderBottom: '1px solid var(--border-subtle)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          borderTop: `2px solid var(--phase-${col.phase})`
        }
      }, /*#__PURE__*/React.createElement(PhaseChip, {
        phase: col.phase,
        size: "sm"
      }), /*#__PURE__*/React.createElement("span", {
        style: {
          font: 'var(--fw-semibold) var(--text-xs) var(--font-mono)',
          color: 'var(--text-muted)'
        }
      }, col.items.length)), /*#__PURE__*/React.createElement("div", {
        style: {
          padding: '10px',
          display: 'flex',
          flexDirection: 'column',
          gap: '9px',
          overflow: 'auto',
          flex: 1
        }
      }, col.items.map(it => /*#__PURE__*/React.createElement(ReleaseCard, _extends({
        key: it.name,
        phase: col.phase
      }, it))), col.items.length === 0 && /*#__PURE__*/React.createElement("div", {
        style: {
          font: 'var(--text-xs) var(--font-mono)',
          color: 'var(--text-faint)',
          textAlign: 'center',
          padding: '18px 0'
        }
      }, "\u2014")));
    })));
  }
  Object.assign(window, {
    Pipeline
  });
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/harrbor-app/Pipeline.jsx", error: String((e && e.message) || e) }); }

// ui_kits/harrbor-app/Settings.jsx
try { (() => {
// Connections — service connection form with Test connection + masked secrets.
(() => {
  const {
    Card,
    TextField,
    SecretField,
    Select,
    Switch,
    Button,
    StatusDot,
    Badge
  } = window.HarrborDesignSystem_2a1653;
  const SERVICES = [{
    id: 'qbit',
    name: 'qBittorrent',
    kind: 'torrent',
    status: 'ok'
  }, {
    id: 'sonarr',
    name: 'Sonarr',
    kind: 'tv',
    status: 'ok'
  }, {
    id: 'radarr',
    name: 'Radarr',
    kind: 'movies',
    status: 'error'
  }, {
    id: 'sftp',
    name: 'SFTP',
    kind: 'transfer',
    status: 'warn'
  }];
  function Settings() {
    const [sel, setSel] = React.useState('sftp');
    const [test, setTest] = React.useState(null); // null | 'testing' | 'ok' | 'fail'
    const [tls, setTls] = React.useState(true);
    const svc = SERVICES.find(s => s.id === sel);
    const runTest = () => {
      setTest('testing');
      setTimeout(() => setTest(svc.status === 'error' ? 'fail' : 'ok'), 1100);
    };
    return /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: '240px 1fr',
        gap: '18px',
        maxWidth: 1040
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: 'column',
        gap: '6px'
      }
    }, SERVICES.map(s => {
      const active = s.id === sel;
      return /*#__PURE__*/React.createElement("button", {
        key: s.id,
        onClick: () => {
          setSel(s.id);
          setTest(null);
        },
        style: {
          display: 'flex',
          alignItems: 'center',
          gap: '10px',
          padding: '11px 13px',
          borderRadius: 'var(--radius-sm)',
          textAlign: 'left',
          cursor: 'pointer',
          background: active ? 'var(--surface-card)' : 'transparent',
          border: `1px solid ${active ? 'var(--border-focus)' : 'var(--border-subtle)'}`,
          boxShadow: active ? '0 0 0 3px var(--accent-glow)' : 'none'
        }
      }, /*#__PURE__*/React.createElement(StatusDot, {
        status: s.status,
        size: 8
      }), /*#__PURE__*/React.createElement("div", null, /*#__PURE__*/React.createElement("div", {
        style: {
          font: 'var(--fw-medium) var(--text-base) var(--font-sans)',
          color: 'var(--text-heading)'
        }
      }, s.name), /*#__PURE__*/React.createElement("div", {
        style: {
          font: 'var(--text-2xs) var(--font-sans)',
          color: 'var(--text-faint)',
          letterSpacing: 'var(--tracking-caps)',
          textTransform: 'uppercase'
        }
      }, s.kind)));
    })), /*#__PURE__*/React.createElement(Card, {
      title: `${svc.name} connection`,
      subtitle: "Credentials are stored encrypted at rest",
      actions: /*#__PURE__*/React.createElement(Badge, {
        tone: svc.status === 'ok' ? 'ok' : svc.status === 'error' ? 'error' : 'warn',
        dot: true
      }, svc.status === 'ok' ? 'Connected' : svc.status === 'error' ? 'Offline' : 'Degraded'),
      footer: /*#__PURE__*/React.createElement("div", {
        style: {
          display: 'flex',
          alignItems: 'center',
          gap: '12px'
        }
      }, /*#__PURE__*/React.createElement(Button, {
        variant: "primary",
        iconLeft: test === 'testing' ? '◴' : '⚡',
        onClick: runTest,
        disabled: test === 'testing'
      }, test === 'testing' ? 'Testing…' : 'Test connection'), /*#__PURE__*/React.createElement(Button, {
        variant: "secondary"
      }, "Save"), test === 'ok' && /*#__PURE__*/React.createElement("span", {
        style: {
          display: 'flex',
          alignItems: 'center',
          gap: '7px',
          font: 'var(--text-sm) var(--font-mono)',
          color: 'var(--green-300)'
        }
      }, /*#__PURE__*/React.createElement(StatusDot, {
        status: "ok",
        size: 7
      }), " handshake ok \xB7 38ms"), test === 'fail' && /*#__PURE__*/React.createElement("span", {
        style: {
          display: 'flex',
          alignItems: 'center',
          gap: '7px',
          font: 'var(--text-sm) var(--font-mono)',
          color: 'var(--red-300)'
        }
      }, /*#__PURE__*/React.createElement(StatusDot, {
        status: "error",
        size: 7
      }), " ECONNREFUSED \xB7 check host"))
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: '1fr 120px',
        gap: '16px'
      }
    }, /*#__PURE__*/React.createElement(TextField, {
      label: "Host",
      mono: true,
      prefix: "sftp://",
      defaultValue: "seed.hetzner.net"
    }), /*#__PURE__*/React.createElement(TextField, {
      label: "Port",
      mono: true,
      defaultValue: "22"
    })), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: '16px',
        marginTop: '16px'
      }
    }, /*#__PURE__*/React.createElement(TextField, {
      label: "Username",
      mono: true,
      defaultValue: "u4417-sub2"
    }), /*#__PURE__*/React.createElement(SecretField, {
      label: "Password / key",
      isSet: true,
      hint: "Ed25519 key fingerprint SHA256:9f\u2026c2"
    })), /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: '16px',
        marginTop: '16px'
      }
    }, /*#__PURE__*/React.createElement(TextField, {
      label: "Remote base path",
      mono: true,
      defaultValue: "/mnt/seedbox/complete"
    }), /*#__PURE__*/React.createElement(Select, {
      label: "On import",
      options: [{
        value: 'move',
        label: 'Move (delete remote)'
      }, {
        value: 'copy',
        label: 'Copy (keep remote)'
      }]
    })), /*#__PURE__*/React.createElement("div", {
      style: {
        marginTop: '18px',
        paddingTop: '16px',
        borderTop: '1px solid var(--border-subtle)',
        display: 'flex',
        flexDirection: 'column',
        gap: '12px'
      }
    }, /*#__PURE__*/React.createElement(Switch, {
      checked: tls,
      onChange: setTls,
      label: "Verify host key on connect"
    }), /*#__PURE__*/React.createElement(Switch, {
      checked: false,
      onChange: () => {},
      label: "Allow insecure ciphers (legacy servers)"
    }))));
  }
  Object.assign(window, {
    Settings
  });
})();
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/harrbor-app/Settings.jsx", error: String((e && e.message) || e) }); }

__ds_ns.Badge = __ds_scope.Badge;

__ds_ns.Card = __ds_scope.Card;

__ds_ns.MetricTile = __ds_scope.MetricTile;

__ds_ns.PhaseChip = __ds_scope.PhaseChip;

__ds_ns.ProgressBar = __ds_scope.ProgressBar;

__ds_ns.StatusDot = __ds_scope.StatusDot;

__ds_ns.Button = __ds_scope.Button;

__ds_ns.IconButton = __ds_scope.IconButton;

__ds_ns.SecretField = __ds_scope.SecretField;

__ds_ns.Select = __ds_scope.Select;

__ds_ns.Switch = __ds_scope.Switch;

__ds_ns.TextField = __ds_scope.TextField;

__ds_ns.AttentionItem = __ds_scope.AttentionItem;

__ds_ns.ConnectionRow = __ds_scope.ConnectionRow;

__ds_ns.FlowRail = __ds_scope.FlowRail;

__ds_ns.ReleaseCard = __ds_scope.ReleaseCard;

})();
