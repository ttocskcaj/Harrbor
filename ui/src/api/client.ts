import createClient from 'openapi-fetch'
import type { paths } from './schema'

/**
 * Typed HTTP client for the Harrbor backend, generated from its OpenAPI doc.
 *
 * Same-origin by design: in dev the Vite proxy forwards `/api` to :8080; in
 * prod the SPA is served from the backend's `wwwroot`, so relative paths hit
 * the same host. Regenerate the types with `pnpm gen:api` when the API changes.
 */
export const client = createClient<paths>({ baseUrl: '/' })
