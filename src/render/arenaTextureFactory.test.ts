// @vitest-environment jsdom
import * as THREE from 'three'
import { describe, expect, it, vi } from 'vitest'

import { GRAPHICS_QUALITY_V1 } from '../presentation'
import { createArenaTextureKit } from './arenaTextureFactory'
import { CLEAN_FUTURE_SPORT_TEXTURE_V1 } from './arenaTextureManifest'

describe('clean-future-sport-texture-v1', () => {
  it('declares local tiling base-color, normal and ORM contract plus a text-free atlas source', () => {
    expect(CLEAN_FUTURE_SPORT_TEXTURE_V1.textureSets.wall.baseColorUri).toMatch(/wall-base-color-source-v1\.png$/)
    expect(CLEAN_FUTURE_SPORT_TEXTURE_V1.textureSets.floor.baseColorUri).toMatch(/floor-base-color-source-v1\.png$/)
    expect(CLEAN_FUTURE_SPORT_TEXTURE_V1.decalAtlas.uri).toMatch(/navigation-decal-atlas-source-v1\.png$/)
    for (const entry of Object.values(CLEAN_FUTURE_SPORT_TEXTURE_V1.textureSets)) {
      expect(entry.normal.kind).toBe('generated-normal')
      expect(entry.orm.kind).toBe('generated-orm')
      expect(entry.variants.high.maxDimension).toBeLessThanOrEqual(2048)
    }
    expect(CLEAN_FUTURE_SPORT_TEXTURE_V1.decalAtlas.maxDimension).toBeLessThanOrEqual(4096)
  })

  it('accounts renderer-owned residency, selects the tier and disposes all maps', () => {
    const dispose = vi.spyOn(THREE.Texture.prototype, 'dispose')
    const low = createArenaTextureKit(GRAPHICS_QUALITY_V1.low)
    const ultra = createArenaTextureKit(GRAPHICS_QUALITY_V1.ultra)
    expect(low.residency).toMatchObject({ effectiveTier: 'low', budgetMiB: 128, decalsIncluded: false })
    expect(ultra.residency).toMatchObject({ effectiveTier: 'high', budgetMiB: 512, decalsIncluded: true })
    expect(ultra.residency.residentMiB).toBeLessThanOrEqual(ultra.residency.budgetMiB)
    expect(ultra.wall).toEqual(expect.objectContaining({ baseColor: expect.any(THREE.Texture), normal: expect.any(THREE.Texture), orm: expect.any(THREE.Texture) }))
    low.dispose()
    ultra.dispose()
    expect(dispose).toHaveBeenCalled()
    dispose.mockRestore()
  })
})
