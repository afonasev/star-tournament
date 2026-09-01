import {
  COLLISION_DISTANCE_QUANTIZATION,
  type CollisionHit,
  type CollisionVector3,
} from '../collision/contracts'
import { deepFreeze } from '../core/deepFreeze'
import type { RobotTargetDefinition } from '../scenario'
import type { PelletDirection } from './pelletPattern'
import { intersectTargetHitVolumes, type TargetVolumeIntersection } from './hitVolumes'

export interface StaticRaycastPort {
  raycast(
    origin: CollisionVector3,
    direction: CollisionVector3,
    maximumDistance: number,
    excludeSemanticIds?: readonly string[],
  ): readonly CollisionHit[]
}

export interface ResolvedPelletImpact {
  readonly pelletIndex: number
  readonly target: TargetVolumeIntersection | null
  readonly surface: CollisionHit | null
  readonly position: CollisionVector3
  readonly normal: CollisionVector3
  readonly distance: number
}

function compareStaticHits(first: CollisionHit, second: CollisionHit): number {
  return first.distance - second.distance || first.semanticId.localeCompare(second.semanticId)
}

export function resolvePelletImpacts(
  origin: CollisionVector3,
  pellets: readonly PelletDirection[],
  maximumDistance: number,
  targets: readonly RobotTargetDefinition[],
  activeTargetIds: ReadonlySet<string>,
  collision: StaticRaycastPort,
  excludeStaticSemanticIds: readonly string[] = [],
): readonly ResolvedPelletImpact[] {
  const impacts: ResolvedPelletImpact[] = []
  for (const pellet of [...pellets].sort((first, second) => first.pelletIndex - second.pelletIndex)) {
    const target = intersectTargetHitVolumes(
      origin,
      pellet.direction,
      maximumDistance,
      targets,
      activeTargetIds,
    )[0]
    const surface = [...collision.raycast(
      origin,
      pellet.direction,
      maximumDistance,
      excludeStaticSemanticIds,
    )].sort(compareStaticHits)[0]
    const targetWins = target !== undefined && (
      surface === undefined || target.distance + COLLISION_DISTANCE_QUANTIZATION < surface.distance
    )
    const winner = targetWins ? target : surface
    if (winner === undefined) continue
    impacts.push({
      pelletIndex: pellet.pelletIndex,
      target: targetWins ? target ?? null : null,
      surface: targetWins ? null : surface ?? null,
      position: winner.point,
      normal: winner.normal,
      distance: winner.distance,
    })
  }
  impacts.sort((first, second) => first.pelletIndex - second.pelletIndex)
  return deepFreeze(impacts)
}
