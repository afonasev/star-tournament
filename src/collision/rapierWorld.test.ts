import { describe, expect, it } from 'vitest'

import { computeArenaContentHash, createArenaDefinition, type ArenaDefinitionPayload } from '../arena/arenaDefinition'
import { COLLISION_ERROR_CODES, CollisionContractError, collisionCheckpointHash } from './contracts'
import { COLLISION_FIXTURE_ARENA } from './fixture'
import {
  activeRapierCollisionWorldCount,
  createRapierCollisionWorld,
} from './rapierWorld'

const player = {
  semanticId: 'player-1',
  position: { x: -6, y: 1, z: -5 },
  radius: 0.4,
  halfHeight: 0.5,
} as const

describe('Rapier collision world', () => {
  it('constructs static geometry in semantic order independent of descriptors', async () => {
    const { contentHash: _contentHash, ...payload } = COLLISION_FIXTURE_ARENA
    const reordered = createArenaDefinition({
      ...payload,
      surfaces: [...COLLISION_FIXTURE_ARENA.surfaces].reverse(),
    } as ArenaDefinitionPayload)
    expect(computeArenaContentHash(reordered)).toBe(reordered.contentHash)

    const first = await createRapierCollisionWorld(COLLISION_FIXTURE_ARENA, [player])
    const second = await createRapierCollisionWorld(reordered, [player])
    try {
      expect(first.staticGeometryHash).toBe(second.staticGeometryHash)
      expect(first.raycast({ x: 0, y: 1, z: 0 }, { x: 0, y: 0, z: -1 }, 10))
        .toEqual(second.raycast({ x: 0, y: 1, z: 0 }, { x: 0, y: 0, z: -1 }, 10))
    } finally {
      first.dispose()
      second.dispose()
    }
  })

  it('returns nearest ordered ray hits and reports invalid overlap placement', async () => {
    const collision = await createRapierCollisionWorld(COLLISION_FIXTURE_ARENA, [player])
    try {
      const hits = collision.raycast({ x: 0, y: 1, z: 0 }, { x: 0, y: 0, z: -1 }, 10)
      expect(hits[0]?.semanticId).toBe('cover-occluder')
      expect(hits.map((hit) => hit.distance)).toEqual([...hits.map((hit) => hit.distance)].sort((a, b) => a - b))
      const tiedHits = hits.filter((hit) => hit.semanticId.startsWith('tie-'))
      expect(tiedHits.map((hit) => hit.semanticId)).toEqual(['tie-a', 'tie-b'])
      expect(tiedHits[0]?.distance).toBe(tiedHits[1]?.distance)
      expect(Object.isFrozen(hits)).toBe(true)

      const overlaps = collision.overlapCapsule({
        ...player,
        semanticId: 'spawn-probe',
        position: { x: 0, y: 1, z: -3 },
      })
      expect(overlaps.map((hit) => hit.semanticId)).toContain('cover-occluder')
    } finally {
      collision.dispose()
    }
  })

  it('contains a capsule against floor and wall while returning serializable contacts', async () => {
    const airborne = { ...player, position: { x: 7.4, y: 3, z: -3 } }
    const collision = await createRapierCollisionWorld(COLLISION_FIXTURE_ARENA, [airborne])
    try {
      const landing = collision.moveCapsule('player-1', { x: 0, y: -5, z: 0 })
      expect(landing.position.y).toBeGreaterThanOrEqual(0.89)
      expect(landing.grounded).toBe(true)
      expect(landing.contacts.some((contact) => contact.semanticId === 'floor')).toBe(true)

      const slide = collision.moveCapsule('player-1', { x: 3, y: 0, z: 2 })
      expect(slide.position.x).toBeLessThan(8)
      expect(slide.position.z).toBeGreaterThan(landing.position.z)
      expect(JSON.parse(JSON.stringify(slide))).toEqual(slide)
    } finally {
      collision.dispose()
    }
  })

  it('blocks movement against living participant capsules and disables them until reprojected', async () => {
    const mover = { ...player, position: { x: -6, y: 1, z: -5 } }
    const blocker = { ...player, semanticId: 'player-2', position: { x: -4, y: 1, z: -5 } }
    const collision = await createRapierCollisionWorld(COLLISION_FIXTURE_ARENA, [mover])
    try {
      collision.projectCapsules([mover, blocker])
      const blocked = collision.moveCapsule(mover.semanticId, { x: 3, y: 0, z: 0 })
      expect(blocked.position.x).toBeLessThan(-4.7)
      expect(blocked.contacts.map(({ semanticId }) => semanticId)).toContain(blocker.semanticId)

      collision.projectCapsules([mover])
      expect(collision.checkpoint(1).capsules.map(({ semanticId }) => semanticId)).toEqual([mover.semanticId])
      const passed = collision.moveCapsule(mover.semanticId, { x: 3, y: 0, z: 0 })
      expect(passed.position.x).toBeGreaterThan(-3.5)

      collision.projectCapsules([mover, blocker])
      const blockedAfterRespawn = collision.moveCapsule(mover.semanticId, { x: 3, y: 0, z: 0 })
      expect(blockedAfterRespawn.position.x).toBeLessThan(-4.7)
    } finally {
      collision.dispose()
    }
  })

  it('covers corner, ceiling, step, slope, and ledge fixture decisions', async () => {
    const capsules = [
      { ...player, semanticId: 'ceiling', position: { x: 0, y: 1, z: 5 } },
      { ...player, semanticId: 'corner', position: { x: -7, y: 1, z: 6 } },
      { ...player, semanticId: 'step-low', position: { x: -5.5, y: 0.91, z: 0 } },
      { ...player, semanticId: 'step-high', position: { x: -2.5, y: 0.91, z: 0 } },
      { ...player, semanticId: 'slope', position: { x: 7, y: 0.91, z: 0 } },
      { ...player, semanticId: 'ledge', position: { x: 6, y: 2.31, z: 5 } },
    ]
    const collision = await createRapierCollisionWorld(COLLISION_FIXTURE_ARENA, capsules)
    try {
      const ceiling = collision.moveCapsule('ceiling', { x: 0, y: 3, z: 0 })
      expect(ceiling.position.y).toBeLessThan(1.11)
      expect(ceiling.contacts.map((contact) => contact.semanticId)).toContain('ceiling-low')

      const corner = collision.moveCapsule('corner', { x: -4, y: 0, z: 4 })
      expect(corner.position.x).toBeGreaterThan(-8.1)
      expect(corner.position.z).toBeLessThan(7.1)
      expect(corner.contacts.map((contact) => contact.semanticId)).toEqual(
        expect.arrayContaining(['corner-north', 'corner-west']),
      )

      const lowStep = collision.moveCapsule('step-low', { x: 2.5, y: -0.2, z: 0 })
      expect(lowStep.position.x).toBeGreaterThan(-3.75)
      expect(lowStep.position.y).toBeGreaterThan(1)

      const highStep = collision.moveCapsule('step-high', { x: 2.5, y: -0.2, z: 0 })
      expect(highStep.position.x).toBeLessThan(-2)

      const slope = collision.moveCapsule('slope', { x: -5.5, y: -0.2, z: 0 })
      expect(slope.position.x).toBeLessThan(4)
      expect(slope.position.y).toBeGreaterThan(1)

      const ledge = collision.moveCapsule('ledge', { x: 3, y: -0.2, z: 0 })
      expect(ledge.position.x).toBeGreaterThan(8)
      expect(ledge.grounded).toBe(false)
    } finally {
      collision.dispose()
    }
  })

  it('produces stable checkpoint hashes and has idempotent disposal', async () => {
    const baseline = activeRapierCollisionWorldCount()
    const first = await createRapierCollisionWorld(COLLISION_FIXTURE_ARENA, [player])
    const second = await createRapierCollisionWorld(COLLISION_FIXTURE_ARENA, [player])
    expect(activeRapierCollisionWorldCount()).toBe(baseline + 2)
    expect(collisionCheckpointHash(first.checkpoint(0))).toBe(collisionCheckpointHash(second.checkpoint(0)))

    first.dispose()
    first.dispose()
    second.dispose()
    expect(activeRapierCollisionWorldCount()).toBe(baseline)
    expect(() => first.raycast({ x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 }, 1)).toThrowError(
      expect.objectContaining<Partial<CollisionContractError>>({ code: COLLISION_ERROR_CODES.worldDisposed }),
    )
  })

  it('derives checkpoint fixture identity from gameplay controller configuration', async () => {
    const configuration = {
      skinWidth: 0.015,
      autostepMaxHeight: 0.4,
      autostepMinWidth: 0.2,
      snapToGroundDistance: 0.16,
      maxSlopeClimbDegrees: 42,
      minSlopeSlideDegrees: 57,
    }
    const first = await createRapierCollisionWorld(COLLISION_FIXTURE_ARENA, [player], configuration)
    const second = await createRapierCollisionWorld(COLLISION_FIXTURE_ARENA, [player], {
      ...configuration,
      skinWidth: 0.02,
    })
    const reconstructed = await createRapierCollisionWorld(COLLISION_FIXTURE_ARENA, [player], {
      ...configuration,
    })
    try {
      expect(first.controllerConfigurationHash).not.toBe(second.controllerConfigurationHash)
      expect(first.controllerConfiguration).toEqual(configuration)
      expect(Object.isFrozen(first.controllerConfiguration)).toBe(true)
      expect(first.checkpoint(0).fixture.id).toContain(first.controllerConfigurationHash)
      expect(second.checkpoint(0).fixture.id).toContain(second.controllerConfigurationHash)
      expect(first.controllerConfigurationHash).toBe(reconstructed.controllerConfigurationHash)
      expect(collisionCheckpointHash(first.checkpoint(0))).toBe(
        collisionCheckpointHash(reconstructed.checkpoint(0)),
      )
      expect(collisionCheckpointHash(first.checkpoint(0))).not.toBe(
        collisionCheckpointHash(second.checkpoint(0)),
      )
    } finally {
      first.dispose()
      second.dispose()
      reconstructed.dispose()
    }
  })

  it('accepts profile-valid disabled autostep and ground snap values', async () => {
    const collision = await createRapierCollisionWorld(COLLISION_FIXTURE_ARENA, [player], {
      skinWidth: 0.01,
      autostepMaxHeight: 0,
      autostepMinWidth: 0,
      snapToGroundDistance: 0,
      maxSlopeClimbDegrees: 0,
      minSlopeSlideDegrees: 60,
    })
    try {
      expect(collision.checkpoint(0).fixture.id).toContain(collision.controllerConfigurationHash)
    } finally {
      collision.dispose()
    }
  })
})
