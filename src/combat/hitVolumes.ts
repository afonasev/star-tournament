import type { CollisionVector3 } from '../collision/contracts'
import { deepFreeze } from '../core/deepFreeze'
import type { RobotTargetDefinition, TargetHitVolume } from '../scenario'

export type TargetHitZone = 'head' | 'torso' | 'limb'

export interface TargetVolumeIntersection {
  readonly targetId: string
  readonly volumeId: string
  readonly zone: TargetHitZone
  readonly distance: number
  readonly point: CollisionVector3
  readonly normal: CollisionVector3
}

interface LocalIntersection {
  readonly distance: number
  readonly normal: CollisionVector3
}

const INTERSECTION_EPSILON = 1e-9
const ZONE_ORDER: Readonly<Record<TargetHitZone, number>> = { head: 0, torso: 1, limb: 2 }

function add(first: CollisionVector3, second: CollisionVector3): CollisionVector3 {
  return { x: first.x + second.x, y: first.y + second.y, z: first.z + second.z }
}

function subtract(first: CollisionVector3, second: CollisionVector3): CollisionVector3 {
  return { x: first.x - second.x, y: first.y - second.y, z: first.z - second.z }
}

function scale(vector: CollisionVector3, scalar: number): CollisionVector3 {
  return { x: vector.x * scalar, y: vector.y * scalar, z: vector.z * scalar }
}

function dot(first: CollisionVector3, second: CollisionVector3): number {
  return first.x * second.x + first.y * second.y + first.z * second.z
}

function length(vector: CollisionVector3): number {
  return Math.hypot(vector.x, vector.y, vector.z)
}

function normalize(vector: CollisionVector3): CollisionVector3 {
  const magnitude = length(vector)
  if (magnitude === 0 || !Number.isFinite(magnitude)) throw new RangeError('Ray direction must be non-zero')
  return scale(vector, 1 / magnitude)
}

function rotateYaw(vector: CollisionVector3, yaw: number): CollisionVector3 {
  const cosine = Math.cos(yaw)
  const sine = Math.sin(yaw)
  return {
    x: cosine * vector.x + sine * vector.z,
    y: vector.y,
    z: -sine * vector.x + cosine * vector.z,
  }
}

function raySphere(
  origin: CollisionVector3,
  direction: CollisionVector3,
  center: CollisionVector3,
  radius: number,
): LocalIntersection | undefined {
  const offset = subtract(origin, center)
  const projected = dot(offset, direction)
  const discriminant = projected * projected - (dot(offset, offset) - radius * radius)
  if (discriminant < 0) return undefined
  const root = Math.sqrt(discriminant)
  const near = -projected - root
  const far = -projected + root
  const distance = near >= 0 ? near : far >= 0 ? far : undefined
  if (distance === undefined) return undefined
  const point = add(origin, scale(direction, distance))
  return { distance, normal: normalize(subtract(point, center)) }
}

function rayBox(
  origin: CollisionVector3,
  direction: CollisionVector3,
  center: CollisionVector3,
  halfExtents: CollisionVector3,
): LocalIntersection | undefined {
  const relative = subtract(origin, center)
  let near = -Infinity
  let far = Infinity
  let nearNormal: CollisionVector3 = { x: 0, y: 0, z: 0 }
  let farNormal: CollisionVector3 = nearNormal
  for (const axis of ['x', 'y', 'z'] as const) {
    const component = direction[axis]
    const minimum = -halfExtents[axis]
    const maximum = halfExtents[axis]
    if (Math.abs(component) < INTERSECTION_EPSILON) {
      if (relative[axis] < minimum || relative[axis] > maximum) return undefined
      continue
    }
    let axisNear = (minimum - relative[axis]) / component
    let axisFar = (maximum - relative[axis]) / component
    let normalSign = -1
    if (axisNear > axisFar) {
      ;[axisNear, axisFar] = [axisFar, axisNear]
      normalSign = 1
    }
    if (axisNear > near) {
      near = axisNear
      nearNormal = { x: 0, y: 0, z: 0, [axis]: normalSign }
    }
    if (axisFar < far) {
      far = axisFar
      farNormal = { x: 0, y: 0, z: 0, [axis]: -normalSign }
    }
    if (near > far) return undefined
  }
  if (far < 0) return undefined
  return near >= 0 ? { distance: near, normal: nearNormal } : { distance: far, normal: farNormal }
}

