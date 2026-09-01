import { RUNTIME_COLLISION_COMPATIBILITY_IDENTITY, parseCollisionCompatibilityIdentity, type CollisionCompatibilityIdentity, type CollisionVector3 } from '../collision/contracts'
import { allocateArenaSpawns, type ArenaDefinition } from '../arena'
import { canonicalSerialize, contentHash, isContentHash, parseCanonicalJson, type ContentHash } from '../core/canonical'
import { deepFreeze } from '../core/deepFreeze'
import { DEFAULT_MATCH_CONFIGURATION, parseMatchConfiguration, type MatchConfiguration, type MatchMode, type MatchParticipantKind, type MatchTeam } from '../match'
import { PROTOTYPE_V1_PROFILE, validateGameDesignProfile, type GameDesignProfile, type ValidatedGameDesignProfile } from '../profiles'
import { COMBAT_SLICE_SCENARIO_V1, combatSliceScenarioIdentity, parseCombatSliceScenario, type CombatSliceScenario, type CombatSliceScenarioIdentity } from '../scenario'
import { createSeededRngState, parseSeededRngState, type SeededRngState } from './rng'

export const SIMULATION_SNAPSHOT_SCHEMA_VERSION = 3 as const
export const PLAYABLE_PLAYER_ID = 'local-player-1' as const
export const PLAYABLE_PLAYER_LIFE_ID = 'local-player-1:life:1' as const
export const DOUBLE_BARREL_SHOTGUN_ID = 'double-barrel-shotgun' as const
export const SIMULATION_SNAPSHOT_ERROR_CODES = { invalidData: 'SIMULATION_SNAPSHOT_INVALID_DATA', unsupportedIdentity: 'SIMULATION_SNAPSHOT_UNSUPPORTED_IDENTITY', unsupportedSchema: 'SIMULATION_SNAPSHOT_UNSUPPORTED_SCHEMA' } as const

export class SimulationSnapshotError extends TypeError {
  readonly code: (typeof SIMULATION_SNAPSHOT_ERROR_CODES)[keyof typeof SIMULATION_SNAPSHOT_ERROR_CODES]
  constructor(code: (typeof SIMULATION_SNAPSHOT_ERROR_CODES)[keyof typeof SIMULATION_SNAPSHOT_ERROR_CODES], message: string) { super(message); this.name = 'SimulationSnapshotError'; this.code = code }
}
export type SimulationSnapshotErrorCode = (typeof SIMULATION_SNAPSHOT_ERROR_CODES)[keyof typeof SIMULATION_SNAPSHOT_ERROR_CODES]

export interface ProfileIdentity { readonly id: string; readonly revision: number; readonly contentHash: ContentHash }
export interface ArenaIdentity { readonly id: string; readonly seed: number; readonly generatorVersion: string; readonly contentHash: ContentHash }
export interface MatchConfigurationIdentity { readonly schemaVersion: 2; readonly id: string; readonly contentHash: ContentHash }
export interface DoubleBarrelShotgunState { readonly semanticId: typeof DOUBLE_BARREL_SHOTGUN_ID; readonly ammo: number; readonly shotSequence: number; readonly nextAllowedShotTick: number; readonly previousFirePressed: boolean }
export interface ParticipantStatistics { readonly kills: number; readonly assists: number; readonly deaths: number; readonly damageDealt: number; readonly damageReceived: number; readonly score: number }
export interface ParticipantKillChain { readonly length: number; readonly lastKillTick: number | null; readonly awardedInChain: number }
export interface DamageLedgerEntry { readonly attackerId: string; readonly lastDamageTick: number }
export interface ParticipantCorpseState { readonly corpseId: string; readonly participantId: string; readonly lifeId: string; readonly position: CollisionVector3; readonly yaw: number; readonly createdTick: number; readonly expiresTick: number }
export interface MatchParticipantState {
  readonly semanticId: string; readonly name: string; readonly kind: MatchParticipantKind; readonly color: string; readonly team: MatchTeam | null
  readonly lifeId: string; readonly lifeSequence: number; readonly position: CollisionVector3; readonly velocity: CollisionVector3; readonly grounded: boolean
  readonly yaw: number; readonly pitch: number; readonly health: number; readonly alive: boolean; readonly deathTick: number | null; readonly respawnTick: number | null
  readonly corpseExpiresTick: number | null; readonly previousJumpPressed: boolean; readonly weapon: DoubleBarrelShotgunState; readonly statistics: ParticipantStatistics
  readonly killChain: ParticipantKillChain; readonly damageLedger: readonly DamageLedgerEntry[]
}
export type PlayablePlayerState = MatchParticipantState
export interface CombatTargetState { readonly semanticId: string; readonly lifeId: string; readonly health: number; readonly active: boolean }
export type CombatHitZone = 'head' | 'torso' | 'limb'
export type MatchPhase = 'running' | 'overtime' | 'finished'
export type MatchFinishTrigger = 'score-limit' | 'time-limit'
export interface MatchFinalResult { readonly winnerType: 'participant' | 'team'; readonly winnerId: string; readonly trigger: MatchFinishTrigger; readonly finalTick: number; readonly finalStandings: readonly string[] }
export interface SimulationMatchState { readonly configuration: MatchConfigurationIdentity; readonly mode: MatchMode; readonly phase: MatchPhase; readonly finishTrigger: MatchFinishTrigger | null; readonly elapsedTicks: number; readonly remainingTicks: number; readonly result: MatchFinalResult | null }

