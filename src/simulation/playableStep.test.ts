import { describe, expect, it } from 'vitest'

import { contentHash } from '../core/canonical'
import { createActionFrame } from '../input/actionFrame'
import { PROTOTYPE_V1_PROFILE } from '../profiles'
import { createInitialSimulationSnapshot, localParticipant } from './playableSnapshot'
import { stepPlayableSimulation, type PlayableCollisionPort } from './playableStep'

const arena = {
  id: 'arena',
  seed: 1,
  generatorVersion: 'test-v1',
  contentHash: contentHash({ arena: 1 }),
} as const

function createCollision(): PlayableCollisionPort & { projected: number } {
  return {
    projected: 0,
    projectCapsules(capsules) {
      this.projected += 1
      expect(capsules).toHaveLength(1)
    },
    moveCapsule(_semanticId, translation) {
      return {
        position: {
          x: translation.x,
          y: PROTOTYPE_V1_PROFILE.rules.player.capsule.halfHeight +
            PROTOTYPE_V1_PROFILE.rules.player.capsule.radius,
          z: 6 + translation.z,
        },
        grounded: true,
        contacts: [],
      }
    },
    raycast: () => [],
  }
}

describe('playable simulation step', () => {
  it('projects the authoritative capsule, moves, looks, and advances one tick', () => {
    const initial = createInitialSimulationSnapshot({ rngSeed: 1, arena })
    const initialPlayer = localParticipant(initial)
    const collision = createCollision()
    const next = stepPlayableSimulation(initial, createActionFrame(1, [
      { participantId: initialPlayer.semanticId, actionId: 'move-forward', value: 1 },
      { participantId: initialPlayer.semanticId, actionId: 'look-yaw', value: 0.5 },
    ]), collision, PROTOTYPE_V1_PROFILE)
    expect(collision.projected).toBe(1)
    expect(next.tick).toBe(1)
    expect(localParticipant(next).position.z).toBeLessThan(initialPlayer.position.z)
    expect(localParticipant(next).yaw).toBeGreaterThan(0)
    expect(next.events).toEqual([])
    expect(Object.isFrozen(next)).toBe(true)
  })

  it('uses jump and fire rising edges stored in the snapshot', () => {
    const initial = createInitialSimulationSnapshot({ rngSeed: 2, arena })
    const participantId = localParticipant(initial).semanticId
    const collision = createCollision()
    const pressed = stepPlayableSimulation(initial, createActionFrame(1, [
      { participantId, actionId: 'jump', value: 1 },
      { participantId, actionId: 'fire', value: 1 },
    ]), collision, PROTOTYPE_V1_PROFILE)
    expect(localParticipant(pressed).previousJumpPressed).toBe(true)
    expect(localParticipant(pressed).weapon.ammo).toBe(19)
    expect(pressed.events.some((event) => event.kind === 'shotgun-shot')).toBe(true)
    const held = stepPlayableSimulation(pressed, createActionFrame(2, [
      { participantId, actionId: 'jump', value: 1 },
      { participantId, actionId: 'fire', value: 1 },
    ]), collision, PROTOTYPE_V1_PROFILE)
    expect(localParticipant(held).weapon.ammo).toBe(19)
    expect(held.events).toEqual([])
  })

  it('rejects non-contiguous ticks and mismatched profile identity before stepping', () => {
    const initial = createInitialSimulationSnapshot({ rngSeed: 3, arena })
    const collision = createCollision()
    expect(() => stepPlayableSimulation(
      initial,
      createActionFrame(2, []),
      collision,
      PROTOTYPE_V1_PROFILE,
    )).toThrow(/next simulation tick/)
    expect(collision.projected).toBe(0)
  })
})
