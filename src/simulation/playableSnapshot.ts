import {
  RUNTIME_COLLISION_COMPATIBILITY_IDENTITY,
  parseCollisionCompatibilityIdentity,
  type CollisionCompatibilityIdentity,
  type CollisionVector3,
} from '../collision/contracts'
import { canonicalSerialize, contentHash, isContentHash, parseCanonicalJson, type ContentHash } from '../core/canonical'
import { deepFreeze } from '../core/deepFreeze'
import {
  PROTOTYPE_V1_PROFILE,
  validateGameDesignProfile,
  type GameDesignProfile,
  type ValidatedGameDesignProfile,
} from '../profiles'
import {
  COMBAT_SLICE_SCENARIO_V1,
  combatSliceScenarioIdentity,
  parseCombatSliceScenario,
  type CombatSliceScenario,
  type CombatSliceScenarioIdentity,
} from '../scenario'
import { createSeededRngState, parseSeededRngState, type SeededRngState } from './rng'

export const SIMULATION_SNAPSHOT_SCHEMA_VERSION = 2 as const
export const PLAYABLE_PLAYER_ID = 'local-player-1' as const
export const PLAYABLE_PLAYER_LIFE_ID = 'local-player-1-life-1' as const
export const DOUBLE_BARREL_SHOTGUN_ID = 'double-barrel-shotgun' as const

export const SIMULATION_SNAPSHOT_ERROR_CODES = {
  invalidData: 'SIMULATION_SNAPSHOT_INVALID_DATA',
  unsupportedIdentity: 'SIMULATION_SNAPSHOT_UNSUPPORTED_IDENTITY',
  unsupportedSchema: 'SIMULATION_SNAPSHOT_UNSUPPORTED_SCHEMA',
} as const

export type SimulationSnapshotErrorCode =
  (typeof SIMULATION_SNAPSHOT_ERROR_CODES)[keyof typeof SIMULATION_SNAPSHOT_ERROR_CODES]

export class SimulationSnapshotError extends TypeError {
  readonly code: SimulationSnapshotErrorCode

  constructor(code: SimulationSnapshotErrorCode, message: string) {
    super(message)
    this.name = 'SimulationSnapshotError'
    this.code = code
  }
}

export interface ProfileIdentity {
  readonly id: string
  readonly revision: number
  readonly contentHash: ContentHash
}

export interface ArenaIdentity {
  readonly id: string
  readonly seed: number
  readonly generatorVersion: string
  readonly contentHash: ContentHash
}

export interface PlayablePlayerState {
  readonly semanticId: typeof PLAYABLE_PLAYER_ID
  readonly lifeId: typeof PLAYABLE_PLAYER_LIFE_ID
  readonly position: CollisionVector3
  readonly velocity: CollisionVector3
  readonly grounded: boolean
  readonly yaw: number
  readonly pitch: number
  readonly health: number
  readonly previousJumpPressed: boolean
}

export interface DoubleBarrelShotgunState {
  readonly semanticId: typeof DOUBLE_BARREL_SHOTGUN_ID
  readonly ammo: number
  readonly shotSequence: number
  readonly nextAllowedShotTick: number
  readonly previousFirePressed: boolean
}

export interface CombatTargetState {
  readonly semanticId: string
  readonly health: number
  readonly active: boolean
}

export type CombatHitZone = 'head' | 'torso' | 'limb'

interface SimulationEventBase {
  readonly eventId: string
  readonly tick: number
}

export interface ShotgunShotEvent extends SimulationEventBase {
  readonly kind: 'shotgun-shot'
  readonly shooterId: typeof PLAYABLE_PLAYER_ID
  readonly shotSequence: number
  readonly origin: CollisionVector3
  readonly pelletDirections: readonly CollisionVector3[]
}

export interface ShotgunImpactEvent extends SimulationEventBase {
  readonly kind: 'shotgun-impact'
  readonly pelletIndex: number
  readonly position: CollisionVector3
  readonly normal: CollisionVector3
  readonly targetId: string | null
  readonly surfaceId: string | null
  readonly zone: CombatHitZone | null
}

export interface TargetDamagedEvent extends SimulationEventBase {
  readonly kind: 'target-damaged'
  readonly targetId: string
  readonly damage: number
  readonly healthAfter: number
  readonly zones: readonly CombatHitZone[]
}

export interface TargetDestroyedEvent extends SimulationEventBase {
  readonly kind: 'target-destroyed'
  readonly targetId: string
}

export interface DryFireEvent extends SimulationEventBase {
  readonly kind: 'dry-fire'
  readonly shooterId: typeof PLAYABLE_PLAYER_ID
}

