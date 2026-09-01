import { contentHash } from '../core/canonical'
import { parseActionFrame, type ActionFrame } from '../input/actionFrame'
import { nextRandomUint32 } from './rng'
import { parseSimulationSnapshot, type SimulationSnapshot } from './snapshot'

// Tick rate defines snapshot, replay and future network compatibility, so it
// is audited as a technical invariant rather than exposed in Game Design Lab.
export const SIMULATION_TICKS_PER_SECOND = 60 as const
export const SIMULATION_FIXED_DELTA_SECONDS = 1 / SIMULATION_TICKS_PER_SECOND

/** Advances the data-only simulation by exactly one fixed tick without mutating either input. */
export function stepSimulation(snapshot: SimulationSnapshot, frame: ActionFrame): SimulationSnapshot {
  const current = parseSimulationSnapshot(snapshot)
  const normalizedFrame = parseActionFrame(frame)
  const expectedTick = current.tick + 1
  if (normalizedFrame.tick !== expectedTick) {
    throw new RangeError(
      `ActionFrame.tick ${normalizedFrame.tick} does not match next simulation tick ${expectedTick}`,
    )
  }

  const sample = nextRandomUint32(current.rng)
  const actionTotal = normalizedFrame.actions.reduce((total, action) => total + action.value, 0)
  if (!Number.isFinite(actionTotal)) {
    throw new RangeError('ActionFrame values overflowed the finite simulation range')
  }

  const previousEntity = current.entities[0]
  const accumulatedInput = previousEntity.accumulatedInput + actionTotal
  const nextX = previousEntity.position.x + actionTotal * SIMULATION_FIXED_DELTA_SECONDS
  if (!Number.isFinite(accumulatedInput) || !Number.isFinite(nextX)) {
    throw new RangeError('Diagnostic simulation state overflowed the finite range')
  }

  return parseSimulationSnapshot({
    ...current,
    tick: expectedTick,
    rng: sample.state,
    entities: [
      {
        ...previousEntity,
        position: {
          ...previousEntity.position,
          x: nextX,
          z: sample.value / 0xffffffff - 0.5,
        },
        accumulatedInput,
        lastActionHash: contentHash(normalizedFrame.actions),
        lastRandomUint32: sample.value,
      },
    ],
  })
}
