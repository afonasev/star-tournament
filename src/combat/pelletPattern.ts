import type { CollisionVector3 } from '../collision/contracts'
import { contentHash } from '../core/canonical'
import { deepFreeze } from '../core/deepFreeze'
import { createSeededRngState, nextRandomUint32 } from '../simulation/rng'

export const SHOTGUN_PELLET_PATTERN_VERSION = 'concentric-spread-v1' as const

export interface ShotSeedIdentity {
  readonly lifeId: string
  readonly shotSequence: number
  readonly tick: number
}

export interface FirstPersonBasis {
  readonly forward: CollisionVector3
  readonly right: CollisionVector3
  readonly up: CollisionVector3
}

export interface PelletDirection {
  readonly pelletIndex: number
  readonly direction: CollisionVector3
}

function normalize(vector: CollisionVector3): CollisionVector3 {
  const length = Math.hypot(vector.x, vector.y, vector.z)
  if (!Number.isFinite(length) || length === 0) throw new RangeError('Direction must be non-zero')
  const component = (value: number): number => {
    const normalized = value / length
    return Object.is(normalized, -0) ? 0 : normalized
  }
  return { x: component(vector.x), y: component(vector.y), z: component(vector.z) }
}

export function firstPersonBasisFromYawPitch(yaw: number, pitch: number): FirstPersonBasis {
  if (!Number.isFinite(yaw) || !Number.isFinite(pitch)) throw new TypeError('Yaw and pitch must be finite')
  const cosPitch = Math.cos(pitch)
  const forward = normalize({
    x: Math.sin(yaw) * cosPitch,
    y: Math.sin(pitch),
    z: -Math.cos(yaw) * cosPitch,
  })
  const right = normalize({ x: Math.cos(yaw), y: 0, z: Math.sin(yaw) })
  const up = normalize({
    x: -Math.sin(yaw) * Math.sin(pitch),
    y: Math.cos(pitch),
    z: Math.cos(yaw) * Math.sin(pitch),
  })
  return deepFreeze({ forward, right, up })
}

function seedFromIdentity(identity: ShotSeedIdentity): number {
  if (identity.lifeId.length === 0) throw new TypeError('lifeId must not be empty')
  if (!Number.isSafeInteger(identity.shotSequence) || identity.shotSequence < 1) {
    throw new RangeError('shotSequence must be a positive safe integer')
  }
  if (!Number.isSafeInteger(identity.tick) || identity.tick < 0) {
    throw new RangeError('tick must be a non-negative safe integer')
  }
  const hash = contentHash({ pattern: SHOTGUN_PELLET_PATTERN_VERSION, ...identity })
  return Number.parseInt(hash.slice(-8), 16) >>> 0
}

function concentricDisk(first: number, second: number): readonly [number, number] {
  const x = first * 2 - 1
  const y = second * 2 - 1
  if (x === 0 && y === 0) return [0, 0]
  if (Math.abs(x) > Math.abs(y)) {
    const radius = x
    const theta = Math.PI / 4 * (y / x)
    return [radius * Math.cos(theta), radius * Math.sin(theta)]
  }
  const radius = y
  const theta = Math.PI / 2 - Math.PI / 4 * (x / y)
  return [radius * Math.cos(theta), radius * Math.sin(theta)]
}

export function generateShotgunPelletDirections(
  identity: ShotSeedIdentity,
  basis: FirstPersonBasis,
  pelletCount: number,
  spreadDegrees: number,
): readonly PelletDirection[] {
  if (!Number.isSafeInteger(pelletCount) || pelletCount < 1) {
    throw new RangeError('pelletCount must be a positive safe integer')
  }
  if (!Number.isFinite(spreadDegrees) || spreadDegrees < 0 || spreadDegrees >= 90) {
    throw new RangeError('spreadDegrees must be between zero and 90')
  }
  const normalizedBasis = {
    forward: normalize(basis.forward),
    right: normalize(basis.right),
    up: normalize(basis.up),
  }
  const spreadScale = Math.tan(spreadDegrees * Math.PI / 180)
  let rng = createSeededRngState(seedFromIdentity(identity))
  const directions: PelletDirection[] = []
  for (let pelletIndex = 0; pelletIndex < pelletCount; pelletIndex += 1) {
    const first = nextRandomUint32(rng)
    rng = first.state
    const second = nextRandomUint32(rng)
    rng = second.state
    const [diskX, diskY] = concentricDisk(first.value / 0xffffffff, second.value / 0xffffffff)
    directions.push({
      pelletIndex,
      direction: normalize({
        x: normalizedBasis.forward.x + spreadScale * (
          normalizedBasis.right.x * diskX + normalizedBasis.up.x * diskY
        ),
        y: normalizedBasis.forward.y + spreadScale * (
          normalizedBasis.right.y * diskX + normalizedBasis.up.y * diskY
        ),
        z: normalizedBasis.forward.z + spreadScale * (
          normalizedBasis.right.z * diskX + normalizedBasis.up.z * diskY
        ),
      }),
    })
  }
  return deepFreeze(directions)
}