interface SimulationEventBase { readonly eventId: string; readonly tick: number }
export interface ShotgunShotEvent extends SimulationEventBase { readonly kind: 'shotgun-shot'; readonly shooterId: string; readonly shotSequence: number; readonly origin: CollisionVector3; readonly pelletDirections: readonly CollisionVector3[] }
export interface ShotgunImpactEvent extends SimulationEventBase { readonly kind: 'shotgun-impact'; readonly pelletIndex: number; readonly position: CollisionVector3; readonly normal: CollisionVector3; readonly targetId: string | null; readonly surfaceId: string | null; readonly zone: CombatHitZone | null }
export interface TargetDamagedEvent extends SimulationEventBase { readonly kind: 'target-damaged'; readonly attackerId: string; readonly targetId: string; readonly targetLifeId: string; readonly damage: number; readonly healthAfter: number; readonly zones: readonly CombatHitZone[] }
export interface TargetDestroyedEvent extends SimulationEventBase { readonly kind: 'target-destroyed'; readonly killerId: string; readonly targetId: string; readonly targetLifeId: string }
export interface ParticipantRespawnedEvent extends SimulationEventBase { readonly kind: 'participant-respawned'; readonly participantId: string; readonly lifeId: string }
export interface DryFireEvent extends SimulationEventBase { readonly kind: 'dry-fire'; readonly shooterId: string }
export type SimulationEvent = ShotgunShotEvent | ShotgunImpactEvent | TargetDamagedEvent | TargetDestroyedEvent | ParticipantRespawnedEvent | DryFireEvent

export interface SimulationSnapshot {
  readonly schemaVersion: typeof SIMULATION_SNAPSHOT_SCHEMA_VERSION; readonly tick: number; readonly rng: SeededRngState; readonly profile: ProfileIdentity; readonly arena: ArenaIdentity; readonly collision: CollisionCompatibilityIdentity; readonly scenario: CombatSliceScenarioIdentity; readonly match: SimulationMatchState; readonly participants: readonly MatchParticipantState[]; readonly corpses: readonly ParticipantCorpseState[]; readonly events: readonly SimulationEvent[]
}
export interface InitialSimulationOptions { readonly rngSeed: number; readonly arena: ArenaIdentity | ArenaDefinition; readonly profile?: GameDesignProfile | ValidatedGameDesignProfile; readonly scenario?: CombatSliceScenario; readonly configuration?: MatchConfiguration; readonly collision?: CollisionCompatibilityIdentity }

