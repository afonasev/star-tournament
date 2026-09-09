import type * as Rapier from '@dimforge/rapier3d-deterministic-compat'

import { arenaTransition, blocksProjectiles, blocksMovement, validateArenaDefinition, type ArenaDefinition } from '../arena/arenaDefinition'
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

/**
 * Backend-neutral gameplay configuration for Rapier's derived character
 * controller. Values come from the active Game Design Profile; the collision
 * adapter only validates and translates them into Rapier units.
 */
export interface CollisionControllerConfiguration {
  readonly skinWidth: number
  readonly autostepMaxHeight: number
  readonly autostepMinWidth: number
  readonly snapToGroundDistance: number
  readonly maxSlopeClimbDegrees: number
  readonly minSlopeSlideDegrees: number
}

const DEFAULT_COLLISION_CONTROLLER_CONFIGURATION: CollisionControllerConfiguration = deepFreeze({
  skinWidth: COLLISION_FIXTURE_CONTROLLER_PROFILE.offset,
  autostepMaxHeight: COLLISION_FIXTURE_CONTROLLER_PROFILE.autostepMaxHeight,
  autostepMinWidth: COLLISION_FIXTURE_CONTROLLER_PROFILE.autostepMinWidth,
  snapToGroundDistance: COLLISION_FIXTURE_CONTROLLER_PROFILE.snapToGroundDistance,
  maxSlopeClimbDegrees: COLLISION_FIXTURE_CONTROLLER_PROFILE.maxSlopeClimbRadians * 180 / Math.PI,
  minSlopeSlideDegrees: COLLISION_FIXTURE_CONTROLLER_PROFILE.minSlopeSlideRadians * 180 / Math.PI,
})

function parseControllerConfiguration(
  value: CollisionControllerConfiguration | undefined,
): Readonly<CollisionControllerConfiguration> {
  if (value === undefined) return DEFAULT_COLLISION_CONTROLLER_CONFIGURATION
  const expectedKeys = [
    'autostepMaxHeight',
    'autostepMinWidth',
    'maxSlopeClimbDegrees',
    'minSlopeSlideDegrees',
    'skinWidth',
    'snapToGroundDistance',
  ]
  const actualKeys = Object.keys(value).sort()
  if (actualKeys.length !== expectedKeys.length || actualKeys.some((key, index) => key !== expectedKeys[index])) {
    throw new CollisionContractError(
      COLLISION_ERROR_CODES.invalidData,
      'Collision controller configuration must contain exactly six fields',
    )
  }
  const entries = Object.entries(value)
  for (const [path, number] of entries) {
    const allowsZero = path === 'autostepMaxHeight' || path === 'autostepMinWidth' ||
      path === 'snapToGroundDistance' || path === 'maxSlopeClimbDegrees'
    if (!Number.isFinite(number) || number < 0 || (!allowsZero && number === 0)) {
      throw new CollisionContractError(
        COLLISION_ERROR_CODES.invalidData,
        `Collision controller configuration ${path} is outside its accepted non-negative range`,
      )
    }
  }
  if (value.maxSlopeClimbDegrees > value.minSlopeSlideDegrees) {
    throw new CollisionContractError(
      COLLISION_ERROR_CODES.invalidData,
      'Collision controller max climb angle must not exceed min slide angle',
    )
  }
  if (value.minSlopeSlideDegrees > 90) {
    throw new CollisionContractError(
      COLLISION_ERROR_CODES.invalidData,
      'Collision controller slope angles must not exceed 90 degrees',
    )
  }
  return deepFreeze({ ...value })
}

type RapierModule = typeof import('@dimforge/rapier3d-deterministic-compat')

interface ParticipantRuntime {
  projection: CollisionCapsuleProjection
  collider: Rapier.Collider
  controller: Rapier.KinematicCharacterController
  active: boolean
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
  readonly controllerConfigurationHash: ContentHash

