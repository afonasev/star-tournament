import { canonicalSerialize, parseCanonicalJson } from '../core/canonical'
import { deepFreeze } from '../core/deepFreeze'
import { parseActionFrame, type ActionFrame } from '../input/actionFrame'
import { runHeadlessSimulation, type HeadlessRunResult } from './headlessRunner'
import { parseSimulationSnapshot, type SimulationSnapshot } from './snapshot'

export const SIMULATION_REPLAY_SCHEMA_VERSION = 1 as const

export interface SimulationReplay {
  readonly schemaVersion: typeof SIMULATION_REPLAY_SCHEMA_VERSION
  readonly initialSnapshot: SimulationSnapshot
  readonly frames: readonly ActionFrame[]
}

function expectRecord(value: unknown, path: string): Record<string, unknown> {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    throw new TypeError(`${path} must be an object`)
  }
  return value as Record<string, unknown>
}

function validateContiguousFrames(
  initialSnapshot: SimulationSnapshot,
  frames: readonly ActionFrame[],
): void {
  frames.forEach((frame, index) => {
    const expectedTick = initialSnapshot.tick + index + 1
    if (frame.tick !== expectedTick) {
      throw new RangeError(
        `SimulationReplay.frames[${index}].tick ${frame.tick} does not match expected tick ${expectedTick}`,
      )
    }
  })
}

export function createSimulationReplay(
  initialSnapshot: SimulationSnapshot,
  frames: readonly ActionFrame[],
): SimulationReplay {
  const parsedInitialSnapshot = parseSimulationSnapshot(initialSnapshot)
  const parsedFrames = frames.map((frame) => parseActionFrame(frame))
  validateContiguousFrames(parsedInitialSnapshot, parsedFrames)
  return deepFreeze({
    schemaVersion: SIMULATION_REPLAY_SCHEMA_VERSION,
    initialSnapshot: parsedInitialSnapshot,
    frames: parsedFrames,
  })
}

export function parseSimulationReplay(input: string | unknown): SimulationReplay {
  const value = typeof input === 'string' ? parseCanonicalJson(input) : input
  canonicalSerialize(value)
  const record = expectRecord(value, 'SimulationReplay')
  const keys = Object.keys(record).sort()
  const expectedKeys = ['frames', 'initialSnapshot', 'schemaVersion']
  if (keys.length !== expectedKeys.length || keys.some((key, index) => key !== expectedKeys[index])) {
    throw new TypeError(`SimulationReplay must contain exactly: ${expectedKeys.join(', ')}`)
  }
  if (record.schemaVersion !== SIMULATION_REPLAY_SCHEMA_VERSION) {
    throw new TypeError(
      `Unsupported SimulationReplay.schemaVersion: ${String(record.schemaVersion)}`,
    )
  }
  if (!Array.isArray(record.frames)) {
    throw new TypeError('SimulationReplay.frames must be an array')
  }
  return createSimulationReplay(
    parseSimulationSnapshot(record.initialSnapshot),
    record.frames.map((frame) => parseActionFrame(frame)),
  )
}

export function serializeSimulationReplay(replay: SimulationReplay): string {
  return canonicalSerialize(parseSimulationReplay(replay))
}

export function runSimulationReplay(replay: SimulationReplay): HeadlessRunResult {
  const parsed = parseSimulationReplay(replay)
  return runHeadlessSimulation(parsed.initialSnapshot, parsed.frames)
}