export const RUNTIME_PROFILE_IDENTITY: ProfileIdentity = deepFreeze({ id: PROTOTYPE_V1_PROFILE.id, revision: PROTOTYPE_V1_PROFILE.revision, contentHash: PROTOTYPE_V1_PROFILE.contentHash as ContentHash })
export const RUNTIME_SCENARIO_IDENTITY = combatSliceScenarioIdentity(COMBAT_SLICE_SCENARIO_V1)

function fail(path: string, reason: string): never { throw new SimulationSnapshotError(SIMULATION_SNAPSHOT_ERROR_CODES.invalidData, `Invalid SimulationSnapshot at ${path}: ${reason}`) }
function unsupported(kind: string, actual: unknown): never { throw new SimulationSnapshotError(SIMULATION_SNAPSHOT_ERROR_CODES.unsupportedIdentity, `Unsupported SimulationSnapshot ${kind} identity: ${canonicalSerialize(actual)}`) }
function record(value: unknown, path: string): Record<string, unknown> { if (value === null || typeof value !== 'object' || Array.isArray(value)) return fail(path, 'must be an object'); return value as Record<string, unknown> }
function exact(value: Record<string, unknown>, keys: readonly string[], path: string): void { const actual = Object.keys(value).sort(); const expected = [...keys].sort(); if (actual.length !== expected.length || actual.some((key, index) => key !== expected[index])) fail(path, `must contain exactly ${expected.join(', ')}`) }
function text(value: unknown, path: string): string { if (typeof value !== 'string' || value.length === 0) return fail(path, 'must be non-empty string'); return value }
function finite(value: unknown, path: string, minimum?: number): number { if (typeof value !== 'number' || !Number.isFinite(value) || (minimum !== undefined && value < minimum)) return fail(path, `must be finite${minimum === undefined ? '' : ` and >= ${minimum}`}`); return value }
function integer(value: unknown, path: string, minimum = 0): number { if (!Number.isSafeInteger(value) || (value as number) < minimum) return fail(path, `must be safe integer >= ${minimum}`); return value as number }
function nullableInteger(value: unknown, path: string): number | null { return value === null ? null : integer(value, path) }
function bool(value: unknown, path: string): boolean { if (typeof value !== 'boolean') return fail(path, 'must be boolean'); return value }
function hash(value: unknown, path: string): ContentHash { if (!isContentHash(value)) return fail(path, 'must be content hash'); return value }
function vector(value: unknown, path: string): CollisionVector3 { const data = record(value, path); exact(data, ['x', 'y', 'z'], path); return { x: finite(data.x, `${path}.x`), y: finite(data.y, `${path}.y`), z: finite(data.z, `${path}.z`) } }
const ZONE_ORDER: Readonly<Record<CombatHitZone, number>> = { head: 0, torso: 1, limb: 2 }
function zones(value: unknown, path: string): readonly CombatHitZone[] { if (!Array.isArray(value)) return fail(path, 'must be array'); return value.map((zone, index): CombatHitZone => { if (zone !== 'head' && zone !== 'torso' && zone !== 'limb') return fail(`${path}[${index}]`, 'invalid zone'); return zone }).sort((a, b) => ZONE_ORDER[a] - ZONE_ORDER[b]) }

