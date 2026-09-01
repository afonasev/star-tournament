import {
  ARENA_DEFINITION_SCHEMA_VERSION,
  computeArenaContentHash,
  validateArenaDefinition,
  type ArenaDefinition,
  type ArenaDefinitionPayload,
  type ArenaPrimitiveSurface,
  type ArenaQuaternion,
} from '../arena/arenaDefinition'
import { contentHash } from '../core/canonical'

export const COLLISION_FIXTURE_ID = 'collision-fixture-v1' as const

const IDENTITY_ORIENTATION: ArenaQuaternion = { x: 0, y: 0, z: 0, w: 1 }

function orientationAroundZ(radians: number): ArenaQuaternion {
  return { x: 0, y: 0, z: Math.sin(radians / 2), w: Math.cos(radians / 2) }
}

const surfaces: ArenaPrimitiveSurface[] = [
  { id: 'ceiling-low', kind: 'box', center: { x: 0, y: 2.15, z: 5 }, size: { x: 4, y: 0.3, z: 4 }, orientation: IDENTITY_ORIENTATION, material: 'accent' },
  { id: 'cover-occluder', kind: 'box', center: { x: 0, y: 1, z: -3 }, size: { x: 2, y: 2, z: 2 }, orientation: IDENTITY_ORIENTATION, material: 'accent' },
  { id: 'corner-north', kind: 'box', center: { x: -3, y: 2, z: 7.5 }, size: { x: 10, y: 4, z: 1 }, orientation: IDENTITY_ORIENTATION, material: 'wall' },
  { id: 'corner-west', kind: 'box', center: { x: -8.5, y: 2, z: 3 }, size: { x: 1, y: 4, z: 10 }, orientation: IDENTITY_ORIENTATION, material: 'wall' },
  { id: 'floor', kind: 'box', center: { x: 0, y: -0.5, z: 0 }, size: { x: 30, y: 1, z: 24 }, orientation: IDENTITY_ORIENTATION, material: 'floor' },
  { id: 'ledge-platform', kind: 'box', center: { x: 6, y: 1.2, z: 5 }, size: { x: 4, y: 0.4, z: 4 }, orientation: IDENTITY_ORIENTATION, material: 'accent' },
  { id: 'slope-climbable', kind: 'box', center: { x: 4, y: 0.65, z: 0 }, size: { x: 5, y: 0.4, z: 3 }, orientation: orientationAroundZ(-Math.PI / 12), material: 'accent' },
  { id: 'step-high', kind: 'box', center: { x: -1, y: 0.6, z: 0 }, size: { x: 1.5, y: 1.2, z: 2 }, orientation: IDENTITY_ORIENTATION, material: 'accent' },
  { id: 'step-low', kind: 'box', center: { x: -4, y: 0.2, z: 0 }, size: { x: 1.5, y: 0.4, z: 2 }, orientation: IDENTITY_ORIENTATION, material: 'accent' },
  { id: 'tie-a', kind: 'box', center: { x: -0.25, y: 1, z: -6 }, size: { x: 0.5, y: 2, z: 0.25 }, orientation: IDENTITY_ORIENTATION, material: 'accent' },
  { id: 'tie-b', kind: 'box', center: { x: 0.25, y: 1, z: -6 }, size: { x: 0.5, y: 2, z: 0.25 }, orientation: IDENTITY_ORIENTATION, material: 'accent' },
  { id: 'wall-slide', kind: 'box', center: { x: 8.5, y: 2, z: -3 }, size: { x: 1, y: 4, z: 8 }, orientation: IDENTITY_ORIENTATION, material: 'wall' },
]

const payload: ArenaDefinitionPayload = {
  schemaVersion: ARENA_DEFINITION_SCHEMA_VERSION,
  id: COLLISION_FIXTURE_ID,
  seed: 0x434f4c4c,
  generatorVersion: COLLISION_FIXTURE_ID,
  surfaces,
  spawnAnchors: [
    { id: 'spawn-clear', position: { x: -6, y: 1, z: -5 }, yawRadians: 0 },
    { id: 'spawn-overlap', position: { x: 0, y: 1, z: -3 }, yawRadians: 0 },
  ],
}

const candidate: ArenaDefinition = {
  ...payload,
  contentHash: contentHash(payload),
}
const validation = validateArenaDefinition(candidate)
if (!validation.ok) {
  throw new Error(`Invalid ${COLLISION_FIXTURE_ID}: ${validation.errors.map((error) => error.message).join(' ')}`)
}

export const COLLISION_FIXTURE_ARENA = validation.arena

if (computeArenaContentHash(COLLISION_FIXTURE_ARENA) !== COLLISION_FIXTURE_ARENA.contentHash) {
  throw new Error(`${COLLISION_FIXTURE_ID} content hash invariant failed`)
}

