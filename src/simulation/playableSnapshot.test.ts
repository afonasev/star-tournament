import { describe, expect, it } from 'vitest'

import { DIAGNOSTIC_ARENA } from '../arena'
import { RUNTIME_COLLISION_COMPATIBILITY_IDENTITY } from '../collision/contracts'
import type { ContentHash } from '../core/canonical'
import { PROTOTYPE_V1_PROFILE, computeProfileContentHash, validateGameDesignProfile, type GameDesignProfile } from '../profiles'
import { COMBAT_SLICE_SCENARIO_V1 } from '../scenario'
import {
  RUNTIME_PROFILE_IDENTITY,
  RUNTIME_SCENARIO_IDENTITY,
  SIMULATION_SNAPSHOT_ERROR_CODES,
  SimulationSnapshotError,
  createInitialSimulationSnapshot,
  localParticipant,
  parseSimulationSnapshot,
  serializeSimulationSnapshot,
  simulationSnapshotHash,
  stationaryParticipants,
} from './playableSnapshot'

function initialSnapshot() {
  return createInitialSimulationSnapshot({
    rngSeed: 42,
    arena: {
      id: DIAGNOSTIC_ARENA.id,
      seed: DIAGNOSTIC_ARENA.seed,
      generatorVersion: DIAGNOSTIC_ARENA.generatorVersion,
      contentHash: DIAGNOSTIC_ARENA.contentHash as ContentHash,
    },
  })
}

