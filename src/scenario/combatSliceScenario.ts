import {
  canonicalSerialize,
  contentHash,
  isContentHash,
  parseCanonicalJson,
  type ContentHash,
} from '../core/canonical'
import { deepFreeze } from '../core/deepFreeze'

export const COMBAT_SLICE_SCENARIO_SCHEMA_VERSION = 1 as const
export const COMBAT_SLICE_SCENARIO_ID = 'combat-slice-scenario-v1' as const

export interface ScenarioVector3 {
  readonly x: number
  readonly y: number
  readonly z: number
}

interface TargetHitVolumeBase {
  readonly semanticId: string
}

export interface TargetHeadSphere extends TargetHitVolumeBase {
  readonly kind: 'sphere'
  readonly zone: 'head'
  readonly centerOffset: ScenarioVector3
  readonly radius: number
}

export interface TargetTorsoBox extends TargetHitVolumeBase {
  readonly kind: 'box'
  readonly zone: 'torso'
  readonly centerOffset: ScenarioVector3
  readonly halfExtents: ScenarioVector3
}

export interface TargetLimbCapsule extends TargetHitVolumeBase {
  readonly kind: 'capsule'
  readonly zone: 'limb'
  readonly startOffset: ScenarioVector3
  readonly endOffset: ScenarioVector3
  readonly radius: number
}

export type TargetHitVolume = TargetHeadSphere | TargetTorsoBox | TargetLimbCapsule

export interface RobotTargetDefinition {
  readonly semanticId: string
  readonly position: ScenarioVector3
  readonly yawRadians: number
  readonly hitVolumes: readonly TargetHitVolume[]
}

export interface ScenarioPlayerSpawn {
  /** Feet anchor; the snapshot builder adds capsule half-height plus radius. */
  readonly position: ScenarioVector3
  readonly yawRadians: number
}

export interface CombatSliceScenarioPayload {
  readonly schemaVersion: typeof COMBAT_SLICE_SCENARIO_SCHEMA_VERSION
  readonly id: typeof COMBAT_SLICE_SCENARIO_ID
  readonly playerSpawn: ScenarioPlayerSpawn
  readonly targets: readonly RobotTargetDefinition[]
}

export interface CombatSliceScenario extends CombatSliceScenarioPayload {
  readonly contentHash: ContentHash
}

export interface CombatSliceScenarioIdentity {
  readonly schemaVersion: typeof COMBAT_SLICE_SCENARIO_SCHEMA_VERSION
  readonly id: typeof COMBAT_SLICE_SCENARIO_ID
  readonly contentHash: ContentHash
}

export const COMBAT_SLICE_SCENARIO_ERROR_CODES = {
  contentHashMismatch: 'COMBAT_SLICE_SCENARIO_CONTENT_HASH_MISMATCH',
  invalidData: 'COMBAT_SLICE_SCENARIO_INVALID_DATA',
  unsupportedIdentity: 'COMBAT_SLICE_SCENARIO_UNSUPPORTED_IDENTITY',
} as const

export type CombatSliceScenarioErrorCode =
  (typeof COMBAT_SLICE_SCENARIO_ERROR_CODES)[keyof typeof COMBAT_SLICE_SCENARIO_ERROR_CODES]

export class CombatSliceScenarioError extends Error {
  readonly code: CombatSliceScenarioErrorCode

  constructor(code: CombatSliceScenarioErrorCode, message: string) {
    super(message)
    this.name = 'CombatSliceScenarioError'
    this.code = code
  }
}

function fail(path: string, reason: string): never {
  throw new CombatSliceScenarioError(
    COMBAT_SLICE_SCENARIO_ERROR_CODES.invalidData,
    `Invalid combat slice scenario at ${path}: ${reason}`,
  )
}

function compareSemanticIds(first: string, second: string): number {
  return first < second ? -1 : first > second ? 1 : 0
}

function expectRecord(value: unknown, path: string): Record<string, unknown> {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    return fail(path, 'must be an object')
  }
  return value as Record<string, unknown>
}

function expectExactKeys(record: Record<string, unknown>, expectedKeys: readonly string[], path: string): void {
  const expected = [...expectedKeys].sort()
  const actual = Object.keys(record).sort()
  if (actual.length !== expected.length || actual.some((key, index) => key !== expected[index])) {
    fail(path, `must contain exactly: ${expected.join(', ')}`)
  }
}

