import { describe, expect, it } from 'vitest'

import { DIAGNOSTIC_ARENA } from '../arena'
import { createActionFrame } from '../input/actionFrame'
import { DEFAULT_MATCH_CONFIGURATION, createMatchConfiguration } from '../match'
import { createInitialSimulationSnapshot, localParticipant, parseSimulationSnapshot } from './playableSnapshot'
import {
  PLAYABLE_SIMULATION_REPLAY_SCHEMA_VERSION,
  PlayableFixedStepHeadlessRunner,
  createPlayableSimulationReplay,
  parsePlayableSimulationReplay,
  runPlayableSimulationReplay,
  serializePlayableSimulationReplay,
  type PlayableSimulationStep,
} from './playableReplay'

function initialSnapshot() {
  return createInitialSimulationSnapshot({
    rngSeed: 42,
    arena: {
      id: DIAGNOSTIC_ARENA.id,
      seed: DIAGNOSTIC_ARENA.seed,
      generatorVersion: DIAGNOSTIC_ARENA.generatorVersion,
      contentHash: DIAGNOSTIC_ARENA.contentHash as `fnv1a64-v1:${string}`,
    },
  })
}

const frames = [
  createActionFrame(1, [{ participantId: 'local-player-1', actionId: 'move-forward', value: 1 }]),
  createActionFrame(2, [{ participantId: 'local-player-1', actionId: 'fire', value: 1 }]),
  createActionFrame(3, [{ participantId: 'local-player-1', actionId: 'fire', value: 0 }]),
]

const fixtureStepper: PlayableSimulationStep = (snapshot, frame) => parseSimulationSnapshot({
  ...snapshot,
  tick: frame.tick,
  participants: snapshot.participants.map((participant) => participant.kind === 'local-seat'
    ? {
        ...participant,
        position: {
          ...participant.position,
          x: participant.position.x + frame.actions.reduce((sum, action) => sum + action.value, 0),
        },
      }
    : participant),
  events: [],
})

describe('playable replay fixtures', () => {
  it('canonically round-trips schema v3 with configuration and the full playable initial snapshot', () => {
    const replay = createPlayableSimulationReplay(initialSnapshot(), frames)
    const restored = parsePlayableSimulationReplay(serializePlayableSimulationReplay(replay))

    expect(replay.schemaVersion).toBe(PLAYABLE_SIMULATION_REPLAY_SCHEMA_VERSION)
    expect(restored).toEqual(replay)
    expect(Object.isFrozen(restored)).toBe(true)
    expect(Object.isFrozen(localParticipant(restored.initialSnapshot))).toBe(true)
    expect(Object.isFrozen(restored.frames)).toBe(true)
  })

  it('runs deterministic injected playable steps and records per-tick hashes', () => {
    const replay = createPlayableSimulationReplay(initialSnapshot(), frames)
    const first = runPlayableSimulationReplay(replay, fixtureStepper)
    const second = runPlayableSimulationReplay(replay, fixtureStepper)

    expect(first).toEqual(second)
    expect(first.tickHashes).toHaveLength(3)
    expect(first.finalSnapshot.tick).toBe(3)
    expect(localParticipant(first.finalSnapshot).position.x).toBe(2)
  })

  it('rejects replay schema mismatches, frame gaps, and invalid step ticks', () => {
    const replay = createPlayableSimulationReplay(initialSnapshot(), frames)
    expect(() => parsePlayableSimulationReplay({ ...replay, schemaVersion: 1 })).toThrow(/schemaVersion/)
    expect(() => createPlayableSimulationReplay(initialSnapshot(), [frames[1]!])).toThrow(/expected tick 1/)

    const runner = new PlayableFixedStepHeadlessRunner(initialSnapshot(), (snapshot) => snapshot)
    expect(() => runner.step(frames[0]!)).toThrow(/return simulation tick 1/)
  })

  it('carries a non-default team configuration through serialize, restore, and per-tick hashing', () => {
    const configuration = createMatchConfiguration({
      schemaVersion: DEFAULT_MATCH_CONFIGURATION.schemaVersion,
      id: DEFAULT_MATCH_CONFIGURATION.id,
      mode: 'teams',
      durationMinutes: 1,
      scoreTargetPoints: 1_000,
      participants: [
        { semanticId: 'local-player-1', name: 'Pilot', kind: 'local-seat', color: '#ef4444', team: 'red' },
        { semanticId: 'robot-center', name: 'Center', kind: 'stationary-fixture', color: '#3b82f6', team: 'blue' },
      ],
    })
    const initial = createInitialSimulationSnapshot({
      rngSeed: 42,
      arena: {
        id: DIAGNOSTIC_ARENA.id,
        seed: DIAGNOSTIC_ARENA.seed,
        generatorVersion: DIAGNOSTIC_ARENA.generatorVersion,
        contentHash: DIAGNOSTIC_ARENA.contentHash as `fnv1a64-v1:${string}`,
      },
      configuration,
    })
    const customFrames = [createActionFrame(1, [])]
    const restored = parsePlayableSimulationReplay(serializePlayableSimulationReplay(
      createPlayableSimulationReplay(initial, customFrames, configuration),
    ))
    const customStepper: PlayableSimulationStep = (snapshot, frame) => parseSimulationSnapshot({
      ...snapshot,
      tick: frame.tick,
      match: {
        ...snapshot.match,
        elapsedTicks: snapshot.match.elapsedTicks + 1,
        remainingTicks: snapshot.match.remainingTicks - 1,
      },
      events: [],
    }, configuration)
    const first = runPlayableSimulationReplay(restored, customStepper)
    const second = runPlayableSimulationReplay(restored, customStepper)
    expect(restored.configuration).toEqual(configuration)
    expect(first).toEqual(second)
    expect(first.tickHashes).toHaveLength(1)
  })
})
