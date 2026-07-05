// Connections — service connection form with Test connection + masked secrets.
(() => {
const { Card, TextField, SecretField, Select, Switch, Button, StatusDot, Badge } = window.HarrborDesignSystem_2a1653;

const SERVICES = [
  { id: 'qbit', name: 'qBittorrent', kind: 'torrent', status: 'ok' },
  { id: 'sonarr', name: 'Sonarr', kind: 'tv', status: 'ok' },
  { id: 'radarr', name: 'Radarr', kind: 'movies', status: 'error' },
  { id: 'sftp', name: 'SFTP', kind: 'transfer', status: 'warn' },
];

function Settings() {
  const [sel, setSel] = React.useState('sftp');
  const [test, setTest] = React.useState(null); // null | 'testing' | 'ok' | 'fail'
  const [tls, setTls] = React.useState(true);
  const svc = SERVICES.find((s) => s.id === sel);

  const runTest = () => {
    setTest('testing');
    setTimeout(() => setTest(svc.status === 'error' ? 'fail' : 'ok'), 1100);
  };

  return (
    <div style={{ display: 'grid', gridTemplateColumns: '240px 1fr', gap: '18px', maxWidth: 1040 }}>
      {/* service list */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
        {SERVICES.map((s) => {
          const active = s.id === sel;
          return (
            <button key={s.id} onClick={() => { setSel(s.id); setTest(null); }} style={{
              display: 'flex', alignItems: 'center', gap: '10px', padding: '11px 13px',
              borderRadius: 'var(--radius-sm)', textAlign: 'left', cursor: 'pointer',
              background: active ? 'var(--surface-card)' : 'transparent',
              border: `1px solid ${active ? 'var(--border-focus)' : 'var(--border-subtle)'}`,
              boxShadow: active ? '0 0 0 3px var(--accent-glow)' : 'none',
            }}>
              <StatusDot status={s.status} size={8} />
              <div>
                <div style={{ font: 'var(--fw-medium) var(--text-base) var(--font-sans)', color: 'var(--text-heading)' }}>{s.name}</div>
                <div style={{ font: 'var(--text-2xs) var(--font-sans)', color: 'var(--text-faint)', letterSpacing: 'var(--tracking-caps)', textTransform: 'uppercase' }}>{s.kind}</div>
              </div>
            </button>
          );
        })}
      </div>

      {/* form */}
      <Card
        title={`${svc.name} connection`}
        subtitle="Credentials are stored encrypted at rest"
        actions={<Badge tone={svc.status === 'ok' ? 'ok' : svc.status === 'error' ? 'error' : 'warn'} dot>{svc.status === 'ok' ? 'Connected' : svc.status === 'error' ? 'Offline' : 'Degraded'}</Badge>}
        footer={
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <Button variant="primary" iconLeft={test === 'testing' ? '◴' : '⚡'} onClick={runTest} disabled={test === 'testing'}>
              {test === 'testing' ? 'Testing…' : 'Test connection'}
            </Button>
            <Button variant="secondary">Save</Button>
            {test === 'ok' && <span style={{ display: 'flex', alignItems: 'center', gap: '7px', font: 'var(--text-sm) var(--font-mono)', color: 'var(--green-300)' }}><StatusDot status="ok" size={7} /> handshake ok · 38ms</span>}
            {test === 'fail' && <span style={{ display: 'flex', alignItems: 'center', gap: '7px', font: 'var(--text-sm) var(--font-mono)', color: 'var(--red-300)' }}><StatusDot status="error" size={7} /> ECONNREFUSED · check host</span>}
          </div>
        }
      >
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 120px', gap: '16px' }}>
          <TextField label="Host" mono prefix="sftp://" defaultValue="seed.hetzner.net" />
          <TextField label="Port" mono defaultValue="22" />
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginTop: '16px' }}>
          <TextField label="Username" mono defaultValue="u4417-sub2" />
          <SecretField label="Password / key" isSet hint="Ed25519 key fingerprint SHA256:9f…c2" />
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginTop: '16px' }}>
          <TextField label="Remote base path" mono defaultValue="/mnt/seedbox/complete" />
          <Select label="On import" options={[{ value: 'move', label: 'Move (delete remote)' }, { value: 'copy', label: 'Copy (keep remote)' }]} />
        </div>
        <div style={{ marginTop: '18px', paddingTop: '16px', borderTop: '1px solid var(--border-subtle)', display: 'flex', flexDirection: 'column', gap: '12px' }}>
          <Switch checked={tls} onChange={setTls} label="Verify host key on connect" />
          <Switch checked={false} onChange={() => {}} label="Allow insecure ciphers (legacy servers)" />
        </div>
      </Card>
    </div>
  );
}

Object.assign(window, { Settings });
})();
