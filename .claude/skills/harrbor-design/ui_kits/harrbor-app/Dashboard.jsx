// Dashboard — connection health, per-phase counts, attention panel.
(() => {
const { Card, MetricTile, FlowRail, ConnectionRow, AttentionItem, Badge, Button } = window.HarrborDesignSystem_2a1653;

function Dashboard({ onNav }) {
  const phases = [
    { phase: 'discover', count: 3 },
    { phase: 'download', count: 7, active: true },
    { phase: 'transfer', count: 12, active: true },
    { phase: 'import', count: 4, stuck: true, stuckCount: 2 },
    { phase: 'cleanup', count: 2 },
    { phase: 'archival', count: 1256 },
  ];
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '18px', maxWidth: 'var(--content-max)' }}>
      {/* flow-rail hero — the pipeline as one connected, flowing line */}
      <Card eyebrow="Pipeline flow" title="Six-phase throughput" subtitle="Live · tap a phase to inspect its lane"
        actions={<Button size="sm" variant="ghost" onClick={() => onNav('pipeline')}>Open board →</Button>}>
        <FlowRail phases={phases} bloom="transfer" onSelect={() => onNav('pipeline')} style={{ padding: '6px 4px 2px' }} />
      </Card>

      {/* metric row */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '12px' }}>
        <MetricTile label="In pipeline" value="1,284" accentBar tone="accent" />
        <MetricTile label="Transferring" value="12" unit="active" tone="accent" delta="+3" />
        <MetricTile label="Stuck" value="3" tone="warn" accentBar delta="+2" />
        <MetricTile label="Failed · 24h" value="1" tone="error" accentBar />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '18px', alignItems: 'start' }}>
        {/* connection health */}
        <Card eyebrow="Services" title="Connection health" subtitle="4 services · last checked 12s ago"
          actions={<Button size="sm" variant="secondary" iconLeft="↻">Recheck</Button>}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '9px' }}>
            <ConnectionRow name="qBittorrent" kind="torrent" host="10.0.0.4:8080" status="ok" detail="24ms · v4.6.4" />
            <ConnectionRow name="Sonarr" kind="tv" host="sonarr.local:8989" status="ok" detail="31ms · v4.0.9" />
            <ConnectionRow name="SFTP" kind="transfer" host="seed.hetzner.net:22" status="warn" detail="latency 410ms" />
            <ConnectionRow name="Radarr" kind="movies" host="radarr.local:7878" status="error" detail="ECONNREFUSED" />
          </div>
        </Card>

        {/* attention */}
        <Card eyebrow="Needs attention" title="Failures & stalls"
          subtitle="Newest first"
          actions={<Badge tone="warn" dot>4 open</Badge>}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '9px' }}>
            <AttentionItem severity="error" message="Import failed — no matching series in Sonarr" releaseId="rel_8fa3c19d" phase="import" time="4m ago" action="Retry" />
            <AttentionItem severity="warn" message="Transfer stalled over SFTP (0 B/s for 6m)" releaseId="rel_1c92aa0" phase="transfer" time="12m ago" action="Inspect" />
            <AttentionItem severity="warn" message="Radarr unreachable — release held before import" releaseId="rel_77de210" phase="import" time="18m ago" action="Inspect" />
            <AttentionItem severity="info" message="Cleanup skipped: seed ratio below 1.0" releaseId="rel_0b4c8e1" phase="cleanup" time="34m ago" action="Override" />
          </div>
        </Card>
      </div>
    </div>
  );
}

Object.assign(window, { Dashboard });
})();
