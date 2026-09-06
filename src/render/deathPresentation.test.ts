import { describe, expect, it } from 'vitest'

import { DEATH_REST_SECONDS, resolveDeathFallProgress, resolveDeathSlideOffset } from './deathPresentation'

describe('death presentation transform', () => {
  it('moves from contact to the final event-derived direction without accumulating drift', () => {
    expect(resolveDeathFallProgress(0)).toBe(0)
    const middle = resolveDeathSlideOffset(.275, Math.PI / 2, .65)
    expect(middle.progress).toBeCloseTo(.5); expect(middle.x).toBeCloseTo(.325); expect(middle.z).toBeCloseTo(0)
    const final = resolveDeathSlideOffset(DEATH_REST_SECONDS, Math.PI / 2, .65)
    expect(final.progress).toBe(1); expect(final.x).toBeCloseTo(.65); expect(final.z).toBeCloseTo(0)
    expect(resolveDeathSlideOffset(DEATH_REST_SECONDS + 5, Math.PI / 2, .65)).toEqual(final)
  })

  it('supports zero and maximum descriptor distances', () => {
    expect(resolveDeathSlideOffset(DEATH_REST_SECONDS, 0, 0)).toMatchObject({ x: 0, z: 0 })
    expect(resolveDeathSlideOffset(DEATH_REST_SECONDS, 0, 1.5)).toMatchObject({ x: 0, z: 1.5 })
  })
})