function profileIdentity(value: unknown): ProfileIdentity { const data = record(value, 'profile'); exact(data, ['id', 'revision', 'contentHash'], 'profile'); const parsed = { id: text(data.id, 'profile.id'), revision: integer(data.revision, 'profile.revision', 1), contentHash: hash(data.contentHash, 'profile.contentHash') }; if (parsed.id !== RUNTIME_PROFILE_IDENTITY.id || parsed.revision !== RUNTIME_PROFILE_IDENTITY.revision || parsed.contentHash !== RUNTIME_PROFILE_IDENTITY.contentHash) return unsupported('profile', parsed); return RUNTIME_PROFILE_IDENTITY }
function arenaIdentity(value: unknown): ArenaIdentity { const data = record(value, 'arena'); exact(data, ['id', 'seed', 'generatorVersion', 'contentHash'], 'arena'); return { id: text(data.id, 'arena.id'), seed: integer(data.seed, 'arena.seed'), generatorVersion: text(data.generatorVersion, 'arena.generatorVersion'), contentHash: hash(data.contentHash, 'arena.contentHash') } }
function scenarioIdentity(value: unknown): CombatSliceScenarioIdentity { const data = record(value, 'scenario'); exact(data, ['schemaVersion', 'id', 'contentHash'], 'scenario'); if (data.schemaVersion !== RUNTIME_SCENARIO_IDENTITY.schemaVersion || data.id !== RUNTIME_SCENARIO_IDENTITY.id || data.contentHash !== RUNTIME_SCENARIO_IDENTITY.contentHash) return unsupported('scenario', data); return RUNTIME_SCENARIO_IDENTITY }
function weapon(value: unknown, path: string): DoubleBarrelShotgunState { const data = record(value, path); exact(data, ['semanticId', 'ammo', 'shotSequence', 'nextAllowedShotTick', 'previousFirePressed'], path); if (data.semanticId !== DOUBLE_BARREL_SHOTGUN_ID) return unsupported('weapon', data.semanticId); return { semanticId: DOUBLE_BARREL_SHOTGUN_ID, ammo: integer(data.ammo, `${path}.ammo`), shotSequence: integer(data.shotSequence, `${path}.shotSequence`), nextAllowedShotTick: integer(data.nextAllowedShotTick, `${path}.nextAllowedShotTick`), previousFirePressed: bool(data.previousFirePressed, `${path}.previousFirePressed`) } }
function statistics(value: unknown, path: string): ParticipantStatistics { const data = record(value, path); exact(data, ['kills', 'assists', 'deaths', 'damageDealt', 'damageReceived', 'score'], path); return { kills: integer(data.kills, `${path}.kills`), assists: integer(data.assists, `${path}.assists`), deaths: integer(data.deaths, `${path}.deaths`), damageDealt: finite(data.damageDealt, `${path}.damageDealt`, 0), damageReceived: finite(data.damageReceived, `${path}.damageReceived`, 0), score: integer(data.score, `${path}.score`) } }
function killChain(value: unknown, path: string): ParticipantKillChain { const data = record(value, path); exact(data, ['length', 'lastKillTick', 'awardedInChain'], path); return { length: integer(data.length, `${path}.length`), lastKillTick: nullableInteger(data.lastKillTick, `${path}.lastKillTick`), awardedInChain: integer(data.awardedInChain, `${path}.awardedInChain`) } }
function ledger(value: unknown, path: string): readonly DamageLedgerEntry[] { if (!Array.isArray(value)) return fail(path, 'must be array'); const entries = value.map((entry, index) => { const data = record(entry, `${path}[${index}]`); exact(data, ['attackerId', 'lastDamageTick'], `${path}[${index}]`); return { attackerId: text(data.attackerId, `${path}[${index}].attackerId`), lastDamageTick: integer(data.lastDamageTick, `${path}[${index}].lastDamageTick`) } }).sort((a, b) => a.attackerId.localeCompare(b.attackerId, 'en')); if (new Set(entries.map(({ attackerId }) => attackerId)).size !== entries.length) fail(path, 'must have unique attackerId'); return entries }

