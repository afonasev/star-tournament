import { describe, expect, it } from 'vitest'

import {
  canonicalArenaJson,
  computeArenaContentHash,
  parseArenaDefinition,
  validateArenaDefinition,
  type ArenaDefinition,
} from './arenaDefinition'
import { DIAGNOSTIC_ARENA } from './diagnosticArena'

function cloneArena(): ArenaDefinition {
  return JSON.parse(JSON.stringify(DIAGNOSTIC_ARENA)) as ArenaDefinition
}

function expectPureSerializableData(value: unknown): void {
  expect(['function', 'symbol', 'bigint', 'undefined']).not.toContain(typeof value)
  if (Array.isArray(value)) {
    value.forEach(expectPureSerializableData)
    return
  }
  if (typeof value === 'object' && value !== null) {
    expect(Object.getPrototypeOf(value)).toBe(Object.prototype)
    Object.values(value).forEach(expectPureSerializableData)
  }
}

describe('diagnostic ArenaDefinition', () => {
  it('is valid versioned pure data with stable identity', () => {
    expect(DIAGNOSTIC_ARENA).toMatchObject({
      schemaVersion: 2,
      id: 'diagnostic-arena',
      seed: 0x53544152,
      generatorVersion: 'diagnostic-v1',
    })
    expect(DIAGNOSTIC_ARENA.surfaces.length).toBeGreaterThan(0)
    expect(DIAGNOSTIC_ARENA.surfaces.every((surface) =>
      surface.orientation.x === 0 &&
      surface.orientation.y === 0 &&
      surface.orientation.z === 0 &&
      surface.orientation.w === 1,
    )).toBe(true)
    expect(DIAGNOSTIC_ARENA.spawnAnchors).toHaveLength(4)
    expect(DIAGNOSTIC_ARENA.contentHash).toMatch(/^fnv1a64-v1:[0-9a-f]{16}$/)
    expect(DIAGNOSTIC_ARENA.contentHash).toBe('fnv1a64-v1:f9a2e251dc0be63d')
    expect(validateArenaDefinition(DIAGNOSTIC_ARENA)).toMatchObject({ ok: true })
    expect(computeArenaContentHash(DIAGNOSTIC_ARENA as ArenaDefinition)).toBe(
      DIAGNOSTIC_ARENA.contentHash,
    )
    expectPureSerializableData(DIAGNOSTIC_ARENA)
  })

  it('round-trips through canonical JSON without changing identity', () => {
    const serialized = canonicalArenaJson(DIAGNOSTIC_ARENA as ArenaDefinition)
    const parsed = parseArenaDefinition(serialized)

    expect(parsed).toMatchObject({ ok: true })
    if (!parsed.ok) throw new Error('Canonical arena did not parse')
    expect(parsed.arena).toEqual(DIAGNOSTIC_ARENA)
    expect(parsed.arena.contentHash).toBe(DIAGNOSTIC_ARENA.contentHash)
    expect(canonicalArenaJson(parsed.arena)).toBe(serialized)
    expect(Object.isFrozen(parsed.arena)).toBe(true)
    expect(Object.isFrozen(parsed.arena.surfaces)).toBe(true)
  })

  it('detaches a validated arena from mutable input', () => {
    const input = cloneArena()
    const result = validateArenaDefinition(input)
    expect(result.ok).toBe(true)
    if (!result.ok) throw new Error('Valid arena was rejected')

    input.spawnAnchors[0]!.position.x = 99
    expect(result.arena.spawnAnchors[0]!.position.x).not.toBe(99)
    expect(Reflect.set(result.arena.spawnAnchors[0]!.position, 'x', 99)).toBe(false)
  })

  it('changes identity when semantic arena data changes', () => {
    const changed = cloneArena()
    changed.surfaces[0]!.orientation = {
      x: 0,
      y: Math.SQRT1_2,
      z: 0,
      w: Math.SQRT1_2,
    }
    expect(computeArenaContentHash(changed)).not.toBe(DIAGNOSTIC_ARENA.contentHash)
  })

  it('requires a finite normalized orientation quaternion on every primitive', () => {
    const missing = cloneArena()
    delete (missing.surfaces[0] as unknown as { orientation?: unknown }).orientation
    expect(validateArenaDefinition(missing)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({ code: 'MISSING_FIELD', paths: ['surfaces.0.orientation'] }),
      ]),
    })

    const nonFinite = cloneArena()
    nonFinite.surfaces[0]!.orientation.w = Number.POSITIVE_INFINITY
    expect(validateArenaDefinition(nonFinite)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({ code: 'INVALID_NUMBER', paths: ['surfaces.0.orientation.w'] }),
      ]),
    })

    const notNormalized = cloneArena()
    notNormalized.surfaces[0]!.orientation = { x: 0, y: 0, z: 0, w: 0.5 }
    expect(validateArenaDefinition(notNormalized)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({ code: 'INVALID_ORIENTATION', paths: ['surfaces.0.orientation'] }),
      ]),
    })
  })

  it('rejects invalid geometry, duplicate ids and stale hashes', () => {
    const invalid = cloneArena()
    invalid.surfaces[0]!.size.y = 0
    invalid.spawnAnchors[1]!.id = invalid.spawnAnchors[0]!.id

    expect(validateArenaDefinition(invalid)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({ code: 'INVALID_SURFACE_SIZE', paths: ['surfaces.0.size.y'] }),
        expect.objectContaining({
          code: 'DUPLICATE_ID',
          paths: ['spawnAnchors.0.id', 'spawnAnchors.1.id'],
        }),
      ]),
    })

    const staleHash = cloneArena()
    staleHash.seed += 1
    expect(validateArenaDefinition(staleHash)).toMatchObject({
      ok: false,
      errors: [expect.objectContaining({ code: 'CONTENT_HASH_MISMATCH', paths: ['contentHash'] })],
    })
  })

  it('rejects malformed JSON and missing required fields', () => {
    expect(parseArenaDefinition('{')).toMatchObject({
      ok: false,
      errors: [{ code: 'INVALID_JSON', paths: ['$'] }],
    })

    const missing = cloneArena() as unknown as { generatorVersion?: string }
    delete missing.generatorVersion
    expect(validateArenaDefinition(missing)).toMatchObject({
      ok: false,
      errors: [expect.objectContaining({ code: 'MISSING_FIELD', paths: ['generatorVersion'] })],
    })
  })

  it('rejects renderer objects and unknown schema fields', () => {
    const contaminated = cloneArena() as ArenaDefinition & {
      rendererObject?: { uuid: string; updateMatrix: () => void }
    }
    contaminated.rendererObject = { uuid: 'three-object', updateMatrix: () => undefined }

    expect(validateArenaDefinition(contaminated)).toMatchObject({
      ok: false,
      errors: expect.arrayContaining([
        expect.objectContaining({ code: 'UNRECOGNIZED_FIELD', paths: ['rendererObject'] }),
      ]),
    })
  })
})
