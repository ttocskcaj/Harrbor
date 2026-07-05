The signature Harrbor motif — the entire six-phase pipeline as one connected line, with segments that shimmer where releases are moving. Use it as the Dashboard hero and as a pinned overview/nav strip on the Pipeline view.

```jsx
<FlowRail
  phases={[
    { phase: 'discover', count: 3 },
    { phase: 'download', count: 7, active: true },
    { phase: 'transfer', count: 12, active: true },
    { phase: 'import', count: 4, stuck: true, stuckCount: 2 },
    { phase: 'cleanup', count: 2 },
    { phase: 'archival', count: 1256 },
  ]}
  bloom="transfer"
  onSelect={(p) => goToColumn(p)}
/>
```

`variant="condensed"` for the slim pinned strip. `bloom` places the single amber harbor-light behind the live-focus phase — use at most one per screen.
