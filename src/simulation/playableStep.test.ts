import { describe, expect, it } from 'vitest'

import { contentHash } from '../core/canonical'
import { createActionFrame } from '../input/actionFrame'
import { PROTOTYPE_V1_PROFILE } from '../profiles'
import { DEFAULT_MATCH_CONFIGURATION, createMatchConfiguration } from '../match'
import { createInitialSimulationSnapshot, localParticipant, parseSimulationSnapshot } from './playableSnapshot'
import {
  livingParticipantCollisionCapsules,
  stepPlayableSimulation,
  type PlayableCollisionPort,
} from './playableStep'

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
      expect(capsules).toHaveLength(4)
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
  it('applies one ordered frame to two local seats without crossing their actions', () => {
    const { contentHash: _contentHash, ...baseConfiguration } = DEFAULT_MATCH_CONFIGURATION
    const configuration = createMatchConfiguration({
      ...baseConfiguration,
      participants: [
        { semanticId: 'local-a', name: 'A', kind: 'local-seat', color: '#3b82f6', team: null, deviceBinding: { kind: 'keyboard-mouse' } },
        { semanticId: 'local-b', name: 'B', kind: 'local-seat', color: '#f59e0b', team: null, deviceBinding: { kind: 'gamepad', gamepadId: 'Pad', connectionSlot: 0 } },
        { semanticId: 'robot-center', name: 'Fixture', kind: 'stationary-fixture', color: '#22c55e', team: null },
        { semanticId: 'robot-left', name: 'Fixture B', kind: 'stationary-fixture', color: '#a855f7', team: null },
      ],
    })
    const initial = createInitialSimulationSnapshot({ rngSeed: 1, arena, configuration })
    const collision = createCollision()
    const next = stepPlayableSimulation(initial, createActionFrame(1, [
      { participantId: 'local-a', actionId: 'move-forward', value: 1 },
      { participantId: 'local-b', actionId: 'move-right', value: 1 },
    ]), collision, PROTOTYPE_V1_PROFILE, configuration)
    expect(next.tick).toBe(1)
    expect(next.participants.find(({ semanticId }) => semanticId === 'local-a')?.position.z).toBeLessThan(6)
    expect(next.participants.find(({ semanticId }) => semanticId === 'local-b')?.position.x).toBeGreaterThan(0)
  })
  it('projects only living roster capsules and converts fixture feet anchors to capsule centers', () => {
    const initial = createInitialSimulationSnapshot({ rngSeed: 1, arena })
    const deadFixture = initial.participants.find(({ kind }) => kind === 'stationary-fixture')
    if (deadFixture === undefined) throw new Error('Missing stationary fixture')
    const snapshot = parseSimulationSnapshot({
      ...initial,
      participants: initial.participants.map((participant) => participant.semanticId === deadFixture.semanticId
        ? { ...participant, health: 0, alive: false, deathTick: 0, respawnTick: null }
        : participant),
    })

    const capsules = livingParticipantCollisionCapsules(snapshot, PROTOTYPE_V1_PROFILE)
    expect(capsules.map(({ semanticId }) => semanticId)).not.toContain(deadFixture.semanticId)
    expect(capsules).toHaveLength(3)
    const livingFixture = snapshot.participants.find((participant) =>
      participant.kind === 'stationary-fixture' && participant.alive
    )
    const fixtureCapsule = capsules.find(({ semanticId }) => semanticId === livingFixture?.semanticId)
    expect(fixtureCapsule?.position.y).toBe(
      (livingFixture?.position.y ?? 0) +
      PROTOTYPE_V1_PROFILE.rules.player.capsule.halfHeight +
      PROTOTYPE_V1_PROFILE.rules.player.capsule.radius,
    )
  })

  it('projects the authoritative capsule, moves, looks, and advances one tick', () => {
    const initial = createInitialSimulationSnapshot({ rngSeed: 1, arena })
    const initialPlayer = localParticipant(initial)
    const collision = createCollision()
    const next = stepPlayableSimulation(initial, createActionFrame(1, [
      { participantId: initialPlayer.semanticId, actionId: 'move-forward', value: 1 },
      { participantId: initialPlayer.semanticId, actionId: 'look-yaw', value: 0.5 },
    ]), collision, PROTOTYPE_V1_PROFILE)
    expect(collision.projected).toBe(3)
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