describe('playable SimulationSnapshot schema v3', () => {
  it('creates profile-driven player, weapon, target, and compatibility state', () => {
    const snapshot = initialSnapshot()
    const player = localParticipant(snapshot)
    const targets = stationaryParticipants(snapshot)
    const capsule = PROTOTYPE_V1_PROFILE.rules.player.capsule

    expect(snapshot.schemaVersion).toBe(4)
    expect(snapshot.tick).toBe(0)
    expect(snapshot.profile).toBe(RUNTIME_PROFILE_IDENTITY)
    expect(snapshot.collision).toBe(RUNTIME_COLLISION_COMPATIBILITY_IDENTITY)
    expect(snapshot.scenario).toBe(RUNTIME_SCENARIO_IDENTITY)
    expect(player).toMatchObject({
      semanticId: 'local-player-1',
      lifeId: 'local-player-1:life:1',
      grounded: true,
      health: PROTOTYPE_V1_PROFILE.rules.player.startingHealth,
      previousJumpPressed: false,
    })
    expect(player.position.y).toBe(
      COMBAT_SLICE_SCENARIO_V1.playerSpawn.position.y + capsule.halfHeight + capsule.radius,
    )
    expect(player.position.y - capsule.halfHeight - capsule.radius).toBe(0)
    expect(player.position.z).toBe(6)
    expect(player.weapon).toEqual({
      semanticId: 'double-barrel-shotgun',
      ammo: PROTOTYPE_V1_PROFILE.rules.weapons.doubleBarrelShotgun.startingAmmo,
      shotSequence: 0,
      nextAllowedShotTick: 0,
      previousFirePressed: false,
    })
    expect(targets.map((target) => target.semanticId)).toEqual([
      'robot-center',
      'robot-left',
      'robot-right',
    ])
    expect(targets.every((target) => target.alive && target.health === 100)).toBe(true)
    expect(snapshot.participants).toHaveLength(4)
    expect(snapshot.match).toMatchObject({ phase: 'running', elapsedTicks: 0, remainingTicks: 18_000 })
    expect(snapshot.events).toEqual([])
  })

  it('canonically round-trips, hashes, normalizes current-tick events, and deep-freezes', () => {
    const source = initialSnapshot()
    const withEvents = {
      ...source,
      tick: 3,
      events: [
        {
          eventId: 'event-02-damage',
          tick: 3,
          kind: 'target-damaged',
          attackerId: 'local-player-1',
          targetId: 'robot-center',
          targetLifeId: 'robot-center:life:1',
          damage: 30,
          healthAfter: 70,
          zones: ['limb', 'head', 'torso'],
        },
        {
          eventId: 'event-01-shot',
          tick: 3,
          kind: 'shotgun-shot',
          shooterId: 'local-player-1',
          shotSequence: 1,
          origin: { x: 0, y: 1.5, z: 8 },
          pelletDirections: [{ x: 0, y: 0, z: -1 }],
        },
      ],
    }
    const parsed = parseSimulationSnapshot(withEvents)
    const restored = parseSimulationSnapshot(serializeSimulationSnapshot(parsed))

    expect(restored).toEqual(parsed)
    expect(restored.events.map((event) => event.eventId)).toEqual([
      'event-01-shot',
      'event-02-damage',
    ])
    expect(restored.events[1]).toMatchObject({ zones: ['head', 'torso', 'limb'] })
    expect(simulationSnapshotHash(restored)).toBe(simulationSnapshotHash(parsed))
    expect(Object.isFrozen(restored)).toBe(true)
    expect(Object.isFrozen(localParticipant(restored).position)).toBe(true)
    expect(Object.isFrozen(restored.participants)).toBe(true)
    expect(Object.isFrozen(restored.events)).toBe(true)
  })

  it.each([
    ['schema', (snapshot: ReturnType<typeof initialSnapshot>) => ({ ...snapshot, schemaVersion: 2 })],
    [
      'profile',
      (snapshot: ReturnType<typeof initialSnapshot>) => ({
        ...snapshot,
        profile: { ...snapshot.profile, revision: snapshot.profile.revision + 1 },
      }),
    ],
    [
      'collision',
      (snapshot: ReturnType<typeof initialSnapshot>) => ({
        ...snapshot,
        collision: { ...snapshot.collision, backendVersion: '0.21.0' },
      }),
    ],
    [
      'scenario',
      (snapshot: ReturnType<typeof initialSnapshot>) => ({
        ...snapshot,
        scenario: { ...snapshot.scenario, contentHash: 'fnv1a64-v1:0000000000000000' },
      }),
    ],
  ])('rejects unsupported %s compatibility before simulation', (kind, mutate) => {
    expect.assertions(2)
    try {
      parseSimulationSnapshot(mutate(initialSnapshot()))
    } catch (error) {
      expect(error).toBeInstanceOf(SimulationSnapshotError)
      expect((error as SimulationSnapshotError).code).toBe(
        kind === 'schema'
          ? SIMULATION_SNAPSHOT_ERROR_CODES.unsupportedSchema
          : SIMULATION_SNAPSHOT_ERROR_CODES.unsupportedIdentity,
      )
    }
  })

  it('rejects stale events, unknown targets, and inconsistent active state', () => {
    const source = initialSnapshot()
    const malformed = [
      {
        ...source,
        tick: 1,
        events: [{ eventId: 'dry', tick: 0, kind: 'dry-fire', shooterId: 'local-player-1' }],
      },
      {
        ...source,
        tick: 1,
        events: [{ eventId: 'destroy', tick: 1, kind: 'target-destroyed', killerId: 'local-player-1', targetId: 'robot-unknown', targetLifeId: 'robot-unknown:life:1' }],
      },
      {
        ...source,
        participants: source.participants.map((participant, index) => index === 0 ? { ...participant, health: 0, alive: true } : participant),
      },
    ]

    for (const value of malformed) {
      expect(() => parseSimulationSnapshot(value)).toThrow(
        expect.objectContaining({ code: SIMULATION_SNAPSHOT_ERROR_CODES.invalidData }),
      )
    }
  })

  it('uses the exact validated startup profile as the snapshot compatibility context', () => {
    const draft = JSON.parse(JSON.stringify(PROTOTYPE_V1_PROFILE)) as GameDesignProfile
    draft.revision += 1
    draft.source = 'local'
    draft.contentHash = computeProfileContentHash(draft)
    const validation = validateGameDesignProfile(draft)
    if (!validation.ok) throw new Error('Expected valid local profile')

    const snapshot = createInitialSimulationSnapshot({ rngSeed: 42, arena: DIAGNOSTIC_ARENA, profile: validation.profile })
    expect(parseSimulationSnapshot(snapshot, undefined, validation.profile).profile).toEqual({
      id: validation.profile.id,
      revision: validation.profile.revision,
      contentHash: validation.profile.contentHash,
    })
    expect(() => parseSimulationSnapshot(snapshot)).toThrow(
      expect.objectContaining({ code: SIMULATION_SNAPSHOT_ERROR_CODES.unsupportedIdentity }),
    )
  })
})
