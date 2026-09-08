// Historical arena-layers-v2 regression corpus; new room geometry has its own corpus in roomArena.test.ts.
import { describe, expect, it } from 'vitest'
import { createRapierCollisionWorld } from '../collision/rapierWorld'
import { PROTOTYPE_V1_PROFILE as legacyV8Profile } from '../profiles/legacyGameDesignProfileV8'
import type { ValidatedGameDesignProfile } from '../profiles'
const profile=legacyV8Profile as unknown as ValidatedGameDesignProfile
import { arenaNavigation } from '../ai/navigation'
import { requireProceduralArena } from './legacyProceduralArenaV2'
import { arenaContentPayload, createArenaDefinition } from './arenaDefinition'
import { DIAGNOSTIC_ARENA } from './diagnosticArena'

const capsule = profile.rules.player.capsule
const offset = capsule.halfHeight+capsule.radius
const controller = { skinWidth:capsule.skinWidth, autostepMaxHeight:profile.rules.player.movement.autostepMaxHeight,autostepMinWidth:profile.rules.player.movement.autostepMinWidth,snapToGroundDistance:profile.rules.player.movement.snapToGroundDistance,maxSlopeClimbDegrees:profile.rules.player.movement.maxSlopeClimbDegrees,minSlopeSlideDegrees:profile.rules.player.movement.minSlopeSlideDegrees }

describe('recipe physical queries', () => {
  it.each(['small','medium','large'] as const)('walks each declared %s gallery ramp in both directions without jumping', async size => {
    const arena=requireProceduralArena({size,seed:1})
    const transitions=arenaNavigation(arena,profile).transitions!
    expect(transitions).toHaveLength(2)
    for(const transition of transitions) for(const reverse of [false,true]) {
      const points=[...transition.points];if(reverse)points.reverse()
      const start=points[0]!,goal=points.at(-1)!
      const world=await createRapierCollisionWorld(arena,[{semanticId:'walker',position:{x:start.x,y:start.y+offset+capsule.skinWidth,z:start.z},radius:capsule.radius,halfHeight:capsule.halfHeight}],controller)
      try {
        let position={x:start.x,y:start.y+offset,z:start.z}
        for(const target of points.slice(1)) for(let tick=0;tick<1000;tick++){
          const dx=target.x-position.x,dz=target.z-position.z,d=Math.hypot(dx,dz)
          if(d<0.02)break
          const result=world.moveCapsule('walker',{x:dx/d*Math.min(0.05,d),y:-capsule.skinWidth,z:dz/d*Math.min(0.05,d)})
          position={...result.position}
        }
        expect(Math.hypot(position.x-goal.x,position.z-goal.z)).toBeLessThan(0.05)
        expect(Math.abs(position.y-offset-goal.y)).toBeLessThan(capsule.skinWidth*4)
      } finally {world.dispose()}
    }
  })
  it('passes a projectile through movement barriers and blocks it at the window', async () => {
    const payload=arenaContentPayload(DIAGNOSTIC_ARENA)
    payload.schemaVersion=4
    payload.surfaces=[payload.surfaces.find(s=>s.id==='floor')!,...['movement-barrier','window'].map((classification,index)=>({id:classification,kind:'box' as const,center:{x:0,y:1,z:index*3},size:{x:4,y:2,z:0.5},orientation:{x:0,y:0,z:0,w:1},material:'wall' as const,semantics:{classification:classification as 'movement-barrier'|'window',movement:true,projectile:index===1,sector:'fixture'}}))]
    const arena=createArenaDefinition(payload)
    const world=await createRapierCollisionWorld(arena,[{semanticId:'walker',position:{x:0,y:offset+capsule.skinWidth,z:-2},radius:capsule.radius,halfHeight:capsule.halfHeight}],controller)
    try {
      const ray=world.raycast({x:0,y:1,z:-2},{x:0,y:0,z:1},10,['walker'])
      expect(ray.map(h=>h.semanticId)).toEqual(['window'])
      const movement=world.moveCapsule('walker',{x:0,y:0,z:4})
      expect(movement.position.z).toBeLessThan(-0.25)
      expect(movement.contacts.some(c=>c.semanticId==='movement-barrier')).toBe(true)
    } finally { world.dispose() }
  })
  it('blocks shots through an upper slab while preserving a balcony firing lane',async()=>{
    const arena=requireProceduralArena({size:'small',seed:2})
    const slab=arena.surfaces.find(s=>s.id==='floor-upper-balcony')!
    const world=await createRapierCollisionWorld(arena,[],controller)
    try {
      expect(world.raycast({x:0,y:1,z:0},{x:0,y:1,z:0},4)[0]?.semanticId).toBe(slab.id)
      // Shoot above the ramp towards its unobstructed lower landing.
      const transition=arenaNavigation(arena,profile).transitions!.find(t=>t.surfaceId==='ramp-west-0')!
      const lower=transition.points[0]!,upper=transition.points.at(-1)!
      const from={x:upper.x,y:upper.y+offset+profile.rules.player.eyeHeight,z:upper.z}
      const to={x:lower.x,y:lower.y+offset+profile.rules.player.eyeHeight,z:lower.z}
      const d=Math.hypot(to.x-from.x,to.y-from.y,to.z-from.z)
      expect(world.raycast(from,{x:(to.x-from.x)/d,y:(to.y-from.y)/d,z:0},d)).toEqual([])
    }finally{world.dispose()}
  })
})

it('physically validates every spawn route in each recipe and size', async () => {
  const {validateArenaPhysicalRoutes}=await import('./physicalValidation')
  for(const size of ['small','medium','large'] as const) for(let seed=0;seed<10;seed++) await validateArenaPhysicalRoutes(requireProceduralArena({size,seed}),profile)
},120000)