function expectNonEmptyString(value: unknown, path: string): string {
  if (typeof value !== 'string' || value.length === 0) {
    return fail(path, 'must be a non-empty string')
  }
  return value
}

function expectFiniteNumber(value: unknown, path: string): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) {
    return fail(path, 'must be a finite number')
  }
  return value
}

function expectPositiveFiniteNumber(value: unknown, path: string): number {
  const number = expectFiniteNumber(value, path)
  if (number <= 0) {
    return fail(path, 'must be greater than zero')
  }
  return number
}

function parseVector3(value: unknown, path: string, positive = false): ScenarioVector3 {
  const record = expectRecord(value, path)
  expectExactKeys(record, ['x', 'y', 'z'], path)
  const vector = {
    x: expectFiniteNumber(record.x, `${path}.x`),
    y: expectFiniteNumber(record.y, `${path}.y`),
    z: expectFiniteNumber(record.z, `${path}.z`),
  }
  if (positive && (vector.x <= 0 || vector.y <= 0 || vector.z <= 0)) {
    return fail(path, 'all components must be greater than zero')
  }
  return vector
}

function parseHitVolume(value: unknown, path: string): TargetHitVolume {
  const record = expectRecord(value, path)
  const semanticId = expectNonEmptyString(record.semanticId, `${path}.semanticId`)

  if (record.kind === 'sphere' && record.zone === 'head') {
    expectExactKeys(record, ['semanticId', 'kind', 'zone', 'centerOffset', 'radius'], path)
    return {
      semanticId,
      kind: 'sphere',
      zone: 'head',
      centerOffset: parseVector3(record.centerOffset, `${path}.centerOffset`),
      radius: expectPositiveFiniteNumber(record.radius, `${path}.radius`),
    }
  }

  if (record.kind === 'box' && record.zone === 'torso') {
    expectExactKeys(record, ['semanticId', 'kind', 'zone', 'centerOffset', 'halfExtents'], path)
    return {
      semanticId,
      kind: 'box',
      zone: 'torso',
      centerOffset: parseVector3(record.centerOffset, `${path}.centerOffset`),
      halfExtents: parseVector3(record.halfExtents, `${path}.halfExtents`, true),
    }
  }

  if (record.kind === 'capsule' && record.zone === 'limb') {
    expectExactKeys(record, ['semanticId', 'kind', 'zone', 'startOffset', 'endOffset', 'radius'], path)
    const startOffset = parseVector3(record.startOffset, `${path}.startOffset`)
    const endOffset = parseVector3(record.endOffset, `${path}.endOffset`)
    if (
      startOffset.x === endOffset.x &&
      startOffset.y === endOffset.y &&
      startOffset.z === endOffset.z
    ) {
      return fail(path, 'capsule endpoints must be different')
    }
    return {
      semanticId,
      kind: 'capsule',
      zone: 'limb',
      startOffset,
      endOffset,
      radius: expectPositiveFiniteNumber(record.radius, `${path}.radius`),
    }
  }

  return fail(path, 'kind and zone must be sphere/head, box/torso, or capsule/limb')
}

function parseTarget(value: unknown, path: string): RobotTargetDefinition {
  const record = expectRecord(value, path)
  expectExactKeys(record, ['semanticId', 'position', 'yawRadians', 'hitVolumes'], path)
  if (!Array.isArray(record.hitVolumes)) {
    return fail(`${path}.hitVolumes`, 'must be an array')
  }

  const hitVolumes = record.hitVolumes
    .map((volume, index) => parseHitVolume(volume, `${path}.hitVolumes[${index}]`))
    .sort((first, second) => compareSemanticIds(first.semanticId, second.semanticId))
  for (let index = 1; index < hitVolumes.length; index += 1) {
    if (hitVolumes[index - 1]?.semanticId === hitVolumes[index]?.semanticId) {
      return fail(`${path}.hitVolumes`, `contains duplicate semanticId ${hitVolumes[index]?.semanticId ?? ''}`)
    }
  }

  const headCount = hitVolumes.filter((volume) => volume.zone === 'head').length
  const torsoCount = hitVolumes.filter((volume) => volume.zone === 'torso').length
  const limbCount = hitVolumes.filter((volume) => volume.zone === 'limb').length
  if (headCount !== 1 || torsoCount !== 1 || limbCount !== 4) {
    return fail(`${path}.hitVolumes`, 'must contain one head sphere, one torso box, and four limb capsules')
  }

  return {
    semanticId: expectNonEmptyString(record.semanticId, `${path}.semanticId`),
    position: parseVector3(record.position, `${path}.position`),
    yawRadians: expectFiniteNumber(record.yawRadians, `${path}.yawRadians`),
    hitVolumes,
  }
}

