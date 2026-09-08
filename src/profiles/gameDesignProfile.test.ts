import { describe, expect, it } from 'vitest'

import {
  GAME_DESIGN_PROFILE_DESCRIPTORS,
  PROTOTYPE_V1_PROFILE,
  canonicalProfileJson,
  computeProfileContentHash,
  validateGameDesignProfile,
  type GameDesignProfile,
} from './gameDesignProfile'

function cloneProfile(): GameDesignProfile {
  return JSON.parse(JSON.stringify(PROTOTYPE_V1_PROFILE)) as GameDesignProfile
}

function collectNumericPaths(value: unknown, prefix: string): string[] {
  if (typeof value === 'number') return [prefix]
  if (Array.isArray(value)) {
    return value.flatMap((child, index) => collectNumericPaths(child, `${prefix}.${index}`))
  }
  if (typeof value === 'object' && value !== null) {
    return Object.entries(value).flatMap(([key, child]) =>
      collectNumericPaths(child, prefix.length === 0 ? key : `${prefix}.${key}`),
    )
  }
  return []
}

function reverseObjectKeys(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(reverseObjectKeys)
  if (typeof value !== 'object' || value === null) return value
  return Object.fromEntries(
    Object.entries(value)
      .reverse()
      .map(([key, child]) => [key, reverseObjectKeys(child)]),
  )
}

function expectDeepFrozen(value: unknown): void {
  if (typeof value !== 'object' || value === null) return
  expect(Object.isFrozen(value)).toBe(true)
  for (const child of Object.values(value)) expectDeepFrozen(child)
}

