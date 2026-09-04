import { describe, expect, it, vi } from 'vitest'
import * as THREE from 'three'

import { CLEAN_FUTURE_SPORT_GLB_V1 } from './arenaGlbManifest'
import { ArenaGlbAssetError, prepareArenaGlbAssets } from './arenaGlbAssets'

function loader() {
  return { loadAsync: vi.fn(async () => {
    const group = new THREE.Group()
    group.add(new THREE.Mesh(new THREE.BoxGeometry(1, 1, 1), new THREE.MeshBasicMaterial()))
    return { scene: group }
  }) }
}

describe('arena GLB template cache', () => {
  it('loads each immutable LOD template, gives clones ownership of transforms, and disposes resources once', async () => {
    const source = loader()
    const cache = await prepareArenaGlbAssets(CLEAN_FUTURE_SPORT_GLB_V1, source as unknown as Parameters<typeof prepareArenaGlbAssets>[1])
    expect(source.loadAsync).toHaveBeenCalledTimes(Object.keys(CLEAN_FUTURE_SPORT_GLB_V1).length * 2)
    const near = cache.createInstance('wall-bay', 1)
    const far = cache.createInstance('wall-bay', 99)
    expect(near).not.toBe(far)
    expect(near.userData.lod).toBe(0)
    expect(far.userData.lod).toBe(1)
    const dispose = vi.spyOn(THREE.BufferGeometry.prototype, 'dispose')
    cache.dispose(); cache.dispose()
    expect(dispose).toHaveBeenCalledTimes(Object.keys(CLEAN_FUTURE_SPORT_GLB_V1).length * 2)
    expect(() => cache.createInstance('wall-bay')).toThrow(ArenaGlbAssetError)
  })

  it('turns a failed required load into an actionable startup error', async () => {
    await expect(prepareArenaGlbAssets(CLEAN_FUTURE_SPORT_GLB_V1, { loadAsync: async () => { throw new Error('404') } } as unknown as Parameters<typeof prepareArenaGlbAssets>[1]))
      .rejects.toThrow('Не удалось подготовить обязательные GLB-модули арены')
  })
})
