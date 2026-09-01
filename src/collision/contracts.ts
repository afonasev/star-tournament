import {
  canonicalSerialize,
  contentHash,
  isContentHash,
  parseCanonicalJson,
  type ContentHash,
} from '../core/canonical'
import { deepFreeze } from '../core/deepFreeze'

export const COLLISION_CONTRACT_VERSION = 'collision-contract-v1' as const
export const COLLISION_BACKEND_PACKAGE =
  '@dimforge/rapier3d-deterministic-compat' as const
export const COLLISION_BACKEND_VERSION = '0.20.0' as const
export const COLLISION_CHECKPOINT_SCHEMA_VERSION = 1 as const

/**
 * Diagnostic query distances are normalized before ordering and hashing. This is
 * a versioned technical invariant of collision-contract-v1, not a balance value.
 */
export const COLLISION_DISTANCE_QUANTIZATION = 0.000001 as const

export const COLLISION_ERROR_CODES = {
  backendInitializationFailed: 'COLLISION_BACKEND_INITIALIZATION_FAILED',
  invalidData: 'COLLISION_INVALID_DATA',
  unsupportedIdentity: 'COLLISION_UNSUPPORTED_IDENTITY',
  worldDisposed: 'COLLISION_WORLD_DISPOSED',
} as const

export type CollisionErrorCode =
  (typeof COLLISION_ERROR_CODES)[keyof typeof COLLISION_ERROR_CODES]

export class CollisionContractError extends Error {
  readonly code: CollisionErrorCode

  constructor(code: CollisionErrorCode, message: string) {
    super(message)
    this.name = 'CollisionContractError'
    this.code = code
  }
}

export interface CollisionCompatibilityIdentity {
  readonly contractVersion: typeof COLLISION_CONTRACT_VERSION
  readonly backendPackage: typeof COLLISION_BACKEND_PACKAGE
  readonly backendVersion: typeof COLLISION_BACKEND_VERSION
}

export interface CollisionVector3 {
  readonly x: number
  readonly y: number
  readonly z: number
}

export interface CollisionCapsuleProjection {
  readonly semanticId: string
  readonly position: CollisionVector3
  readonly radius: number
  readonly halfHeight: number
}

export interface CollisionContact {
  readonly semanticId: string
  readonly distance: number
  readonly point: CollisionVector3
  readonly normal: CollisionVector3
}

export interface CollisionHit {
  readonly semanticId: string
  readonly distance: number
  readonly point: CollisionVector3
  readonly normal: CollisionVector3
}

export interface CollisionCapsuleMovementResult {
  readonly position: CollisionVector3
  readonly grounded: boolean
  readonly contacts: readonly CollisionContact[]
}

export interface CollisionArenaIdentity {
  readonly id: string
  readonly seed: number
  readonly generatorVersion: string
  readonly contentHash: ContentHash
}

export interface CollisionFixtureIdentity {
  readonly id: string
  readonly version: number
}

export interface CollisionCheckpointPayload {
  readonly schemaVersion: typeof COLLISION_CHECKPOINT_SCHEMA_VERSION
  readonly tick: number
  readonly compatibility: CollisionCompatibilityIdentity
  readonly arena: CollisionArenaIdentity
  readonly fixture: CollisionFixtureIdentity
  readonly capsules: readonly CollisionCapsuleProjection[]
}

export const RUNTIME_COLLISION_COMPATIBILITY_IDENTITY: CollisionCompatibilityIdentity =
  deepFreeze({
    contractVersion: COLLISION_CONTRACT_VERSION,
    backendPackage: COLLISION_BACKEND_PACKAGE,
    backendVersion: COLLISION_BACKEND_VERSION,
  })

function invalidData(path: string, reason: string): never {
  throw new CollisionContractError(
    COLLISION_ERROR_CODES.invalidData,
    `Invalid collision data at ${path}: ${reason}`,
  )
}

function expectRecord(value: unknown, path: string): Record<string, unknown> {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    return invalidData(path, 'must be an object')
  }
  return value as Record<string, unknown>
}

function expectExactKeys(record: Record<string, unknown>, keys: readonly string[], path: string): void {
  const expected = [...keys].sort()
  const actual = Object.keys(record).sort()
  if (actual.length !== expected.length || actual.some((key, index) => key !== expected[index])) {
    invalidData(path, `must contain exactly: ${expected.join(', ')}`)
  }
}

function expectNonEmptyString(value: unknown, path: string): string {
  if (typeof value !== 'string' || value.length === 0) {
    return invalidData(path, 'must be a non-empty string')
  }
  return value
}

function expectFiniteNumber(value: unknown, path: string): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) {
    return invalidData(path, 'must be a finite number')
  }
  return value
}

function expectPositiveFiniteNumber(value: unknown, path: string): number {
  const number = expectFiniteNumber(value, path)
  if (number <= 0) {
    return invalidData(path, 'must be greater than zero')
  }
  return number
}

