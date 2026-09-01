import type * as Rapier from '@dimforge/rapier3d-deterministic-compat'

import { validateArenaDefinition, type ArenaDefinition } from '../arena/arenaDefinition'
import { contentHash, type ContentHash } from '../core/canonical'
import { deepFreeze } from '../core/deepFreeze'
import { initializeCollisionBackend } from './backendInitialization'
import {
  COLLISION_CHECKPOINT_SCHEMA_VERSION,
  COLLISION_ERROR_CODES,
  CollisionContractError,
  RUNTIME_COLLISION_COMPATIBILITY_IDENTITY,
  parseCollisionCapsuleMovementResult,
  parseCollisionCapsuleProjection,
  parseCollisionCheckpointPayload,
  parseCollisionHits,
  type CollisionCapsuleMovementResult,
  type CollisionCapsuleProjection,
  type CollisionCheckpointPayload,
  type CollisionHit,
  type CollisionVector3,
} from './contracts'

// Diagnostic-only versioned fixture settings. Gameplay movement will expose its
// accepted values through Game Design Profile descriptors in the movement change.
export const COLLISION_FIXTURE_CONTROLLER_PROFILE = deepFreeze({
  id: 'collision-kcc-fixture-v1',
  version: 1,
  offset: 0.01,
  autostepMaxHeight: 0.45,
  autostepMinWidth: 0.25,
  snapToGroundDistance: 0.2,
  maxSlopeClimbRadians: Math.PI / 4,
  minSlopeSlideRadians: Math.PI / 3,
})

type RapierModule = typeof import('@dimforge/rapier3d-deterministic-compat')

interface ParticipantRuntime {
  projection: CollisionCapsuleProjection
  collider: Rapier.Collider
  controller: Rapier.KinematicCharacterController
}

let activeWorlds = 0

export function activeRapierCollisionWorldCount(): number {
  return activeWorlds
}

function vector(value: CollisionVector3): Rapier.Vector3 {
  return { x: value.x, y: value.y, z: value.z }
}

function assertArena(arena: ArenaDefinition): Readonly<ArenaDefinition> {
  const validation = validateArenaDefinition(arena)
  if (!validation.ok) {
    throw new CollisionContractError(
      COLLISION_ERROR_CODES.invalidData,
      `Invalid collision arena: ${validation.errors.map((error) => error.message).join(' ')}`,
    )
  }
  return validation.arena
}

function assertUniqueCapsules(
  capsules: readonly CollisionCapsuleProjection[],
): readonly CollisionCapsuleProjection[] {
  const normalized = capsules
    .map((capsule) => parseCollisionCapsuleProjection(capsule))
    .sort((left, right) => left.semanticId.localeCompare(right.semanticId))
  for (let index = 1; index < normalized.length; index += 1) {
    if (normalized[index - 1]?.semanticId === normalized[index]?.semanticId) {
      throw new CollisionContractError(
        COLLISION_ERROR_CODES.invalidData,
        `Duplicate collision capsule semanticId: ${normalized[index]?.semanticId ?? ''}`,
      )
    }
  }
  return normalized
}

export class RapierCollisionWorld {
  readonly staticGeometryHash: ContentHash

  private disposed = false
  private readonly staticSemanticIdByHandle = new Map<number, string>()
  private readonly participantBySemanticId = new Map<string, ParticipantRuntime>()

