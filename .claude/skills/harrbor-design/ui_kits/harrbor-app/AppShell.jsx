// Harrbor app shell — sidebar nav + topbar. Shared chrome for all screens.
(() => {
const { StatusDot, Badge } = window.HarrborDesignSystem_2a1653;

const NAV = [
  { id: 'dashboard', label: 'Dashboard', icon: '◵' },
  { id: 'pipeline',  label: 'Pipeline',  icon: '⇥' },
  { id: 'jobs',      label: 'Jobs',      icon: '≡' },
  { id: 'settings',  label: 'Connections', icon: '⚙' },
];

// Ticking mono telemetry — makes the whole console feel live/monitored.
function Telemetry() {
  const [t, setT] = React.useState({ rate: 48.2, flight: 6, sync: 12 });
  React.useEffect(() => {
    const id = setInterval(() => {
      setT((prev) => ({
        rate: Math.max(0, +(prev.rate + (Math.random() - 0.5) * 6).toFixed(1)),
        flight: Math.max(0, prev.flight + (Math.random() < 0.3 ? (Math.random() < 0.5 ? -1 : 1) : 0)),
        sync: prev.sync >= 30 ? 0 : prev.sync + 2,
      }));
    }, 2000);
    return () => clearInterval(id);
  }, []);
  return (
    <div style={{ font: 'var(--text-2xs) var(--font-mono)', color: 'var(--text-faint)', lineHeight: 1.6, fontVariantNumeric: 'tabular-nums' }}>
      <div><span style={{ color: 'var(--status-active)' }}>↑</span> {t.rate.toFixed(1)} MB/s · {t.flight} in flight</div>
      <div>sync {t.sync}s ago · up 6d 04h · v0.9.4</div>
    </div>
  );
}

function Sidebar({ current, onNav }) {
  return (
    <aside style={{
      width: 'var(--sidebar-w)', flexShrink: 0, background: 'var(--bg-app)',
      borderRight: '1px solid var(--border-subtle)', display: 'flex', flexDirection: 'column',
      padding: '0 0 12px',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '10px', height: 'var(--topbar-h)', padding: '0 16px', borderBottom: '1px solid var(--border-subtle)' }}>
        <span style={{ position: 'relative', display: 'grid', placeItems: 'center', width: 28, height: 28 }}>
          <span aria-hidden style={{ position: 'absolute', width: 34, height: 34, borderRadius: '50%', background: 'var(--harbor-bloom)', animation: 'beacon-breath 3.6s var(--ease-inout) infinite' }} />
          <img src="../../assets/harrbor-mark.svg" width="28" height="28" alt="" style={{ position: 'relative' }} />
        </span>
        <span style={{ font: 'var(--fw-semibold) var(--text-lg) var(--font-sans)', color: 'var(--text-heading)', letterSpacing: 'var(--tracking-tight)' }}>Harrbor</span>
      </div>
      <nav style={{ padding: '12px 10px', display: 'flex', flexDirection: 'column', gap: '2px' }}>
        {NAV.map((n) => {
          const active = current === n.id;
          return (
            <button key={n.id} onClick={() => onNav(n.id)} style={{
              display: 'flex', alignItems: 'center', gap: '11px', padding: '0 12px', height: 34,
              borderRadius: 'var(--radius-sm)', border: '1px solid transparent',
              background: active ? 'var(--accent-glow)' : 'transparent',
              color: active ? 'var(--accent-text)' : 'var(--text-muted)',
              font: `${active ? 'var(--fw-semibold)' : 'var(--fw-medium)'} var(--text-base) var(--font-sans)`,
              cursor: 'pointer', textAlign: 'left', width: '100%',
              boxShadow: active ? 'inset 2px 0 0 var(--accent)' : 'none',
            }}
              onMouseEnter={(e) => { if (!active) e.currentTarget.style.background = 'var(--surface-hover)'; }}
              onMouseLeave={(e) => { if (!active) e.currentTarget.style.background = 'transparent'; }}
            >
              <span style={{ width: 16, textAlign: 'center', fontSize: '14px' }}>{n.icon}</span>{n.label}
            </button>
          );
        })}
      </nav>
      <div style={{ marginTop: 'auto', padding: '12px 16px', borderTop: '1px solid var(--border-subtle)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '9px' }}>
          <StatusDot status="ok" pulse size={8} />
          <span style={{ font: 'var(--text-xs) var(--font-sans)', color: 'var(--text-muted)' }}>Orchestrator online</span>
        </div>
        <Telemetry />
      </div>
    </aside>
  );
}

function Topbar({ title, subtitle, right }) {
  return (
    <header style={{
      height: 'var(--topbar-h)', flexShrink: 0, display: 'flex', alignItems: 'center', gap: '14px',
      padding: '0 22px', borderBottom: '1px solid var(--border-subtle)', background: 'var(--bg-page)',
    }}>
      <div>
        <div style={{ font: 'var(--fw-semibold) var(--text-lg) var(--font-sans)', color: 'var(--text-heading)' }}>{title}</div>
        {subtitle && <div style={{ font: 'var(--text-2xs) var(--font-mono)', color: 'var(--text-faint)', marginTop: '1px' }}>{subtitle}</div>}
      </div>
      <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: '10px' }}>{right}</div>
    </header>
  );
}

function AppShell({ current, onNav, title, subtitle, topRight, children }) {
  return (
    <div style={{ display: 'flex', height: '100vh', background: 'var(--bg-page)', color: 'var(--text-body)', font: 'var(--type-body)' }}>
      <Sidebar current={current} onNav={onNav} />
      <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
        <Topbar title={title} subtitle={subtitle} right={topRight} />
        <main style={{ flex: 1, minHeight: 0, overflow: 'auto', padding: '22px', position: 'relative', backgroundImage: 'var(--harbor-floor)', backgroundAttachment: 'local' }}>{children}</main>
      </div>
    </div>
  );
}

Object.assign(window, { AppShell, Sidebar, Topbar });
})();
