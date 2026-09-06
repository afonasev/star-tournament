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
  expect([...recipes].sort()).toEqual(['flanked-courtyard','grand-hall','service-loop','split-atrium'])
},30000)
