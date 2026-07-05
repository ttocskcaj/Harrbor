import createQueryClient from 'openapi-react-query'
import { client } from './client'

/**
 * TanStack Query bindings over the typed client. Method + path are checked
 * against the OpenAPI schema, and `data` is fully typed off the response.
 *
 *   const { data, isLoading, error } = $api.useQuery('get', '/api/dashboard')
 */
export const $api = createQueryClient(client)
