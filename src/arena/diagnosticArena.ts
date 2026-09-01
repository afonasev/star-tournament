import { contentHash } from '../core/canonical'

import {
  ARENA_DEFINITION_SCHEMA_VERSION,
  type ArenaBoxSurface,
  type ArenaDefinition,
  type ArenaDefinitionPayload,
  type ArenaSpawnAnchor,
} from './arenaDefinition'

const surfaces: ArenaBoxSurface[] = [
  { id: 'floor', kind: 'box', center: { x: 0, y: -0.5, z: 0 }, size: { x: 28, y: 1, z: 20 }, material: 'floor' },
  { id: 'wall-north', kind: 'box', center: { x: 0, y: 2, z: -10.5 }, size: { x: 28, y: 5, z: 1 }, material: 'wall' },
  { id: 'wall-south', kind: 'box', center: { x: 0, y: 2, z: 10.5 }, size: { x: 28, y: 5, z: 1 }, material: 'wall' },
  { id: 'wall-west', kind: 'box', center: { x: -14.5, y: 2, z: 0 }, size: { x: 1, y: 5, z: 22 }, material: 'wall' },
  { id: 'wall-east', kind: 'box', center: { x: 14.5, y: 2, z: 0 }, size: { x: 1, y: 5, z: 22 }, material: 'wall' },
  { id: 'cover-center', kind: 'box', center: { x: 0, y: 1, z: 0 }, size: { x: 3, y: 2, z: 3 }, material: 'accent' },
  { id: 'cover-west', kind: 'box', center: { x: -7, y: 0.75, z: 3 }, size: { x: 3, y: 1.5, z: 5 }, material: 'accent' },
  { id: 'cover-east', kind: 'box', center: { x: 7, y: 0.75, z: -3 }, size: { x: 3, y: 1.5, z: 5 }, material: 'accent' },
]

const spawnAnchors: ArenaSpawnAnchor[] = [
  { id: 'spawn-north-west', position: { x: -10, y: 0, z: -7 }, yawRadians: Math.PI * 0.25 },
  { id: 'spawn-north-east', position: { x: 10, y: 0, z: -7 }, yawRadians: Math.PI * 0.75 },
  { id: 'spawn-south-east', position: { x: 10, y: 0, z: 7 }, yawRadians: Math.PI * 1.25 },
  { id: 'spawn-south-west', position: { x: -10, y: 0, z: 7 }, yawRadians: Math.PI * 1.75 },
]

const payload: ArenaDefinitionPayload = {
  schemaVersion: ARENA_DEFINITION_SCHEMA_VERSION,
  id: 'diagnostic-arena',
  seed: 0x53544152,
  generatorVersion: 'diagnostic-v1',
  surfaces,
  spawnAnchors,
}

function freezeVector(vector: { x: number; y: number; z: number }): void {
  Object.freeze(vector)
}

for (const surface of surfaces) {
  freezeVector(surface.center)
  freezeVector(surface.size)
  Object.freeze(surface)
}
for (const spawnAnchor of spawnAnchors) {
  freezeVector(spawnAnchor.position)
  Object.freeze(spawnAnchor)
}
Object.freeze(surfaces)
Object.freeze(spawnAnchors)

export const DIAGNOSTIC_ARENA: Readonly<ArenaDefinition> = Object.freeze({
  ...payload,
  contentHash: contentHash(payload),
})
