import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import { ARENA_GLB_ASSET_KEYS, CLEAN_FUTURE_SPORT_GLB_V1, validateArenaGlbManifest } from './arenaGlbManifest'

function readGlbAsset(name: string): { readonly asset: { readonly version: string; readonly extras: unknown }; readonly nodes: unknown } {
  const bytes = readFileSync(new URL(`../../public/assets/arena/${name}`, import.meta.url))
  expect(bytes.readUInt32LE(0)).toBe(0x46546c67)
  expect(bytes.readUInt32LE(4)).toBe(2)
  const jsonLength = bytes.readUInt32LE(12)
  expect(bytes.readUInt32LE(16)).toBe(0x4e4f534a)
  return JSON.parse(bytes.subarray(20, 20 + jsonLength).toString('utf8').trim())
}

describe('clean-future-sport-glb-v1 manifest', () => {
  it('has four stable complete entries with the authored spatial contract', () => {
    expect(Object.keys(CLEAN_FUTURE_SPORT_GLB_V1)).toEqual(ARENA_GLB_ASSET_KEYS)
    expect(validateArenaGlbManifest(CLEAN_FUTURE_SPORT_GLB_V1)).toEqual([])
    for (const key of ARENA_GLB_ASSET_KEYS) {
      const entry = CLEAN_FUTURE_SPORT_GLB_V1[key]
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

  it('audits local GLB LOD files for the approved metric pivot and attachment contract', () => {
    for (const key of ['portal-facade', 'wall-bay', 'floor-route-guide', 'central-landmark'] as const) {
      const entry = CLEAN_FUTURE_SPORT_GLB_V1[key]
      const lod0 = readGlbAsset(`${entry.key}-lod0.glb`)
      const lod1 = readGlbAsset(`${entry.key}-lod1.glb`)
      expect(lod0.asset.version).toBe('2.0')
      expect(lod0.asset.extras).toEqual({
        units: entry.units,
        up: entry.axes.up,
        front: entry.axes.front,
        pivot: entry.pivot,
        attachmentBoundsMeters: [entry.attachmentBoundsMeters.x, entry.attachmentBoundsMeters.y, entry.attachmentBoundsMeters.z],
      })
      expect(lod1.asset.extras).toEqual(lod0.asset.extras)
      expect(lod1.nodes).toEqual(lod0.nodes)
    }
  })
})
