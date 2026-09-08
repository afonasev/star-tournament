// Historical arena-layers-v2 regression corpus; new room geometry has its own corpus in roomArena.test.ts.
import { expect,it } from 'vitest'
import { requireProceduralArena } from '../arena/legacyProceduralArenaV2'
import { arenaNavigation } from '../ai/navigation'
import { PROTOTYPE_V1_PROFILE as legacyV8Profile } from '../profiles/legacyGameDesignProfileV8'
import type { ValidatedGameDesignProfile } from '../profiles'
const profile=legacyV8Profile as unknown as ValidatedGameDesignProfile
import { createRapierCollisionWorld } from './rapierWorld'

it('never snaps through the existing floor on medium/1 east-03 and repeats identical contacts',async()=>{
  const arena=requireProceduralArena({size:'medium',seed:1})
  const c=profile.rules.player.capsule,m=profile.rules.player.movement
  const start=arena.spawnAnchors.find(a=>a.id==='spawn-east-03')!.position
  const route=arenaNavigation(arena,profile).route(start,{x:0,y:0,z:0})
  expect(route.every(p=>p.y===0)).toBe(true)
  const run=async()=>{
    const world=await createRapierCollisionWorld(arena,[{semanticId:'walker',position:{x:start.x,y:c.halfHeight+c.radius+c.skinWidth,z:start.z},radius:c.radius,halfHeight:c.halfHeight}],{skinWidth:c.skinWidth,autostepMaxHeight:m.autostepMaxHeight,autostepMinWidth:m.autostepMinWidth,snapToGroundDistance:m.snapToGroundDistance,maxSlopeClimbDegrees:m.maxSlopeClimbDegrees,minSlopeSlideDegrees:m.minSlopeSlideDegrees})
    let position={x:start.x,y:c.halfHeight+c.radius+c.skinWidth,z:start.z}
    const samples=[]
    try{
      for(const target of [start,...route]){
        let move=world.moveCapsule('walker',{x:0,y:-c.skinWidth,z:0})
        for(let tick=0;tick<2000;tick++){
          position={...move.position}
          expect(position.y-c.halfHeight-c.radius).toBeGreaterThanOrEqual(-c.skinWidth)
          const dx=target.x-position.x,dz=target.z-position.z,d=Math.hypot(dx,dz)
          if(d<=c.skinWidth&&move.grounded)break
          move=world.moveCapsule('walker',{x:dx/d*Math.min(c.radius/4,d),y:-c.skinWidth,z:dz/d*Math.min(c.radius/4,d)})
        }
        samples.push(move)
      }
      expect(Math.hypot(position.x,position.z)).toBeLessThan(c.skinWidth*2)
      return samples
    }finally{world.dispose()}
  }
  expect(await run()).toEqual(await run())
})