  constructor(
    private readonly rapier: RapierModule,
    private readonly world: Rapier.World,
    private readonly arena: Readonly<ArenaDefinition>,
    capsules: readonly CollisionCapsuleProjection[],
  ) {
    const sortedSurfaces = [...arena.surfaces].sort((left, right) => left.id.localeCompare(right.id))
    this.staticGeometryHash = contentHash(sortedSurfaces)

    for (const surface of sortedSurfaces) {
      const descriptor = rapier.ColliderDesc.cuboid(
        surface.size.x / 2,
        surface.size.y / 2,
        surface.size.z / 2,
      )
        .setTranslation(surface.center.x, surface.center.y, surface.center.z)
        .setRotation(surface.orientation)
      const collider = world.createCollider(descriptor)
      this.staticSemanticIdByHandle.set(collider.handle, surface.id)
    }

    for (const projection of assertUniqueCapsules(capsules)) {
      const collider = world.createCollider(
        rapier.ColliderDesc.capsule(projection.halfHeight, projection.radius).setTranslation(
          projection.position.x,
          projection.position.y,
          projection.position.z,
        ),
      )
      const controller = world.createCharacterController(COLLISION_FIXTURE_CONTROLLER_PROFILE.offset)
      controller.setApplyImpulsesToDynamicBodies(false)
      controller.setSlideEnabled(true)
      controller.enableAutostep(
        COLLISION_FIXTURE_CONTROLLER_PROFILE.autostepMaxHeight,
        COLLISION_FIXTURE_CONTROLLER_PROFILE.autostepMinWidth,
        false,
      )
      controller.enableSnapToGround(COLLISION_FIXTURE_CONTROLLER_PROFILE.snapToGroundDistance)
      controller.setMaxSlopeClimbAngle(COLLISION_FIXTURE_CONTROLLER_PROFILE.maxSlopeClimbRadians)
      controller.setMinSlopeSlideAngle(COLLISION_FIXTURE_CONTROLLER_PROFILE.minSlopeSlideRadians)
      this.participantBySemanticId.set(projection.semanticId, { projection, collider, controller })
    }
    // Rapier refreshes its broad/narrow-phase caches on a world step. With zero
    // gravity and only fixed/parentless colliders this is deterministic derived
    // initialization, not a gameplay simulation step or hidden authority.
    world.step()
    activeWorlds += 1
  }

  private assertActive(): void {
    if (this.disposed) {
      throw new CollisionContractError(
        COLLISION_ERROR_CODES.worldDisposed,
        'Collision world has already been disposed',
      )
    }
  }

  projectCapsules(capsules: readonly CollisionCapsuleProjection[]): void {
    this.assertActive()
    const normalized = assertUniqueCapsules(capsules)
    if (normalized.length !== this.participantBySemanticId.size) {
      throw new CollisionContractError(
        COLLISION_ERROR_CODES.invalidData,
        'Projected capsule set does not match constructed world',
      )
    }
    for (const projection of normalized) {
      const runtime = this.participantBySemanticId.get(projection.semanticId)
      if (
        runtime === undefined ||
        runtime.projection.radius !== projection.radius ||
        runtime.projection.halfHeight !== projection.halfHeight
      ) {
        throw new CollisionContractError(
          COLLISION_ERROR_CODES.invalidData,
          `Projected capsule shape does not match ${projection.semanticId}`,
        )
      }
      runtime.collider.setTranslation(vector(projection.position))
      runtime.projection = projection
    }
  }

  moveCapsule(semanticId: string, desiredTranslation: CollisionVector3): CollisionCapsuleMovementResult {
    this.assertActive()
    const runtime = this.participantBySemanticId.get(semanticId)
    if (runtime === undefined) {
      throw new CollisionContractError(
        COLLISION_ERROR_CODES.invalidData,
        `Unknown collision capsule: ${semanticId}`,
      )
    }
    runtime.collider.setTranslation(vector(runtime.projection.position))
    runtime.controller.computeColliderMovement(
      runtime.collider,
      vector(desiredTranslation),
      undefined,
      undefined,
      (collider) => this.staticSemanticIdByHandle.has(collider.handle),
    )
    const movement = runtime.controller.computedMovement()
    const position = {
      x: runtime.projection.position.x + movement.x,
      y: runtime.projection.position.y + movement.y,
      z: runtime.projection.position.z + movement.z,
    }
    const contacts = Array.from({ length: runtime.controller.numComputedCollisions() }, (_, index) => {
      const collision = runtime.controller.computedCollision(index)
      const hitId = collision?.collider === null || collision === null
        ? undefined
        : this.staticSemanticIdByHandle.get(collision.collider.handle)
      if (collision === null || hitId === undefined) return undefined
      return {
        semanticId: hitId,
        distance: collision.toi,
        point: collision.witness1,
        normal: collision.normal1,
      }
    }).filter((contact): contact is NonNullable<typeof contact> => contact !== undefined)

    const result = parseCollisionCapsuleMovementResult({
      position,
      grounded: runtime.controller.computedGrounded(),
      contacts,
    })
    runtime.collider.setTranslation(vector(result.position))
    runtime.projection = parseCollisionCapsuleProjection({ ...runtime.projection, position: result.position })
    return result
  }

