import { expect, it } from 'vitest'
import { generateProceduralArena } from './proceduralArena'
it('accepts all named recipes across a deterministic seed matrix', () => {
  const recipes=new Set<string>()
  for (const size of ['small', 'medium', 'large'] as const) for (let seed=0;seed<32;seed++) {
    const result=generateProceduralArena({size,seed})
    if (result.ok) {
      recipes.add(result.arena.recipeId!)
      expect(result.report.metrics.maximumRouteDistanceMeters).toBeGreaterThan(0)
      expect(result.arena.attachments?.every(a=>a.kind !== 'relief-niche' || result.arena.surfaces.find(s=>s.id===a.surfaceId)?.semantics?.classification==='wall')).toBe(true)
    }
    if (!result.ok) (() => { throw new Error(`${size}/${seed}: ${JSON.stringify(result.reports[0])}`) })()
  }
  expect([...recipes].sort()).toEqual(['basement-service-loop','bypass-gallery','dual-combat-decks','fire-balconies','wide-bypass-hall'])
},30000)

it('compiles an accessible raised main deck around the basement bowl', () => {
  const result = generateProceduralArena({ size: 'medium', seed: 4 })
  expect(result.ok).toBe(true)
  if (!result.ok) return
  const { arena } = result
  expect(arena.recipeId).toBe('basement-service-loop')
  expect(arena.surfaces.find(surface => surface.id === 'floor')?.semantics?.sector).toBe('basement')
  expect(arena.surfaces.filter(surface => surface.id.startsWith('floor-main-'))).toHaveLength(4)
  expect(arena.surfaces.filter(surface => surface.id.startsWith('ramp-basement-'))).not.toHaveLength(0)
  expect(arena.regions.filter(region => region.id.startsWith('combat-basement-'))).toHaveLength(4)
  expect(arena.links.filter(link => link.transitionSurfaceId?.startsWith('ramp-basement-'))).not.toHaveLength(0)
  expect(new Set(arena.regions.map(region => region.supportSurfaceId)).size).toBeGreaterThan(2)
})

it('gives the elevated firing recipes physical guard slabs', () => {
  for (const [seed, recipeId] of [[2, 'fire-balconies'], [3, 'dual-combat-decks']] as const) {
    const result = generateProceduralArena({ size: 'medium', seed })
    expect(result.ok).toBe(true)
    if (!result.ok) continue
    expect(result.arena.recipeId).toBe(recipeId)
    expect(result.arena.surfaces.filter(surface => surface.semantics?.classification === 'slab')).toHaveLength(2)
  }
})
