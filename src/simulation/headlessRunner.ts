import type { ContentHash } from '../core/canonical'
import { deepFreeze } from '../core/deepFreeze'
import type { ActionFrame } from '../input/actionFrame'
import {
  parseSimulationSnapshot,
  simulationSnapshotHash,
  type SimulationSnapshot,
} from './snapshot'
import { stepSimulation } from './step'

export interface HeadlessRunResult {
  readonly finalSnapshot: SimulationSnapshot
  readonly tickHashes: readonly ContentHash[]
}

/**
 * Synchronous fixed-step adapter used by tests, replay and future server tooling.
 * It has no wall-clock or render API, so only supplied ActionFrames can advance state.
 */
export class FixedStepHeadlessRunner {
  private currentSnapshot: SimulationSnapshot
  private readonly hashes: ContentHash[] = []

  constructor(initialSnapshot: SimulationSnapshot) {
    this.currentSnapshot = parseSimulationSnapshot(initialSnapshot)
  }

  get snapshot(): SimulationSnapshot {
    return this.currentSnapshot
  }

  get tickHashes(): readonly ContentHash[] {
    return deepFreeze([...this.hashes])
  }

  step(frame: ActionFrame): SimulationSnapshot {
    this.currentSnapshot = stepSimulation(this.currentSnapshot, frame)
    this.hashes.push(simulationSnapshotHash(this.currentSnapshot))
    return this.currentSnapshot
  }

  run(frames: readonly ActionFrame[]): HeadlessRunResult {
    for (const frame of frames) {
      this.step(frame)
    }
    return deepFreeze({ finalSnapshot: this.currentSnapshot, tickHashes: [...this.hashes] })
  }
}

export function runHeadlessSimulation(
  initialSnapshot: SimulationSnapshot,
  frames: readonly ActionFrame[],
): HeadlessRunResult {
  return new FixedStepHeadlessRunner(initialSnapshot).run(frames)
}