  private disposed = false
  private readonly projectileHandles = new Set<number>()
  private readonly movementHandles = new Set<number>()
  private readonly staticSemanticIdByHandle = new Map<number, string>()
  private readonly participantSemanticIdByHandle = new Map<number, string>()
  private readonly participantBySemanticId = new Map<string, ParticipantRuntime>()
  private readonly stairBounds: Array<{minimumX:number;maximumX:number;z:number;halfWidth:number}>=[]

  constructor(
    private readonly rapier: RapierModule,
    private readonly world: Rapier.World,
    private readonly arena: Readonly<ArenaDefinition>,
    capsules: readonly CollisionCapsuleProjection[],
    readonly controllerConfiguration: Readonly<CollisionControllerConfiguration>,
  ) {
    const sortedSurfaces = [...arena.surfaces].sort((left, right) => left.id.localeCompare(right.id))
    for(const link of arena.links){
      const t=arenaTransition(link)
      if(t?.kind!=='stairs')continue
      const steps=t.surfaceIds.map(id=>sortedSurfaces.find(s=>s.id===id)!)
      this.stairBounds.push({minimumX:Math.min(...steps.map(s=>s.center.x-s.size.x/2)),maximumX:Math.max(...steps.map(s=>s.center.x+s.size.x/2)),z:steps[0]!.center.z,halfWidth:Math.min(...steps.map(s=>s.size.z/2))})
    }
    this.staticGeometryHash = contentHash(sortedSurfaces)
    this.controllerConfigurationHash = contentHash(controllerConfiguration)

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
      if (blocksProjectiles(surface)) this.projectileHandles.add(collider.handle)
      if (blocksMovement(surface)) this.movementHandles.add(collider.handle)
    }

