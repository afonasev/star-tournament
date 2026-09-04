import * as THREE from 'three'
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js'

import { CLEAN_FUTURE_SPORT_GLB_V1, type ArenaGlbAssetEntry, type ArenaGlbAssetKey, validateArenaGlbManifest } from './arenaGlbManifest'

export class ArenaGlbAssetError extends Error {
  constructor(message: string, readonly cause?: unknown) { super(message); this.name = 'ArenaGlbAssetError' }
}

export interface ArenaGlbTemplateCache {
  readonly createInstance: (key: ArenaGlbAssetKey, distanceMeters?: number) => THREE.Object3D
  readonly dispose: () => void
}

type Loader = Pick<GLTFLoader, 'loadAsync'>

function disposeObject(object: THREE.Object3D, seenGeometry: Set<THREE.BufferGeometry>, seenMaterial: Set<THREE.Material>): void {
  object.traverse((child) => {
    if (!(child instanceof THREE.Mesh)) return
    if (!seenGeometry.has(child.geometry)) { seenGeometry.add(child.geometry); child.geometry.dispose() }
    for (const material of Array.isArray(child.material) ? child.material : [child.material]) {
      if (!seenMaterial.has(material)) { seenMaterial.add(material); material.dispose() }
    }
  })
}

export async function prepareArenaGlbAssets(
  manifest = CLEAN_FUTURE_SPORT_GLB_V1,
  loader: Loader = new GLTFLoader(),
): Promise<ArenaGlbTemplateCache> {
  const errors = validateArenaGlbManifest(manifest)
  if (errors.length > 0) throw new ArenaGlbAssetError(errors.join('; '))
  try {
    const templates = new Map<ArenaGlbAssetKey, readonly [THREE.Object3D, THREE.Object3D]>()
    await Promise.all(Object.values(manifest).map(async (entry) => {
      const [lod0, lod1] = await Promise.all([loader.loadAsync(entry.lod.lod0Uri), loader.loadAsync(entry.lod.lod1Uri)])
      templates.set(entry.key, [lod0.scene, lod1.scene])
    }))
    let disposed = false
    return {
      createInstance(key, distanceMeters = 0) {
        if (disposed) throw new ArenaGlbAssetError('GLB template cache is disposed')
        const entry = manifest[key]
        const template = templates.get(key)
        if (template === undefined) throw new ArenaGlbAssetError(`Required GLB template is unavailable: ${key}`)
        const useLod1 = distanceMeters > entry.lod.switchDistanceMeters + entry.lod.hysteresisMeters
        const instance = template[useLod1 ? 1 : 0].clone(true)
        instance.userData.arenaGlbKey = key
        instance.userData.lod = useLod1 ? 1 : 0
        return instance
      },
      dispose() {
        if (disposed) return
        disposed = true
        const geometries = new Set<THREE.BufferGeometry>()
        const materials = new Set<THREE.Material>()
        for (const pair of templates.values()) for (const template of pair) disposeObject(template, geometries, materials)
        templates.clear()
      },
    }
  } catch (error) {
    throw new ArenaGlbAssetError('Не удалось подготовить обязательные GLB-модули арены', error)
  }
}

export function arenaGlbAssetEntry(key: ArenaGlbAssetKey): ArenaGlbAssetEntry { return CLEAN_FUTURE_SPORT_GLB_V1[key] }
