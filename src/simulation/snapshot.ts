import {
  canonicalSerialize,
  contentHash,
  isContentHash,
  parseCanonicalJson,
  type ContentHash,
} from '../core/canonical'
import { deepFreeze } from '../core/deepFreeze'
import { createSeededRngState, parseSeededRngState, type SeededRngState } from './rng'

export const SIMULATION_SNAPSHOT_SCHEMA_VERSION = 1 as const
export const DIAGNOSTIC_ENTITY_ID = 'diagnostic-probe' as const

// Presentation may render this data later; the height is only a diagnostic marker, not gameplay balance.
const DIAGNOSTIC_PROBE_HEIGHT = 1

export interface ProfileIdentity {
  readonly id: string
  readonly revision: number
  readonly contentHash: string
}

export interface ArenaIdentity {
  readonly id: string
  readonly seed: number
  readonly generatorVersion: string
  readonly contentHash: string
}

export interface DiagnosticEntityState {
  readonly id: typeof DIAGNOSTIC_ENTITY_ID
  readonly position: Readonly<{
    x: number
    y: number
    z: number
  }>
  readonly accumulatedInput: number
  readonly lastActionHash: ContentHash
  readonly lastRandomUint32: number
}

export interface SimulationSnapshot {
  readonly schemaVersion: typeof SIMULATION_SNAPSHOT_SCHEMA_VERSION
  readonly tick: number
  readonly rng: SeededRngState
  readonly profile: ProfileIdentity
  readonly arena: ArenaIdentity
  readonly entities: readonly [DiagnosticEntityState]
}

export interface InitialSimulationOptions {
  readonly rngSeed: number
  readonly profile: ProfileIdentity
  readonly arena: ArenaIdentity
}

function expectRecord(value: unknown, path: string): Record<string, unknown> {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    throw new TypeError(`${path} must be an object`)
  }
  return value as Record<string, unknown>
}

function expectExactKeys(record: Record<string, unknown>, keys: readonly string[], path: string): void {
  const expected = [...keys].sort()
  const actual = Object.keys(record).sort()
  if (actual.length !== expected.length || actual.some((key, index) => key !== expected[index])) {
    throw new TypeError(`${path} must contain exactly: ${expected.join(', ')}`)
  }
}

function expectNonEmptyString(value: unknown, path: string): string {
  if (typeof value !== 'string' || value.length === 0) {
    throw new TypeError(`${path} must be a non-empty string`)
  }
  return value
}

function expectSafeInteger(value: unknown, path: string, minimum?: number): number {
  if (!Number.isSafeInteger(value) || (minimum !== undefined && (value as number) < minimum)) {
    throw new RangeError(
      `${path} must be a safe integer${minimum === undefined ? '' : ` greater than or equal to ${minimum}`}`,
    )
  }
  return value as number
}

function expectFiniteNumber(value: unknown, path: string): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) {
    throw new TypeError(`${path} must be a finite number`)
  }
  return value
}

function expectContentHash(value: unknown, path: string): ContentHash {
  if (!isContentHash(value)) {
    throw new TypeError(`${path} must be a ${'fnv1a64-v1'} content hash`)
  }
  return value
}

function parseProfileIdentity(value: unknown): ProfileIdentity {
  const record = expectRecord(value, 'SimulationSnapshot.profile')
  expectExactKeys(record, ['id', 'revision', 'contentHash'], 'SimulationSnapshot.profile')
  return {
    id: expectNonEmptyString(record.id, 'SimulationSnapshot.profile.id'),
    revision: expectSafeInteger(record.revision, 'SimulationSnapshot.profile.revision', 1),
    contentHash: expectContentHash(record.contentHash, 'SimulationSnapshot.profile.contentHash'),
  }
}

function parseArenaIdentity(value: unknown): ArenaIdentity {
  const record = expectRecord(value, 'SimulationSnapshot.arena')
  expectExactKeys(
    record,
    ['id', 'seed', 'generatorVersion', 'contentHash'],
    'SimulationSnapshot.arena',
  )
  return {
    id: expectNonEmptyString(record.id, 'SimulationSnapshot.arena.id'),
    seed: expectSafeInteger(record.seed, 'SimulationSnapshot.arena.seed'),
    generatorVersion: expectNonEmptyString(
      record.generatorVersion,
      'SimulationSnapshot.arena.generatorVersion',
    ),
    contentHash: expectContentHash(record.contentHash, 'SimulationSnapshot.arena.contentHash'),
  }
}

