import type { components } from './schema'

/**
 * Convenience aliases for the codegen'd OpenAPI schemas, so feature code can
 * write `ReleaseListItem` instead of `components['schemas']['ReleaseListItem']`.
 * Numeric fields are typed `number | string` by openapi-typescript — coerce
 * with `Number(...)` at the edge (see `@/lib/format`).
 */
type Schemas = components['schemas']

export type DashboardResponse = Schemas['DashboardResponse']
export type PipelineCounts = Schemas['PipelineCounts']
export type ThroughputSummary = Schemas['ThroughputSummary']
export type AttentionSummary = Schemas['AttentionSummary']
export type AttentionItem = Schemas['AttentionItem']
export type JobSnapshot = Schemas['JobSnapshot']
export type ConnectionHealth = Schemas['ConnectionHealth']
export type ConnectionHealthStatus = Schemas['ConnectionHealthStatus']

export type ReleaseListItem = Schemas['ReleaseListItem']
export type ReleasePhase = Schemas['ReleasePhase']
export type ReleaseStatus = Schemas['ReleaseStatus']