export type SimulationEvent =
  | ShotgunShotEvent
  | ShotgunImpactEvent
  | TargetDamagedEvent
  | TargetDestroyedEvent
  | DryFireEvent

export interface SimulationSnapshot {
  readonly schemaVersion: typeof SIMULATION_SNAPSHOT_SCHEMA_VERSION
  readonly tick: number
  readonly rng: SeededRngState
  readonly profile: ProfileIdentity
  readonly arena: ArenaIdentity
  readonly collision: CollisionCompatibilityIdentity
  readonly scenario: CombatSliceScenarioIdentity
  readonly player: PlayablePlayerState
  readonly weapon: DoubleBarrelShotgunState
  readonly targets: readonly CombatTargetState[]
  readonly events: readonly SimulationEvent[]
}

export interface InitialSimulationOptions {
  readonly rngSeed: number
  readonly arena: ArenaIdentity
  readonly profile?: GameDesignProfile | ValidatedGameDesignProfile
  readonly scenario?: CombatSliceScenario
  readonly collision?: CollisionCompatibilityIdentity
}

export const RUNTIME_PROFILE_IDENTITY: ProfileIdentity = deepFreeze({
  id: PROTOTYPE_V1_PROFILE.id,
  revision: PROTOTYPE_V1_PROFILE.revision,
  contentHash: PROTOTYPE_V1_PROFILE.contentHash as ContentHash,
})

export const RUNTIME_SCENARIO_IDENTITY: CombatSliceScenarioIdentity =
  combatSliceScenarioIdentity(COMBAT_SLICE_SCENARIO_V1)

const zoneOrder: Readonly<Record<CombatHitZone, number>> = { head: 0, torso: 1, limb: 2 }

function fail(path: string, reason: string): never {
  throw new SimulationSnapshotError(
    SIMULATION_SNAPSHOT_ERROR_CODES.invalidData,
    `Invalid SimulationSnapshot at ${path}: ${reason}`,
  )
}

function unsupported(kind: string, actual: unknown): never {
  throw new SimulationSnapshotError(
    SIMULATION_SNAPSHOT_ERROR_CODES.unsupportedIdentity,
    `Unsupported SimulationSnapshot ${kind} identity: ${canonicalSerialize(actual)}`,
  )
}

function compareIds(first: string, second: string): number {
  return first < second ? -1 : first > second ? 1 : 0
}

function record(value: unknown, path: string): Record<string, unknown> {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) return fail(path, 'must be an object')
  return value as Record<string, unknown>
}

function exactKeys(value: Record<string, unknown>, expectedKeys: readonly string[], path: string): void {
  const actual = Object.keys(value).sort()
  const expected = [...expectedKeys].sort()
  if (actual.length !== expected.length || actual.some((key, index) => key !== expected[index])) {
    fail(path, `must contain exactly: ${expected.join(', ')}`)
  }
}

function string(value: unknown, path: string): string {
  if (typeof value !== 'string' || value.length === 0) return fail(path, 'must be a non-empty string')
  return value
}

function finite(value: unknown, path: string, minimum?: number): number {
  if (typeof value !== 'number' || !Number.isFinite(value) || (minimum !== undefined && value < minimum)) {
    return fail(path, `must be finite${minimum === undefined ? '' : ` and >= ${minimum}`}`)
  }
  return value
}

function integer(value: unknown, path: string, minimum: number): number {
  if (!Number.isSafeInteger(value) || (value as number) < minimum) return fail(path, `must be a safe integer >= ${minimum}`)
  return value as number
}

function boolean(value: unknown, path: string): boolean {
  if (typeof value !== 'boolean') return fail(path, 'must be a boolean')
  return value
}

function hash(value: unknown, path: string): ContentHash {
  if (!isContentHash(value)) return fail(path, 'must be a versioned content hash')
  return value
}

function vector(value: unknown, path: string): CollisionVector3 {
  const data = record(value, path)
  exactKeys(data, ['x', 'y', 'z'], path)
  return { x: finite(data.x, `${path}.x`), y: finite(data.y, `${path}.y`), z: finite(data.z, `${path}.z`) }
}

