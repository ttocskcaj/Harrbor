Masked secret input for API keys / passwords with a reveal toggle and a "stored" flag.

```jsx
<SecretField label="API key" isSet hint="Stored encrypted at rest" />
```

Set `isSet` when a value already exists on the server (shows "● stored", masked placeholder).