function participant(value: unknown, index: number, configuration: MatchConfiguration): MatchParticipantState {
  const path = `participants[${index}]`; const data = record(value, path)
  exact(data, ['semanticId', 'name', 'kind', 'color', 'team', 'lifeId', 'lifeSequence', 'position', 'velocity', 'grounded', 'yaw', 'pitch', 'health', 'alive', 'deathTick', 'respawnTick', 'corpseExpiresTick', 'previousJumpPressed', 'weapon', 'statistics', 'killChain', 'damageLedger'], path)
  const semanticId = text(data.semanticId, `${path}.semanticId`); const descriptor = configuration.participants.find((candidate) => candidate.semanticId === semanticId); if (descriptor === undefined) return unsupported('participant', semanticId)
  if (data.name !== descriptor.name || data.kind !== descriptor.kind || data.color !== descriptor.color || data.team !== descriptor.team) return unsupported('participant descriptor', data)
  const alive = bool(data.alive, `${path}.alive`); const health = finite(data.health, `${path}.health`, 0); if (alive !== (health > 0)) fail(path, 'alive must equal health > 0')
  return { semanticId, name: descriptor.name, kind: descriptor.kind, color: descriptor.color, team: descriptor.team, lifeId: text(data.lifeId, `${path}.lifeId`), lifeSequence: integer(data.lifeSequence, `${path}.lifeSequence`, 1), position: vector(data.position, `${path}.position`), velocity: vector(data.velocity, `${path}.velocity`), grounded: bool(data.grounded, `${path}.grounded`), yaw: finite(data.yaw, `${path}.yaw`), pitch: finite(data.pitch, `${path}.pitch`), health, alive, deathTick: nullableInteger(data.deathTick, `${path}.deathTick`), respawnTick: nullableInteger(data.respawnTick, `${path}.respawnTick`), corpseExpiresTick: nullableInteger(data.corpseExpiresTick, `${path}.corpseExpiresTick`), previousJumpPressed: bool(data.previousJumpPressed, `${path}.previousJumpPressed`), weapon: weapon(data.weapon, `${path}.weapon`), statistics: statistics(data.statistics, `${path}.statistics`), killChain: killChain(data.killChain, `${path}.killChain`), damageLedger: ledger(data.damageLedger, `${path}.damageLedger`) }
}

function corpses(value: unknown, tick: number, participantIds: ReadonlySet<string>): readonly ParticipantCorpseState[] {
  if (!Array.isArray(value)) return fail('corpses', 'must be array')
  const parsed = value.map((entry, index): ParticipantCorpseState => {
    const path = `corpses[${index}]`; const data = record(entry, path)
    exact(data, ['corpseId', 'participantId', 'lifeId', 'position', 'yaw', 'createdTick', 'expiresTick'], path)
    const participantId = text(data.participantId, `${path}.participantId`); if (!participantIds.has(participantId)) fail(`${path}.participantId`, 'unknown participant')
    const createdTick = integer(data.createdTick, `${path}.createdTick`); const expiresTick = integer(data.expiresTick, `${path}.expiresTick`); if (createdTick > tick || expiresTick <= tick) fail(path, 'must have createdTick <= snapshot tick < expiresTick')
    return { corpseId: text(data.corpseId, `${path}.corpseId`), participantId, lifeId: text(data.lifeId, `${path}.lifeId`), position: vector(data.position, `${path}.position`), yaw: finite(data.yaw, `${path}.yaw`), createdTick, expiresTick }
  }).sort((first, second) => first.corpseId.localeCompare(second.corpseId, 'en'))
  if (new Set(parsed.map(({ corpseId }) => corpseId)).size !== parsed.length) fail('corpses', 'duplicate corpseId')
  return parsed
}

function matchState(value: unknown, configuration: MatchConfiguration, tick: number): SimulationMatchState {
  const data = record(value, 'match'); exact(data, ['configuration', 'mode', 'phase', 'finishTrigger', 'elapsedTicks', 'remainingTicks', 'result'], 'match')
  const identity = record(data.configuration, 'match.configuration'); exact(identity, ['schemaVersion', 'id', 'contentHash'], 'match.configuration'); if (identity.schemaVersion !== configuration.schemaVersion || identity.id !== configuration.id || identity.contentHash !== configuration.contentHash) return unsupported('configuration', identity)
  if (data.mode !== configuration.mode) return unsupported('match mode', data.mode); if (data.phase !== 'running' && data.phase !== 'overtime' && data.phase !== 'finished') fail('match.phase', 'must be running, overtime, or finished'); if (data.finishTrigger !== null && data.finishTrigger !== 'score-limit' && data.finishTrigger !== 'time-limit') fail('match.finishTrigger', 'invalid')
  let result: MatchFinalResult | null = null
  if (data.result !== null) { const output = record(data.result, 'match.result'); exact(output, ['winnerType', 'winnerId', 'trigger', 'finalTick', 'finalStandings'], 'match.result'); if (output.winnerType !== 'participant' && output.winnerType !== 'team') fail('match.result.winnerType', 'invalid'); if (output.trigger !== 'score-limit' && output.trigger !== 'time-limit') fail('match.result.trigger', 'invalid'); if (!Array.isArray(output.finalStandings)) fail('match.result.finalStandings', 'must be array'); result = { winnerType: output.winnerType, winnerId: text(output.winnerId, 'match.result.winnerId'), trigger: output.trigger, finalTick: integer(output.finalTick, 'match.result.finalTick'), finalStandings: output.finalStandings.map((id, i) => text(id, `match.result.finalStandings[${i}]`)) } }
  if ((data.phase === 'finished') !== (result !== null)) fail('match', 'finished phase must have result'); if (result !== null && result.finalTick !== tick) fail('match.result.finalTick', 'must equal snapshot tick')
  return { configuration: { schemaVersion: 2, id: configuration.id, contentHash: configuration.contentHash }, mode: configuration.mode, phase: data.phase, finishTrigger: data.finishTrigger as MatchFinishTrigger | null, elapsedTicks: integer(data.elapsedTicks, 'match.elapsedTicks'), remainingTicks: integer(data.remainingTicks, 'match.remainingTicks'), result }
}

