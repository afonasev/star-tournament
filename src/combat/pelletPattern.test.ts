import { describe, expect, it } from 'vitest'

import {
  firstPersonBasisFromYawPitch,
  generateShotgunPelletDirections,
} from './pelletPattern'

describe('deterministic shotgun pellet pattern', () => {
  const basis = firstPersonBasisFromYawPitch(0, 0)
  const seed = { lifeId: 'life-1', shotSequence: 1, tick: 42 } as const

  it('builds the accepted first-person coordinate basis', () => {
    expect(basis.forward).toEqual({ x: 0, y: 0, z: -1 })
    expect(basis.right).toEqual({ x: 1, y: 0, z: 0 })
    expect(basis.up).toEqual({ x: 0, y: 1, z: 0 })
  })

  it('returns the requested ordered count of normalized directions inside spread', () => {
    const pellets = generateShotgunPelletDirections(seed, basis, 12, 6)
    expect(pellets).toHaveLength(12)
    expect(pellets.map((pellet) => pellet.pelletIndex)).toEqual([...Array(12).keys()])
    for (const pellet of pellets) {
      expect(Math.hypot(pellet.direction.x, pellet.direction.y, pellet.direction.z)).toBeCloseTo(1)
      const angle = Math.acos(-pellet.direction.z) * 180 / Math.PI
      expect(angle).toBeLessThanOrEqual(6 + 1e-10)
    }
    expect(Object.isFrozen(pellets)).toBe(true)
  })

  it('is identical for the same identity and changes with shot sequence', () => {
    const first = generateShotgunPelletDirections(seed, basis, 10, 5)
    const repeated = generateShotgunPelletDirections(seed, basis, 10, 5)
    const nextShot = generateShotgunPelletDirections({ ...seed, shotSequence: 2 }, basis, 10, 5)
    expect(repeated).toEqual(first)
    expect(nextShot).not.toEqual(first)
  })

  it('collapses to the forward ray at zero spread', () => {
    const pellets = generateShotgunPelletDirections(seed, basis, 3, 0)
    expect(pellets.map((pellet) => pellet.direction)).toEqual([
      basis.forward,
      basis.forward,
      basis.forward,
    ])
  })
})