function rayCapsule(
  origin: CollisionVector3,
  direction: CollisionVector3,
  start: CollisionVector3,
  end: CollisionVector3,
  radius: number,
): LocalIntersection | undefined {
  const axis = subtract(end, start)
  const offset = subtract(origin, start)
  const axisSquared = dot(axis, axis)
  const axisRay = dot(axis, direction)
  const axisOffset = dot(axis, offset)
  const rayOffset = dot(direction, offset)
  const offsetSquared = dot(offset, offset)
  const quadraticA = axisSquared - axisRay * axisRay
  const quadraticB = axisSquared * rayOffset - axisOffset * axisRay
  const quadraticC = axisSquared * offsetSquared - axisOffset * axisOffset - radius * radius * axisSquared
  const candidates: LocalIntersection[] = []

  if (Math.abs(quadraticA) > INTERSECTION_EPSILON) {
    const discriminant = quadraticB * quadraticB - quadraticA * quadraticC
    if (discriminant >= 0) {
      const root = Math.sqrt(discriminant)
      for (const distance of [(-quadraticB - root) / quadraticA, (-quadraticB + root) / quadraticA]) {
        const projected = axisOffset + distance * axisRay
        if (distance >= 0 && projected > 0 && projected < axisSquared) {
          const point = add(origin, scale(direction, distance))
          const closest = add(start, scale(axis, projected / axisSquared))
          candidates.push({ distance, normal: normalize(subtract(point, closest)) })
        }
      }
    }
  }
  const startHit = raySphere(origin, direction, start, radius)
  const endHit = raySphere(origin, direction, end, radius)
  if (startHit !== undefined) candidates.push(startHit)
  if (endHit !== undefined) candidates.push(endHit)
  return candidates.sort((first, second) => first.distance - second.distance)[0]
}

function intersectVolume(
  localOrigin: CollisionVector3,
  localDirection: CollisionVector3,
  volume: TargetHitVolume,
): LocalIntersection | undefined {
  switch (volume.kind) {
    case 'sphere':
      return raySphere(localOrigin, localDirection, volume.centerOffset, volume.radius)
    case 'box':
      return rayBox(localOrigin, localDirection, volume.centerOffset, volume.halfExtents)
    case 'capsule':
      return rayCapsule(localOrigin, localDirection, volume.startOffset, volume.endOffset, volume.radius)
  }
}

export function intersectTargetHitVolumes(
  origin: CollisionVector3,
  direction: CollisionVector3,
  maximumDistance: number,
  targets: readonly RobotTargetDefinition[],
  activeTargetIds?: ReadonlySet<string>,
): readonly TargetVolumeIntersection[] {
  if (!Number.isFinite(maximumDistance) || maximumDistance < 0) {
    throw new RangeError('maximumDistance must be non-negative')
  }
  const normalizedDirection = normalize(direction)
  const hits: TargetVolumeIntersection[] = []
  for (const target of targets) {
    if (activeTargetIds !== undefined && !activeTargetIds.has(target.semanticId)) continue
    const localOrigin = rotateYaw(subtract(origin, target.position), -target.yawRadians)
    const localDirection = rotateYaw(normalizedDirection, -target.yawRadians)
    for (const volume of target.hitVolumes) {
      const intersection = intersectVolume(localOrigin, localDirection, volume)
      if (intersection === undefined || intersection.distance > maximumDistance) continue
      const point = add(origin, scale(normalizedDirection, intersection.distance))
      hits.push({
        targetId: target.semanticId,
        volumeId: volume.semanticId,
        zone: volume.zone,
        distance: intersection.distance,
        point,
        normal: rotateYaw(intersection.normal, target.yawRadians),
      })
    }
  }
  hits.sort((first, second) =>
    first.distance - second.distance ||
    first.targetId.localeCompare(second.targetId) ||
    ZONE_ORDER[first.zone] - ZONE_ORDER[second.zone] ||
    first.volumeId.localeCompare(second.volumeId),
  )
  return deepFreeze(hits)
}