describe('prototype-v1 GameDesignProfile', () => {
  it('contains every approved numeric prototype rule', () => {
    expect(PROTOTYPE_V1_PROFILE).toMatchObject({
      schemaVersion: 10,
      id: 'prototype-v1',
      revision: 11,
      source: 'shipped',
      rules: {
        player: {
          startingHealth: 100,
          capsule: { radius: 0.4, halfHeight: 0.5, skinWidth: 0.01 },
          eyeHeight: 0.55,
          movement: {
            maximumGroundSpeed: 9,
            groundAcceleration: 45,
            groundDeceleration: 32,
            airAcceleration: 12,
            gravity: 24,
            jumpSpeed: 8.5,
            autostepMaxHeight: 0.45,
            autostepMinWidth: 0.25,
            snapToGroundDistance: 0.2,
            maxSlopeClimbDegrees: 45,
            minSlopeSlideDegrees: 60,
          },
        },
        camera: {
          horizontalSensitivity: 4,
          verticalSensitivity: 3.2,
          maximumPitchDegrees: 85,
          fieldOfViewDegrees: 90,
          nearPlane: 0.05,
          farPlane: 250,
        },
        weapons: {
          doubleBarrelShotgun: {
            startingAmmo: 20,
            pelletCount: 12,
            spreadDegrees: 7,
            rangeMeters: 35,
            cooldownSeconds: 0.7,
            muzzleFeedbackSeconds: 0.08,
            impactFeedbackSeconds: 0.12,
            hitmarkerSeconds: 0.15,
            damage: { head: 100, torso: 70, limb: 30 },
          },
        },
        match: {
          duration: { defaultMinutes: 5, minimumMinutes: 1, maximumMinutes: 30 },
          scoreTarget: {
            defaultPoints: 3_000,
            minimumPoints: 1_000,
            maximumPoints: 20_000,
            stepPoints: 100,
          },
        },
        scoring: {
          assist: { windowSeconds: 5, points: 50 },
          killChain: {
            maximumGapSeconds: 10,
            cumulativeTotals: [100, 300, 500, 800, 1_200],
            postFiveIncrementPoints: 400,
          },
        },
        death: { killcamSeconds: 3, corpseSeconds: 10 },
        arenaGenerator: {
          presets: {
            small: { widthMeters: 34, depthMeters: 26, corridorWidthMeters: 4, doorwayWidthMeters: 3, minimumSpawnSeparationMeters: 4, spawnSlotCount: 16 },
            medium: { widthMeters: 46, depthMeters: 34, corridorWidthMeters: 5, doorwayWidthMeters: 3.5, minimumSpawnSeparationMeters: 5, spawnSlotCount: 16 },
            large: { widthMeters: 60, depthMeters: 44, corridorWidthMeters: 6, doorwayWidthMeters: 4, minimumSpawnSeparationMeters: 6, spawnSlotCount: 20 },
          },
        },
      },
    })
    expect(validateGameDesignProfile(PROTOTYPE_V1_PROFILE)).toMatchObject({ ok: true })
  })

  it('has exactly one complete descriptor for every numeric rule', () => {
    const numericPaths = collectNumericPaths(PROTOTYPE_V1_PROFILE.rules, 'rules').sort()
    const descriptorPaths = GAME_DESIGN_PROFILE_DESCRIPTORS.map(({ path }) => path).sort()

    expect(descriptorPaths).toEqual(numericPaths)
    expect(new Set(descriptorPaths).size).toBe(descriptorPaths.length)
    for (const descriptor of GAME_DESIGN_PROFILE_DESCRIPTORS) {
      expect(descriptor.path).not.toHaveLength(0)
      expect(descriptor.group).not.toHaveLength(0)
      expect(descriptor.label).not.toHaveLength(0)
      expect(descriptor.description).not.toHaveLength(0)
      expect(descriptor.unit).not.toHaveLength(0)
      expect(descriptor.minimum).toBeLessThanOrEqual(descriptor.maximum)
      expect(descriptor.step).toBeGreaterThan(0)
    }
  })

  it('deep-freezes shipped rules and descriptor metadata', () => {
    expectDeepFrozen(PROTOTYPE_V1_PROFILE)
    expectDeepFrozen(GAME_DESIGN_PROFILE_DESCRIPTORS)

    const rules = PROTOTYPE_V1_PROFILE.rules as unknown as Record<string, unknown>
    expect(Reflect.set(rules, 'player', { startingHealth: 1 })).toBe(false)
    expect(PROTOTYPE_V1_PROFILE.rules.player.startingHealth).toBe(100)
  })

  it('returns a detached immutable snapshot for a valid local profile', () => {
    const input = cloneProfile()
    input.source = 'local'
    input.revision = 4
    input.contentHash = computeProfileContentHash(input)

    const result = validateGameDesignProfile(input)
    expect(result.ok).toBe(true)
    if (!result.ok) throw new Error('Valid local profile was rejected')

    expectDeepFrozen(result.profile)
    expect(result.profile).not.toBe(input)
    input.rules.player.startingHealth = 1
    expect(result.profile.rules.player.startingHealth).toBe(100)
    expect(Reflect.set(result.profile.rules.player, 'startingHealth', 1)).toBe(false)
  })

  it('reports stable paths for missing, range and step errors', () => {
    const missing = cloneProfile() as unknown as { rules: { player: Record<string, unknown> } }
    delete missing.rules.player.startingHealth
    expect(validateGameDesignProfile(missing)).toMatchObject({
      ok: false,
      errors: [
        expect.objectContaining({ code: 'MISSING_FIELD', paths: ['rules.player.startingHealth'] }),
      ],
    })

    const outOfRange = cloneProfile()
    outOfRange.rules.weapons.doubleBarrelShotgun.pelletCount = 65
    expect(validateGameDesignProfile(outOfRange)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({
          code: 'OUT_OF_RANGE',
          paths: ['rules.weapons.doubleBarrelShotgun.pelletCount'],
        }),
      ]),
    })

    const invalidStep = cloneProfile()
    invalidStep.rules.player.capsule.radius = 0.405
    expect(validateGameDesignProfile(invalidStep)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({ code: 'INVALID_STEP', paths: ['rules.player.capsule.radius'] }),
      ]),
    })
  })

  it('rejects fields outside the complete schema and descriptor registry', () => {
    const extraRule = cloneProfile() as GameDesignProfile & {
      rules: GameDesignProfile['rules'] & { hiddenModifier: number }
    }
    extraRule.rules.hiddenModifier = 2
    expect(validateGameDesignProfile(extraRule)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({ code: 'UNRECOGNIZED_FIELD', paths: ['rules.hiddenModifier'] }),
      ]),
    })

    const extraTotal = cloneProfile()
    ;(extraTotal.rules.scoring.killChain.cumulativeTotals as number[]).push(1_600)
    expect(validateGameDesignProfile(extraTotal)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({
          code: 'UNRECOGNIZED_FIELD',
          paths: ['rules.scoring.killChain.cumulativeTotals.5'],
        }),
      ]),
    })
  })

  it('rejects deterministic cross-field violations with every related path', () => {
    const invalidDuration = cloneProfile()
    invalidDuration.rules.match.duration.defaultMinutes = 15
    invalidDuration.rules.match.duration.maximumMinutes = 10
    expect(validateGameDesignProfile(invalidDuration)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({
          code: 'MATCH_DURATION_ORDER',
          paths: [
            'rules.match.duration.minimumMinutes',
            'rules.match.duration.defaultMinutes',
            'rules.match.duration.maximumMinutes',
          ],
        }),
      ]),
    })

    const invalidTarget = cloneProfile()
    invalidTarget.rules.match.scoreTarget.defaultPoints = 3_050
    invalidTarget.contentHash = computeProfileContentHash(invalidTarget)
    expect(validateGameDesignProfile(invalidTarget)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({
          code: 'MATCH_SCORE_TARGET_ORDER',
          paths: [
            'rules.match.scoreTarget.minimumPoints',
            'rules.match.scoreTarget.defaultPoints',
            'rules.match.scoreTarget.maximumPoints',
            'rules.match.scoreTarget.stepPoints',
          ],
        }),
      ]),
    })

    const invalidTotals = cloneProfile()
    invalidTotals.rules.scoring.killChain.cumulativeTotals = [100, 300, 300, 800, 1_200]
    expect(validateGameDesignProfile(invalidTotals)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({
          code: 'KILL_CHAIN_TOTALS_NOT_INCREASING',
          paths: [
            'rules.scoring.killChain.cumulativeTotals.0',
            'rules.scoring.killChain.cumulativeTotals.1',
            'rules.scoring.killChain.cumulativeTotals.2',
            'rules.scoring.killChain.cumulativeTotals.3',
            'rules.scoring.killChain.cumulativeTotals.4',
          ],
        }),
      ]),
    })

    const invalidCapsule = cloneProfile()
    invalidCapsule.rules.player.capsule.radius = 0.2
    invalidCapsule.rules.player.capsule.skinWidth = 0.2
    expect(validateGameDesignProfile(invalidCapsule)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({
          code: 'CAPSULE_GEOMETRY_INVALID',
          paths: ['rules.player.capsule.radius', 'rules.player.capsule.skinWidth'],
        }),
      ]),
    })

    const invalidEye = cloneProfile()
    invalidEye.rules.player.eyeHeight = 1
    expect(validateGameDesignProfile(invalidEye)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({
          code: 'EYE_HEIGHT_OUTSIDE_CAPSULE',
          paths: [
            'rules.player.eyeHeight',
            'rules.player.capsule.halfHeight',
            'rules.player.capsule.radius',
            'rules.player.capsule.skinWidth',
          ],
        }),
      ]),
    })

    const invalidCamera = cloneProfile()
    invalidCamera.rules.camera.nearPlane = 10
    invalidCamera.rules.camera.farPlane = 10
    expect(validateGameDesignProfile(invalidCamera)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({
          code: 'CAMERA_PLANES_ORDER',
          paths: ['rules.camera.nearPlane', 'rules.camera.farPlane'],
        }),
      ]),
    })

    const invalidSlope = cloneProfile()
    invalidSlope.rules.player.movement.maxSlopeClimbDegrees = 70
    invalidSlope.rules.player.movement.minSlopeSlideDegrees = 60
    expect(validateGameDesignProfile(invalidSlope)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({
          code: 'SLOPE_ANGLE_ORDER',
          paths: [
            'rules.player.movement.maxSlopeClimbDegrees',
            'rules.player.movement.minSlopeSlideDegrees',
          ],
        }),
      ]),
    })

    const invalidWeapon = cloneProfile()
    invalidWeapon.rules.weapons.doubleBarrelShotgun.cooldownSeconds = 0
    expect(validateGameDesignProfile(invalidWeapon)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({
          code: 'SHOTGUN_CONTRACT_INVALID',
          paths: [
            'rules.weapons.doubleBarrelShotgun.pelletCount',
            'rules.weapons.doubleBarrelShotgun.rangeMeters',
            'rules.weapons.doubleBarrelShotgun.cooldownSeconds',
          ],
        }),
      ]),
    })

    const invalidArena = cloneProfile()
    invalidArena.rules.arenaGenerator.presets.small.routeInsetMeters = 14
    expect(validateGameDesignProfile(invalidArena)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({
          code: 'ARENA_GENERATOR_CONTRACT_INVALID',
          paths: expect.arrayContaining([
            'rules.arenaGenerator.presets.small.routeInsetMeters',
            'rules.arenaGenerator.presets.small.widthMeters',
          ]),
        }),
      ]),
    })

    const invalidDoorway = cloneProfile()
    invalidDoorway.rules.arenaGenerator.presets.small.doorwayWidthMeters = 4
    expect(validateGameDesignProfile(invalidDoorway)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({
          code: 'ARENA_GENERATOR_CONTRACT_INVALID',
          paths: expect.arrayContaining([
            'rules.arenaGenerator.presets.small.corridorWidthMeters',
            'rules.arenaGenerator.presets.small.doorwayWidthMeters',
            'rules.arenaGenerator.presets.small.wallThicknessMeters',
          ]),
        }),
      ]),
    })
  })

  it('derives identity from all effective content except contentHash', () => {
    expect(PROTOTYPE_V1_PROFILE.contentHash).toMatch(/^fnv1a64-v1:[0-9a-f]{16}$/)
    expect(PROTOTYPE_V1_PROFILE.contentHash).toBe('fnv1a64-v1:175b900d6f85f32c')
    expect(computeProfileContentHash(PROTOTYPE_V1_PROFILE as GameDesignProfile)).toBe(
      PROTOTYPE_V1_PROFILE.contentHash,
    )

    const reordered = reverseObjectKeys(PROTOTYPE_V1_PROFILE) as GameDesignProfile
    expect(computeProfileContentHash(reordered)).toBe(PROTOTYPE_V1_PROFILE.contentHash)

    const changed = cloneProfile()
    changed.rules.player.startingHealth += 1
    expect(computeProfileContentHash(changed)).not.toBe(PROTOTYPE_V1_PROFILE.contentHash)

    const wrongHash = cloneProfile()
    wrongHash.contentHash = 'fnv1a64-v1:0000000000000000'
    expect(validateGameDesignProfile(wrongHash)).toMatchObject({
      ok: false,
      errors: [expect.objectContaining({ code: 'CONTENT_HASH_MISMATCH', paths: ['contentHash'] })],
    })
  })

  it('has a canonical JSON representation independent of insertion order', () => {
    const reordered = reverseObjectKeys(PROTOTYPE_V1_PROFILE) as GameDesignProfile
    expect(canonicalProfileJson(reordered)).toBe(canonicalProfileJson(PROTOTYPE_V1_PROFILE as GameDesignProfile))
  })
})
