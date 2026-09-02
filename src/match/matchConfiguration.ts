import { canonicalSerialize, contentHash, isContentHash, type ContentHash } from '../core/canonical'
import { deepFreeze } from '../core/deepFreeze'
import { ARENA_SIZE_IDS, type ArenaSize } from '../arena'
import { PROTOTYPE_V1_PROFILE, validateGameDesignProfile, type GameDesignProfile, type ValidatedGameDesignProfile } from '../profiles'
import { TEAM_COLORS } from './participantPalette'

export const MATCH_CONFIGURATION_SCHEMA_VERSION = 3 as const
export const MATCH_CONFIGURATION_ID = 'local-match-v1' as const
export { TEAM_COLORS, VETTED_PARTICIPANT_COLORS } from './participantPalette'

export type MatchMode = 'ffa' | 'teams'
export type MatchTeam = 'team-a' | 'team-b'
export type MatchParticipantKind = 'local-seat' | 'stationary-fixture'

export interface MatchParticipantDescriptor {
  readonly semanticId: string
  readonly name: string
  readonly kind: MatchParticipantKind
  readonly color: string
  readonly team: MatchTeam | null
}

export interface MatchConfigurationPayload {
  readonly schemaVersion: typeof MATCH_CONFIGURATION_SCHEMA_VERSION
  readonly id: typeof MATCH_CONFIGURATION_ID
  readonly mode: MatchMode
  readonly arenaSize: ArenaSize
  readonly arenaSeed: number
  readonly durationMinutes: number
  readonly scoreTargetPoints: number | null
  readonly participants: readonly MatchParticipantDescriptor[]
}

export interface MatchConfiguration extends MatchConfigurationPayload {
  readonly contentHash: ContentHash
}

export class MatchConfigurationError extends TypeError {
  readonly path: string

  constructor(path: string, reason: string) {
    super(`Invalid MatchConfiguration at ${path}: ${reason}`)
    this.name = 'MatchConfigurationError'
    this.path = path
  }
}

const parsedConfigurationCache = new WeakMap<object, MatchConfiguration>()

function fail(path: string, reason: string): never {
  throw new MatchConfigurationError(path, reason)
}

function record(value: unknown, path: string): Record<string, unknown> {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) return fail(path, 'must be an object')
  return value as Record<string, unknown>
}

function exactKeys(value: Record<string, unknown>, keys: readonly string[], path: string): void {
  const expected = [...keys].sort()
  const actual = Object.keys(value).sort()
  if (expected.length !== actual.length || actual.some((key, index) => key !== expected[index])) {
    fail(path, `must contain exactly ${expected.join(', ')}`)
  }
}

function nonEmpty(value: unknown, path: string): string {
  if (typeof value !== 'string' || value.trim().length === 0) return fail(path, 'must be a non-empty string')
  return value.trim()
}

function integer(value: unknown, path: string): number {
  if (!Number.isSafeInteger(value)) return fail(path, 'must be a safe integer')
  return value as number
}