function events(value: unknown, tick: number, participantIds: ReadonlySet<string>): readonly SimulationEvent[] {
  if (!Array.isArray(value)) return fail('events', 'must be array')
  const parsed = value.map((entry, index): SimulationEvent => {
    const path = `events[${index}]`; const data = record(entry, path); const base = { eventId: text(data.eventId, `${path}.eventId`), tick: integer(data.tick, `${path}.tick`) }; if (base.tick !== tick) fail(`${path}.tick`, 'must equal snapshot tick')
    switch (data.kind) {
      case 'shotgun-shot': exact(data, ['eventId', 'tick', 'kind', 'shooterId', 'shotSequence', 'origin', 'pelletDirections'], path); if (!Array.isArray(data.pelletDirections)) fail(`${path}.pelletDirections`, 'must be array'); return { ...base, kind: 'shotgun-shot', shooterId: text(data.shooterId, `${path}.shooterId`), shotSequence: integer(data.shotSequence, `${path}.shotSequence`, 1), origin: vector(data.origin, `${path}.origin`), pelletDirections: data.pelletDirections.map((direction, i) => vector(direction, `${path}.pelletDirections[${i}]`)) }
      case 'shotgun-impact': exact(data, ['eventId', 'tick', 'kind', 'pelletIndex', 'position', 'normal', 'targetId', 'surfaceId', 'zone'], path); return { ...base, kind: 'shotgun-impact', pelletIndex: integer(data.pelletIndex, `${path}.pelletIndex`), position: vector(data.position, `${path}.position`), normal: vector(data.normal, `${path}.normal`), targetId: data.targetId === null ? null : text(data.targetId, `${path}.targetId`), surfaceId: data.surfaceId === null ? null : text(data.surfaceId, `${path}.surfaceId`), zone: data.zone as CombatHitZone | null }
      case 'target-damaged': exact(data, ['eventId', 'tick', 'kind', 'attackerId', 'targetId', 'targetLifeId', 'damage', 'healthAfter', 'zones'], path); return { ...base, kind: 'target-damaged', attackerId: text(data.attackerId, `${path}.attackerId`), targetId: text(data.targetId, `${path}.targetId`), targetLifeId: text(data.targetLifeId, `${path}.targetLifeId`), damage: finite(data.damage, `${path}.damage`, 0), healthAfter: finite(data.healthAfter, `${path}.healthAfter`, 0), zones: zones(data.zones, `${path}.zones`) }
      case 'target-destroyed': exact(data, ['eventId', 'tick', 'kind', 'killerId', 'targetId', 'targetLifeId'], path); return { ...base, kind: 'target-destroyed', killerId: text(data.killerId, `${path}.killerId`), targetId: text(data.targetId, `${path}.targetId`), targetLifeId: text(data.targetLifeId, `${path}.targetLifeId`) }
      case 'participant-respawned': exact(data, ['eventId', 'tick', 'kind', 'participantId', 'lifeId'], path); return { ...base, kind: 'participant-respawned', participantId: text(data.participantId, `${path}.participantId`), lifeId: text(data.lifeId, `${path}.lifeId`) }
      case 'dry-fire': exact(data, ['eventId', 'tick', 'kind', 'shooterId'], path); return { ...base, kind: 'dry-fire', shooterId: text(data.shooterId, `${path}.shooterId`) }
      default: return fail(`${path}.kind`, 'unsupported')
    }
  }).sort((a, b) => a.eventId.localeCompare(b.eventId, 'en'))
  for (const event of parsed) {
    const values = event as unknown as Record<string, unknown>
    for (const key of ['shooterId', 'attackerId', 'killerId', 'targetId', 'participantId'] as const) {
      const participantId = values[key]
      if (participantId !== undefined && participantId !== null && !participantIds.has(participantId as string)) fail(`events.${event.eventId}.${key}`, 'unknown participant')
    }
  }
  if (new Set(parsed.map(({ eventId }) => eventId)).size !== parsed.length) fail('events', 'duplicate eventId'); return parsed
}

