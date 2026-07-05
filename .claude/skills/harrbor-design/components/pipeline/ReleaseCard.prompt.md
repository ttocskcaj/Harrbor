One release as it sits in a pipeline column — mono name, job lane, phase, size, and a live transfer bar.

```jsx
<ReleaseCard name="Severance.S02E07.1080p" job="tv-shows" phase="transfer" size="3.9 GB" progress={72} active eta="2m14s" />
```

`stuck` flags a stalled item (amber edge + glow); `active` adds pulse + striped bar.
