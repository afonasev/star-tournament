import * as THREE from 'three'

import type { GraphicsQualityProfile, TextureQualityTier } from '../presentation'
import { CLEAN_FUTURE_SPORT_TEXTURE_V1, type ArenaTextureSurface, type ArenaTextureSetManifestEntry } from './arenaTextureManifest'

export interface ArenaTextureResidency {
  readonly requestedTier: TextureQualityTier
  readonly effectiveTier: TextureQualityTier
  readonly budgetMiB: number
  readonly residentMiB: number
  readonly decalsIncluded: boolean
  readonly fallbackReason?: string
}

export interface ArenaTextureSet {
  readonly baseColor: THREE.Texture
  readonly normal: THREE.Texture
  readonly orm: THREE.Texture
}

export interface ArenaTextureKit {
  readonly wall: ArenaTextureSet
  readonly floor: ArenaTextureSet
  readonly basementFloor: ArenaTextureSet
  readonly galleryWall: ArenaTextureSet
  readonly decalAtlas?: THREE.Texture
  readonly residency: ArenaTextureResidency
  readonly dispose: () => void
}

function generatedNormal(strength: number): THREE.DataTexture {
  const intensity = Math.round(Math.max(0, Math.min(1, strength)) * 24)
  const texture = new THREE.DataTexture(new Uint8Array([
    128 - intensity, 128, 255, 255,
    128 + intensity, 128, 255, 255,
    128, 128 - intensity, 255, 255,
    128, 128 + intensity, 255, 255,
  ]), 2, 2, THREE.RGBAFormat)
  texture.wrapS = texture.wrapT = THREE.RepeatWrapping
  texture.needsUpdate = true
  return texture
}

function generatedOrm(entry: ArenaTextureSetManifestEntry): THREE.DataTexture {
  const { ambientOcclusion, roughness, metalness } = entry.orm
  const texture = new THREE.DataTexture(new Uint8Array([
    Math.round(ambientOcclusion * 255), Math.round(roughness * 255), Math.round(metalness * 255), 255,
  ]), 1, 1, THREE.RGBAFormat)
  texture.needsUpdate = true
  return texture
}

function loadBaseColor(entry: ArenaTextureSetManifestEntry, tier: TextureQualityTier, onReady: () => void): THREE.Texture {
  const texture = new THREE.TextureLoader().load(entry.baseColorUri, onReady)
  texture.colorSpace = THREE.SRGBColorSpace
  texture.wrapS = texture.wrapT = THREE.RepeatWrapping
  texture.repeat.set(entry.repeat, entry.repeat)
  texture.generateMipmaps = tier === 'high'
  texture.minFilter = tier === 'high' ? THREE.LinearMipmapLinearFilter : THREE.LinearFilter
  texture.anisotropy = tier === 'high' ? 4 : 1
  return texture
}

function resolveResidency(quality: GraphicsQualityProfile): ArenaTextureResidency {
  let tier = quality.textureTier
  let includeDecals = quality.decals
  const estimate = (selectedTier: TextureQualityTier, decals: boolean) => (
    CLEAN_FUTURE_SPORT_TEXTURE_V1.textureSets.wall.variants[selectedTier].estimatedResidentMiB +
    CLEAN_FUTURE_SPORT_TEXTURE_V1.textureSets.floor.variants[selectedTier].estimatedResidentMiB +
    CLEAN_FUTURE_SPORT_TEXTURE_V1.textureSets.basementFloor.variants[selectedTier].estimatedResidentMiB +
    CLEAN_FUTURE_SPORT_TEXTURE_V1.textureSets.galleryWall.variants[selectedTier].estimatedResidentMiB +
    (decals ? CLEAN_FUTURE_SPORT_TEXTURE_V1.decalAtlas.estimatedResidentMiB : 0)
  )
  let residentMiB = estimate(tier, includeDecals)
  let fallbackReason: string | undefined
  if (residentMiB > quality.textureBudgetMiB && tier === 'high') {
    tier = 'low'
    residentMiB = estimate(tier, includeDecals)
    fallbackReason = 'Texture tier снижен до Low в пределах budget.'
  }
  if (residentMiB > quality.textureBudgetMiB && includeDecals) {
    includeDecals = false
    residentMiB = estimate(tier, includeDecals)
    fallbackReason = 'Decals отключены в пределах texture budget.'
  }
  return Object.freeze({ requestedTier: quality.textureTier, effectiveTier: tier, budgetMiB: quality.textureBudgetMiB, residentMiB, decalsIncluded: includeDecals, fallbackReason })
}

function makeSet(surface: ArenaTextureSurface, tier: TextureQualityTier, onReady: () => void): ArenaTextureSet {
  const entry = CLEAN_FUTURE_SPORT_TEXTURE_V1.textureSets[surface]
  return Object.freeze({
    baseColor: loadBaseColor(entry, tier, onReady),
    normal: generatedNormal(entry.normal.strength),
    orm: generatedOrm(entry),
  })
}

export function createArenaTextureKit(quality: GraphicsQualityProfile, onReady: () => void = () => undefined): ArenaTextureKit {
  const residency = resolveResidency(quality)
  const wall = makeSet('wall', residency.effectiveTier, onReady)
  const floor = makeSet('floor', residency.effectiveTier, onReady)
  const basementFloor = makeSet('basementFloor', residency.effectiveTier, onReady)
  const galleryWall = makeSet('galleryWall', residency.effectiveTier, onReady)
  const decalAtlas = residency.decalsIncluded
    ? new THREE.TextureLoader().load(CLEAN_FUTURE_SPORT_TEXTURE_V1.decalAtlas.uri, onReady)
    : undefined
  if (decalAtlas !== undefined) {
    decalAtlas.colorSpace = THREE.SRGBColorSpace
    decalAtlas.generateMipmaps = residency.effectiveTier === 'high'
    decalAtlas.minFilter = residency.effectiveTier === 'high' ? THREE.LinearMipmapLinearFilter : THREE.LinearFilter
  }
  const textures = [wall.baseColor, wall.normal, wall.orm, floor.baseColor, floor.normal, floor.orm, basementFloor.baseColor, basementFloor.normal, basementFloor.orm, galleryWall.baseColor, galleryWall.normal, galleryWall.orm, decalAtlas]
    .filter((texture): texture is THREE.Texture => texture !== undefined)
  let disposed = false
  return Object.freeze({
    wall,
    floor,
    basementFloor,
    galleryWall,
    decalAtlas,
    residency,
    dispose: () => {
      if (disposed) return
      disposed = true
      for (const texture of textures) texture.dispose()
    },
  })
}
