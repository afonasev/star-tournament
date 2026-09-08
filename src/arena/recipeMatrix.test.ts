import { expect, it } from 'vitest'
import { generateProceduralArena } from './proceduralArena'
import { allocateArenaSpawns } from './spawnAllocation'
import { PROTOTYPE_V1_PROFILE as profile } from '../profiles'
it('accepts all named recipes across a deterministic seed matrix', () => {
  const recipes=new Set<string>()
  for (const size of ['small', 'medium', 'large'] as const) for (let seed=0;seed<32;seed++) {
    const result=generateProceduralArena({size,seed})
    if (result.ok) {
      recipes.add(result.arena.recipeId!)
      for(const mode of ['ffa','teams'] as const){
        const participants=Array.from({length:8},(_,index)=>({semanticId:`p-${index}`,team:mode==='ffa'?null:index%2===0?'team-a' as const:'team-b' as const}))
        const allocations=allocateArenaSpawns({arena:result.arena,mode,participants,capsuleDiameterMeters:2*(profile.rules.player.capsule.radius+profile.rules.player.capsule.skinWidth),minimumOpponentSeparationMeters:profile.rules.arenaGenerator.presets[size].minimumSpawnSeparationMeters})
        expect(new Set(allocations.map(a=>a.anchorId)).size).toBe(8)
      }
      expect(result.report.metrics.maximumRouteDistanceMeters).toBeGreaterThan(0)
      expect(result.arena.attachments?.every(a=>a.kind !== 'relief-niche' || result.arena.surfaces.find(s=>s.id===a.surfaceId)?.semantics?.classification==='wall')).toBe(true)
    }
    if (!result.ok) (() => { throw new Error(`${size}/${seed}: ${JSON.stringify(result.reports[0])}`) })()
  }
  expect([...recipes].sort()).toEqual(['basement-service-loop','bypass-gallery','dual-combat-decks','fire-balconies','wide-bypass-hall'])
},30000)

it('compiles a continuous main slab with two descents and an overlapping basement bypass', () => {
  const result = generateProceduralArena({ size: 'medium', seed: 4 })
  expect(result.ok).toBe(true)
  if (!result.ok) return
  const { arena } = result
  expect(arena.recipeId).toBe('basement-service-loop')
  expect(arena.surfaces.find(surface => surface.id === 'floor')?.semantics?.sector).toBe('basement')
  expect(arena.surfaces.filter(surface => surface.id === 'floor-main')).toHaveLength(1)
  expect(arena.surfaces.filter(surface => surface.id.startsWith('ramp-basement-'))).not.toHaveLength(0)
  expect(arena.regions.filter(region => region.id.startsWith('combat-basement-'))).toHaveLength(4)
  expect(arena.links.filter(link => link.transitionSurfaceId?.startsWith('ramp-basement-'))).not.toHaveLength(0)
  expect(new Set(arena.regions.map(region => region.supportSurfaceId)).size).toBe(2)
})

it('gives the elevated firing recipes physical guard slabs', () => {
  for (const [seed, recipeId] of [[2, 'fire-balconies'], [3, 'dual-combat-decks']] as const) {
    const result = generateProceduralArena({ size: 'medium', seed })
    expect(result.ok).toBe(true)
    if (!result.ok) continue
    expect(result.arena.recipeId).toBe(recipeId)
    expect(result.arena.surfaces.filter(surface => surface.id.startsWith('slab-'))).toHaveLength(2)
  }
})
