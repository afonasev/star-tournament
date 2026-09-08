import {expect,it} from 'vitest'
import {compileRoomArena} from '../arena/roomArena'
import {arenaContentPayload,createArenaDefinition} from '../arena/arenaDefinition'
import {PROTOTYPE_V1_PROFILE as profile} from '../profiles'
import {createRapierCollisionWorld} from './rapierWorld'

it.each(['clear','ceiling','wall','participant','side'])('low speed autostep respects %s',async kind=>{
 const original=compileRoomArena('small',0,profile),payload=arenaContentPayload(original)
 const t=original.links.find(l=>l.transition?.kind==='stairs')!.transition!,step=original.surfaces.find(s=>s.id===t.surfaceIds[0])!
 const c=profile.rules.player.capsule,m=profile.rules.player.movement,edge=step.center.x+step.size.x/2,offset=c.radius+c.halfHeight
 const floor=original.surfaces.find(s=>s.id==='floor')!,floorY=floor.center.y+floor.size.y/2
 const start={x:edge+c.radius+c.skinWidth*2,y:floorY+offset+c.skinWidth,z:step.center.z}
 if(kind==='side'){start.x=step.center.x;start.z=step.center.z+step.size.z/2+c.radius+c.skinWidth*2}
 if(kind==='ceiling'||kind==='wall')payload.surfaces.push({...step,id:'test-blocker',semantics:{classification:'wall',movement:true,projectile:true,sector:'test'},center:kind==='ceiling'?{x:edge,y:start.y+offset+.05,z:start.z}:{x:edge-.05,y:floorY+2,z:start.z},size:kind==='ceiling'?{x:4,y:.08,z:step.size.z}:{x:.08,y:4,z:step.size.z}})
 const arena=createArenaDefinition(payload)
 const other={semanticId:'other',position:{x:edge-c.radius-.05,y:start.y+step.size.y,z:start.z},radius:c.radius,halfHeight:c.halfHeight}
 const world=await createRapierCollisionWorld(arena,[{semanticId:'walker',position:start,radius:c.radius,halfHeight:c.halfHeight},...(kind==='participant'?[other]:[])],{skinWidth:c.skinWidth,autostepMaxHeight:m.autostepMaxHeight,autostepMinWidth:m.autostepMinWidth,snapToGroundDistance:m.snapToGroundDistance,maxSlopeClimbDegrees:m.maxSlopeClimbDegrees,minSlopeSlideDegrees:m.minSlopeSlideDegrees})
 let p=start
 try{
  for(let i=0;i<180;i++)p=world.moveCapsule('walker',kind==='side'?{x:0,y:-c.skinWidth,z:-.01}:{x:-.01,y:-c.skinWidth,z:0}).position
  if(kind==='clear')expect(start.x-p.x).toBeGreaterThan(1.7)
  if(kind==='ceiling'){expect(p.x).toBeGreaterThan(edge);expect(p.y+offset).toBeLessThanOrEqual(start.y+offset+.01)}
  if(kind==='wall')expect(p.x-c.radius).toBeGreaterThanOrEqual(edge-.01-c.skinWidth)
  if(kind==='participant')expect(Math.hypot(p.x-other.position.x,p.z-other.position.z)).toBeGreaterThanOrEqual(2*c.radius)
  if(kind==='side')expect(p.z).toBeGreaterThan(step.center.z+step.size.z/2)
 }finally{world.dispose()}
})
