import { deepFreeze } from '../core/deepFreeze'

export const GRAPHICS_QUALITY_SCHEMA_ID = 'graphics-quality-v1' as const
export const GRAPHICS_QUALITY_STORAGE_KEY = 'star-tournament.graphics-quality-v1'
export type GraphicsQualityPreset = 'low' | 'balanced' | 'high' | 'ultra'
export type TextureQualityTier = 'low' | 'high'
export type GeometryLodPreference = 'lod1' | 'lod0'

export interface GraphicsQualityProfile {
  readonly schemaId: typeof GRAPHICS_QUALITY_SCHEMA_ID
  readonly id: GraphicsQualityPreset
  readonly label: string
  readonly renderScale: number
  readonly textureBudgetMiB: 128 | 256 | 384 | 512
  readonly textureTier: TextureQualityTier
  readonly geometryLod: GeometryLodPreference
  readonly decals: boolean
  readonly postProcessing: boolean
}

export interface GraphicsQualityPreference {
  readonly schemaId: typeof GRAPHICS_QUALITY_SCHEMA_ID
  readonly preset: GraphicsQualityPreset
  readonly overrides?: Partial<Pick<GraphicsQualityProfile,
    'renderScale' | 'textureTier' | 'geometryLod' | 'decals' | 'postProcessing'>>
}

export interface GraphicsQualityResolution {
  readonly profile: GraphicsQualityProfile
  readonly fallbackReason?: string
}

const profiles = {
  low: { schemaId: GRAPHICS_QUALITY_SCHEMA_ID, id: 'low', label: 'Low', renderScale: 0.7, textureBudgetMiB: 128, textureTier: 'low', geometryLod: 'lod1', decals: false, postProcessing: false },
  balanced: { schemaId: GRAPHICS_QUALITY_SCHEMA_ID, id: 'balanced', label: 'Balanced', renderScale: 1, textureBudgetMiB: 256, textureTier: 'low', geometryLod: 'lod1', decals: true, postProcessing: false },
  high: { schemaId: GRAPHICS_QUALITY_SCHEMA_ID, id: 'high', label: 'High', renderScale: 1.25, textureBudgetMiB: 384, textureTier: 'high', geometryLod: 'lod0', decals: true, postProcessing: false },
  ultra: { schemaId: GRAPHICS_QUALITY_SCHEMA_ID, id: 'ultra', label: 'Ultra', renderScale: 1.5, textureBudgetMiB: 512, textureTier: 'high', geometryLod: 'lod0', decals: true, postProcessing: true },
} satisfies Record<GraphicsQualityPreset, GraphicsQualityProfile>

export const GRAPHICS_QUALITY_V1 = deepFreeze(profiles)
const presetIds = new Set<GraphicsQualityPreset>(Object.keys(profiles) as GraphicsQualityPreset[])

function isPreset(value: unknown): value is GraphicsQualityPreset {
  return typeof value === 'string' && presetIds.has(value as GraphicsQualityPreset)
}

function validOverride(value: unknown): value is NonNullable<GraphicsQualityPreference['overrides']> {
  if (value === undefined) return true
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return false
  const candidate = value as Record<string, unknown>
  const validKeys = ['renderScale', 'textureTier', 'geometryLod', 'decals', 'postProcessing']
  if (Object.keys(candidate).some((key) => !validKeys.includes(key))) return false
  return (candidate.renderScale === undefined || (typeof candidate.renderScale === 'number' && [0.7, 1, 1.25, 1.5].includes(candidate.renderScale))) &&
    (candidate.textureTier === undefined || candidate.textureTier === 'low' || candidate.textureTier === 'high') &&
    (candidate.geometryLod === undefined || candidate.geometryLod === 'lod0' || candidate.geometryLod === 'lod1') &&
    (candidate.decals === undefined || typeof candidate.decals === 'boolean') &&
    (candidate.postProcessing === undefined || typeof candidate.postProcessing === 'boolean')
}

export function validateGraphicsQualityPreference(value: unknown): value is GraphicsQualityPreference {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return false
  const candidate = value as Record<string, unknown>
  return candidate.schemaId === GRAPHICS_QUALITY_SCHEMA_ID && isPreset(candidate.preset) && validOverride(candidate.overrides)
}

export function defaultGraphicsQualityPreference(): GraphicsQualityPreference {
  return deepFreeze({ schemaId: GRAPHICS_QUALITY_SCHEMA_ID, preset: 'balanced' })
}

export function resolveGraphicsQualityPreference(value: unknown): GraphicsQualityResolution {
  // Preferences never become match state: malformed local data must safely
  // recover to the shipped renderer baseline instead of blocking a session.
  if (!validateGraphicsQualityPreference(value)) {
    return deepFreeze({ profile: GRAPHICS_QUALITY_V1.balanced, fallbackReason: 'Неверная локальная настройка: использован Balanced.' })
  }
  const base = GRAPHICS_QUALITY_V1[value.preset]
  return deepFreeze({ profile: deepFreeze({ ...base, ...value.overrides }) })
}

export function parseGraphicsQualityPreference(value: string | null): GraphicsQualityResolution {
  if (value === null) return deepFreeze({ profile: GRAPHICS_QUALITY_V1.balanced })
  // Early v1 stored just the preset; retain that local preference compatibly.
  if (isPreset(value)) return deepFreeze({ profile: GRAPHICS_QUALITY_V1[value] })
  try { return resolveGraphicsQualityPreference(JSON.parse(value) as unknown) } catch {
    return resolveGraphicsQualityPreference(undefined)
  }
}

export function serializeGraphicsQualityPreference(preference: GraphicsQualityPreference): string {
  return JSON.stringify(preference)
}

/** Compatibility helper for callers that only have a preset identifier. */
export function resolveGraphicsQuality(value: unknown): GraphicsQualityProfile {
  return isPreset(value) ? GRAPHICS_QUALITY_V1[value] : GRAPHICS_QUALITY_V1.balanced
}
