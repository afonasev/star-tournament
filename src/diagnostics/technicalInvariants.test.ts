import { describe, expect, it } from 'vitest'

import { GAME_DESIGN_PROFILE_DESCRIPTORS } from '../profiles/gameDesignProfile'
import { TECHNICAL_INVARIANTS } from './technicalInvariants'

describe('технические инварианты runtime', () => {
  it('имеют обоснование и не представлены редактируемыми descriptors', () => {
    const descriptorPaths = new Set(
      GAME_DESIGN_PROFILE_DESCRIPTORS.map((descriptor) => descriptor.path),
    )

    expect(TECHNICAL_INVARIANTS).not.toHaveLength(0)
    expect(new Set(TECHNICAL_INVARIANTS.map((invariant) => invariant.id)).size).toBe(
      TECHNICAL_INVARIANTS.length,
    )
    for (const invariant of TECHNICAL_INVARIANTS) {
      expect(invariant.id).not.toHaveLength(0)
      expect(invariant.rationale).not.toHaveLength(0)
      expect(descriptorPaths.has(invariant.id)).toBe(false)
    }
  })
})