function parseDiagnosticEntity(value: unknown): DiagnosticEntityState {
  const record = expectRecord(value, 'SimulationSnapshot.entities[0]')
  expectExactKeys(
    record,
    ['id', 'position', 'accumulatedInput', 'lastActionHash', 'lastRandomUint32'],
    'SimulationSnapshot.entities[0]',
  )
  if (record.id !== DIAGNOSTIC_ENTITY_ID) {
    throw new TypeError(`SimulationSnapshot.entities[0].id must be ${DIAGNOSTIC_ENTITY_ID}`)
  }
  const position = expectRecord(record.position, 'SimulationSnapshot.entities[0].position')
  expectExactKeys(position, ['x', 'y', 'z'], 'SimulationSnapshot.entities[0].position')
  const lastRandomUint32 = expectSafeInteger(
    record.lastRandomUint32,
    'SimulationSnapshot.entities[0].lastRandomUint32',
    0,
  )
  if (lastRandomUint32 > 0xffffffff) {
    throw new RangeError('SimulationSnapshot.entities[0].lastRandomUint32 must be uint32')
  }
  return {
    id: DIAGNOSTIC_ENTITY_ID,
    position: {
      x: expectFiniteNumber(position.x, 'SimulationSnapshot.entities[0].position.x'),
      y: expectFiniteNumber(position.y, 'SimulationSnapshot.entities[0].position.y'),
      z: expectFiniteNumber(position.z, 'SimulationSnapshot.entities[0].position.z'),
    },
    accumulatedInput: expectFiniteNumber(
      record.accumulatedInput,
      'SimulationSnapshot.entities[0].accumulatedInput',
    ),
    lastActionHash: expectContentHash(
      record.lastActionHash,
      'SimulationSnapshot.entities[0].lastActionHash',
    ),
    lastRandomUint32,
  }
}

export function createInitialSimulationSnapshot(options: InitialSimulationOptions): SimulationSnapshot {
  return parseSimulationSnapshot({
    schemaVersion: SIMULATION_SNAPSHOT_SCHEMA_VERSION,
    tick: 0,
    rng: createSeededRngState(options.rngSeed),
    profile: options.profile,
    arena: options.arena,
    entities: [
      {
        id: DIAGNOSTIC_ENTITY_ID,
        position: { x: 0, y: DIAGNOSTIC_PROBE_HEIGHT, z: 0 },
        accumulatedInput: 0,
        lastActionHash: contentHash([]),
        lastRandomUint32: 0,
      },
    ],
  })
}

export function parseSimulationSnapshot(input: string | unknown): SimulationSnapshot {
  const value = typeof input === 'string' ? parseCanonicalJson(input) : input
  canonicalSerialize(value)
  const record = expectRecord(value, 'SimulationSnapshot')
  expectExactKeys(
    record,
    ['schemaVersion', 'tick', 'rng', 'profile', 'arena', 'entities'],
    'SimulationSnapshot',
  )
  if (record.schemaVersion !== SIMULATION_SNAPSHOT_SCHEMA_VERSION) {
    throw new TypeError(
      `Unsupported SimulationSnapshot.schemaVersion: ${String(record.schemaVersion)}`,
    )
  }
  if (!Array.isArray(record.entities) || record.entities.length !== 1) {
    throw new TypeError('SimulationSnapshot.entities must contain exactly one diagnostic entity')
  }

  return deepFreeze({
    schemaVersion: SIMULATION_SNAPSHOT_SCHEMA_VERSION,
    tick: expectSafeInteger(record.tick, 'SimulationSnapshot.tick', 0),
    rng: parseSeededRngState(record.rng),
    profile: parseProfileIdentity(record.profile),
    arena: parseArenaIdentity(record.arena),
    entities: [parseDiagnosticEntity(record.entities[0])],
  })
}

export function serializeSimulationSnapshot(snapshot: SimulationSnapshot): string {
  return canonicalSerialize(parseSimulationSnapshot(snapshot))
}

export function simulationSnapshotHash(snapshot: SimulationSnapshot): ContentHash {
  return contentHash(parseSimulationSnapshot(snapshot))
}
