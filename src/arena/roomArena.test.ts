import {expect,it} from 'vitest'
import {compileRoomArena} from './roomArena'
import {PROTOTYPE_V1_PROFILE as profile} from '../profiles'
import {validateArenaGameplay} from './arenaGameplayValidation'
import {validateArenaPhysicalRoutes} from './physicalValidation'
import {allocateArenaSpawns} from './spawnAllocation'
import {createRapierCollisionWorld} from '../collision/rapierWorld'
import {arenaContentPayload,createArenaDefinition} from './arenaDefinition'
it('accepts the closed room compiler',()=>{
 const a=compileRoomArena('small',0,profile)
 const report=validateArenaGameplay(a,profile)
 expect(report.errors,JSON.stringify(report)).toEqual([])
})
it('walks every closed-room combat route and both ramps',async()=>{
 await validateArenaPhysicalRoutes(compileRoomArena('small',0,profile),profile)
},30000)
it.each(['small','medium','large'] as const)('accepts structural variants and modes for %s',size=>{
 const families=new Map<string,Set<string>>(),transitions=new Map<string,Set<string>>();let closed=0
 for(let seed=0;seed<24;seed++){
  const arena=compileRoomArena(size,seed,profile),report=validateArenaGameplay(arena,profile)
  expect(report.errors,`${size}/${seed}: ${JSON.stringify(report)}`).toEqual([])
  const variants=families.get(arena.recipeId!)??new Set<string>()
  variants.add(arena.links.filter(l=>l.id.startsWith('door:')).map(l=>l.id).join('|'));families.set(arena.recipeId!,variants)
  const placements=transitions.get(arena.recipeId!)??new Set<string>()
  placements.add(arena.surfaces.filter(s=>s.semantics?.classification==='ramp').map(s=>`${s.center.x},${s.center.z}`).join('|'));transitions.set(arena.recipeId!,placements)
  if(!['bypass-gallery','fire-balconies'].includes(arena.recipeId!))closed++
  for(const mode of ['ffa','teams'] as const){
   const participants=Array.from({length:8},(_,i)=>({semanticId:`p${i}`,team:mode==='ffa'?null:i%2?'team-a' as const:'team-b' as const}))
   expect(allocateArenaSpawns({arena,mode,participants,capsuleDiameterMeters:2*(profile.rules.player.capsule.radius+profile.rules.player.capsule.skinWidth),minimumOpponentSeparationMeters:profile.rules.arenaGenerator.presets[size].minimumSpawnSeparationMeters})).toHaveLength(8)
  }
 }
 expect(families.size).toBe(5);expect(closed).toBeGreaterThan(12)
 for(const variants of families.values())expect(variants.size).toBeGreaterThanOrEqual(profile.rules.arenaGenerator.presets[size].minimumStructuralVariants)
 for(const placements of transitions.values())expect(placements.size).toBeGreaterThanOrEqual(profile.rules.arenaGenerator.presets[size].minimumStructuralVariants)
},120000)
it('rejects a displaced roof and a missing enclosing wall',()=>{
 const arena=compileRoomArena('small',0,profile)
 const displaced=arenaContentPayload(arena)
 displaced.surfaces=displaced.surfaces.map(s=>s.id==='ceiling:combat-upper'?{...s,center:{...s.center,x:10000}}:s)
 expect(validateArenaGameplay(createArenaDefinition(displaced),profile).errors.some(e=>e.semanticIds.includes('combat-upper'))).toBe(true)
 const missing=arenaContentPayload(arena),wall='room-shell:side:room-1-0-0'
 missing.surfaces=missing.surfaces.filter(s=>s.id!==wall)
 missing.attachments=missing.attachments?.filter(a=>a.surfaceId!==wall)
 expect(validateArenaGameplay(createArenaDefinition(missing),profile).errors.some(e=>e.semanticIds.includes('room-1-0-0'))).toBe(true)
})
it('rejects a missing local ceiling and blocks jumping and shots through it',async()=>{
 const arena=compileRoomArena('small',0,profile),room=arena.regions.find(r=>r.id==='room-0-1-0')!
 const payload=arenaContentPayload(arena);payload.surfaces=payload.surfaces.filter(s=>s.id!==`ceiling:${room.id}`)
 expect(validateArenaGameplay(createArenaDefinition(payload),profile).errors.some(e=>e.semanticIds.includes(room.id))).toBe(true)
 const c=profile.rules.player.capsule,m=profile.rules.player.movement,offset=c.radius+c.halfHeight
 const start={x:room.center.x,y:room.center.y+offset+c.skinWidth,z:room.center.z}
 const world=await createRapierCollisionWorld(arena,[{semanticId:'jumper',position:start,radius:c.radius,halfHeight:c.halfHeight}],{skinWidth:c.skinWidth,autostepMaxHeight:m.autostepMaxHeight,autostepMinWidth:m.autostepMinWidth,snapToGroundDistance:m.snapToGroundDistance,maxSlopeClimbDegrees:m.maxSlopeClimbDegrees,minSlopeSlideDegrees:m.minSlopeSlideDegrees})
 try{
  const hit=world.raycast({...start,y:start.y+profile.rules.player.eyeHeight},{x:0,y:1,z:0},room.size.y,['jumper'])[0]
  expect(hit?.semanticId).toBe(`ceiling:${room.id}`)
  const jump=world.moveCapsule('jumper',{x:0,y:room.size.y*2,z:0})
  expect(jump.position.y+offset).toBeLessThanOrEqual(room.center.y+room.size.y+c.skinWidth)
  expect(jump.contacts.some(h=>h.semanticId===`ceiling:${room.id}`)).toBe(true)
 }finally{world.dispose()}
})