function profileIdentity(value: unknown): ProfileIdentity {
  const data = record(value, 'profile')
  exactKeys(data, ['id', 'revision', 'contentHash'], 'profile')
  const parsed = {
    id: string(data.id, 'profile.id'),
    revision: integer(data.revision, 'profile.revision', 1),
    contentHash: hash(data.contentHash, 'profile.contentHash'),
  }
  if (
    parsed.id !== RUNTIME_PROFILE_IDENTITY.id ||
    parsed.revision !== RUNTIME_PROFILE_IDENTITY.revision ||
    parsed.contentHash !== RUNTIME_PROFILE_IDENTITY.contentHash
  ) return unsupported('profile', parsed)
  return RUNTIME_PROFILE_IDENTITY
}

function arenaIdentity(value: unknown): ArenaIdentity {
  const data = record(value, 'arena')
  exactKeys(data, ['id', 'seed', 'generatorVersion', 'contentHash'], 'arena')
  return {
    id: string(data.id, 'arena.id'),
    seed: integer(data.seed, 'arena.seed', 0),
    generatorVersion: string(data.generatorVersion, 'arena.generatorVersion'),
    contentHash: hash(data.contentHash, 'arena.contentHash'),
  }
}

function collisionIdentity(value: unknown): CollisionCompatibilityIdentity {
  try {
    return parseCollisionCompatibilityIdentity(value)
  } catch {
    return unsupported('collision', value)
  }
}

function scenarioIdentity(value: unknown): CombatSliceScenarioIdentity {
  const data = record(value, 'scenario')
  exactKeys(data, ['schemaVersion', 'id', 'contentHash'], 'scenario')
  if (
    data.schemaVersion !== RUNTIME_SCENARIO_IDENTITY.schemaVersion ||
    data.id !== RUNTIME_SCENARIO_IDENTITY.id ||
    data.contentHash !== RUNTIME_SCENARIO_IDENTITY.contentHash
  ) return unsupported('scenario', data)
  return RUNTIME_SCENARIO_IDENTITY
}

function playerState(value: unknown): PlayablePlayerState {
  const data = record(value, 'player')
  exactKeys(data, ['semanticId', 'lifeId', 'position', 'velocity', 'grounded', 'yaw', 'pitch', 'health', 'previousJumpPressed'], 'player')
  if (data.semanticId !== PLAYABLE_PLAYER_ID || data.lifeId !== PLAYABLE_PLAYER_LIFE_ID) {
    return unsupported('player', { semanticId: data.semanticId, lifeId: data.lifeId })
  }
  return {
    semanticId: PLAYABLE_PLAYER_ID,
    lifeId: PLAYABLE_PLAYER_LIFE_ID,
    position: vector(data.position, 'player.position'),
    velocity: vector(data.velocity, 'player.velocity'),
    grounded: boolean(data.grounded, 'player.grounded'),
    yaw: finite(data.yaw, 'player.yaw'),
    pitch: finite(data.pitch, 'player.pitch'),
    health: finite(data.health, 'player.health', 0),
    previousJumpPressed: boolean(data.previousJumpPressed, 'player.previousJumpPressed'),
  }
}

function weaponState(value: unknown): DoubleBarrelShotgunState {
  const data = record(value, 'weapon')
  exactKeys(data, ['semanticId', 'ammo', 'shotSequence', 'nextAllowedShotTick', 'previousFirePressed'], 'weapon')
  if (data.semanticId !== DOUBLE_BARREL_SHOTGUN_ID) return unsupported('weapon', { semanticId: data.semanticId })
  return {
    semanticId: DOUBLE_BARREL_SHOTGUN_ID,
    ammo: integer(data.ammo, 'weapon.ammo', 0),
    shotSequence: integer(data.shotSequence, 'weapon.shotSequence', 0),
    nextAllowedShotTick: integer(data.nextAllowedShotTick, 'weapon.nextAllowedShotTick', 0),
    previousFirePressed: boolean(data.previousFirePressed, 'weapon.previousFirePressed'),
  }
}

function targetStates(value: unknown): readonly CombatTargetState[] {
  if (!Array.isArray(value)) return fail('targets', 'must be an array')
  const targets = value.map((entry, index): CombatTargetState => {
    const path = `targets[${index}]`
    const data = record(entry, path)
    exactKeys(data, ['semanticId', 'health', 'active'], path)
    const health = finite(data.health, `${path}.health`, 0)
    const active = boolean(data.active, `${path}.active`)
    if (active !== (health > 0)) fail(path, 'active must be true exactly while health is positive')
    return { semanticId: string(data.semanticId, `${path}.semanticId`), health, active }
  }).sort((first, second) => compareIds(first.semanticId, second.semanticId))
  const expected = COMBAT_SLICE_SCENARIO_V1.targets.map((target) => target.semanticId)
  if (targets.length !== expected.length || targets.some((target, index) => target.semanticId !== expected[index])) {
    return unsupported('target set', targets.map((target) => target.semanticId))
  }
  return targets
}