function expectSafeInteger(value: unknown, path: string, minimum: number): number {
  if (!Number.isSafeInteger(value) || (value as number) < minimum) {
    return invalidData(path, `must be a safe integer greater than or equal to ${minimum}`)
  }
  return value as number
}

function parseVector3(value: unknown, path: string): CollisionVector3 {
  const record = expectRecord(value, path)
  expectExactKeys(record, ['x', 'y', 'z'], path)
  return {
    x: expectFiniteNumber(record.x, `${path}.x`),
    y: expectFiniteNumber(record.y, `${path}.y`),
    z: expectFiniteNumber(record.z, `${path}.z`),
  }
}

export function parseCollisionCompatibilityIdentity(
  value: unknown,
): CollisionCompatibilityIdentity {
  const record = expectRecord(value, 'CollisionCompatibilityIdentity')
  expectExactKeys(
    record,
    ['contractVersion', 'backendPackage', 'backendVersion'],
    'CollisionCompatibilityIdentity',
  )

  const actual = {
    contractVersion: expectNonEmptyString(
      record.contractVersion,
      'CollisionCompatibilityIdentity.contractVersion',
    ),
    backendPackage: expectNonEmptyString(
      record.backendPackage,
      'CollisionCompatibilityIdentity.backendPackage',
    ),
    backendVersion: expectNonEmptyString(
      record.backendVersion,
      'CollisionCompatibilityIdentity.backendVersion',
    ),
  }

  if (
    actual.contractVersion !== COLLISION_CONTRACT_VERSION ||
    actual.backendPackage !== COLLISION_BACKEND_PACKAGE ||
    actual.backendVersion !== COLLISION_BACKEND_VERSION
  ) {
    throw new CollisionContractError(
      COLLISION_ERROR_CODES.unsupportedIdentity,
      `Unsupported collision compatibility identity: ${canonicalSerialize(actual)}`,
    )
  }

  return RUNTIME_COLLISION_COMPATIBILITY_IDENTITY
}

export function collisionCompatibilityIdentityEquals(
  first: CollisionCompatibilityIdentity,
  second: CollisionCompatibilityIdentity,
): boolean {
  return canonicalSerialize(first) === canonicalSerialize(second)
}

export function normalizeCollisionDistance(value: number): number {
  const finite = expectFiniteNumber(value, 'CollisionDistance')
  if (finite < 0) {
    return invalidData('CollisionDistance', 'must be greater than or equal to zero')
  }
  const normalized = Math.round(finite / COLLISION_DISTANCE_QUANTIZATION) * COLLISION_DISTANCE_QUANTIZATION
  return Object.is(normalized, -0) ? 0 : normalized
}

export function compareCollisionSemanticIds(first: string, second: string): number {
  return first < second ? -1 : first > second ? 1 : 0
}

export function parseCollisionCapsuleProjection(
  value: unknown,
  path = 'CollisionCapsuleProjection',
): CollisionCapsuleProjection {
  const record = expectRecord(value, path)
  expectExactKeys(record, ['semanticId', 'position', 'radius', 'halfHeight'], path)
  return deepFreeze({
    semanticId: expectNonEmptyString(record.semanticId, `${path}.semanticId`),
    position: parseVector3(record.position, `${path}.position`),
    radius: expectPositiveFiniteNumber(record.radius, `${path}.radius`),
    halfHeight: expectPositiveFiniteNumber(record.halfHeight, `${path}.halfHeight`),
  })
}

function parseContactLike<T extends CollisionContact | CollisionHit>(
  value: unknown,
  path: string,
): T {
  const record = expectRecord(value, path)
  expectExactKeys(record, ['semanticId', 'distance', 'point', 'normal'], path)
  return {
    semanticId: expectNonEmptyString(record.semanticId, `${path}.semanticId`),
    distance: normalizeCollisionDistance(expectFiniteNumber(record.distance, `${path}.distance`)),
    point: parseVector3(record.point, `${path}.point`),
    normal: parseVector3(record.normal, `${path}.normal`),
  } as T
}

function compareOrderedCollisionEntry(
  first: CollisionContact | CollisionHit,
  second: CollisionContact | CollisionHit,
): number {
  const distanceOrder = first.distance - second.distance
  if (distanceOrder !== 0) {
    return distanceOrder
  }
  const semanticOrder = compareCollisionSemanticIds(first.semanticId, second.semanticId)
  if (semanticOrder !== 0) {
    return semanticOrder
  }
  return compareCollisionSemanticIds(canonicalSerialize(first), canonicalSerialize(second))
}

function parseOrderedEntries<T extends CollisionContact | CollisionHit>(
  value: unknown,
  path: string,
): readonly T[] {
  if (!Array.isArray(value)) {
    return invalidData(path, 'must be an array')
  }
  return deepFreeze(
    value
      .map((entry, index) => parseContactLike<T>(entry, `${path}[${index}]`))
      .sort(compareOrderedCollisionEntry),
  )
}

