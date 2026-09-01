import type { CollisionVector3 } from '../collision/contracts'
import { deepFreeze } from '../core/deepFreeze'
import type { RobotTargetDefinition } from '../scenario'
import { firstPersonBasisFromYawPitch, generateShotgunPelletDirections } from './pelletPattern'
import { resolvePelletImpacts, type StaticRaycastPort } from './occlusion'
import type { TargetHitZone } from './hitVolumes'

export interface ShotgunPlayerState {
  readonly semanticId: string
  readonly lifeId: string
  readonly position: CollisionVector3
  readonly yaw: number
  readonly pitch: number
}

export interface ShotgunWeaponState {
  readonly semanticId: string
  readonly ammo: number
  readonly shotSequence: number
  readonly nextAllowedShotTick: number
  readonly previousFirePressed: boolean
}

export interface ShotgunTargetState {
  readonly semanticId: string
  readonly health: number
  readonly active: boolean
}

export interface ShotgunTuning {
  readonly eyeHeight: number
  readonly pelletCount: number
  readonly spreadDegrees: number
  readonly rangeMeters: number
  readonly cooldownSeconds: number
  readonly damage: Readonly<Record<TargetHitZone, number>>
}

export type ShotgunSimulationEvent =
  | {
      readonly kind: 'shotgun-shot'
      readonly eventId: string
      readonly tick: number
      readonly shooterId: string
      readonly shotSequence: number
      readonly origin: CollisionVector3
      readonly pelletDirections: readonly CollisionVector3[]
    }
  | {
      readonly kind: 'shotgun-impact'
      readonly eventId: string
      readonly tick: number
      readonly pelletIndex: number
      readonly position: CollisionVector3
      readonly normal: CollisionVector3
      readonly targetId: string | null
      readonly surfaceId: string | null
      readonly zone: TargetHitZone | null
    }
  | {
      readonly kind: 'target-damaged'
      readonly eventId: string
      readonly tick: number
      readonly targetId: string
      readonly damage: number
      readonly healthAfter: number
      readonly zones: readonly TargetHitZone[]
    }
  | {
      readonly kind: 'target-destroyed'
      readonly eventId: string
      readonly tick: number
      readonly targetId: string
    }
  | {
      readonly kind: 'dry-fire'
      readonly eventId: string
      readonly tick: number
      readonly shooterId: string
    }

export interface ShotgunResolution {
  readonly weapon: ShotgunWeaponState
  readonly targets: readonly ShotgunTargetState[]
  readonly events: readonly ShotgunSimulationEvent[]
}

const ZONE_ORDER: Readonly<Record<TargetHitZone, number>> = { head: 0, torso: 1, limb: 2 }

function eventId(tick: number, suffix: string): string {
  return `tick-${tick}:${suffix}`
}

export function calculateZonedPelletDamage(
  counts: Readonly<Record<TargetHitZone, number>>,
  pelletCount: number,
  damage: Readonly<Record<TargetHitZone, number>>,
): number {
  if (!Number.isSafeInteger(pelletCount) || pelletCount < 1) {
    throw new RangeError('pelletCount must be positive')
  }
  return (['head', 'torso', 'limb'] as const).reduce((total, zone) => {
    const count = counts[zone]
    if (!Number.isSafeInteger(count) || count < 0) throw new RangeError(`${zone} count must be non-negative`)
    return total + damage[zone] * count / pelletCount
  }, 0)
}

