import { type ContentHash } from '../core/canonical'
import { PROTOTYPE_V1_PROFILE } from '../profiles/gameDesignProfile'

import {
  ARENA_DEFINITION_SCHEMA_VERSION,
  createArenaDefinition,
  type ArenaBoxSurface,
  type ArenaDefinition,
  type ArenaDefinitionPayload,
  type ArenaQuaternion,
  type ArenaSpawnAnchor,
} from './arenaDefinition'

function identityOrientation(): ArenaQuaternion {
  return { x: 0, y: 0, z: 0, w: 1 }
}

const surfaces: ArenaBoxSurface[] = [
  { id: 'floor', kind: 'box', center: { x: 0, y: -0.5, z: 0 }, size: { x: 28, y: 1, z: 20 }, orientation: identityOrientation(), material: 'floor' },
  { id: 'wall-north', kind: 'box', center: { x: 0, y: 2, z: -10.5 }, size: { x: 28, y: 5, z: 1 }, orientation: identityOrientation(), material: 'wall' },
  { id: 'wall-south', kind: 'box', center: { x: 0, y: 2, z: 10.5 }, size: { x: 28, y: 5, z: 1 }, orientation: identityOrientation(), material: 'wall' },
  { id: 'wall-west', kind: 'box', center: { x: -14.5, y: 2, z: 0 }, size: { x: 1, y: 5, z: 22 }, orientation: identityOrientation(), material: 'wall' },
  { id: 'wall-east', kind: 'box', center: { x: 14.5, y: 2, z: 0 }, size: { x: 1, y: 5, z: 22 }, orientation: identityOrientation(), material: 'wall' },
  { id: 'cover-center', kind: 'box', center: { x: 0, y: 1, z: 0 }, size: { x: 3, y: 2, z: 3 }, orientation: identityOrientation(), material: 'accent' },
  { id: 'cover-west', kind: 'box', center: { x: -7, y: 0.75, z: 3 }, size: { x: 3, y: 1.5, z: 5 }, orientation: identityOrientation(), material: 'accent' },
  { id: 'cover-east', kind: 'box', center: { x: 7, y: 0.75, z: -3 }, size: { x: 3, y: 1.5, z: 5 }, orientation: identityOrientation(), material: 'accent' },
]

const spawnAnchors: ArenaSpawnAnchor[] = [
  { id: 'spawn-north-west', spawnRegionId: 'spawn-region-west', position: { x: -10, y: 0, z: -7 }, yawRadians: Math.PI * 0.25 },
  { id: 'spawn-north-east', spawnRegionId: 'spawn-region-north', position: { x: 10, y: 0, z: -7 }, yawRadians: Math.PI * 0.75 },
  { id: 'spawn-south-east', spawnRegionId: 'spawn-region-east', position: { x: 10, y: 0, z: 7 }, yawRadians: Math.PI * 1.25 },
  { id: 'spawn-south-west', spawnRegionId: 'spawn-region-south', position: { x: -10, y: 0, z: 7 }, yawRadians: Math.PI * 1.75 },
]

const payload: ArenaDefinitionPayload = {
  schemaVersion: ARENA_DEFINITION_SCHEMA_VERSION,
  id: 'diagnostic-arena',
  seed: 0x53544152,
  size: 'small',
  generatorVersion: 'diagnostic-v1',
  generatorProfile: { id: PROTOTYPE_V1_PROFILE.id, revision: PROTOTYPE_V1_PROFILE.revision, contentHash: PROTOTYPE_V1_PROFILE.contentHash as ContentHash },
  regions: [
    { id: 'region-center', role: 'combat', center: { x: 0, y: 0, z: 0 }, size: { x: 12, y: 1, z: 8 } },
    { id: 'region-east', role: 'spawn', center: { x: 10, y: 0, z: 0 }, size: { x: 6, y: 1, z: 14 } },
    { id: 'region-north', role: 'spawn', center: { x: 0, y: 0, z: -7 }, size: { x: 16, y: 1, z: 5 } },
    { id: 'region-south', role: 'spawn', center: { x: 0, y: 0, z: 7 }, size: { x: 16, y: 1, z: 5 } },
    { id: 'region-west', role: 'spawn', center: { x: -10, y: 0, z: 0 }, size: { x: 6, y: 1, z: 14 } },
  ],
  links: [
    { id: 'link-center-east', fromRegionId: 'region-center', toRegionId: 'region-east', exposure: 'open' },
    { id: 'link-center-north', fromRegionId: 'region-center', toRegionId: 'region-north', exposure: 'covered' },
    { id: 'link-center-south', fromRegionId: 'region-center', toRegionId: 'region-south', exposure: 'covered' },
    { id: 'link-center-west', fromRegionId: 'region-center', toRegionId: 'region-west', exposure: 'open' },
    { id: 'link-east-north', fromRegionId: 'region-east', toRegionId: 'region-north', exposure: 'covered' },
    { id: 'link-east-south', fromRegionId: 'region-east', toRegionId: 'region-south', exposure: 'covered' },
    { id: 'link-north-west', fromRegionId: 'region-north', toRegionId: 'region-west', exposure: 'covered' },
    { id: 'link-south-west', fromRegionId: 'region-south', toRegionId: 'region-west', exposure: 'covered' },
  ],
  surfaces,
  spawnRegions: [
    { id: 'spawn-region-east', regionId: 'region-east', anchorIds: ['spawn-south-east'] },
    { id: 'spawn-region-north', regionId: 'region-north', anchorIds: ['spawn-north-east'] },
    { id: 'spawn-region-south', regionId: 'region-south', anchorIds: ['spawn-south-west'] },
    { id: 'spawn-region-west', regionId: 'region-west', anchorIds: ['spawn-north-west'] },
  ],
  spawnAnchors,
}

function freezeVector(vector: { x: number; y: number; z: number }): void {
  Object.freeze(vector)
}

for (const surface of surfaces) {
  freezeVector(surface.center)
  freezeVector(surface.size)
  Object.freeze(surface.orientation)
  Object.freeze(surface)
}
for (const spawnAnchor of spawnAnchors) {
  freezeVector(spawnAnchor.position)
  Object.freeze(spawnAnchor)
}
Object.freeze(surfaces)
Object.freeze(spawnAnchors)

export const DIAGNOSTIC_ARENA: Readonly<ArenaDefinition> = Object.freeze(createArenaDefinition(payload))
