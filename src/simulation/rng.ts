import { deepFreeze } from '../core/deepFreeze'

export const RNG_ALGORITHM = 'xorshift32-v1' as const

// Zero is an absorbing xorshift32 state, so seed zero maps to this fixed technical fallback.
const ZERO_SEED_FALLBACK = 0x6d2b79f5

export interface SeededRngState {
  readonly algorithm: typeof RNG_ALGORITHM
  readonly state: number
}

export interface RngSample {
  readonly state: SeededRngState
  readonly value: number
}

function assertUint32(value: unknown, path: string): asserts value is number {
  if (!Number.isInteger(value) || (value as number) < 0 || (value as number) > 0xffffffff) {
    throw new RangeError(`${path} must be an unsigned 32-bit integer`)
  }
}

export function createSeededRngState(seed: number): SeededRngState {
  if (!Number.isSafeInteger(seed)) {
    throw new RangeError('RNG seed must be a safe integer')
  }
  const normalized = seed >>> 0
  return deepFreeze({
    algorithm: RNG_ALGORITHM,
    state: normalized === 0 ? ZERO_SEED_FALLBACK : normalized,
  })
}

export function parseSeededRngState(value: unknown): SeededRngState {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    throw new TypeError('SimulationSnapshot.rng must be an object')
  }
  const record = value as Record<string, unknown>
  const keys = Object.keys(record).sort()
  if (keys.length !== 2 || keys[0] !== 'algorithm' || keys[1] !== 'state') {
    throw new TypeError('SimulationSnapshot.rng must contain exactly: algorithm, state')
  }
  if (record.algorithm !== RNG_ALGORITHM) {
    throw new TypeError(`Unsupported RNG algorithm: ${String(record.algorithm)}`)
  }
  assertUint32(record.state, 'SimulationSnapshot.rng.state')
  if (record.state === 0) {
    throw new RangeError('SimulationSnapshot.rng.state must not be zero')
  }
  return deepFreeze({ algorithm: RNG_ALGORITHM, state: record.state })
}

export function nextRandomUint32(current: SeededRngState): RngSample {
  const validated = parseSeededRngState(current)
  let next = validated.state
  next ^= next << 13
  next ^= next >>> 17
  next ^= next << 5
  next >>>= 0

  const state = deepFreeze({ algorithm: RNG_ALGORITHM, state: next })
  return deepFreeze({ state, value: next })
}
