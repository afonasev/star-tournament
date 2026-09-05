import { describe, expect, it, vi } from 'vitest'
import * as THREE from 'three'

import { createRobotGlbPresentation } from './participantWeaponPresentation'
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

  it('preserves authored equipment maps in instances and releases shared textures once', async () => {
    const map = new THREE.Texture()
    const disposeMap = vi.spyOn(map, 'dispose')
    const source = { loadAsync: vi.fn(async () => {
      const scene = new THREE.Group()
      const shell = new THREE.MeshStandardMaterial({ map }); shell.name = 'shell'
      const identity = new THREE.MeshStandardMaterial(); identity.name = 'identity'
      scene.add(new THREE.Mesh(new THREE.BoxGeometry(), shell), new THREE.Mesh(new THREE.BoxGeometry(), identity))
      return { scene }
    }) }
    const cache = await prepareArenaGlbAssets(CLEAN_FUTURE_SPORT_GLB_V1, source as unknown as Parameters<typeof prepareArenaGlbAssets>[1])
    const resources = { geometries: [], materials: [] as THREE.Material[] }
    const robot = createRobotGlbPresentation(resources, cache, new THREE.Texture(), '#ff3300')
    const shell = resources.materials.find((m) => m.name === 'shell') as THREE.MeshStandardMaterial
    expect(shell.map).toBe(map)
    expect(robot.identityMaterials[0]?.map).toBeNull()
    for (const material of resources.materials) material.dispose()
    expect(disposeMap).not.toHaveBeenCalled()
    cache.dispose(); cache.dispose()
    expect(disposeMap).toHaveBeenCalledTimes(1)
  })

  it('turns a failed required load into an actionable startup error', async () => {
    await expect(prepareArenaGlbAssets(CLEAN_FUTURE_SPORT_GLB_V1, { loadAsync: async () => { throw new Error('404') } } as unknown as Parameters<typeof prepareArenaGlbAssets>[1]))
      .rejects.toThrow('Не удалось подготовить обязательные GLB-модули арены')
  })
})