    for (const projection of assertUniqueCapsules(capsules)) this.createParticipantRuntime(projection)
    // Rapier refreshes its broad/narrow-phase caches on a world step. With zero
    // gravity and only fixed/parentless colliders this is deterministic derived
    // initialization, not a gameplay simulation step or hidden authority.
    world.step()
    activeWorlds += 1
  }

  private createParticipantRuntime(projection: CollisionCapsuleProjection): ParticipantRuntime {
    const collider = this.world.createCollider(
      this.rapier.ColliderDesc.capsule(projection.halfHeight, projection.radius).setTranslation(
        projection.position.x,
        projection.position.y,
        projection.position.z,
      ),
    )
    const controller = this.world.createCharacterController(this.controllerConfiguration.skinWidth)
    controller.setApplyImpulsesToDynamicBodies(false)
    controller.setSlideEnabled(true)
    if (
      this.controllerConfiguration.autostepMaxHeight === 0 ||
      this.controllerConfiguration.autostepMinWidth === 0
    ) {
      controller.disableAutostep()
    } else {
      controller.enableAutostep(
        this.controllerConfiguration.autostepMaxHeight,
        this.controllerConfiguration.autostepMinWidth,
        false,
      )
    }
    if (this.controllerConfiguration.snapToGroundDistance === 0) {
      controller.disableSnapToGround()
    } else {
      controller.enableSnapToGround(this.controllerConfiguration.snapToGroundDistance)
    }
    controller.setMaxSlopeClimbAngle(this.controllerConfiguration.maxSlopeClimbDegrees * Math.PI / 180)
    controller.setMinSlopeSlideAngle(this.controllerConfiguration.minSlopeSlideDegrees * Math.PI / 180)
    const runtime = { projection, collider, controller, active: true }
    this.participantBySemanticId.set(projection.semanticId, runtime)
    this.participantSemanticIdByHandle.set(collider.handle, projection.semanticId)
    return runtime
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
    for (const projection of normalized) {
      const runtime = this.participantBySemanticId.get(projection.semanticId)
      if (runtime !== undefined && (
        runtime.projection.radius !== projection.radius ||
        runtime.projection.halfHeight !== projection.halfHeight
      )) {
        throw new CollisionContractError(
          COLLISION_ERROR_CODES.invalidData,
          `Projected capsule shape does not match ${projection.semanticId}`,
        )
      }
    }
    let createdRuntime = false
    for (const projection of normalized) {
      if (!this.participantBySemanticId.has(projection.semanticId)) {
        this.createParticipantRuntime(projection)
        createdRuntime = true
      }
    }
    if (createdRuntime) {
      // A restored world may first encounter a participant when that life
      // respawns. Fixed parentless colliders have no physics state to advance;
      // this deterministic step only refreshes Rapier's scene-query caches.
      this.world.step()
    }
    const projectedIds = new Set(normalized.map(({ semanticId }) => semanticId))
    for (const [semanticId, runtime] of this.participantBySemanticId) {
      if (runtime.active && !projectedIds.has(semanticId)) {
        runtime.active = false
        runtime.collider.setEnabled(false)
      }
    }
    for (const projection of normalized) {
      const runtime = this.participantBySemanticId.get(projection.semanticId)
      if (runtime === undefined) throw new Error(`Missing collision capsule ${projection.semanticId}`)
      runtime.collider.setTranslation(vector(projection.position))
      if (!runtime.active) runtime.collider.setEnabled(true)
      runtime.projection = projection
      runtime.active = true
    }
  }

  moveCapsule(semanticId: string, desiredTranslation: CollisionVector3): CollisionCapsuleMovementResult {
    this.assertActive()
    const runtime = this.participantBySemanticId.get(semanticId)
    if (runtime === undefined || !runtime.active) {
      throw new CollisionContractError(
        COLLISION_ERROR_CODES.invalidData,
        `Unknown collision capsule: ${semanticId}`,
      )
    }
    runtime.collider.setTranslation(vector(runtime.projection.position))
    const acceptsCollider = (collider: Rapier.Collider) => {
      if (this.staticSemanticIdByHandle.has(collider.handle)) return this.movementHandles.has(collider.handle)
      const otherId = this.participantSemanticIdByHandle.get(collider.handle)
      return otherId !== undefined && otherId !== semanticId &&
        this.participantBySemanticId.get(otherId)?.active === true
    }
    // A fully contained flat-floor movement cannot descend to another support.
    // Disable optional snap before querying: its shallow undershoot can leave
    // a wider capsule embedded and stuck on the following tick.
    // Preserve historical generator/profile collision paths.
    const improvedFlatContact=this.arena.generatorVersion==='arena-rooms-v2'&&this.arena.generatorProfile.contentHash!=='fnv1a64-v1:175b900d6f85f32c'
    const flatFeet=runtime.projection.position.y-runtime.projection.halfHeight-runtime.projection.radius
    const flatSkin=this.controllerConfiguration.skinWidth
    const flatSupported=improvedFlatContact&&desiredTranslation.y<=0&&this.arena.surfaces.some(s=>{
      if(s.semantics?.classification!=='floor'||!blocksMovement(s)||s.orientation.x!==0||s.orientation.y!==0||s.orientation.z!==0)return false
      const top=s.center.y+s.size.y/2,p=runtime.projection.position,r=runtime.projection.radius+flatSkin
      return flatFeet>=top-flatSkin&&flatFeet<=top+flatSkin*2
        &&Math.abs(p.x-s.center.x)<s.size.x/2-r&&Math.abs(p.z-s.center.z)<s.size.z/2-r
        &&Math.abs(p.x+desiredTranslation.x-s.center.x)<s.size.x/2-r&&Math.abs(p.z+desiredTranslation.z-s.center.z)<s.size.z/2-r
    })
    if(flatSupported)runtime.controller.disableSnapToGround()
    try {
    runtime.controller.computeColliderMovement(
      runtime.collider,
      vector(desiredTranslation),
      undefined,
      undefined,
      acceptsCollider,
    )
    } finally {
      if(flatSupported&&this.controllerConfiguration.snapToGroundDistance>0)runtime.controller.enableSnapToGround(this.controllerConfiguration.snapToGroundDistance)
    }
    let movement = runtime.controller.computedMovement()
    // arena-layers-v2: Rapier 0.20 snap can overshoot a contacted flat floor.
    // Requery the identical pose/displacement without that optional snap only
    // when the candidate crosses the current support. Never clamp feet or
    // suppress collisions; restore the configured snap for the next movement.
    const skin=this.controllerConfiguration.skinWidth
    const feet=runtime.projection.position.y-runtime.projection.halfHeight-runtime.projection.radius
    const crossesSupport=(candidate:CollisionVector3)=>this.arena.surfaces.some(s=>{
      if(s.semantics?.classification!=='floor'||!blocksMovement(s)||s.orientation.x!==0||s.orientation.y!==0||s.orientation.z!==0)return false
      const top=s.center.y+s.size.y/2
      return feet>=top-skin&&feet<=top+(improvedFlatContact?Math.max(this.controllerConfiguration.snapToGroundDistance,skin*2):this.controllerConfiguration.snapToGroundDistance)
        &&feet+candidate.y<top-(improvedFlatContact?1e-4:skin) // Rapier f32 contact tolerance (0.1 mm); skin is not penetration allowance.
        &&Math.abs(runtime.projection.position.x-s.center.x)<s.size.x/2-runtime.projection.radius
        &&Math.abs(runtime.projection.position.z-s.center.z)<s.size.z/2-runtime.projection.radius
        &&Math.abs(runtime.projection.position.x+candidate.x-s.center.x)<s.size.x/2-runtime.projection.radius
        &&Math.abs(runtime.projection.position.z+candidate.z-s.center.z)<s.size.z/2-runtime.projection.radius
    })
    const layeredGenerator=['arena-layers-v2','shipped-fallback-layers-v2','arena-rooms-v1','arena-rooms-v2'].includes(this.arena.generatorVersion)
    const penetratesSupport=layeredGenerator&&(improvedFlatContact||this.controllerConfiguration.snapToGroundDistance>0)&&(improvedFlatContact||movement.y<desiredTranslation.y-skin)&&crossesSupport(movement)
    let retainedFlatSupport=false
    if(penetratesSupport){
      runtime.controller.disableSnapToGround()
      try {
        runtime.controller.computeColliderMovement(runtime.collider,vector(desiredTranslation),undefined,undefined,acceptsCollider)
        movement=runtime.controller.computedMovement()
        if(crossesSupport(movement)&&flatSupported){
          // Reject gravity into the confirmed floor; requery horizontal motion
          // with every blocker still active, never clamp or teleport the pose.
          runtime.controller.computeColliderMovement(runtime.collider,vector({x:desiredTranslation.x,y:0,z:desiredTranslation.z}),undefined,undefined,acceptsCollider)
          movement=runtime.controller.computedMovement()
          retainedFlatSupport=true
        }
        if(crossesSupport(movement))throw new CollisionContractError(COLLISION_ERROR_CODES.invalidData,`KCC movement crosses its existing support after snap retry: ${JSON.stringify({position:runtime.projection.position,desiredTranslation,movement,flatSupported})}`)
      } finally {if(this.controllerConfiguration.snapToGroundDistance>0)runtime.controller.enableSnapToGround(this.controllerConfiguration.snapToGroundDistance)}
    }
    let grounded=runtime.controller.computedGrounded()||retainedFlatSupport
    let contacts = Array.from({ length: runtime.controller.numComputedCollisions() }, (_, index) => {
      const collision = runtime.controller.computedCollision(index)
      const hitId = collision?.collider === null || collision === null
        ? undefined
        : this.semanticIdForHandle(collision.collider.handle)
      if (collision === null || hitId === undefined) return undefined
      return {
        semanticId: hitId,
        distance: collision.toi,
        point: collision.witness1,
        normal: collision.normal1,
      }
    }).filter((contact): contact is NonNullable<typeof contact> => contact !== undefined)

    // Rapier's autostep can stall on a real riser at small analog displacement.
    // Retry a fully swept step, never increasing requested XZ or moving through a blocker.
    const horizontal=Math.hypot(desiredTranslation.x,desiredTranslation.z)
    const padding=runtime.projection.radius+skin,pose=runtime.projection.position
    const nearStairs=this.stairBounds.some(b=>pose.x>=b.minimumX-padding&&pose.x<=b.maximumX+padding&&Math.abs(pose.z-b.z)<=b.halfWidth-padding)
    if(this.arena.schemaVersion>=6&&desiredTranslation.y<=0&&horizontal>0&&nearStairs){
      const p=runtime.projection.position,q={x:0,y:0,z:0,w:1}
      const cast=(at:CollisionVector3,delta:CollisionVector3)=>this.world.castShape(at,q,delta,runtime.collider.shape,skin,1,false,undefined,undefined,runtime.collider,undefined,acceptsCollider)
      const support=cast(p,{x:0,y:-skin*2,z:0})
      const supportId=support?this.semanticIdForHandle(support.collider.handle):undefined
      const link=this.arena.links.find(l=>{
        const t=arenaTransition(l)
        if(t?.kind!=='stairs')return false
        const ids=[this.arena.regions.find(r=>r.id===l.fromRegionId)?.supportSurfaceId,...t.surfaceIds,this.arena.regions.find(r=>r.id===l.toRegionId)?.supportSurfaceId]
        const index=ids.indexOf(supportId)
        const first=this.arena.surfaces.find(s=>s.id===t.surfaceIds[0])!,last=this.arena.surfaces.find(s=>s.id===t.surfaceIds.at(-1))!
        const descending=desiredTranslation.x*(last.center.x-first.center.x)<0
        return index>=0&&((descending&&index>0)||contacts.some(hit=>t.surfaceIds.includes(hit.semanticId)&&Math.abs(ids.indexOf(hit.semanticId)-index)<=1))
      })
      // Numerical normal tolerance rejects grazing side contacts, not a slope rule.
      if(link&&support&&support.normal1.y>1e-3){
        const t=arenaTransition(link)!,first=this.arena.surfaces.find(s=>s.id===t.surfaceIds[0])!,last=this.arena.surfaces.find(s=>s.id===t.surfaceIds.at(-1))!
        const radius=runtime.projection.radius,laneWidth=Math.min(first.size.z,last.size.z)/2-radius-skin
        // Current compiler's stairs run along X. Side entry must use ordinary collision.
        const minimumX=Math.min(first.center.x-first.size.x/2,last.center.x-last.size.x/2)-radius-skin
        const maximumX=Math.max(first.center.x+first.size.x/2,last.center.x+last.size.x/2)+radius+skin
        const inLane=p.x>=minimumX&&p.x<=maximumX&&Math.abs(p.z-first.center.z)<=laneWidth&&Math.abs(p.z+desiredTranslation.z-first.center.z)<=laneWidth
        if(inLane){
          const maxUp=this.controllerConfiguration.autostepMaxHeight
          const ids=[this.arena.regions.find(r=>r.id===link.fromRegionId)?.supportSurfaceId,...t.surfaceIds,this.arena.regions.find(r=>r.id===link.toRegionId)?.supportSurfaceId]
          const descending=desiredTranslation.x*(last.center.x-first.center.x)<0
          if(descending){
            // Only the neighbouring declared tread may retain contact on descent.
            // XZ is the ordinary KCC result, so walls and capsules keep their response.
            const end={x:p.x+movement.x,y:p.y+movement.y,z:p.z+movement.z}
            const landing=cast(end,{x:0,y:-maxUp,z:0}),id=landing?this.semanticIdForHandle(landing.collider.handle):undefined
            const index=ids.indexOf(id),previous=ids.indexOf(supportId)
            if(landing&&id&&index>=0&&(index===previous||index===previous-1)&&landing.normal1.y>0){
              const dy=movement.y-maxUp*landing.time_of_impact
              // Keep KCC's numerically stable contact when no extra snap is needed.
              if(dy>=-maxUp&&dy<movement.y-skin){movement={x:movement.x,y:dy,z:movement.z};grounded=true;contacts=[{semanticId:id,distance:landing.time_of_impact,point:landing.witness1,normal:landing.normal1}]}
            }
          }else if(movement.x*desiredTranslation.x+movement.z*desiredTranslation.z<horizontal*horizontal-Number.EPSILON){
          const ceiling=cast(p,{x:0,y:maxUp,z:0}),up=maxUp*(ceiling?.time_of_impact??1)
          const raised={x:p.x,y:p.y+up,z:p.z},across={x:desiredTranslation.x,y:0,z:desiredTranslation.z}
          if(up>skin&&!cast(raised,across)){
            const end={x:p.x+across.x,y:raised.y,z:p.z+across.z},down=up+skin*2
            const landing=cast(end,{x:0,y:-down,z:0}),landingId=landing?this.semanticIdForHandle(landing.collider.handle):undefined
            const ids=[this.arena.regions.find(r=>r.id===link.fromRegionId)?.supportSurfaceId,...t.surfaceIds,this.arena.regions.find(r=>r.id===link.toRegionId)?.supportSurfaceId]
            if(landing&&landingId&&ids.includes(landingId)&&Math.abs(ids.indexOf(landingId)-ids.indexOf(supportId))<=1){
              const dy=up-down*landing.time_of_impact
              if(dy>=-skin*2&&dy<=maxUp&&landing.normal1.y>0){
                movement={x:across.x,y:dy,z:across.z};grounded=true
                contacts=[{semanticId:landingId,distance:landing.time_of_impact,point:landing.witness1,normal:landing.normal1}]
              }
            }
          }
          }
        }
      }
    }
    const position={x:runtime.projection.position.x+movement.x,y:runtime.projection.position.y+movement.y,z:runtime.projection.position.z+movement.z}

    const result = parseCollisionCapsuleMovementResult({
      position,
      grounded,
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
        return semanticId !== undefined && !excluded.has(semanticId) && (!this.staticSemanticIdByHandle.has(collider.handle) || this.projectileHandles.has(collider.handle))
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
        return semanticId !== undefined && !excluded.has(semanticId) && (!this.staticSemanticIdByHandle.has(collider.handle) || this.movementHandles.has(collider.handle))
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
        id: this.controllerConfiguration === DEFAULT_COLLISION_CONTROLLER_CONFIGURATION
          ? COLLISION_FIXTURE_CONTROLLER_PROFILE.id
          : `collision-kcc-config-v1:${this.controllerConfigurationHash}`,
        version: 1,
      },
      capsules: [...this.participantBySemanticId.values()]
        .filter(({ active }) => active)
        .map((runtime) => runtime.projection),
    })
  }

  dispose(): void {
    if (this.disposed) return
    this.disposed = true
    this.projectileHandles.clear()
    this.movementHandles.clear()
    this.staticSemanticIdByHandle.clear()
    this.participantSemanticIdByHandle.clear()
    this.participantBySemanticId.clear()
    this.world.free()
    activeWorlds -= 1
  }

  private semanticIdForHandle(handle: number): string | undefined {
    const staticId = this.staticSemanticIdByHandle.get(handle)
    if (staticId !== undefined) return staticId
    const participantId = this.participantSemanticIdByHandle.get(handle)
    return participantId !== undefined && this.participantBySemanticId.get(participantId)?.active === true
      ? participantId
      : undefined
  }
}

export async function createRapierCollisionWorld(
  arena: ArenaDefinition,
  capsules: readonly CollisionCapsuleProjection[],
  controllerConfiguration?: CollisionControllerConfiguration,
): Promise<RapierCollisionWorld> {
  const ready = await initializeCollisionBackend()
  const rapier = ready.module as RapierModule
  const world = new rapier.World({ x: 0, y: 0, z: 0 })
  try {
    return new RapierCollisionWorld(
      rapier,
      world,
      assertArena(arena),
      capsules,
      parseControllerConfiguration(controllerConfiguration),
    )
  } catch (error) {
    world.free()
    throw error
  }
}