export function resolveShotgunTrigger(
  tick: number,
  ticksPerSecond: number,
  firePressed: boolean,
  player: ShotgunPlayerState,
  weapon: ShotgunWeaponState,
  targets: readonly ShotgunTargetState[],
  targetDefinitions: readonly RobotTargetDefinition[],
  tuning: ShotgunTuning,
  collision: StaticRaycastPort,
): ShotgunResolution {
  if (!Number.isSafeInteger(tick) || tick < 1) throw new RangeError('tick must be positive')
  if (!Number.isSafeInteger(ticksPerSecond) || ticksPerSecond < 1) {
    throw new RangeError('ticksPerSecond must be positive')
  }
  const sortedTargets = [...targets].sort((first, second) => first.semanticId.localeCompare(second.semanticId))
  const risingEdge = firePressed && !weapon.previousFirePressed
  const withPressedState: ShotgunWeaponState = { ...weapon, previousFirePressed: firePressed }
  if (!risingEdge || tick < weapon.nextAllowedShotTick) {
    return deepFreeze({ weapon: withPressedState, targets: sortedTargets, events: [] })
  }
  if (weapon.ammo === 0) {
    return deepFreeze({
      weapon: withPressedState,
      targets: sortedTargets,
      events: [{
        kind: 'dry-fire',
        eventId: eventId(tick, `dry-fire:${player.semanticId}`),
        tick,
        shooterId: player.semanticId,
      }],
    })
  }

  const shotSequence = weapon.shotSequence + 1
  const origin = {
    x: player.position.x,
    y: player.position.y + tuning.eyeHeight,
    z: player.position.z,
  }
  const pellets = generateShotgunPelletDirections(
    { lifeId: player.lifeId, shotSequence, tick },
    firstPersonBasisFromYawPitch(player.yaw, player.pitch),
    tuning.pelletCount,
    tuning.spreadDegrees,
  )
  const activeTargetIds = new Set(sortedTargets.filter((target) => target.active).map((target) => target.semanticId))
  const impacts = resolvePelletImpacts(
    origin,
    pellets,
    tuning.rangeMeters,
    targetDefinitions,
    activeTargetIds,
    collision,
    [player.semanticId],
  )
  const events: ShotgunSimulationEvent[] = [{
    kind: 'shotgun-shot',
    eventId: eventId(tick, `shot:${player.semanticId}:${shotSequence}`),
    tick,
    shooterId: player.semanticId,
    shotSequence,
    origin,
    pelletDirections: pellets.map((pellet) => pellet.direction),
  }]
  const damageByTarget = new Map<string, { counts: Record<TargetHitZone, number>; zones: Set<TargetHitZone> }>()
  for (const impact of impacts) {
    events.push({
      kind: 'shotgun-impact',
      eventId: eventId(tick, `impact:${impact.pelletIndex}`),
      tick,
      pelletIndex: impact.pelletIndex,
      position: impact.position,
      normal: impact.normal,
      targetId: impact.target?.targetId ?? null,
      surfaceId: impact.surface?.semanticId ?? null,
      zone: impact.target?.zone ?? null,
    })
    if (impact.target !== null) {
      const accumulated = damageByTarget.get(impact.target.targetId) ?? {
        counts: { head: 0, torso: 0, limb: 0 },
        zones: new Set<TargetHitZone>(),
      }
      accumulated.counts[impact.target.zone] += 1
      accumulated.zones.add(impact.target.zone)
      damageByTarget.set(impact.target.targetId, accumulated)
    }
  }

  const nextTargets = sortedTargets.map((target): ShotgunTargetState => {
    const accumulated = damageByTarget.get(target.semanticId)
    if (accumulated === undefined || !target.active) return target
    const damage = calculateZonedPelletDamage(
      accumulated.counts,
      tuning.pelletCount,
      tuning.damage,
    )
    const health = Math.max(0, target.health - damage)
    const active = health > 0
    const zones = [...accumulated.zones].sort((first, second) => ZONE_ORDER[first] - ZONE_ORDER[second])
    events.push({
      kind: 'target-damaged',
      eventId: eventId(tick, `damaged:${target.semanticId}`),
      tick,
      targetId: target.semanticId,
      damage,
      healthAfter: health,
      zones,
    })
    if (!active) {
      events.push({
        kind: 'target-destroyed',
        eventId: eventId(tick, `destroyed:${target.semanticId}`),
        tick,
        targetId: target.semanticId,
      })
    }
    return { ...target, health, active }
  })
  const cooldownTicks = Math.max(1, Math.ceil(tuning.cooldownSeconds * ticksPerSecond))
  return deepFreeze({
    weapon: {
      ...withPressedState,
      ammo: weapon.ammo - 1,
      shotSequence,
      nextAllowedShotTick: tick + cooldownTicks,
    },
    targets: nextTargets,
    events,
  })
}