function parseParticipant(value: unknown, index: number): MatchParticipantDescriptor {
  const path = `participants[${index}]`
  const data = record(value, path)
  exactKeys(data, ['semanticId', 'name', 'kind', 'color', 'team'], path)
  if (data.kind !== 'local-seat' && data.kind !== 'stationary-fixture') {
    return fail(`${path}.kind`, 'must be local-seat or stationary-fixture')
  }
  if (data.team !== null && data.team !== 'team-a' && data.team !== 'team-b') {
    return fail(`${path}.team`, 'must be team-a, team-b, or null')
  }
  const color = nonEmpty(data.color, `${path}.color`).toLowerCase()
  if (!/^#[0-9a-f]{6}$/.test(color)) return fail(`${path}.color`, 'must be a six-digit hex color')
  return {
    semanticId: nonEmpty(data.semanticId, `${path}.semanticId`),
    name: nonEmpty(data.name, `${path}.name`),
    kind: data.kind,
    color,
    team: data.team,
  }
}

function compatibleStep(value: number, minimum: number, step: number): boolean {
  const scaled = (value - minimum) / step
  return Math.abs(scaled - Math.round(scaled)) <= 1e-9
}

export function matchConfigurationContentPayload(configuration: MatchConfiguration): MatchConfigurationPayload {
  const { contentHash: _contentHash, ...payload } = configuration
  return payload
}

export function computeMatchConfigurationContentHash(configuration: MatchConfiguration): ContentHash {
  return contentHash(matchConfigurationContentPayload(configuration))
}

export function canonicalMatchConfigurationJson(configuration: MatchConfiguration): string {
  return canonicalSerialize(parseMatchConfiguration(configuration))
}

export function parseMatchConfiguration(
  input: unknown,
  profileInput: GameDesignProfile | ValidatedGameDesignProfile = PROTOTYPE_V1_PROFILE,
): MatchConfiguration {
  if (input !== null && typeof input === 'object') {
    const cached = parsedConfigurationCache.get(input)
    if (cached !== undefined) return cached
  }
  const profileValidation = validateGameDesignProfile(profileInput)
  if (!profileValidation.ok) return fail('profile', 'must be a valid GameDesignProfile')
  const profile = profileValidation.profile
  const data = record(input, '$')
  exactKeys(data, ['schemaVersion', 'id', 'mode', 'arenaSize', 'arenaSeed', 'durationMinutes', 'scoreTargetPoints', 'participants', 'contentHash'], '$')
  if (data.schemaVersion !== MATCH_CONFIGURATION_SCHEMA_VERSION) fail('schemaVersion', `must equal ${MATCH_CONFIGURATION_SCHEMA_VERSION}`)
  if (data.id !== MATCH_CONFIGURATION_ID) fail('id', `must equal ${MATCH_CONFIGURATION_ID}`)
  if (data.mode !== 'ffa' && data.mode !== 'teams') fail('mode', 'must be ffa or teams')
  if (!ARENA_SIZE_IDS.includes(data.arenaSize as ArenaSize)) fail('arenaSize', 'must be small, medium, or large')
  const arenaSeed = integer(data.arenaSeed, 'arenaSeed')
  if (arenaSeed < 0 || arenaSeed > 0xffffffff) fail('arenaSeed', 'must be an unsigned 32-bit integer')
  const durationMinutes = integer(data.durationMinutes, 'durationMinutes')
  const duration = profile.rules.match.duration
  if (durationMinutes < duration.minimumMinutes || durationMinutes > duration.maximumMinutes) {
    fail('durationMinutes', `must be between ${duration.minimumMinutes} and ${duration.maximumMinutes}`)
  }
  let scoreTargetPoints: number | null = null
  if (data.scoreTargetPoints !== null) {
    scoreTargetPoints = integer(data.scoreTargetPoints, 'scoreTargetPoints')
    const target = profile.rules.match.scoreTarget
    if (
      scoreTargetPoints < target.minimumPoints ||
      scoreTargetPoints > target.maximumPoints ||
      !compatibleStep(scoreTargetPoints, target.minimumPoints, target.stepPoints)
    ) fail('scoreTargetPoints', `must be ${target.minimumPoints}-${target.maximumPoints} in steps of ${target.stepPoints}`)
  }
  if (!Array.isArray(data.participants) || data.participants.length < 2 || data.participants.length > 8) {
    fail('participants', 'must contain 2-8 participants')
  }
  const participants = data.participants.map(parseParticipant)
    .sort((first, second) => first.semanticId.localeCompare(second.semanticId, 'en'))
  const ids = new Set(participants.map(({ semanticId }) => semanticId))
  if (ids.size !== participants.length) fail('participants', 'must have unique semanticId values')
  if (participants.filter(({ kind }) => kind === 'local-seat').length !== 1) {
    fail('participants', 'must contain exactly one local-seat participant')
  }
  if (data.mode === 'ffa') {
    if (participants.some(({ team }) => team !== null)) fail('participants', 'FFA participants must not have teams')
    if (new Set(participants.map(({ color }) => color)).size !== participants.length) {
      fail('participants', 'FFA participant colors must be unique')
    }
  } else {
    if (!participants.some(({ team }) => team === 'team-a') || !participants.some(({ team }) => team === 'team-b')) {
      fail('participants', 'teams mode requires non-empty team-a and team-b')
    }
    for (const [index, participant] of participants.entries()) {
      if (participant.team === null) fail(`participants[${index}].team`, 'teams participant must have a team')
      if (participant.color !== TEAM_COLORS[participant.team]) {
        fail(`participants[${index}].color`, `must match ${participant.team} team color`)
      }
    }
  }
  if (typeof data.contentHash !== 'string' || !isContentHash(data.contentHash)) fail('contentHash', 'must be a content hash')
  const parsed = {
    schemaVersion: MATCH_CONFIGURATION_SCHEMA_VERSION,
    id: MATCH_CONFIGURATION_ID,
    mode: data.mode,
    arenaSize: data.arenaSize as ArenaSize,
    arenaSeed,
    durationMinutes,
    scoreTargetPoints,
    participants,
    contentHash: data.contentHash,
  } satisfies MatchConfiguration
  if (computeMatchConfigurationContentHash(parsed) !== parsed.contentHash) fail('contentHash', 'does not match effective content')
  const frozen = deepFreeze(parsed)
  parsedConfigurationCache.set(data, frozen)
  parsedConfigurationCache.set(frozen, frozen)
  return frozen
}

export function createMatchConfiguration(
  payload: MatchConfigurationPayload,
  profile: GameDesignProfile | ValidatedGameDesignProfile = PROTOTYPE_V1_PROFILE,
): MatchConfiguration {
  const normalizedPayload: MatchConfigurationPayload = {
    ...payload,
    participants: payload.participants.map((participant) => ({
      ...participant,
      semanticId: participant.semanticId.trim(),
      name: participant.name.trim(),
      color: participant.color.toLowerCase(),
    })).sort((first, second) => first.semanticId.localeCompare(second.semanticId, 'en')),
  }
  const candidate = { ...normalizedPayload, contentHash: contentHash(normalizedPayload) }
  return parseMatchConfiguration(candidate, profile)
}

export const DEFAULT_MATCH_CONFIGURATION = createMatchConfiguration({
  schemaVersion: MATCH_CONFIGURATION_SCHEMA_VERSION,
  id: MATCH_CONFIGURATION_ID,
  mode: 'ffa',
  arenaSize: 'medium',
  arenaSeed: 0x53544152,
  durationMinutes: PROTOTYPE_V1_PROFILE.rules.match.duration.defaultMinutes,
  scoreTargetPoints: null,
  participants: [
    { semanticId: 'local-player-1', name: 'Pilot', kind: 'local-seat', color: '#3b82f6', team: null },
    { semanticId: 'robot-center', name: 'Center', kind: 'stationary-fixture', color: '#f59e0b', team: null },
    { semanticId: 'robot-left', name: 'Left', kind: 'stationary-fixture', color: '#22c55e', team: null },
    { semanticId: 'robot-right', name: 'Right', kind: 'stationary-fixture', color: '#a855f7', team: null },
  ],
})
