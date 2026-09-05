import { simulationMath } from '../core/simulationMath'
import type { CollisionCapsuleMovementResult, CollisionVector3 } from '../collision/contracts'
import { deepFreeze } from '../core/deepFreeze'

export interface PlayerMovementState {
  readonly position: CollisionVector3
  readonly velocity: CollisionVector3
  readonly grounded: boolean
  readonly yaw: number
  readonly pitch: number
}

export interface PlayerMovementInput {
  readonly forward: number
  readonly right: number
  readonly lookYaw: number
  readonly lookPitch: number
  readonly jumpPressed: boolean
}

export interface PlayerMovementTuning {
  readonly maximumGroundSpeed: number
  readonly groundAcceleration: number
  readonly groundDeceleration: number
  readonly airAcceleration: number
  readonly gravity: number
  readonly jumpSpeed: number
  readonly horizontalSensitivityDegrees: number
  readonly verticalSensitivityDegrees: number
  readonly maximumPitchDegrees: number
}

export interface MovementIntent {
  readonly desiredTranslation: CollisionVector3
  readonly proposedVelocity: CollisionVector3
  readonly yaw: number
  readonly pitch: number
}

const DEGREES_TO_RADIANS = Math.PI / 180

function assertFinite(value: number, path: string): void {
  if (!Number.isFinite(value)) throw new TypeError(`${path} must be finite`)
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.max(minimum, Math.min(maximum, value))
}

function moveTowards(current: number, target: number, maximumDelta: number): number {
  const delta = target - current
  if (Math.abs(delta) <= maximumDelta) return target
  return current + Math.sign(delta) * maximumDelta
}

function normalizeYaw(value: number): number {
  const fullTurn = Math.PI * 2
  const normalized = ((value + Math.PI) % fullTurn + fullTurn) % fullTurn - Math.PI
  return Object.is(normalized, -0) ? 0 : normalized
}

export function horizontalWishDirection(
  yaw: number,
  forward: number,
  right: number,
): CollisionVector3 {
  assertFinite(yaw, 'yaw')
  assertFinite(forward, 'forward')
  assertFinite(right, 'right')
  const clampedForward = clamp(forward, -1, 1)
  const clampedRight = clamp(right, -1, 1)
  const localLength = simulationMath.hypot(clampedForward, clampedRight)
  if (localLength === 0) return deepFreeze({ x: 0, y: 0, z: 0 })
  const scale = localLength > 1 ? 1 / localLength : 1
  const forwardX = simulationMath.sin(yaw)
  const forwardZ = -simulationMath.cos(yaw)
  const rightX = simulationMath.cos(yaw)
  const rightZ = simulationMath.sin(yaw)
  return deepFreeze({
    x: (forwardX * clampedForward + rightX * clampedRight) * scale,
    y: 0,
    z: (forwardZ * clampedForward + rightZ * clampedRight) * scale,
  })
}

export function buildMovementIntent(
  state: PlayerMovementState,
  input: PlayerMovementInput,
  tuning: PlayerMovementTuning,
  fixedDeltaSeconds: number,
): MovementIntent {
  if (!Number.isFinite(fixedDeltaSeconds) || fixedDeltaSeconds <= 0) {
    throw new RangeError('fixedDeltaSeconds must be greater than zero')
  }
  for (const [path, value] of Object.entries(tuning)) {
    if (!Number.isFinite(value) || value < 0) throw new RangeError(`${path} must be non-negative`)
  }

  const yaw = normalizeYaw(
    state.yaw + input.lookYaw * tuning.horizontalSensitivityDegrees * DEGREES_TO_RADIANS,
  )
  const maximumPitch = tuning.maximumPitchDegrees * DEGREES_TO_RADIANS
  const pitch = clamp(
    state.pitch + input.lookPitch * tuning.verticalSensitivityDegrees * DEGREES_TO_RADIANS,
    -maximumPitch,
    maximumPitch,
  )
  const wish = horizontalWishDirection(yaw, input.forward, input.right)
  const hasWish = wish.x !== 0 || wish.z !== 0
  let horizontalX = state.velocity.x
  let horizontalZ = state.velocity.z

  if (hasWish) {
    const targetX = wish.x * tuning.maximumGroundSpeed
    const targetZ = wish.z * tuning.maximumGroundSpeed
    const acceleration = state.grounded ? tuning.groundAcceleration : tuning.airAcceleration
    const maximumDelta = acceleration * fixedDeltaSeconds
    const deltaX = targetX - horizontalX
    const deltaZ = targetZ - horizontalZ
    const deltaLength = simulationMath.hypot(deltaX, deltaZ)
    const scale = deltaLength > maximumDelta && deltaLength > 0 ? maximumDelta / deltaLength : 1
    horizontalX += deltaX * scale
    horizontalZ += deltaZ * scale
  } else if (state.grounded) {
    const speed = simulationMath.hypot(horizontalX, horizontalZ)
    const nextSpeed = moveTowards(speed, 0, tuning.groundDeceleration * fixedDeltaSeconds)
    const scale = speed === 0 ? 0 : nextSpeed / speed
    horizontalX *= scale
    horizontalZ *= scale
  }

  const horizontalSpeed = simulationMath.hypot(horizontalX, horizontalZ)
  if (horizontalSpeed > tuning.maximumGroundSpeed && horizontalSpeed > 0) {
    const scale = tuning.maximumGroundSpeed / horizontalSpeed
    horizontalX *= scale
    horizontalZ *= scale
  }

  let vertical = state.velocity.y
  if (input.jumpPressed && state.grounded) vertical = tuning.jumpSpeed
  vertical -= tuning.gravity * fixedDeltaSeconds
  const proposedVelocity = deepFreeze({ x: horizontalX, y: vertical, z: horizontalZ })
  return deepFreeze({
    desiredTranslation: {
      x: proposedVelocity.x * fixedDeltaSeconds,
      y: proposedVelocity.y * fixedDeltaSeconds,
      z: proposedVelocity.z * fixedDeltaSeconds,
    },
    proposedVelocity,
    yaw,
    pitch,
  })
}

export function applyCollisionMovement(
  previous: PlayerMovementState,
  intent: MovementIntent,
  collision: CollisionCapsuleMovementResult,
  fixedDeltaSeconds: number,
): PlayerMovementState {
  if (!Number.isFinite(fixedDeltaSeconds) || fixedDeltaSeconds <= 0) {
    throw new RangeError('fixedDeltaSeconds must be greater than zero')
  }
  return deepFreeze({
    position: collision.position,
    velocity: {
      x: (collision.position.x - previous.position.x) / fixedDeltaSeconds,
      y: (collision.position.y - previous.position.y) / fixedDeltaSeconds,
      z: (collision.position.z - previous.position.z) / fixedDeltaSeconds,
    },
    grounded: collision.grounded,
    yaw: intent.yaw,
    pitch: intent.pitch,
  })
}
