import { describe, expect, it } from 'vitest'

import type { CollisionCapsuleProjection, CollisionVector3 } from '../collision/contracts'
import { COLLISION_FIXTURE_ARENA } from '../collision/fixture'
import {
  createRapierCollisionWorld,
  type CollisionControllerConfiguration,
  type RapierCollisionWorld,
} from '../collision/rapierWorld'
import type { ContentHash } from '../core/canonical'
import { createActionFrame, type CanonicalActionId } from '../input/actionFrame'
import { PROTOTYPE_V1_PROFILE } from '../profiles'
import {
  createInitialSimulationSnapshot,
  localParticipant,
  parseSimulationSnapshot,
  type SimulationSnapshot,
} from './playableSnapshot'
import { livingParticipantCollisionCapsules, stepPlayableSimulation } from './playableStep'

const profile = PROTOTYPE_V1_PROFILE

const controllerConfiguration: CollisionControllerConfiguration = {
  skinWidth: profile.rules.player.capsule.skinWidth,
  autostepMaxHeight: profile.rules.player.movement.autostepMaxHeight,
  autostepMinWidth: profile.rules.player.movement.autostepMinWidth,
  snapToGroundDistance: profile.rules.player.movement.snapToGroundDistance,
  maxSlopeClimbDegrees: profile.rules.player.movement.maxSlopeClimbDegrees,
  minSlopeSlideDegrees: profile.rules.player.movement.minSlopeSlideDegrees,
}

function initialAt(
  position: CollisionVector3,
  velocity: CollisionVector3 = { x: 0, y: 0, z: 0 },
  grounded = true,
): SimulationSnapshot {
  const initial = createInitialSimulationSnapshot({
    rngSeed: 0x51ce,
    arena: {
      id: COLLISION_FIXTURE_ARENA.id,
      seed: COLLISION_FIXTURE_ARENA.seed,
      generatorVersion: COLLISION_FIXTURE_ARENA.generatorVersion,
      contentHash: COLLISION_FIXTURE_ARENA.contentHash as ContentHash,
    },
  })
  return parseSimulationSnapshot({
    ...initial,
    participants: initial.participants.map((participant) => participant.kind === 'local-seat'
      ? { ...participant, position, velocity, grounded }
      : participant),
  })
}

function playerCapsule(snapshot: SimulationSnapshot): CollisionCapsuleProjection {
  const player = localParticipant(snapshot)
  return {
    semanticId: player.semanticId,
    position: player.position,
    radius: profile.rules.player.capsule.radius,
    halfHeight: profile.rules.player.capsule.halfHeight,
  }
}

async function createWorld(snapshot: SimulationSnapshot): Promise<RapierCollisionWorld> {
  return createRapierCollisionWorld(
    COLLISION_FIXTURE_ARENA,
    livingParticipantCollisionCapsules(snapshot, profile),
    controllerConfiguration,
  )
}

function step(
  snapshot: SimulationSnapshot,
  collision: RapierCollisionWorld,
  actions: readonly { actionId: CanonicalActionId; value: number }[] = [],
): SimulationSnapshot {
  return stepPlayableSimulation(
    snapshot,
    createActionFrame(
      snapshot.tick + 1,
      actions.map((action) => ({ participantId: localParticipant(snapshot).semanticId, ...action })),
    ),
    collision,
    profile,
  )
}

function run(
  snapshot: SimulationSnapshot,
  collision: RapierCollisionWorld,
  tickCount: number,
  actions: readonly { actionId: CanonicalActionId; value: number }[],
): SimulationSnapshot {
  let current = snapshot
  for (let index = 0; index < tickCount; index += 1) current = step(current, collision, actions)
  return current
}

