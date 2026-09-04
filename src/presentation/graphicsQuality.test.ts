import { describe, expect, it } from 'vitest'

import {
  GRAPHICS_QUALITY_V1,
  parseGraphicsQualityPreference,
  resolveGraphicsQualityPreference,
  serializeGraphicsQualityPreference,
} from './graphicsQuality'

describe('graphics-quality-v1', () => {
  it('ships immutable Low/Balanced/High/Ultra renderer-only budgets', () => {
    expect(Object.values(GRAPHICS_QUALITY_V1).map(({ id, textureBudgetMiB }) => [id, textureBudgetMiB])).toEqual([
      ['low', 128], ['balanced', 256], ['high', 384], ['ultra', 512],
    ])
    expect(Object.isFrozen(GRAPHICS_QUALITY_V1)).toBe(true)
    expect(GRAPHICS_QUALITY_V1.balanced.schemaId).toBe('graphics-quality-v1')
  })

  it('keeps compatible local presets and falls back safely from malformed local persistence', () => {
    expect(parseGraphicsQualityPreference('high').profile).toBe(GRAPHICS_QUALITY_V1.high)
    expect(parseGraphicsQualityPreference('{broken').profile).toBe(GRAPHICS_QUALITY_V1.balanced)
    expect(parseGraphicsQualityPreference('{broken').fallbackReason).toMatch(/Balanced/)
    const value = { schemaId: 'graphics-quality-v1' as const, preset: 'ultra' as const, overrides: { decals: false } }
    expect(resolveGraphicsQualityPreference(JSON.parse(serializeGraphicsQualityPreference(value))).profile)
      .toMatchObject({ id: 'ultra', decals: false, textureBudgetMiB: 512 })
  })
})
