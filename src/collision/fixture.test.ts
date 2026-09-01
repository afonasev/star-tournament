import { describe, expect, it } from 'vitest'

import { validateArenaDefinition } from '../arena/arenaDefinition'
import { COLLISION_FIXTURE_ARENA, COLLISION_FIXTURE_ID } from './fixture'

describe('collision-fixture-v1', () => {
  it('is valid, immutable, and has stable semantic geometry identity', () => {
    expect(validateArenaDefinition(COLLISION_FIXTURE_ARENA).ok).toBe(true)
    expect(Object.isFrozen(COLLISION_FIXTURE_ARENA)).toBe(true)
    expect(COLLISION_FIXTURE_ARENA.id).toBe(COLLISION_FIXTURE_ID)
    expect(COLLISION_FIXTURE_ARENA.contentHash).toBe('fnv1a64-v1:e539711a0942acbe')
    expect(COLLISION_FIXTURE_ARENA.surfaces.map((surface) => surface.id)).toEqual([
      'ceiling-low',
      'corner-north',
      'corner-west',
      'cover-occluder',
      'floor',
      'ledge-platform',
      'slope-climbable',
      'step-high',
      'step-low',
      'tie-a',
      'tie-b',
      'wall-slide',
    ])
  })

  it('contains clear and overlapping spawn anchors', () => {
    expect(COLLISION_FIXTURE_ARENA.spawnAnchors.map((spawn) => spawn.id)).toEqual([
      'spawn-clear',
      'spawn-overlap',
    ])
  })
})