export function localParticipant(snapshot: SimulationSnapshot): MatchParticipantState { const local = snapshot.participants.find(({ kind }) => kind === 'local-seat'); if (local === undefined) return fail('participants', 'missing local-seat'); return local }
export function stationaryParticipants(snapshot: SimulationSnapshot): readonly MatchParticipantState[] { return snapshot.participants.filter(({ kind }) => kind === 'stationary-fixture') }

export function createInitialSimulationSnapshot(options: InitialSimulationOptions): SimulationSnapshot {
  const profileValidation = validateGameDesignProfile(options.profile ?? PROTOTYPE_V1_PROFILE); if (!profileValidation.ok) return fail('profile', 'invalid')
  const profile = profileValidation.profile; const scenario = parseCombatSliceScenario(options.scenario ?? COMBAT_SLICE_SCENARIO_V1); const configuration = parseMatchConfiguration(options.configuration ?? DEFAULT_MATCH_CONFIGURATION, profile); const fixtureById = new Map(scenario.targets.map((target) => [target.semanticId, target]))
  const fullArena = 'surfaces' in options.arena ? options.arena : undefined
  const allocationById = fullArena === undefined ? undefined : new Map(allocateArenaSpawns({ arena: fullArena, mode: configuration.mode, participants: configuration.participants, capsuleDiameterMeters: (profile.rules.player.capsule.radius + profile.rules.player.capsule.skinWidth) * 2, minimumOpponentSeparationMeters: profile.rules.arenaGenerator.presets[fullArena.size].minimumSpawnSeparationMeters }).map((allocation) => [allocation.participantId, allocation]))
  const participants = configuration.participants.map((descriptor): MatchParticipantState => { const fixture = descriptor.kind === 'local-seat' ? scenario.playerSpawn : fixtureById.get(descriptor.semanticId); if (fixture === undefined) return unsupported('configuration fixture', descriptor.semanticId); const allocation = allocationById?.get(descriptor.semanticId); const sourcePosition = allocation?.position ?? fixture.position; const position = descriptor.kind === 'local-seat' ? { x: sourcePosition.x, y: sourcePosition.y + profile.rules.player.capsule.halfHeight + profile.rules.player.capsule.radius, z: sourcePosition.z } : sourcePosition; return { ...descriptor, lifeId: `${descriptor.semanticId}:life:1`, lifeSequence: 1, position, velocity: { x: 0, y: 0, z: 0 }, grounded: true, yaw: allocation?.yawRadians ?? fixture.yawRadians, pitch: 0, health: profile.rules.player.startingHealth, alive: true, deathTick: null, respawnTick: null, corpseExpiresTick: null, previousJumpPressed: false, weapon: { semanticId: DOUBLE_BARREL_SHOTGUN_ID, ammo: profile.rules.weapons.doubleBarrelShotgun.startingAmmo, shotSequence: 0, nextAllowedShotTick: 0, previousFirePressed: false }, statistics: { kills: 0, assists: 0, deaths: 0, damageDealt: 0, damageReceived: 0, score: 0 }, killChain: { length: 0, lastKillTick: null, awardedInChain: 0 }, damageLedger: [] } })
  const totalTicks = configuration.durationMinutes * 60 * 60
  const arena = { id: options.arena.id, seed: options.arena.seed, generatorVersion: options.arena.generatorVersion, contentHash: options.arena.contentHash }
  return parseSimulationSnapshot({ schemaVersion: SIMULATION_SNAPSHOT_SCHEMA_VERSION, tick: 0, rng: createSeededRngState(options.rngSeed), profile: { id: profile.id, revision: profile.revision, contentHash: profile.contentHash }, arena, collision: options.collision ?? RUNTIME_COLLISION_COMPATIBILITY_IDENTITY, scenario: combatSliceScenarioIdentity(scenario), match: { configuration: { schemaVersion: 2, id: configuration.id, contentHash: configuration.contentHash }, mode: configuration.mode, phase: 'running', finishTrigger: null, elapsedTicks: 0, remainingTicks: totalTicks, result: null }, participants, corpses: [], events: [] }, configuration)
}

