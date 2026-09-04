import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import { ARENA_GLB_ASSET_KEYS, CLEAN_FUTURE_SPORT_GLB_V1, validateArenaGlbManifest } from './arenaGlbManifest'

function readGlbAsset(name: string, directory = 'arena'): { readonly asset: { readonly version: string; readonly extras: unknown }; readonly nodes: readonly { readonly name: string }[]; readonly materials: readonly { readonly name: string }[] } {
  const bytes = readFileSync(new URL(`../../public/assets/${directory}/${name}`, import.meta.url))
  expect(bytes.readUInt32LE(0)).toBe(0x46546c67)
  expect(bytes.readUInt32LE(4)).toBe(2)
  const jsonLength = bytes.readUInt32LE(12)
  expect(bytes.readUInt32LE(16)).toBe(0x4e4f534a)
  return JSON.parse(bytes.subarray(20, 20 + jsonLength).toString('utf8').trim())
}

describe('clean-future-sport-glb-v1 manifest', () => {
  it('has stable complete entries with the authored spatial contract', () => {
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

  it('audits high-detail participant GLB pairs without a second asset contract', () => {
    const requiredFeatures = {
      'light-sport-robot': ['robot:identity:core', 'robot:identity:visor', 'robot:identity:rear-beacon', 'robot:identity:shoulder:-1', 'robot:identity:shoulder:1', 'robot:joint:shoulder-collar:-1', 'robot:hand:finger:-1:0:0', 'robot:torso:armour-layer:0'],
      'double-barrel-shotgun': ['weapon:barrel-shroud:-1', 'weapon:barrel-shroud:1', 'weapon:muzzle-opening:-1', 'weapon:muzzle-opening:1', 'weapon:inner-bore:-1', 'weapon:inner-bore:1', 'weapon:energy-chamber', 'weapon:energy:coil:0', 'weapon:barrel:vent:-1:0', 'weapon:barrel-rail:-1', 'weapon:owner-indicator'],
      'first-person-shotgun': ['viewmodel:forearm:-1', 'viewmodel:forearm:1', 'viewmodel:hand:-1', 'viewmodel:hand:1', 'viewmodel:hand:finger:-1:0:0', 'weapon:muzzle-opening:-1', 'weapon:muzzle-opening:1', 'weapon:inner-bore:-1', 'weapon:energy:coil:0'],
    } as const
    for (const key of ['light-sport-robot', 'double-barrel-shotgun', 'first-person-shotgun'] as const) {
      const entry = CLEAN_FUTURE_SPORT_GLB_V1[key]
      const lod0 = readGlbAsset(`${entry.key}-lod0.glb`, 'participants')
      const lod1 = readGlbAsset(`${entry.key}-lod1.glb`, 'participants')
      const extras = { units: entry.units, up: entry.axes.up, front: entry.axes.front, pivot: entry.pivot, attachmentBoundsMeters: [entry.attachmentBoundsMeters.x, entry.attachmentBoundsMeters.y, entry.attachmentBoundsMeters.z] }
      expect(lod0.asset).toMatchObject({ version: '2.0', extras })
      expect(lod1.asset).toMatchObject({ version: '2.0', extras })
      expect(lod0.materials.map(({ name }) => name)).toContain('identity')
      expect(lod1.materials.map(({ name }) => name)).toContain('identity')
      expect(lod0.nodes.map(({ name }) => name)).toEqual(expect.arrayContaining(Array.from(requiredFeatures[key])))
      expect(lod1.nodes.map(({ name }) => name)).toEqual(expect.arrayContaining(Array.from(requiredFeatures[key])))
      expect(lod0.nodes.length).toBeGreaterThanOrEqual(lod1.nodes.length * 1.75)
    }
  })
})
