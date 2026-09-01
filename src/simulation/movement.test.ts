import { describe, expect, it } from 'vitest'

import {
  applyCollisionMovement,
  buildMovementIntent,
  horizontalWishDirection,
  type PlayerMovementState,
} from './movement'

const tuning = {
  maximumGroundSpeed: 10,
  groundAcceleration: 20,
  groundDeceleration: 10,
  airAcceleration: 4,
  gravity: 18,
  jumpSpeed: 8,
  horizontalSensitivityDegrees: 90,
  verticalSensitivityDegrees: 90,
  maximumPitchDegrees: 80,
} as const

const grounded = {
  position: { x: 0, y: 1, z: 0 },
  velocity: { x: 0, y: 0, z: 0 },
  grounded: true,
  yaw: 0,
  pitch: 0,
} as const

const idleInput = {
  forward: 0,
  right: 0,
  lookYaw: 0,
  lookPitch: 0,
  jumpPressed: false,
} as const

describe('first-person movement math', () => {
  it('builds normalized yaw-relative wish directions', () => {
    expect(horizontalWishDirection(0, 1, 0)).toEqual({ x: 0, y: 0, z: -1 })
    expect(horizontalWishDirection(Math.PI / 2, 1, 0).x).toBeCloseTo(1)
    const diagonal = horizontalWishDirection(0, 1, 1)
    expect(Math.hypot(diagonal.x, diagonal.z)).toBeCloseTo(1)
  })

  it('accelerates, caps speed, and decelerates to rest on ground', () => {
    let state: PlayerMovementState = grounded
    for (let index = 0; index < 120; index += 1) {
      const intent = buildMovementIntent(state, { ...idleInput, forward: 1 }, tuning, 1 / 60)
      state = { ...state, velocity: intent.proposedVelocity }
    }
    expect(Math.hypot(state.velocity.x, state.velocity.z)).toBeCloseTo(tuning.maximumGroundSpeed)
    for (let index = 0; index < 120; index += 1) {
      const intent = buildMovementIntent(state, idleInput, tuning, 1 / 60)
      state = { ...state, velocity: intent.proposedVelocity }
    }
    expect(Math.hypot(state.velocity.x, state.velocity.z)).toBe(0)
  })

  it('applies weaker air control without exceeding the shared speed cap', () => {
    const groundIntent = buildMovementIntent(grounded, { ...idleInput, right: 1 }, tuning, 0.1)
    const airIntent = buildMovementIntent(
      { ...grounded, grounded: false },
      { ...idleInput, right: 1 },
      tuning,
      0.1,
    )
    expect(groundIntent.proposedVelocity.x).toBeGreaterThan(airIntent.proposedVelocity.x)
    expect(Math.hypot(airIntent.proposedVelocity.x, airIntent.proposedVelocity.z)).toBeLessThanOrEqual(10)
  })

  it('applies one jump impulse only when the supplied edge is grounded', () => {
    const jump = buildMovementIntent(grounded, { ...idleInput, jumpPressed: true }, tuning, 1 / 60)
    expect(jump.proposedVelocity.y).toBeGreaterThan(0)
    const heldInAir = buildMovementIntent(
      { ...grounded, grounded: false, velocity: jump.proposedVelocity },
      { ...idleInput, jumpPressed: true },
      tuning,
      1 / 60,
    )
    expect(heldInAir.proposedVelocity.y).toBeLessThan(jump.proposedVelocity.y)
  })

  it('wraps yaw and clamps pitch without frame-cadence dependence', () => {
    const once = buildMovementIntent(
      grounded,
      { ...idleInput, lookYaw: 1, lookPitch: 1 },
      tuning,
      1 / 30,
    )
    const twiceA = buildMovementIntent(
      grounded,
      { ...idleInput, lookYaw: 0.5, lookPitch: 0.5 },
      tuning,
      1 / 60,
    )
    const twiceB = buildMovementIntent(
      { ...grounded, yaw: twiceA.yaw, pitch: twiceA.pitch },
      { ...idleInput, lookYaw: 0.5, lookPitch: 0.5 },
      tuning,
      1 / 60,
    )
    expect(twiceB.yaw).toBeCloseTo(once.yaw)
    expect(twiceB.pitch).toBeCloseTo(once.pitch)
    const clamped = buildMovementIntent(
      grounded,
      { ...idleInput, lookPitch: 10 },
      tuning,
      1 / 60,
    )
    expect(clamped.pitch).toBeCloseTo(80 * Math.PI / 180)
  })

  it('derives velocity from collision-authorized displacement', () => {
    const intent = buildMovementIntent(grounded, { ...idleInput, forward: 1 }, tuning, 0.1)
    const resolved = applyCollisionMovement(
      grounded,
      intent,
      { position: { x: 0, y: 1, z: -0.1 }, grounded: true, contacts: [] },
      0.1,
    )
    expect(resolved.velocity).toEqual({ x: 0, y: 0, z: -1 })
    expect(resolved.grounded).toBe(true)
  })
})