export function parseSimulationSnapshot(input: string | unknown, configurationInput: MatchConfiguration = DEFAULT_MATCH_CONFIGURATION): SimulationSnapshot {
  let value: unknown; try { value = typeof input === 'string' ? parseCanonicalJson(input) : input; canonicalSerialize(value !== null && typeof value === 'object' && !Array.isArray(value) ? { ...(value as Record<string, unknown>) } : value) } catch (error) { if (error instanceof SimulationSnapshotError) throw error; return fail('$', error instanceof Error ? error.message : 'invalid canonical data') }
  const configuration = parseMatchConfiguration(configurationInput); const data = record(value, '$'); exact(data, ['schemaVersion', 'tick', 'rng', 'profile', 'arena', 'collision', 'scenario', 'match', 'participants', 'corpses', 'events'], '$'); if (data.schemaVersion !== SIMULATION_SNAPSHOT_SCHEMA_VERSION) throw new SimulationSnapshotError(SIMULATION_SNAPSHOT_ERROR_CODES.unsupportedSchema, `Unsupported SimulationSnapshot.schemaVersion: ${String(data.schemaVersion)}`)
  const tick = integer(data.tick, 'tick'); if (!Array.isArray(data.participants)) fail('participants', 'must be array'); const participants = data.participants.map((entry, index) => participant(entry, index, configuration)).sort((a, b) => a.semanticId.localeCompare(b.semanticId, 'en')); if (participants.length !== configuration.participants.length || participants.some((entry, index) => entry.semanticId !== configuration.participants[index]?.semanticId)) return unsupported('participant set', participants.map(({ semanticId }) => semanticId))
  let collision: CollisionCompatibilityIdentity
  try { collision = parseCollisionCompatibilityIdentity(data.collision) } catch { return unsupported('collision', data.collision) }
  const participantIds = new Set(participants.map(({ semanticId }) => semanticId))
  return deepFreeze({ schemaVersion: SIMULATION_SNAPSHOT_SCHEMA_VERSION, tick, rng: parseSeededRngState(data.rng), profile: profileIdentity(data.profile), arena: arenaIdentity(data.arena), collision, scenario: scenarioIdentity(data.scenario), match: matchState(data.match, configuration, tick), participants, corpses: corpses(data.corpses, tick, participantIds), events: events(data.events, tick, participantIds) })
}

export function serializeSimulationSnapshot(snapshot: SimulationSnapshot, configuration: MatchConfiguration = DEFAULT_MATCH_CONFIGURATION): string { return canonicalSerialize({ ...parseSimulationSnapshot(snapshot, configuration) }) }
export function simulationSnapshotHash(snapshot: SimulationSnapshot, configuration: MatchConfiguration = DEFAULT_MATCH_CONFIGURATION): ContentHash { return contentHash({ ...parseSimulationSnapshot(snapshot, configuration) }) }