function parsePlayerSpawn(value: unknown): ScenarioPlayerSpawn {
  const record = expectRecord(value, 'CombatSliceScenario.playerSpawn')
  expectExactKeys(record, ['position', 'yawRadians'], 'CombatSliceScenario.playerSpawn')
  return {
    position: parseVector3(record.position, 'CombatSliceScenario.playerSpawn.position'),
    yawRadians: expectFiniteNumber(
      record.yawRadians,
      'CombatSliceScenario.playerSpawn.yawRadians',
    ),
  }
}

function parsePayload(value: unknown): CombatSliceScenarioPayload {
  const record = expectRecord(value, 'CombatSliceScenario')
  expectExactKeys(record, ['schemaVersion', 'id', 'playerSpawn', 'targets'], 'CombatSliceScenario')
  if (
    record.schemaVersion !== COMBAT_SLICE_SCENARIO_SCHEMA_VERSION ||
    record.id !== COMBAT_SLICE_SCENARIO_ID
  ) {
    throw new CombatSliceScenarioError(
      COMBAT_SLICE_SCENARIO_ERROR_CODES.unsupportedIdentity,
      `Unsupported combat slice scenario identity: ${String(record.id)}@${String(record.schemaVersion)}`,
    )
  }
  if (!Array.isArray(record.targets) || record.targets.length === 0) {
    return fail('CombatSliceScenario.targets', 'must be a non-empty array')
  }

  const targets = record.targets
    .map((target, index) => parseTarget(target, `CombatSliceScenario.targets[${index}]`))
    .sort((first, second) => compareSemanticIds(first.semanticId, second.semanticId))
  for (let index = 1; index < targets.length; index += 1) {
    if (targets[index - 1]?.semanticId === targets[index]?.semanticId) {
      return fail('CombatSliceScenario.targets', `contains duplicate semanticId ${targets[index]?.semanticId ?? ''}`)
    }
  }

  return {
    schemaVersion: COMBAT_SLICE_SCENARIO_SCHEMA_VERSION,
    id: COMBAT_SLICE_SCENARIO_ID,
    playerSpawn: parsePlayerSpawn(record.playerSpawn),
    targets,
  }
}

export function createCombatSliceScenario(payload: CombatSliceScenarioPayload): CombatSliceScenario {
  const normalized = parsePayload(payload)
  return deepFreeze({ ...normalized, contentHash: contentHash(normalized) })
}

export function parseCombatSliceScenario(input: string | unknown): CombatSliceScenario {
  let value: unknown
  try {
    value = typeof input === 'string' ? parseCanonicalJson(input) : input
    canonicalSerialize(value)
  } catch (error) {
    if (error instanceof CombatSliceScenarioError) {
      throw error
    }
    return fail('CombatSliceScenario', error instanceof Error ? error.message : 'must be canonical JSON data')
  }

  const record = expectRecord(value, 'CombatSliceScenario')
  expectExactKeys(
    record,
    ['schemaVersion', 'id', 'playerSpawn', 'targets', 'contentHash'],
    'CombatSliceScenario',
  )
  if (!isContentHash(record.contentHash)) {
    return fail('CombatSliceScenario.contentHash', 'must be a versioned content hash')
  }
  const normalized = parsePayload({
    schemaVersion: record.schemaVersion,
    id: record.id,
    playerSpawn: record.playerSpawn,
    targets: record.targets,
  })
  const expectedHash = contentHash(normalized)
  if (record.contentHash !== expectedHash) {
    throw new CombatSliceScenarioError(
      COMBAT_SLICE_SCENARIO_ERROR_CODES.contentHashMismatch,
      `Combat slice scenario contentHash must be ${expectedHash}`,
    )
  }
  return deepFreeze({ ...normalized, contentHash: expectedHash })
}

export function serializeCombatSliceScenario(scenario: CombatSliceScenario): string {
  return canonicalSerialize(parseCombatSliceScenario(scenario))
}

export function combatSliceScenarioIdentity(
  scenario: CombatSliceScenario,
): CombatSliceScenarioIdentity {
  const normalized = parseCombatSliceScenario(scenario)
  return deepFreeze({
    schemaVersion: normalized.schemaVersion,
    id: normalized.id,
    contentHash: normalized.contentHash,
  })
}
