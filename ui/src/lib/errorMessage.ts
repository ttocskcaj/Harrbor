/**
 * Translate a TanStack Query error (whose declared type is `never`, since the
 * endpoints advertise no error responses) into a human sentence. Lifted from
 * the original foundation demo.
 */
export function errorMessage(err: unknown): string {
  if (err instanceof Error) {
    // A missing/unreachable backend surfaces either as an undefined query result
    // ("Query data cannot be undefined", from a 502 with no body) or a
    // "Failed to fetch" — translate both into the actual fix.
    const m = err.message
    if (m.includes('data cannot be undefined') || m.toLowerCase().includes('failed to fetch')) {
      return 'Backend unreachable — start the API on :8080 (dotnet run --project src/Harrbor).'
    }
    return m
  }
  if (typeof err === 'string') return err
  if (err && typeof err === 'object' && 'title' in err && typeof err.title === 'string') return err.title
  return 'Request failed'
}
