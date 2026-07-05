// Pipeline — six-phase board; releases flow left→right, stuck items surfaced.
(() => {
const { ReleaseCard, PhaseChip, FlowRail, Badge, Button, Select } = window.HarrborDesignSystem_2a1653;

const COLUMNS = [
  { phase: 'discover', items: [
    { name: 'The.Bear.S03E02.1080p', job: 'tv-shows', size: '2.8 GB' },
    { name: 'Furiosa.2024.2160p', job: 'movies', size: '24 GB' },
  ]},
  { phase: 'download', items: [
    { name: 'Shogun.S01E09.2160p', job: 'tv-shows', size: '6.1 GB', progress: 88, active: true, eta: '1m02s' },
    { name: 'Fallout.S01E05.1080p', job: 'tv-shows', size: '3.4 GB', progress: 41, active: true, eta: '5m40s' },
  ]},
  { phase: 'transfer', items: [
    { name: 'Severance.S02E07.1080p', job: 'tv-shows', size: '3.9 GB', progress: 72, active: true, eta: '2m14s' },
    { name: 'Dune.Part.Two.2160p', job: 'movies', size: '21 GB', progress: 38, stuck: true, eta: '—' },
    { name: 'Ripley.S01E04.1080p', job: 'tv-shows', size: '2.2 GB', progress: 95, active: true, eta: '18s' },
  ]},
  { phase: 'import', items: [
    { name: 'Poor.Things.2023.2160p', job: 'movies', size: '19 GB', stuck: true, eta: '—' },
    { name: 'True.Detective.S04E06', job: 'tv-shows', size: '3.1 GB' },
  ]},
  { phase: 'cleanup', items: [
    { name: 'Oppenheimer.2023.2160p', job: 'movies', size: '28 GB' },
  ]},
  { phase: 'archival', items: [
    { name: 'Succession.S04E10.1080p', job: 'tv-shows', size: '4.0 GB' },
  ]},
];

function Pipeline() {
  const [activePhase, setActivePhase] = React.useState(null);
  const railPhases = COLUMNS.map((c) => ({
    phase: c.phase,
    count: c.items.length,
    active: c.items.some((it) => it.active),
    stuck: c.items.some((it) => it.stuck),
    stuckCount: c.items.filter((it) => it.stuck).length,
  }));
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '14px', height: '100%' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
        <div style={{ width: 200 }}>
          <Select options={[{ value: 'all', label: 'All jobs' }, { value: 'tv', label: 'tv-shows' }, { value: 'mov', label: 'movies' }]} size="sm" />
        </div>
        <Badge tone="warn" dot>2 stuck</Badge>
        <Badge tone="active" dot>6 in flight</Badge>
        <div style={{ marginLeft: 'auto' }}><Button size="sm" variant="secondary" iconLeft="↻">Refresh</Button></div>
      </div>

      {/* condensed flow-rail — overview + column-jump nav */}
      <div style={{ background: 'var(--surface-panel)', border: '1px solid var(--border-subtle)', borderRadius: 'var(--radius-lg)', padding: '10px 14px' }}>
        <FlowRail variant="condensed" phases={railPhases} activePhase={activePhase}
          onSelect={(p) => setActivePhase((cur) => (cur === p ? null : p))} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(6, minmax(200px, 1fr))', gap: '12px', flex: 1, minHeight: 0 }}>
        {COLUMNS.map((col) => {
          const dim = activePhase && activePhase !== col.phase;
          const sel = activePhase === col.phase;
          return (
          <div key={col.phase} style={{
            display: 'flex', flexDirection: 'column', minHeight: 0,
            background: 'var(--surface-panel)',
            border: `1px solid ${sel ? 'var(--border-focus)' : 'var(--border-subtle)'}`,
            boxShadow: sel ? '0 0 0 3px var(--accent-glow)' : 'none',
            borderRadius: 'var(--radius-lg)', overflow: 'hidden',
            opacity: dim ? 0.5 : 1, transition: 'opacity var(--dur-med), border-color var(--dur-fast)',
          }}>
            <div style={{
              padding: '11px 12px', borderBottom: '1px solid var(--border-subtle)',
              display: 'flex', alignItems: 'center', justifyContent: 'space-between',
              borderTop: `2px solid var(--phase-${col.phase})`,
            }}>
              <PhaseChip phase={col.phase} size="sm" />
              <span style={{ font: 'var(--fw-semibold) var(--text-xs) var(--font-mono)', color: 'var(--text-muted)' }}>{col.items.length}</span>
            </div>
            <div style={{ padding: '10px', display: 'flex', flexDirection: 'column', gap: '9px', overflow: 'auto', flex: 1 }}>
              {col.items.map((it) => <ReleaseCard key={it.name} phase={col.phase} {...it} />)}
              {col.items.length === 0 && <div style={{ font: 'var(--text-xs) var(--font-mono)', color: 'var(--text-faint)', textAlign: 'center', padding: '18px 0' }}>—</div>}
            </div>
          </div>
          );
        })}
      </div>
    </div>
  );
}

Object.assign(window, { Pipeline });
})();