describe('stepPlayableSimulation with RapierCollisionWorld', () => {
  it('blocks the local seat on a living fixture capsule but permits passage after its death', async () => {
    const participantSnapshot = (blockerAlive: boolean): SimulationSnapshot => {
      const base = initialAt({ x: -6, y: 0.91, z: -5 })
      return parseSimulationSnapshot({
        ...base,
        participants: base.participants.map((participant) => {
          if (participant.kind !== 'stationary-fixture') return participant
          if (participant.semanticId === 'robot-center') {
            return {
              ...participant,
              position: { x: -4, y: 0, z: -5 },
              health: blockerAlive ? participant.health : 0,
              alive: blockerAlive,
              deathTick: blockerAlive ? null : 0,
            }
          }
          return { ...participant, position: { x: 40, y: 0, z: 40 } }
        }),
      })
    }

    const living = participantSnapshot(true)
    const livingCollision = await createWorld(living)
    try {
      const blocked = run(living, livingCollision, 60, [{ actionId: 'move-right', value: 1 }])
      expect(localParticipant(blocked).position.x).toBeLessThan(-4.7)
    } finally {
      livingCollision.dispose()
    }

    const dead = participantSnapshot(false)
    const deadCollision = await createWorld(dead)
    try {
      const passed = run(dead, deadCollision, 60, [{ actionId: 'move-right', value: 1 }])
      expect(localParticipant(passed).position.x).toBeGreaterThan(-4)
    } finally {
      deadCollision.dispose()
    }
  })

  it('places the initial local-seat capsule clear of static geometry before any input tick', async () => {
    const initial = initialAt(localParticipant(createInitialSimulationSnapshot({
      rngSeed: 0x51ce,
      arena: {
        id: COLLISION_FIXTURE_ARENA.id,
        seed: COLLISION_FIXTURE_ARENA.seed,
        generatorVersion: COLLISION_FIXTURE_ARENA.generatorVersion,
        contentHash: COLLISION_FIXTURE_ARENA.contentHash as ContentHash,
      },
    })).position)
    const collision = await createWorld(initial)
    try {
      const capsule = playerCapsule(initial)
      const overlaps = collision.overlapCapsule(
        { ...capsule, semanticId: 'initial-spawn-probe' },
        [capsule.semanticId],
      )
      expect(overlaps.map(({ semanticId }) => semanticId)).toEqual(['floor'])
    } finally {
      collision.dispose()
    }
  })

  it('lands on the floor without tunnelling even from a multi-meter vertical tick', async () => {
    const initial = initialAt({ x: 10, y: 3, z: 8 }, { x: 0, y: -300, z: 0 }, false)
    const collision = await createWorld(initial)
    try {
      const landed = step(initial, collision)
      expect(localParticipant(landed).position.y).toBeGreaterThanOrEqual(0.89)
      expect(localParticipant(landed).position.y).toBeLessThan(0.92)
      expect(localParticipant(landed).grounded).toBe(true)
      const settled = step(landed, collision)
      expect(localParticipant(settled).velocity.y).toBeGreaterThan(-1)
    } finally {
      collision.dispose()
    }
  })

  it('slides along a wall and remains outside both planes of a corner', async () => {
    const wallInitial = initialAt(
      { x: 7.45, y: 0.91, z: -3 },
      { x: 8, y: 0, z: 4 },
    )
    const wallCollision = await createWorld(wallInitial)
    try {
      const slid = step(wallInitial, wallCollision)
      expect(localParticipant(slid).position.x).toBeLessThan(7.61)
      expect(localParticipant(slid).position.z).toBeGreaterThan(localParticipant(wallInitial).position.z)
    } finally {
      wallCollision.dispose()
    }

    const cornerInitial = initialAt(
      { x: -7.45, y: 0.91, z: 6.45 },
      { x: -7, y: 0, z: 7 },
    )
    const cornerCollision = await createWorld(cornerInitial)
    try {
      const contained = step(cornerInitial, cornerCollision)
      expect(localParticipant(contained).position.x).toBeGreaterThan(-7.61)
      expect(localParticipant(contained).position.z).toBeLessThan(6.61)
    } finally {
      cornerCollision.dispose()
    }
  })

  it('blocks upward motion at the low ceiling', async () => {
    const initial = initialAt({ x: 0, y: 1, z: 5 }, { x: 0, y: 120, z: 0 }, false)
    const collision = await createWorld(initial)
    try {
      const blocked = step(initial, collision)
      expect(localParticipant(blocked).position.y).toBeLessThan(1.11)
      expect(localParticipant(blocked).velocity.y).toBeLessThan(7)
    } finally {
      collision.dispose()
    }
  })

  it('autosteps the low obstacle through fixed playable action frames', async () => {
    const lowInitial = initialAt({ x: -5.5, y: 0.91, z: 0 }, { x: 9, y: 0, z: 0 })

    const directCollision = await createWorld(lowInitial)
    try {
      const direct = directCollision.moveCapsule(
        localParticipant(lowInitial).semanticId,
        { x: 2.5, y: -0.2, z: 0 },
      )
      expect(direct.position.x).toBeGreaterThan(-3.75)
      expect(direct.position.y).toBeGreaterThan(1)
    } finally {
      directCollision.dispose()
    }

    const lowCollision = await createWorld(lowInitial)
    try {
      let crossed = lowInitial
      let maximumX = localParticipant(crossed).position.x
      let maximumY = localParticipant(crossed).position.y
      for (let index = 0; index < 60; index += 1) {
        crossed = step(crossed, lowCollision, [{ actionId: 'move-right', value: 1 }])
        maximumX = Math.max(maximumX, localParticipant(crossed).position.x)
        maximumY = Math.max(maximumY, localParticipant(crossed).position.y)
      }
      expect(maximumX).toBeGreaterThan(-3.75)
      expect(maximumY).toBeGreaterThan(1)
    } finally {
      lowCollision.dispose()
    }
  })

  it('rejects the high obstacle through fixed playable action frames', async () => {
    const highInitial = initialAt({ x: -2.5, y: 0.91, z: 0 }, { x: 9, y: 0, z: 0 })
    const highCollision = await createWorld(highInitial)
    try {
      const blocked = run(highInitial, highCollision, 60, [{ actionId: 'move-right', value: 1 }])
      expect(localParticipant(blocked).position.x).toBeLessThan(-2)
      expect(localParticipant(blocked).position.y).toBeLessThan(1)
    } finally {
      highCollision.dispose()
    }
  })

  it('climbs the accepted slope and becomes airborne after leaving the ledge', async () => {
    const slopeInitial = initialAt({ x: 7, y: 0.91, z: 0 }, { x: -9, y: 0, z: 0 })
    const slopeCollision = await createWorld(slopeInitial)
    try {
      const climbed = run(slopeInitial, slopeCollision, 26, [{ actionId: 'move-right', value: -1 }])
      expect(localParticipant(climbed).position.x).toBeLessThan(4)
      expect(localParticipant(climbed).position.y).toBeGreaterThan(1)
    } finally {
      slopeCollision.dispose()
    }

    const ledgeInitial = initialAt({ x: 6, y: 2.31, z: 5 }, { x: 9, y: 0, z: 0 })
    const ledgeCollision = await createWorld(ledgeInitial)
    try {
      const airborne = run(ledgeInitial, ledgeCollision, 18, [{ actionId: 'move-right', value: 1 }])
      expect(localParticipant(airborne).position.x).toBeGreaterThan(8)
      expect(localParticipant(airborne).grounded).toBe(false)
      expect(localParticipant(airborne).position.y).toBeLessThan(2.31)
    } finally {
      ledgeCollision.dispose()
    }
  })
})