function zone(value: unknown, path: string): CombatHitZone {
  if (value !== 'head' && value !== 'torso' && value !== 'limb') return fail(path, 'must be head, torso, or limb')
  return value
}

function zones(value: unknown, path: string): readonly CombatHitZone[] {
  if (!Array.isArray(value) || value.length === 0) return fail(path, 'must be a non-empty array')
  const parsed = value.map((item, index) => zone(item, `${path}[${index}]`))
    .sort((first, second) => zoneOrder[first] - zoneOrder[second])
  if (parsed.some((item, index) => index > 0 && parsed[index - 1] === item)) return fail(path, 'must contain unique zones')
  return parsed
}

function nullableString(value: unknown, path: string): string | null {
  return value === null ? null : string(value, path)
}

function event(value: unknown, index: number, snapshotTick: number): SimulationEvent {
  const path = `events[${index}]`
  const data = record(value, path)
  const eventId = string(data.eventId, `${path}.eventId`)
  if (data.tick !== snapshotTick) fail(`${path}.tick`, `must equal snapshot tick ${snapshotTick}`)
  switch (data.kind) {
    case 'shotgun-shot':
      exactKeys(data, ['eventId', 'tick', 'kind', 'shooterId', 'shotSequence', 'origin', 'pelletDirections'], path)
      if (data.shooterId !== PLAYABLE_PLAYER_ID) return unsupported('event shooter', { shooterId: data.shooterId })
      if (!Array.isArray(data.pelletDirections) || data.pelletDirections.length === 0) return fail(`${path}.pelletDirections`, 'must be non-empty')
      return {
        eventId,
        tick: snapshotTick,
        kind: 'shotgun-shot',
        shooterId: PLAYABLE_PLAYER_ID,
        shotSequence: integer(data.shotSequence, `${path}.shotSequence`, 1),
        origin: vector(data.origin, `${path}.origin`),
        pelletDirections: data.pelletDirections.map((direction, pelletIndex) => vector(direction, `${path}.pelletDirections[${pelletIndex}]`)),
      }
    case 'shotgun-impact': {
      exactKeys(data, ['eventId', 'tick', 'kind', 'pelletIndex', 'position', 'normal', 'targetId', 'surfaceId', 'zone'], path)
      const targetId = nullableString(data.targetId, `${path}.targetId`)
      const surfaceId = nullableString(data.surfaceId, `${path}.surfaceId`)
      if ((targetId === null) === (surfaceId === null)) fail(path, 'must reference exactly one targetId or surfaceId')
      const hitZone = data.zone === null ? null : zone(data.zone, `${path}.zone`)
      if ((targetId === null) !== (hitZone === null)) fail(path, 'zone must be present exactly for target impacts')
      return { eventId, tick: snapshotTick, kind: 'shotgun-impact', pelletIndex: integer(data.pelletIndex, `${path}.pelletIndex`, 0), position: vector(data.position, `${path}.position`), normal: vector(data.normal, `${path}.normal`), targetId, surfaceId, zone: hitZone }
    }
    case 'target-damaged':
      exactKeys(data, ['eventId', 'tick', 'kind', 'targetId', 'damage', 'healthAfter', 'zones'], path)
      return { eventId, tick: snapshotTick, kind: 'target-damaged', targetId: string(data.targetId, `${path}.targetId`), damage: finite(data.damage, `${path}.damage`, Number.MIN_VALUE), healthAfter: finite(data.healthAfter, `${path}.healthAfter`, 0), zones: zones(data.zones, `${path}.zones`) }
    case 'target-destroyed':
      exactKeys(data, ['eventId', 'tick', 'kind', 'targetId'], path)
      return { eventId, tick: snapshotTick, kind: 'target-destroyed', targetId: string(data.targetId, `${path}.targetId`) }
    case 'dry-fire':
      exactKeys(data, ['eventId', 'tick', 'kind', 'shooterId'], path)
      if (data.shooterId !== PLAYABLE_PLAYER_ID) return unsupported('event shooter', { shooterId: data.shooterId })
      return { eventId, tick: snapshotTick, kind: 'dry-fire', shooterId: PLAYABLE_PLAYER_ID }
    default:
      return fail(`${path}.kind`, 'is unsupported')
  }
}

