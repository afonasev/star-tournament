import {expect,it} from 'vitest'
import {compileRoomArena} from './roomArena'
import {arenaContentPayload,arenaTransition,createArenaDefinition} from './arenaDefinition'
import {validateArenaGameplay} from './arenaGameplayValidation'
import {arenaNavigation} from '../ai/navigation'
import {PROTOTYPE_V1_PROFILE as profile} from '../profiles'
import {PROTOTYPE_V1_PROFILE as legacy} from '../profiles/legacyGameDesignProfileV9'

it('selects approximately half of independent transitions as reproducible physical stairs',()=>{
 let stairs=0,total=0;const combinations=new Set<string>()
 for(let seed=0;seed<128;seed++){
  const arena=compileRoomArena('small',seed,profile),transitions=arena.links.map(arenaTransition).filter(t=>t!==undefined)
  if(seed<4)expect(compileRoomArena('small',seed,profile).contentHash).toBe(arena.contentHash)
  combinations.add(transitions.map(t=>t.kind).join('/'))
  for(const t of transitions){total++;if(t.kind==='stairs'){
   stairs++
   const surfaces=t.surfaceIds.map(id=>arena.surfaces.find(s=>s.id===id)!)
   expect(surfaces.every(s=>s.semantics?.classification==='floor')).toBe(true)
   expect(surfaces.every(s=>s.size.x>=profile.rules.arenaGenerator.presets.small.stairMinimumTreadMeters)).toBe(true)
   for(let i=1;i<surfaces.length;i++)expect(surfaces[i]!.size.y-surfaces[i-1]!.size.y).toBeLessThanOrEqual(profile.rules.arenaGenerator.presets.small.stairMaximumRiseMeters)
  }}
 }
 expect(stairs/total).toBeGreaterThan(.4);expect(stairs/total).toBeLessThan(.6);expect(combinations.size).toBe(4)
})
it('preserves the schema 9 profile and historical room generator output',()=>{
 const arena=compileRoomArena('small',0,legacy as unknown as typeof profile)
 expect(arena.schemaVersion).toBe(5);expect(arena.generatorVersion).toBe('arena-rooms-v1')
 expect(arena.contentHash).toBe('fnv1a64-v1:6a6e4bea2c7dd9d3')
 expect(arena.links.filter(l=>l.transitionSurfaceId).length).toBe(2)
})
it('rejects repeated, missing and disordered stair supports',()=>{
 const arena=compileRoomArena('small',0,profile)
 for(const mode of ['duplicate','missing','reversed']){
  const payload=arenaContentPayload(arena),t=payload.links.find(l=>l.transition?.kind==='stairs')!.transition!
  t.surfaceIds=mode==='duplicate'?[t.surfaceIds[0]!,t.surfaceIds[0]!]:mode==='missing'?['missing',t.surfaceIds[0]!]:t.surfaceIds.reverse()
  if(mode==='reversed')expect(validateArenaGameplay(createArenaDefinition(payload),profile).errors.length).toBeGreaterThan(0)
  else expect(()=>createArenaDefinition(payload)).toThrow()
 }
})
it('replans from every tread toward either landing using only its ordered transition',()=>{
 const arena=compileRoomArena('small',0,profile),nav=arenaNavigation(arena,profile),t=nav.transitions!.find(t=>t.kind==='stairs')!
 for(let i=1;i<t.points.length-1;i++)for(const goal of [t.points[0]!,t.points.at(-1)!]){
  const route=nav.route(t.points[i]!,goal)
  expect(route.length,`step ${i} to ${goal.supportSurfaceId}`).toBeGreaterThan(0)
  expect(route.at(-1)).toEqual(goal)
  const indices=route.map(p=>t.points.findIndex(q=>q.x===p.x&&q.y===p.y&&q.z===p.z))
  for(let n=1;n<indices.length;n++)expect(Math.sign(indices[n]!-indices[n-1]!)).toBe(goal===t.points[0]?-1:1)
 }
})
