import { deepFreeze } from '../core/deepFreeze'

export const GRAPHICS_QUALITY_STORAGE_KEY = 'star-tournament.graphics-quality-v1'
export type GraphicsQualityPreset = 'low' | 'balanced' | 'high' | 'ultra'

export interface GraphicsQualityProfile {
  readonly id: GraphicsQualityPreset
  readonly label: string
  readonly renderScale: number
  readonly textureBudgetMiB: number
  readonly textureTier: 'low' | 'high'
  readonly geometryLod: 'lod1' | 'lod0'
  readonly decals: boolean
  readonly postProcessing: boolean
}

export const GRAPHICS_QUALITY_V1 = deepFreeze<Record<GraphicsQualityPreset, GraphicsQualityProfile>>({
  low: { id: 'low', label: 'Low', renderScale: 0.7, textureBudgetMiB: 128, textureTier: 'low', geometryLod: 'lod1', decals: false, postProcessing: false },
  balanced: { id: 'balanced', label: 'Balanced', renderScale: 1, textureBudgetMiB: 256, textureTier: 'low', geometryLod: 'lod1', decals: true, postProcessing: false },
  high: { id: 'high', label: 'High', renderScale: 1.25, textureBudgetMiB: 384, textureTier: 'high', geometryLod: 'lod0', decals: true, postProcessing: false },
  ultra: { id: 'ultra', label: 'Ultra', renderScale: 1.5, textureBudgetMiB: 512, textureTier: 'high', geometryLod: 'lod0', decals: true, postProcessing: true },
})

export function resolveGraphicsQuality(value: unknown): GraphicsQualityProfile {
  return typeof value === 'string' && value in GRAPHICS_QUALITY_V1
    ? GRAPHICS_QUALITY_V1[value as GraphicsQualityPreset]
    : GRAPHICS_QUALITY_V1.balanced
}
