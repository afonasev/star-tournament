import { describe, expect, it } from 'vitest'
import { ARENA_GLB_ASSET_KEYS, CLEAN_FUTURE_SPORT_GLB_V1, validateArenaGlbManifest } from './arenaGlbManifest'

describe('clean-future-sport-glb-v1 manifest', () => {
  it('has four stable complete entries with the authored spatial contract', () => {
    expect(Object.keys(CLEAN_FUTURE_SPORT_GLB_V1)).toEqual(ARENA_GLB_ASSET_KEYS)
    expect(validateArenaGlbManifest(CLEAN_FUTURE_SPORT_GLB_V1)).toEqual([])
    for (const entry of Object.values(CLEAN_FUTURE_SPORT_GLB_V1)) {
      expect(entry.units).toBe('meters')
      expect(entry.axes).toEqual({ up: '+Y', front: '+Z' })
      expect(entry.lod.lod0Uri).toMatch(/\.glb$/)
      expect(entry.lod.lod1Uri).toMatch(/\.glb$/)
    }
  })

  it('rejects a missing required module before presentation startup', () => {
    const incomplete = { ...CLEAN_FUTURE_SPORT_GLB_V1 }
    delete (incomplete as Partial<typeof incomplete>)['portal-facade']
    expect(validateArenaGlbManifest(incomplete)).toContain('Missing required GLB asset: portal-facade')
  })
})