function events(value: unknown, tick: number, targetIds: ReadonlySet<string>): readonly SimulationEvent[] {
  if (!Array.isArray(value)) return fail('events', 'must be an array')
  const parsed = value.map((item, index) => event(item, index, tick))
    .sort((first, second) => compareIds(first.eventId, second.eventId))
  for (let index = 0; index < parsed.length; index += 1) {
    const current = parsed[index]
    if (index > 0 && parsed[index - 1]?.eventId === current?.eventId) fail('events', `contains duplicate eventId ${current?.eventId ?? ''}`)
    if (current !== undefined && 'targetId' in current && current.targetId !== null && !targetIds.has(current.targetId)) {
      fail(`${current.eventId}.targetId`, `references unknown target ${current.targetId}`)
    }
  }
  return parsed
}

export function createInitialSimulationSnapshot(options: InitialSimulationOptions): SimulationSnapshot {
  const profileValidation = validateGameDesignProfile(options.profile ?? PROTOTYPE_V1_PROFILE)
  if (!profileValidation.ok) return fail('profile', `failed validation: ${profileValidation.errors.map((error) => error.code).join(', ')}`)
  const profile = profileValidation.profile
  const scenario = parseCombatSliceScenario(options.scenario ?? COMBAT_SLICE_SCENARIO_V1)
  const playerCenterY = scenario.playerSpawn.position.y + profile.rules.player.capsule.halfHeight + profile.rules.player.capsule.radius

  return parseSimulationSnapshot({
    schemaVersion: SIMULATION_SNAPSHOT_SCHEMA_VERSION,
    tick: 0,
    rng: createSeededRngState(options.rngSeed),
    profile: { id: profile.id, revision: profile.revision, contentHash: profile.contentHash },
    arena: options.arena,
    collision: options.collision ?? RUNTIME_COLLISION_COMPATIBILITY_IDENTITY,
    scenario: combatSliceScenarioIdentity(scenario),
    player: {
      semanticId: PLAYABLE_PLAYER_ID,
      lifeId: PLAYABLE_PLAYER_LIFE_ID,
      position: { x: scenario.playerSpawn.position.x, y: playerCenterY, z: scenario.playerSpawn.position.z },
      velocity: { x: 0, y: 0, z: 0 },
      grounded: true,
      yaw: scenario.playerSpawn.yawRadians,
      pitch: 0,
      health: profile.rules.player.startingHealth,
      previousJumpPressed: false,
    },
    weapon: { semanticId: DOUBLE_BARREL_SHOTGUN_ID, ammo: profile.rules.weapons.doubleBarrelShotgun.startingAmmo, shotSequence: 0, nextAllowedShotTick: 0, previousFirePressed: false },
    targets: scenario.targets.map((target) => ({ semanticId: target.semanticId, health: profile.rules.player.startingHealth, active: true })),
    events: [],
  })
}

export function parseSimulationSnapshot(input: string | unknown): SimulationSnapshot {
  let value: unknown
  try {
    value = typeof input === 'string' ? parseCanonicalJson(input) : input
    canonicalSerialize(value)
  } catch (error) {
    if (error instanceof SimulationSnapshotError) throw error
    return fail('$', error instanceof Error ? error.message : 'must be canonical JSON data')
  }
  const data = record(value, '$')
  exactKeys(data, ['schemaVersion', 'tick', 'rng', 'profile', 'arena', 'collision', 'scenario', 'player', 'weapon', 'targets', 'events'], '$')
  if (data.schemaVersion !== SIMULATION_SNAPSHOT_SCHEMA_VERSION) {
    throw new SimulationSnapshotError(SIMULATION_SNAPSHOT_ERROR_CODES.unsupportedSchema, `Unsupported SimulationSnapshot.schemaVersion: ${String(data.schemaVersion)}`)
  }
  const tick = integer(data.tick, 'tick', 0)
  const targets = targetStates(data.targets)
  return deepFreeze({
    schemaVersion: SIMULATION_SNAPSHOT_SCHEMA_VERSION,
    tick,
    rng: parseSeededRngState(data.rng),
    profile: profileIdentity(data.profile),
    arena: arenaIdentity(data.arena),
    collision: collisionIdentity(data.collision),
    scenario: scenarioIdentity(data.scenario),
    player: playerState(data.player),
    weapon: weaponState(data.weapon),
    targets,
    events: events(data.events, tick, new Set(targets.map((target) => target.semanticId))),
  })
}

export function serializeSimulationSnapshot(snapshot: SimulationSnapshot): string {
  return canonicalSerialize(parseSimulationSnapshot(snapshot))
}

export function simulationSnapshotHash(snapshot: SimulationSnapshot): ContentHash {
  return contentHash(parseSimulationSnapshot(snapshot))
}