  raycast(
    origin: CollisionVector3,
    direction: CollisionVector3,
    maxDistance: number,
    excludeSemanticIds: readonly string[] = [],
  ): readonly CollisionHit[] {
    this.assertActive()
    const length = Math.hypot(direction.x, direction.y, direction.z)
    if (!Number.isFinite(length) || length === 0 || !Number.isFinite(maxDistance) || maxDistance < 0) {
      throw new CollisionContractError(COLLISION_ERROR_CODES.invalidData, 'Invalid collision ray')
    }
    const normalizedDirection = {
      x: direction.x / length,
      y: direction.y / length,
      z: direction.z / length,
    }
    const excluded = new Set(excludeSemanticIds)
    const ray = new this.rapier.Ray(vector(origin), normalizedDirection)
    const hits: CollisionHit[] = []
    this.world.intersectionsWithRay(
      ray,
      maxDistance,
      true,
      (intersection) => {
        const semanticId = this.semanticIdForHandle(intersection.collider.handle)
        if (semanticId !== undefined && !excluded.has(semanticId)) {
          hits.push({
            semanticId,
            distance: intersection.timeOfImpact,
            point: ray.pointAt(intersection.timeOfImpact),
            normal: intersection.normal,
          })
        }
        return true
      },
      undefined,
      undefined,
      undefined,
      undefined,
      (collider) => {
        const semanticId = this.semanticIdForHandle(collider.handle)
        return semanticId !== undefined && !excluded.has(semanticId)
      },
    )
    return parseCollisionHits(hits)
  }

  overlapCapsule(
    projection: CollisionCapsuleProjection,
    excludeSemanticIds: readonly string[] = [],
  ): readonly CollisionHit[] {
    this.assertActive()
    const normalized = parseCollisionCapsuleProjection(projection)
    const excluded = new Set(excludeSemanticIds)
    const hits: CollisionHit[] = []
    this.world.intersectionsWithShape(
      vector(normalized.position),
      { x: 0, y: 0, z: 0, w: 1 },
      new this.rapier.Capsule(normalized.halfHeight, normalized.radius),
      (collider) => {
        const semanticId = this.semanticIdForHandle(collider.handle)
        if (semanticId !== undefined && !excluded.has(semanticId)) {
          hits.push({
            semanticId,
            distance: 0,
            point: normalized.position,
            normal: { x: 0, y: 0, z: 0 },
          })
        }
        return true
      },
      undefined,
      undefined,
      undefined,
      undefined,
      (collider) => {
        const semanticId = this.semanticIdForHandle(collider.handle)
        return semanticId !== undefined && !excluded.has(semanticId)
      },
    )
    return parseCollisionHits(hits)
  }

  checkpoint(tick: number): CollisionCheckpointPayload {
    this.assertActive()
    return parseCollisionCheckpointPayload({
      schemaVersion: COLLISION_CHECKPOINT_SCHEMA_VERSION,
      tick,
      compatibility: RUNTIME_COLLISION_COMPATIBILITY_IDENTITY,
      arena: {
        id: this.arena.id,
        seed: this.arena.seed,
        generatorVersion: this.arena.generatorVersion,
        contentHash: this.arena.contentHash,
      },
      fixture: {
        id: COLLISION_FIXTURE_CONTROLLER_PROFILE.id,
        version: COLLISION_FIXTURE_CONTROLLER_PROFILE.version,
      },
      capsules: [...this.participantBySemanticId.values()].map((runtime) => runtime.projection),
    })
  }

  dispose(): void {
    if (this.disposed) return
    this.disposed = true
    this.staticSemanticIdByHandle.clear()
    this.participantBySemanticId.clear()
    this.world.free()
    activeWorlds -= 1
  }

  private semanticIdForHandle(handle: number): string | undefined {
    const staticId = this.staticSemanticIdByHandle.get(handle)
    if (staticId !== undefined) return staticId
    for (const [semanticId, runtime] of this.participantBySemanticId) {
      if (runtime.collider.handle === handle) return semanticId
    }
    return undefined
  }
}

export async function createRapierCollisionWorld(
  arena: ArenaDefinition,
  capsules: readonly CollisionCapsuleProjection[],
): Promise<RapierCollisionWorld> {
  const ready = await initializeCollisionBackend()
  const rapier = ready.module as RapierModule
  const world = new rapier.World({ x: 0, y: 0, z: 0 })
  try {
    return new RapierCollisionWorld(rapier, world, assertArena(arena), capsules)
  } catch (error) {
    world.free()
    throw error
  }
}
