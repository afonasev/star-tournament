import { describe, expect, it } from 'vitest'
import { DIAGNOSTIC_ARENA } from './diagnosticArena'
import { arenaContentPayload, blocksProjectiles, canonicalArenaJson, createArenaDefinition, parseArenaDefinition, validateArenaDefinition } from './arenaDefinition'

describe('versioned surface queries', () => {
  it('preserves historical v3 bytes and all-query behavior', () => {
    const bytes = canonicalArenaJson(DIAGNOSTIC_ARENA)
    const parsed = parseArenaDefinition(bytes)
    expect(parsed.ok).toBe(true)
    if (!parsed.ok) throw new Error('historical definition rejected')
    expect(parsed.arena.schemaVersion).toBe(3)
    expect(canonicalArenaJson(parsed.arena)).toBe(bytes)
    expect(parsed.arena.surfaces.every(blocksProjectiles)).toBe(true)
  })
  it('round trips explicit masks and rejects inconsistent window and barrier masks', () => {
    const payload = arenaContentPayload(DIAGNOSTIC_ARENA)
    payload.schemaVersion = 4
    payload.recipeId = 'fixture'
    payload.surfaces = payload.surfaces.map(s => ({ ...s, semantics: { classification: s.material === 'floor' ? 'floor' : 'wall', movement: true, projectile: true, sector: 'center' } }))
    const barrier = payload.surfaces.find(s => s.id === 'cover-center')!
    barrier.semantics = { classification: 'movement-barrier', movement: true, projectile: false, sector: 'center' }
    const arena = createArenaDefinition(payload)
    expect(parseArenaDefinition(canonicalArenaJson(arena))).toEqual({ ok: true, arena })
    for (const classification of ['window', 'movement-barrier'] as const) {
      const invalid = structuredClone(arena)
      invalid.surfaces[0]!.semantics = { classification, movement: true, projectile: classification === 'movement-barrier', sector: 'center' }
      expect(validateArenaDefinition(invalid)).toMatchObject({ ok: false, errors: expect.arrayContaining([expect.objectContaining({ code: 'INVALID_SURFACE_MASK' })]) })
    }
  })
})
