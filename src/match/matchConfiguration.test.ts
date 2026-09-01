import { describe, expect, it } from 'vitest'

import { PROTOTYPE_V1_PROFILE } from '../profiles'
import {
  DEFAULT_MATCH_CONFIGURATION,
  MATCH_CONFIGURATION_ID,
  MATCH_CONFIGURATION_SCHEMA_VERSION,
  TEAM_COLORS,
  createMatchConfiguration,
  parseMatchConfiguration,
  type MatchConfigurationPayload,
} from './matchConfiguration'

function payload(overrides: Partial<MatchConfigurationPayload> = {}): MatchConfigurationPayload {
  return {
    schemaVersion: MATCH_CONFIGURATION_SCHEMA_VERSION,
    id: MATCH_CONFIGURATION_ID,
    mode: 'ffa',
    arenaSize: 'medium',
    arenaSeed: 0x53544152,
    durationMinutes: 5,
    scoreTargetPoints: null,
    participants: [
      { semanticId: 'local', name: 'Local', kind: 'local-seat', color: '#ffffff', team: null },
      { semanticId: 'fixture', name: 'Fixture', kind: 'stationary-fixture', color: '#ffcc00', team: null },
    ],
    ...overrides,
  }
}

describe('MatchConfiguration', () => {
  it('defaults to a concrete medium arena seed and includes both in identity', () => {
    expect(DEFAULT_MATCH_CONFIGURATION).toMatchObject({ schemaVersion: 2, arenaSize: 'medium', arenaSeed: 0x53544152 })
    const changed = createMatchConfiguration(payload({ arenaSize: 'large', arenaSeed: 7 }))
    expect(changed.contentHash).not.toBe(DEFAULT_MATCH_CONFIGURATION.contentHash)
  })

  it('rejects unsupported size and seeds outside uint32', () => {
    expect(() => createMatchConfiguration(payload({ arenaSize: 'giant' as 'medium' }))).toThrow(/arenaSize/)
    expect(() => createMatchConfiguration(payload({ arenaSeed: 0x1_0000_0000 }))).toThrow(/arenaSeed/)
  })
  it('creates a canonical immutable FFA configuration and keeps target disabled by default', () => {
    const configuration = createMatchConfiguration(payload())
    expect(configuration).toMatchObject({ mode: 'ffa', durationMinutes: 5, scoreTargetPoints: null })
    expect(configuration.participants.map(({ semanticId }) => semanticId)).toEqual(['fixture', 'local'])
    expect(Object.isFrozen(configuration)).toBe(true)
    expect(Object.isFrozen(configuration.participants)).toBe(true)
    expect(parseMatchConfiguration(configuration)).toEqual(configuration)
  })

  it('accepts two non-empty teams and derives their exact colors', () => {
    const configuration = createMatchConfiguration(payload({
      mode: 'teams',
      scoreTargetPoints: 3_000,
      participants: [
        { semanticId: 'local', name: 'Local', kind: 'local-seat', color: TEAM_COLORS.red, team: 'red' },
        { semanticId: 'fixture', name: 'Fixture', kind: 'stationary-fixture', color: TEAM_COLORS.blue, team: 'blue' },
      ],
    }))
    expect(configuration.mode).toBe('teams')
    expect(configuration.participants.map(({ team }) => team)).toEqual(['blue', 'red'])
  })

  it('rejects invalid roster size, duplicate ids, empty teams and unsupported participant kinds', () => {
    expect(() => createMatchConfiguration(payload({ participants: [payload().participants[0]!] }))).toThrow(/2-8/)
    expect(() => createMatchConfiguration(payload({ participants: [payload().participants[0]!, payload().participants[0]!] }))).toThrow(/unique/)
    expect(() => createMatchConfiguration(payload({
      mode: 'teams',
      participants: [
        { semanticId: 'local', name: 'Local', kind: 'local-seat', color: TEAM_COLORS.red, team: 'red' },
        { semanticId: 'fixture', name: 'Fixture', kind: 'stationary-fixture', color: TEAM_COLORS.red, team: 'red' },
      ],
    }))).toThrow(/non-empty red and blue/)
    const unsupported = payload() as unknown as { participants: Array<Record<string, unknown>> }
    unsupported.participants[1]!.kind = 'bot'
    expect(() => createMatchConfiguration(unsupported as unknown as MatchConfigurationPayload)).toThrow(/local-seat or stationary-fixture/)
  })

  it('uses profile duration and target range/step metadata', () => {
    expect(() => createMatchConfiguration(payload({ durationMinutes: 31 }))).toThrow(/durationMinutes/)
    expect(() => createMatchConfiguration(payload({ scoreTargetPoints: 900 }))).toThrow(/scoreTargetPoints/)
    expect(() => createMatchConfiguration(payload({ scoreTargetPoints: 1_050 }))).toThrow(/steps of 100/)
    expect(createMatchConfiguration(payload({ scoreTargetPoints: 20_000 })).scoreTargetPoints).toBe(20_000)
    expect(DEFAULT_MATCH_CONFIGURATION.durationMinutes).toBe(PROTOTYPE_V1_PROFILE.rules.match.duration.defaultMinutes)
  })
})
