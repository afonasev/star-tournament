import { canonicalSerialize, parseCanonicalJson } from '../core/canonical'
import { deepFreeze } from '../core/deepFreeze'
import { parseActionFrame, type ActionFrame } from '../input/actionFrame'
import {
  parseSimulationSnapshot,
  simulationSnapshotHash,
  type SimulationSnapshot,
} from './playableSnapshot'
import type { ContentHash } from '../core/canonical'

export const PLAYABLE_SIMULATION_REPLAY_SCHEMA_VERSION = 2 as const

export interface PlayableSimulationReplay {
  readonly schemaVersion: typeof PLAYABLE_SIMULATION_REPLAY_SCHEMA_VERSION
  readonly initialSnapshot: SimulationSnapshot
  readonly frames: readonly ActionFrame[]
}

export interface PlayableHeadlessRunResult {
  readonly finalSnapshot: SimulationSnapshot
  readonly tickHashes: readonly ContentHash[]
}

export type PlayableSimulationStep = (
  snapshot: SimulationSnapshot,
  frame: ActionFrame,
) => SimulationSnapshot

function record(value: unknown, path: string): Record<string, unknown> {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    throw new TypeError(`${path} must be an object`)
  }
  return value as Record<string, unknown>
}

function validateFrames(initialSnapshot: SimulationSnapshot, frames: readonly ActionFrame[]): void {
  frames.forEach((frame, index) => {
    const expectedTick = initialSnapshot.tick + index + 1
    if (frame.tick !== expectedTick) {
      throw new RangeError(
        `PlayableSimulationReplay.frames[${index}].tick ${frame.tick} does not match expected tick ${expectedTick}`,
      )
    }
  })
}

export function createPlayableSimulationReplay(
  initialSnapshot: SimulationSnapshot,
  frames: readonly ActionFrame[],
): PlayableSimulationReplay {
  const parsedSnapshot = parseSimulationSnapshot(initialSnapshot)
  const parsedFrames = frames.map((frame) => parseActionFrame(frame))
  validateFrames(parsedSnapshot, parsedFrames)
  return deepFreeze({
    schemaVersion: PLAYABLE_SIMULATION_REPLAY_SCHEMA_VERSION,
    initialSnapshot: parsedSnapshot,
    frames: parsedFrames,
  })
}

export function parsePlayableSimulationReplay(input: string | unknown): PlayableSimulationReplay {
  const value = typeof input === 'string' ? parseCanonicalJson(input) : input
  canonicalSerialize(value)
  const data = record(value, 'PlayableSimulationReplay')
  const actualKeys = Object.keys(data).sort()
  const expectedKeys = ['frames', 'initialSnapshot', 'schemaVersion']
  if (
    actualKeys.length !== expectedKeys.length ||
    actualKeys.some((key, index) => key !== expectedKeys[index])
  ) {
    throw new TypeError(`PlayableSimulationReplay must contain exactly: ${expectedKeys.join(', ')}`)
  }
  if (data.schemaVersion !== PLAYABLE_SIMULATION_REPLAY_SCHEMA_VERSION) {
    throw new TypeError(
      `Unsupported PlayableSimulationReplay.schemaVersion: ${String(data.schemaVersion)}`,
    )
  }
  if (!Array.isArray(data.frames)) {
    throw new TypeError('PlayableSimulationReplay.frames must be an array')
  }
  return createPlayableSimulationReplay(
    parseSimulationSnapshot(data.initialSnapshot),
    data.frames.map((frame) => parseActionFrame(frame)),
  )
}

export function serializePlayableSimulationReplay(replay: PlayableSimulationReplay): string {
  return canonicalSerialize(parsePlayableSimulationReplay(replay))
}

export class PlayableFixedStepHeadlessRunner {
  private currentSnapshot: SimulationSnapshot
  private readonly hashes: ContentHash[] = []

  constructor(initialSnapshot: SimulationSnapshot, private readonly stepper: PlayableSimulationStep) {
    this.currentSnapshot = parseSimulationSnapshot(initialSnapshot)
  }

  get snapshot(): SimulationSnapshot {
    return this.currentSnapshot
  }

  get tickHashes(): readonly ContentHash[] {
    return deepFreeze([...this.hashes])
  }

  step(frame: ActionFrame): SimulationSnapshot {
    const normalizedFrame = parseActionFrame(frame)
    const expectedTick = this.currentSnapshot.tick + 1
    if (normalizedFrame.tick !== expectedTick) {
      throw new RangeError(`ActionFrame.tick ${normalizedFrame.tick} does not match next simulation tick ${expectedTick}`)
    }
    this.currentSnapshot = parseSimulationSnapshot(this.stepper(this.currentSnapshot, normalizedFrame))
    if (this.currentSnapshot.tick !== expectedTick) {
      throw new RangeError(`Playable step must return simulation tick ${expectedTick}`)
    }
    this.hashes.push(simulationSnapshotHash(this.currentSnapshot))
    return this.currentSnapshot
  }

  run(frames: readonly ActionFrame[]): PlayableHeadlessRunResult {
    for (const frame of frames) this.step(frame)
    return deepFreeze({ finalSnapshot: this.currentSnapshot, tickHashes: [...this.hashes] })
  }
}

export function runPlayableSimulationReplay(
  replay: PlayableSimulationReplay,
  stepper: PlayableSimulationStep,
): PlayableHeadlessRunResult {
  const parsed = parsePlayableSimulationReplay(replay)
  return new PlayableFixedStepHeadlessRunner(parsed.initialSnapshot, stepper).run(parsed.frames)
}
