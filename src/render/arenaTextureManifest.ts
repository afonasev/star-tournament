import { deepFreeze } from '../core/deepFreeze'
import type { TextureQualityTier } from '../presentation'

export type ArenaTextureSurface = 'wall' | 'floor' | 'basementFloor' | 'galleryWall'
export type ArenaTextureMapKind = 'baseColor' | 'normal' | 'orm'

export interface ArenaTextureVariant {
  readonly id: string
  readonly maxDimension: number
  readonly estimatedResidentMiB: number
}

export interface ArenaTextureSetManifestEntry {
  readonly surface: ArenaTextureSurface
  readonly baseColorUri: string
  readonly normal: Readonly<{ readonly kind: 'generated-normal'; readonly strength: number }>
  readonly orm: Readonly<{ readonly kind: 'generated-orm'; readonly roughness: number; readonly metalness: number; readonly ambientOcclusion: number }>
  readonly variants: Readonly<Record<TextureQualityTier, ArenaTextureVariant>>
  readonly repeat: number
}

export interface ArenaDecalAtlasManifestEntry {
  readonly id: 'navigation-decal-atlas'
  readonly uri: string
  readonly maxDimension: number
  readonly estimatedResidentMiB: number
  readonly grid: Readonly<{ readonly columns: 4; readonly rows: 4 }>
}

export const CLEAN_FUTURE_SPORT_TEXTURE_V1 = deepFreeze({
  id: 'clean-future-sport-texture-v1',
  textureSets: {
    wall: {
      surface: 'wall',
      baseColorUri: '/assets/textures/arena-source/wall-base-color-source-v1.png',
      normal: { kind: 'generated-normal', strength: 0.32 },
      orm: { kind: 'generated-orm', roughness: 0.58, metalness: 0.04, ambientOcclusion: 1 },
      variants: {
        low: { id: 'wall-tiling-512', maxDimension: 512, estimatedResidentMiB: 3 },
        high: { id: 'wall-tiling-1254', maxDimension: 1254, estimatedResidentMiB: 12 },
      },
      repeat: 2,
    },
    floor: {
      surface: 'floor',
      baseColorUri: '/assets/textures/arena-source/floor-base-color-source-v1.png',
      normal: { kind: 'generated-normal', strength: 0.22 },
      orm: { kind: 'generated-orm', roughness: 0.72, metalness: 0.02, ambientOcclusion: 1 },
      variants: {
        low: { id: 'floor-tiling-512', maxDimension: 512, estimatedResidentMiB: 3 },
        high: { id: 'floor-tiling-1254', maxDimension: 1254, estimatedResidentMiB: 12 },
      },
      repeat: 3,
    },
    basementFloor: {
      surface: 'basementFloor',
      baseColorUri: '/assets/textures/arena-source/basement-floor-base-color-source-v1.png',
      normal: { kind: 'generated-normal', strength: 0.28 },
      orm: { kind: 'generated-orm', roughness: 0.66, metalness: 0.08, ambientOcclusion: 1 },
      variants: {
        low: { id: 'basement-floor-tiling-512', maxDimension: 512, estimatedResidentMiB: 3 },
        high: { id: 'basement-floor-tiling-1254', maxDimension: 1254, estimatedResidentMiB: 12 },
      },
      repeat: 3,
    },
    galleryWall: {
      surface: 'galleryWall',
      baseColorUri: '/assets/textures/arena-source/gallery-wall-base-color-source-v1.png',
      normal: { kind: 'generated-normal', strength: 0.2 },
      orm: { kind: 'generated-orm', roughness: 0.62, metalness: 0.05, ambientOcclusion: 1 },
      variants: {
        low: { id: 'gallery-wall-tiling-512', maxDimension: 512, estimatedResidentMiB: 3 },
        high: { id: 'gallery-wall-tiling-1254', maxDimension: 1254, estimatedResidentMiB: 12 },
      },
      repeat: 2,
    },
  } satisfies Record<ArenaTextureSurface, ArenaTextureSetManifestEntry>,
  decalAtlas: {
    id: 'navigation-decal-atlas',
    uri: '/assets/textures/arena-source/navigation-decal-atlas-source-v1.png',
    maxDimension: 1254,
    estimatedResidentMiB: 12,
    grid: { columns: 4, rows: 4 },
  } satisfies ArenaDecalAtlasManifestEntry,
})
