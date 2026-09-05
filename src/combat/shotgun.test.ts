import { describe, expect, it, vi } from 'vitest'

import type { RobotTargetDefinition } from '../scenario'
import type { StaticRaycastPort } from './occlusion'
import {
  calculateZonedPelletDamage,
  resolveShotgunTrigger,
  type ShotgunWeaponState,
} from './shotgun'

const targetDefinition: RobotTargetDefinition = {
  semanticId: 'target', name: 'Target', participantKind: 'stationary-fixture', position: { x: 0, y: 0, z: 0 }, yawRadians: 0,
  hitVolumes: [
    { semanticId: 'head', kind: 'sphere', zone: 'head', centerOffset: { x: 0, y: 2, z: 0 }, radius: 2 },
    { semanticId: 'torso', kind: 'box', zone: 'torso', centerOffset: { x: 0, y: -10, z: 0 }, halfExtents: { x: 0.1, y: 0.1, z: 0.1 } },
    { semanticId: 'arm-left', kind: 'capsule', zone: 'limb', startOffset: { x: -10, y: 1, z: 0 }, endOffset: { x: -10, y: 2, z: 0 }, radius: 0.1 },
    { semanticId: 'arm-right', kind: 'capsule', zone: 'limb', startOffset: { x: 10, y: 1, z: 0 }, endOffset: { x: 10, y: 2, z: 0 }, radius: 0.1 },
    { semanticId: 'leg-left', kind: 'capsule', zone: 'limb', startOffset: { x: -10, y: -1, z: 0 }, endOffset: { x: -10, y: 0, z: 0 }, radius: 0.1 },
    { semanticId: 'leg-right', kind: 'capsule', zone: 'limb', startOffset: { x: 10, y: -1, z: 0 }, endOffset: { x: 10, y: 0, z: 0 }, radius: 0.1 },
  ],
}
const player = { semanticId: 'player', lifeId: 'life-1', position: { x: 0, y: 1.45, z: 3 }, yaw: 0, pitch: 0 }
const weapon: ShotgunWeaponState = { semanticId: 'shotgun', ammo: 20, shotSequence: 0, nextAllowedShotTick: 0, previousFirePressed: false }
const targets = [{ semanticId: 'target', lifeId: 'target:life:1', health: 100, active: true }] as const
const tuning = { eyeHeight: 0.55,
  emptyRefillAmmo: 20, pelletCount: 12, spreadDegrees: 0, rangeMeters: 35, cooldownSeconds: 0.7, damage: { head: 100, torso: 70, limb: 30 } } as const
const clearCollision = { raycast: () => [] }

describe('double-barrel shotgun state rules', () => {
  it('consumes one ammo and destroys a target with a complete head blast', () => {
    const raycast = vi.fn((..._arguments: Parameters<StaticRaycastPort['raycast']>) => [])
    const result = resolveShotgunTrigger(
      1,
      60,
      true,
      player,
      weapon,
      targets,
      [targetDefinition],
      tuning,
      { raycast },
    )
    expect(result.weapon).toMatchObject({ ammo: 19, shotSequence: 1, previousFirePressed: true })
    expect(result.targets[0]).toMatchObject({ health: 0, active: false })
    expect(result.events.filter((event) => event.kind === 'shotgun-impact')).toHaveLength(12)
    expect(result.events.some((event) => event.kind === 'target-destroyed')).toBe(true)
    expect(raycast).toHaveBeenCalledTimes(tuning.pelletCount)
    expect(raycast.mock.calls.every((call) =>
      call[3]?.includes(player.semanticId) === true && call[3]?.includes(targetDefinition.semanticId) === true
    )).toBe(true)
  })

  it('does not repeat while held or before cooldown expires', () => {
    const first = resolveShotgunTrigger(1, 60, true, player, weapon, targets, [targetDefinition], tuning, clearCollision)
    const held = resolveShotgunTrigger(2, 60, true, player, first.weapon, first.targets, [targetDefinition], tuning, clearCollision)
    expect(held.weapon.ammo).toBe(19)
    const released = resolveShotgunTrigger(3, 60, false, player, held.weapon, held.targets, [targetDefinition], tuning, clearCollision)
    const tooSoon = resolveShotgunTrigger(4, 60, true, player, released.weapon, released.targets, [targetDefinition], tuning, clearCollision)
    expect(tooSoon.weapon.ammo).toBe(19)
    expect(tooSoon.events).toEqual([])
  })

  it('refills a zero state before firing and ignores inactive targets', () => {
    const empty = { ...weapon, ammo: 0 }
    const dry = resolveShotgunTrigger(1, 60, true, player, empty, [{ ...targets[0], active: false }], [targetDefinition], tuning, clearCollision)
    expect(dry.weapon.shotSequence).toBe(1)
    expect(dry.weapon.ammo).toBe(19)
    expect(dry.events[0]).toMatchObject({ kind: 'shotgun-shot', shooterId: 'player' })
    expect(dry.targets[0]?.health).toBe(100)

    const postDestroy = resolveShotgunTrigger(
      1,
      60,
      true,
      player,
      weapon,
      [{ ...targets[0], health: 0, active: false }],
      [targetDefinition],
      tuning,
      clearCollision,
    )
    expect(postDestroy.targets[0]).toMatchObject({ health: 0, active: false })
    expect(postDestroy.events.some((event) => event.kind === 'target-damaged')).toBe(false)
  })

  it('reduces torso, limb, and mixed pellet damage by the configured pellet count', () => {
    expect(calculateZonedPelletDamage(
      { head: 0, torso: 12, limb: 0 },
      12,
      tuning.damage,
    )).toBe(70)
    expect(calculateZonedPelletDamage(
      { head: 0, torso: 0, limb: 12 },
      12,
      tuning.damage,
    )).toBe(30)
    expect(calculateZonedPelletDamage(
      { head: 1, torso: 1, limb: 1 },
      3,
      tuning.damage,
    )).toBeCloseTo((100 + 70 + 30) / 3)
  })

  it('automatically refills after the twentieth shot and preserves trigger cadence', () => {
    let currentWeapon = weapon
    let tick = 1
    let fired = 0
    for (let index = 0; index < 20; index += 1) {
      const shot = resolveShotgunTrigger(tick, 60, true, player, currentWeapon, [], [], tuning, clearCollision)
      currentWeapon = shot.weapon
      fired += shot.events.some((event) => event.kind === 'shotgun-shot') ? 1 : 0
      currentWeapon = resolveShotgunTrigger(tick + 1, 60, false, player, currentWeapon, [], [], tuning, clearCollision).weapon
      tick = currentWeapon.nextAllowedShotTick
    }
    expect(fired).toBe(20)
    expect(currentWeapon.ammo).toBe(20)
    const dry = resolveShotgunTrigger(tick + 1, 60, true, player, currentWeapon, [], [], tuning, clearCollision)
    expect(dry.events[0]?.kind).toBe('shotgun-shot')
    expect(dry.weapon.ammo).toBe(19)
  })
})
