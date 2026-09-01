import { describe, expect, it } from 'vitest'

import { PROTOTYPE_V1_PROFILE } from '../profiles/gameDesignProfile'
import {
  GameplayUiProjector,
  createGameplayUiSnapshot,
  type GameplayUiSourceSnapshot,
} from './gameplayUiSnapshot'

describe('createGameplayUiSnapshot', () => {
  it('returns detached deeply immutable presentation data', () => {
    const input = {
      status: 'running',
      health: 100,
      maximumHealth: 100,
      weaponName: 'Двухстволка',
      ammo: 20,
      crosshairFeedback: 'idle',
      debug: {
        tick: 1,
        stateHash: 'state',
        profileIdentity: 'profile',
        arenaIdentity: 'arena',
        collisionIdentity: 'collision',
        playerPosition: { x: 1, y: 2, z: 3 },
        playerYawDegrees: 45,
        playerPitchDegrees: -10,
        playerGrounded: true,
        ammo: 20,
        targets: [{ semanticId: 'target-a', health: 100, active: true }],
      },
    } as const

    const result = createGameplayUiSnapshot(input)

    expect(result).toEqual(input)
    expect(result).not.toBe(input)
    expect(Object.isFrozen(result)).toBe(true)
    expect(Object.isFrozen(result.debug)).toBe(true)
    expect(Object.isFrozen(result.debug?.playerPosition)).toBe(true)
    expect(Object.isFrozen(result.debug?.targets)).toBe(true)
    expect(Object.isFrozen(result.debug?.targets[0])).toBe(true)
    expect(Reflect.set(result, 'ammo', 0)).toBe(false)
  })

  it('rejects impossible HUD values before rendering', () => {
    const valid = {
      status: 'running',
      health: 100,
      maximumHealth: 100,
      weaponName: 'Двухстволка',
      ammo: 20,
      crosshairFeedback: 'idle',
    } as const

    expect(() => createGameplayUiSnapshot({ ...valid, health: 101 })).toThrow(/maximumHealth/)
    expect(() => createGameplayUiSnapshot({ ...valid, ammo: -1 })).toThrow(/ammo/)
    expect(() => createGameplayUiSnapshot({ ...valid, weaponName: '' })).toThrow(/weaponName/)
    expect(() => createGameplayUiSnapshot({ ...valid, status: 'error' })).toThrow(/errorMessage/)
  })

  it('rejects invalid debug acceptance telemetry', () => {
    const valid = {
      status: 'running',
      health: 100,
      maximumHealth: 100,
      weaponName: 'Двухстволка',
      ammo: 20,
      crosshairFeedback: 'idle',
      debug: {
        tick: 1,
        stateHash: 'state',
        profileIdentity: 'profile',
        arenaIdentity: 'arena',
        collisionIdentity: 'collision',
        playerPosition: { x: 0, y: 0, z: 0 },
        playerYawDegrees: 0,
        playerPitchDegrees: 0,
        playerGrounded: true,
        ammo: 20,
        targets: [{ semanticId: 'target-a', health: 100, active: true }],
      },
    } as const

    expect(() => createGameplayUiSnapshot({
      ...valid,
      debug: { ...valid.debug, playerYawDegrees: Number.NaN },
    })).toThrow(/playerYawDegrees/)
    expect(() => createGameplayUiSnapshot({
      ...valid,
      debug: { ...valid.debug, targets: [{ semanticId: '', health: 100, active: true }] },
    })).toThrow(/semanticId/)
  })

  it('projects playable state and retains profile-timed hit and dry-fire feedback', () => {
    const projector = new GameplayUiProjector({ profile: PROTOTYPE_V1_PROFILE })
    const source = (
      tick: number,
      kind?: 'target-damaged' | 'dry-fire',
    ): GameplayUiSourceSnapshot => ({
      tick,
      player: { health: 70 },
      weapon: { ammo: 12 },
      events: kind === undefined ? [] : [{ eventId: `event-${tick}`, tick, kind }],
    })

    const hit = projector.project(source(5, 'target-damaged'), 'running')
    expect(hit).toMatchObject({
      status: 'running',
      health: 70,
      maximumHealth: 100,
      ammo: 12,
      crosshairFeedback: 'hit',
    })
    expect(projector.project(source(14), 'running').crosshairFeedback).toBe('hit')
    expect(projector.project(source(15), 'running').crosshairFeedback).toBe('idle')

    expect(projector.project(source(20, 'dry-fire'), 'running').crosshairFeedback).toBe('dry-fire')
    expect(projector.project(source(1), 'start').crosshairFeedback).toBe('idle')
  })
})
