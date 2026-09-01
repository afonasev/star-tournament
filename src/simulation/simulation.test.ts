import { describe, expect, it } from 'vitest'

import { contentHash } from '../core/canonical'
import { createActionFrame, type ActionFrame } from '../input/actionFrame'
import {
  createInitialSimulationSnapshot,
  createSeededRngState,
  createSimulationReplay,
  FixedStepHeadlessRunner,
  nextRandomUint32,
  parseSimulationReplay,
  parseSimulationSnapshot,
  runHeadlessSimulation,
  runSimulationReplay,
  serializeSimulationReplay,
  serializeSimulationSnapshot,
  SIMULATION_TICKS_PER_SECOND,
  simulationSnapshotHash,
  stepSimulation,
} from './index'

const profileHash = contentHash({ profile: 'prototype-v1' })
const arenaHash = contentHash({ arena: 'diagnostic-v1', seed: 42 })

function initialSnapshot() {
  return createInitialSimulationSnapshot({
    rngSeed: 42,
    profile: { id: 'prototype-v1', revision: 1, contentHash: profileHash },
    arena: {
      id: 'diagnostic-arena',
      seed: 42,
      generatorVersion: 'diagnostic-v1',
      contentHash: arenaHash,
    },
  })
}

function actionFrames(count: number): readonly ActionFrame[] {
  return Array.from({ length: count }, (_, index) =>
    createActionFrame(index + 1, [
      {
        participantId: 'local-1',
        actionId: 'diagnostic.signal',
        value: index % 3 === 0 ? 1 : -0.25,
      },
      {
        participantId: 'bot-1',
        actionId: 'diagnostic.signal',
        value: (index % 5) * 0.1,
      },
    ]),
  )
}

describe('seeded RNG', () => {
  it('produces identical non-zero xorshift32 sequences for equal seeds', () => {
    let first = createSeededRngState(0)
    let second = createSeededRngState(0)
    const firstValues: number[] = []
    const secondValues: number[] = []

    for (let index = 0; index < 8; index += 1) {
      const firstSample = nextRandomUint32(first)
      const secondSample = nextRandomUint32(second)
      first = firstSample.state
      second = secondSample.state
      firstValues.push(firstSample.value)
      secondValues.push(secondSample.value)
    }

    expect(firstValues).toEqual(secondValues)
    expect(firstValues.every((value) => value !== 0)).toBe(true)
    expect(firstValues).toEqual([
      1085196063, 2447379481, 2618286376, 1701901981, 265159372, 1030440423, 4012273292,
      2080899351,
    ])
  })
})

describe('SimulationSnapshot', () => {
  it('round-trips schema, tick, entities, identity, RNG, and state hash', () => {
    const source = stepSimulation(initialSnapshot(), createActionFrame(1, []))
    const restored = parseSimulationSnapshot(serializeSimulationSnapshot(source))

    expect(restored).toEqual(source)
    expect(restored.schemaVersion).toBe(1)
    expect(restored.tick).toBe(1)
    expect(restored.rng).toEqual(source.rng)
    expect(restored.entities).toEqual(source.entities)
    expect(simulationSnapshotHash(restored)).toBe(simulationSnapshotHash(source))
    expect(Object.isFrozen(restored)).toBe(true)
    expect(Object.isFrozen(restored.entities[0].position)).toBe(true)
  })

  it('rejects unsupported versions and missing required state', () => {
    const valid = initialSnapshot()
    expect(() => parseSimulationSnapshot({ ...valid, schemaVersion: 2 })).toThrow(/schemaVersion/)
    const { rng: _rng, ...withoutRng } = valid
    expect(() => parseSimulationSnapshot(withoutRng)).toThrow(/contain exactly/)
  })
})

describe('fixed-step simulation', () => {
  it('uses the 60 Hz technical invariant and enforces the next target tick', () => {
    expect(SIMULATION_TICKS_PER_SECOND).toBe(60)
    const source = initialSnapshot()
    expect(() => stepSimulation(source, createActionFrame(2, []))).toThrow(/next simulation tick 1/)
  })

  it('does not mutate the snapshot or ActionFrame inputs', () => {
    const source = initialSnapshot()
    const frame = createActionFrame(1, [
      { participantId: 'local-1', actionId: 'diagnostic.signal', value: 1 },
    ])
    const sourceBefore = serializeSimulationSnapshot(source)
    const frameBefore = JSON.stringify(frame)

    const next = stepSimulation(source, frame)

    expect(serializeSimulationSnapshot(source)).toBe(sourceBefore)
    expect(JSON.stringify(frame)).toBe(frameBefore)
    expect(next).not.toBe(source)
    expect(next.entities[0]).not.toBe(source.entities[0])
  })

  it('produces an identical hash after every tick for the same inputs', () => {
    const frames = actionFrames(180)
    const first = runHeadlessSimulation(initialSnapshot(), frames)
    const second = runHeadlessSimulation(initialSnapshot(), frames)

    expect(first.tickHashes).toHaveLength(180)
    expect(first.tickHashes).toEqual(second.tickHashes)
    expect(first.finalSnapshot).toEqual(second.finalSnapshot)
    expect(first.finalSnapshot.tick).toBe(180)
  })

  it('is independent from how often a renderer observes immutable snapshots', () => {
    const frames = actionFrames(120)
    const everyTick = new FixedStepHeadlessRunner(initialSnapshot())
    const sparseAndRepeated = new FixedStepHeadlessRunner(initialSnapshot())

    frames.forEach((frame, index) => {
      everyTick.step(frame)
      void simulationSnapshotHash(everyTick.snapshot)

      sparseAndRepeated.step(frame)
      if (index % 7 === 0) {
        void simulationSnapshotHash(sparseAndRepeated.snapshot)
        void simulationSnapshotHash(sparseAndRepeated.snapshot)
        void simulationSnapshotHash(sparseAndRepeated.snapshot)
      }
    })

    expect(sparseAndRepeated.tickHashes).toEqual(everyTick.tickHashes)
    expect(simulationSnapshotHash(sparseAndRepeated.snapshot)).toBe(
      simulationSnapshotHash(everyTick.snapshot),
    )
  })
})

describe('SimulationReplay', () => {
  it('round-trips and reaches the same final tick and state hash', () => {
    const initial = initialSnapshot()
    const frames = actionFrames(90)
    const replay = createSimulationReplay(initial, frames)
    const restored = parseSimulationReplay(serializeSimulationReplay(replay))
    const direct = runHeadlessSimulation(initial, frames)
    const replayed = runSimulationReplay(restored)

    expect(restored).toEqual(replay)
    expect(replayed.finalSnapshot.tick).toBe(90)
    expect(replayed.tickHashes).toEqual(direct.tickHashes)
    expect(simulationSnapshotHash(replayed.finalSnapshot)).toBe(
      simulationSnapshotHash(direct.finalSnapshot),
    )
  })

  it('rejects gaps and out-of-order replay frames', () => {
    expect(() =>
      createSimulationReplay(initialSnapshot(), [
        createActionFrame(1, []),
        createActionFrame(3, []),
      ]),
    ).toThrow(/expected tick 2/)
  })
})
