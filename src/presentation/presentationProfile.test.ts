import { describe, expect, it } from 'vitest'

import { DIAGNOSTIC_ARENA } from '../arena'
import { canonicalSerialize, type ContentHash } from '../core/canonical'
import { PROTOTYPE_V1_PROFILE } from '../profiles'
import {
  createInitialPlayableSimulationSnapshot,
  createPlayableSimulationReplay,
  playableSimulationSnapshotHash,
  serializePlayableSimulationReplay,
} from '../simulation'
import {
  PRESENTATION_BALANCED_V1_PROFILE,
  PRESENTATION_PROFILE_DESCRIPTORS,
  canonicalPresentationProfileJson,
  computePresentationProfileContentHash,
  validatePresentationProfile,
  type PresentationProfile,
} from './presentationProfile'

function cloneProfile(): PresentationProfile {
  return JSON.parse(JSON.stringify(PRESENTATION_BALANCED_V1_PROFILE)) as PresentationProfile
}

function expectDeepFrozen(value: unknown): void {
  if (typeof value !== 'object' || value === null) return
  expect(Object.isFrozen(value)).toBe(true)
  for (const child of Object.values(value)) expectDeepFrozen(child)
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

describe('presentation-balanced-v1 PresentationProfile', () => {
  it('ships the approved identity and balanced defaults', () => {
    expect(PRESENTATION_BALANCED_V1_PROFILE).toMatchObject({
      schemaVersion: 1,
      id: 'presentation-balanced-v1',
      revision: 2,
      source: 'shipped',
      render: { backingBufferPixelBudget: 2_073_600 },
      ui: { maximumPublicationsPerSecond: 10 },
    })
    expect(validatePresentationProfile(PRESENTATION_BALANCED_V1_PROFILE)).toMatchObject({ ok: true })
  })

  it('has exactly one complete descriptor for each numeric presentation value', () => {
    const paths = [...Object.keys(PRESENTATION_BALANCED_V1_PROFILE.render).map(key=>`render.${key}`),...Object.keys(PRESENTATION_BALANCED_V1_PROFILE.ui).map(key=>`ui.${key}`)]
    expect(PRESENTATION_PROFILE_DESCRIPTORS.map(d=>d.path).sort()).toEqual(paths.sort())
    expect(new Set(PRESENTATION_PROFILE_DESCRIPTORS.map(d=>d.path)).size).toBe(paths.length)
    for (const descriptor of PRESENTATION_PROFILE_DESCRIPTORS) {
      expect(descriptor.label).not.toHaveLength(0)
      expect(descriptor.description).not.toHaveLength(0)
      expect(descriptor.minimum).toBeLessThanOrEqual(descriptor.maximum)
      expect(descriptor.step).toBeGreaterThan(0)
    }
  })

  it('deep-freezes the shipped profile, descriptors and validated local snapshots', () => {
    expectDeepFrozen(PRESENTATION_BALANCED_V1_PROFILE)
    expectDeepFrozen(PRESENTATION_PROFILE_DESCRIPTORS)

    const input = cloneProfile()
    input.source = 'local'
    input.revision = 2
    input.contentHash = computePresentationProfileContentHash(input)
    const result = validatePresentationProfile(input)
    expect(result.ok).toBe(true)
    if (!result.ok) throw new Error('Valid local presentation profile was rejected')

    expectDeepFrozen(result.profile)
    expect(result.profile).not.toBe(input)
    input.render.backingBufferPixelBudget = 230_400
    expect(result.profile.render.backingBufferPixelBudget).toBe(2_073_600)
    expect(Reflect.set(result.profile.ui, 'maximumPublicationsPerSecond', 60)).toBe(false)
  })

  it('reports stable missing, type, range and step validation errors', () => {
    const missing = cloneProfile() as unknown as { render: Record<string, unknown> }
    delete missing.render.backingBufferPixelBudget
    expect(validatePresentationProfile(missing)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({
          code: 'MISSING_FIELD',
          paths: ['render.backingBufferPixelBudget'],
        }),
      ]),
    })

    const invalidType = cloneProfile() as unknown as { ui: { maximumPublicationsPerSecond: unknown } }
    invalidType.ui.maximumPublicationsPerSecond = '10'
    expect(validatePresentationProfile(invalidType)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({
          code: 'INVALID_TYPE',
          paths: ['ui.maximumPublicationsPerSecond'],
        }),
      ]),
    })

    const outOfRange = cloneProfile()
    outOfRange.render.backingBufferPixelBudget = 8_352_000
    expect(validatePresentationProfile(outOfRange)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({
          code: 'OUT_OF_RANGE',
          paths: ['render.backingBufferPixelBudget'],
        }),
      ]),
    })

    const invalidStep = cloneProfile()
    invalidStep.ui.maximumPublicationsPerSecond = 10.5
    expect(validatePresentationProfile(invalidStep)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({
          code: 'INVALID_STEP',
          paths: ['ui.maximumPublicationsPerSecond'],
        }),
      ]),
    })
  })

  it('rejects fields outside the presentation schema and descriptor registry', () => {
    const extra = cloneProfile() as PresentationProfile & {
      render: PresentationProfile['render'] & { dynamicResolution: number }
    }
    extra.render.dynamicResolution = 1

    expect(validatePresentationProfile(extra)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({
          code: 'UNRECOGNIZED_FIELD',
          paths: ['render.dynamicResolution'],
        }),
      ]),
    })
  })

  it('derives a stable canonical identity from all effective presentation content', () => {
    expect(PRESENTATION_BALANCED_V1_PROFILE.contentHash).toMatch(/^fnv1a64-v1:[0-9a-f]{16}$/)
    expect(PRESENTATION_BALANCED_V1_PROFILE.contentHash).toBe(
      'fnv1a64-v1:30cb0b96ac4a99ff',
    )
    expect(computePresentationProfileContentHash(cloneProfile())).toBe(
      PRESENTATION_BALANCED_V1_PROFILE.contentHash,
    )

    const reordered = reverseObjectKeys(PRESENTATION_BALANCED_V1_PROFILE) as PresentationProfile
    expect(computePresentationProfileContentHash(reordered)).toBe(
      PRESENTATION_BALANCED_V1_PROFILE.contentHash,
    )
    expect(canonicalPresentationProfileJson(reordered)).toBe(
      canonicalPresentationProfileJson(PRESENTATION_BALANCED_V1_PROFILE as PresentationProfile),
    )

    const changed = cloneProfile()
    changed.ui.maximumPublicationsPerSecond = 20
    expect(computePresentationProfileContentHash(changed)).not.toBe(
      PRESENTATION_BALANCED_V1_PROFILE.contentHash,
    )

    const wrongHash = cloneProfile()
    wrongHash.contentHash = 'fnv1a64-v1:0000000000000000'
    expect(validatePresentationProfile(wrongHash)).toMatchObject({
      ok: false,
      errors: [expect.objectContaining({ code: 'CONTENT_HASH_MISMATCH', paths: ['contentHash'] })],
    })
  })

  it('does not enter playable snapshot, replay identity or state hash', () => {
    const snapshot = createInitialPlayableSimulationSnapshot({
      profile: PROTOTYPE_V1_PROFILE,
      arena: {
        id: DIAGNOSTIC_ARENA.id,
        seed: DIAGNOSTIC_ARENA.seed,
        generatorVersion: DIAGNOSTIC_ARENA.generatorVersion,
        contentHash: DIAGNOSTIC_ARENA.contentHash as ContentHash,
      },
      rngSeed: 1,
    })
    const serializedSnapshot = canonicalSerialize(snapshot)
    const serializedReplay = serializePlayableSimulationReplay(
      createPlayableSimulationReplay(snapshot, []),
    )
    const hashBefore = playableSimulationSnapshotHash(snapshot)

    const localPresentation = cloneProfile()
    localPresentation.source = 'local'
    localPresentation.revision = 2
    localPresentation.render.backingBufferPixelBudget = 921_600
    localPresentation.contentHash = computePresentationProfileContentHash(localPresentation)
    expect(validatePresentationProfile(localPresentation)).toMatchObject({ ok: true })

    expect(serializedSnapshot).not.toContain(PRESENTATION_BALANCED_V1_PROFILE.id)
    expect(serializedSnapshot).not.toContain(PRESENTATION_BALANCED_V1_PROFILE.contentHash)
    expect(serializedSnapshot).not.toContain(localPresentation.id)
    expect(serializedSnapshot).not.toContain(localPresentation.contentHash)
    expect(serializedReplay).not.toContain(PRESENTATION_BALANCED_V1_PROFILE.id)
    expect(serializedReplay).not.toContain(PRESENTATION_BALANCED_V1_PROFILE.contentHash)
    expect(serializedReplay).not.toContain(localPresentation.id)
    expect(serializedReplay).not.toContain(localPresentation.contentHash)
    expect(playableSimulationSnapshotHash(snapshot)).toBe(hashBefore)
  })
})