export function parseCollisionContacts(value: unknown): readonly CollisionContact[] {
  return parseOrderedEntries<CollisionContact>(value, 'CollisionContacts')
}

export function parseCollisionHits(value: unknown): readonly CollisionHit[] {
  return parseOrderedEntries<CollisionHit>(value, 'CollisionHits')
}

export function parseCollisionCapsuleMovementResult(
  value: unknown,
): CollisionCapsuleMovementResult {
  const record = expectRecord(value, 'CollisionCapsuleMovementResult')
  expectExactKeys(record, ['position', 'grounded', 'contacts'], 'CollisionCapsuleMovementResult')
  if (typeof record.grounded !== 'boolean') {
    return invalidData('CollisionCapsuleMovementResult.grounded', 'must be a boolean')
  }
  return deepFreeze({
    position: parseVector3(record.position, 'CollisionCapsuleMovementResult.position'),
    grounded: record.grounded,
    contacts: parseCollisionContacts(record.contacts),
  })
}

function parseArenaIdentity(value: unknown): CollisionArenaIdentity {
  const record = expectRecord(value, 'CollisionCheckpointPayload.arena')
  expectExactKeys(record, ['id', 'seed', 'generatorVersion', 'contentHash'], 'CollisionCheckpointPayload.arena')
  if (!isContentHash(record.contentHash)) {
    return invalidData('CollisionCheckpointPayload.arena.contentHash', 'must be a versioned content hash')
  }
  return {
    id: expectNonEmptyString(record.id, 'CollisionCheckpointPayload.arena.id'),
    seed: expectSafeInteger(record.seed, 'CollisionCheckpointPayload.arena.seed', 0),
    generatorVersion: expectNonEmptyString(
      record.generatorVersion,
      'CollisionCheckpointPayload.arena.generatorVersion',
    ),
    contentHash: record.contentHash,
  }
}

function parseFixtureIdentity(value: unknown): CollisionFixtureIdentity {
  const record = expectRecord(value, 'CollisionCheckpointPayload.fixture')
  expectExactKeys(record, ['id', 'version'], 'CollisionCheckpointPayload.fixture')
  return {
    id: expectNonEmptyString(record.id, 'CollisionCheckpointPayload.fixture.id'),
    version: expectSafeInteger(record.version, 'CollisionCheckpointPayload.fixture.version', 1),
  }
}

export function parseCollisionCheckpointPayload(
  input: string | unknown,
): CollisionCheckpointPayload {
  let value: unknown
  try {
    value = typeof input === 'string' ? parseCanonicalJson(input) : input
    canonicalSerialize(value)
  } catch (error) {
    if (error instanceof CollisionContractError) {
      throw error
    }
    return invalidData(
      'CollisionCheckpointPayload',
      error instanceof Error ? error.message : 'must be canonical JSON data',
    )
  }

  const record = expectRecord(value, 'CollisionCheckpointPayload')
  expectExactKeys(
    record,
    ['schemaVersion', 'tick', 'compatibility', 'arena', 'fixture', 'capsules'],
    'CollisionCheckpointPayload',
  )
  if (record.schemaVersion !== COLLISION_CHECKPOINT_SCHEMA_VERSION) {
    throw new CollisionContractError(
      COLLISION_ERROR_CODES.unsupportedIdentity,
      `Unsupported CollisionCheckpointPayload.schemaVersion: ${String(record.schemaVersion)}`,
    )
  }
  if (!Array.isArray(record.capsules)) {
    return invalidData('CollisionCheckpointPayload.capsules', 'must be an array')
  }

  const capsules = record.capsules
    .map((capsule, index) =>
      parseCollisionCapsuleProjection(capsule, `CollisionCheckpointPayload.capsules[${index}]`),
    )
    .sort((first, second) => compareCollisionSemanticIds(first.semanticId, second.semanticId))
  for (let index = 1; index < capsules.length; index += 1) {
    if (capsules[index - 1]?.semanticId === capsules[index]?.semanticId) {
      return invalidData(
        'CollisionCheckpointPayload.capsules',
        `contains duplicate semanticId ${capsules[index]?.semanticId ?? ''}`,
      )
    }
  }

  return deepFreeze({
    schemaVersion: COLLISION_CHECKPOINT_SCHEMA_VERSION,
    tick: expectSafeInteger(record.tick, 'CollisionCheckpointPayload.tick', 0),
    compatibility: parseCollisionCompatibilityIdentity(record.compatibility),
    arena: parseArenaIdentity(record.arena),
    fixture: parseFixtureIdentity(record.fixture),
    capsules,
  })
}

export function serializeCollisionCheckpointPayload(
  payload: CollisionCheckpointPayload,
): string {
  return canonicalSerialize(parseCollisionCheckpointPayload(payload))
}

export function collisionCheckpointHash(payload: CollisionCheckpointPayload): ContentHash {
  return contentHash(parseCollisionCheckpointPayload(payload))
}
