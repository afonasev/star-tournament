import {expect,it} from 'vitest'
import {compileRoomArena} from '../arena/roomArena'
import {arenaNavigation} from '../ai/navigation'
import {PROTOTYPE_V1_PROFILE as profile} from '../profiles'
import {createRapierCollisionWorld} from '../collision/rapierWorld'
import {createInitialSimulationSnapshot,parseSimulationSnapshot} from './playableSnapshot'
import {stepPlayableSimulation,livingParticipantCollisionCapsules} from './playableStep'
import {createMatchConfiguration,DEFAULT_MATCH_CONFIGURATION} from '../match'
import {createActionFrame} from '../input/actionFrame'
import {arenaContentPayload,createArenaDefinition} from '../arena/arenaDefinition'

it.each(['ramp','stairs'].flatMap(kind=>[.1,1].flatMap(input=>[0,.1].map(diagonal=>({kind,input,diagonal})))) )('keeps normal running tempo up and down physical $kind at input $input diagonal $diagonal without a crest launch',async ({kind,input,diagonal})=>{
 const generated=compileRoomArena('small',0,profile),nav=arenaNavigation(generated,profile),c=profile.rules.player.capsule,m=profile.rules.player.movement
 const {recipeId:_recipe,...payload}=arenaContentPayload(generated)
 const flat=createArenaDefinition({...payload,attachments:[],generatorVersion:'flat-tempo-fixture',surfaces:payload.surfaces.filter(s=>s.id==='floor'),links:[],regions:payload.regions.map(r=>({...r,supportSurfaceId:'floor',center:{...r.center,y:0}})),spawnAnchors:payload.spawnAnchors.map(a=>({...a,supportSurfaceId:'floor',position:{...a.position,y:0}}))})
 const {contentHash:_,...base}=DEFAULT_MATCH_CONFIGURATION
 const config=createMatchConfiguration({...base,arenaSize:'small',arenaSeed:0})
 const results:unknown[]=[]
 for(const transition of nav.transitions!.filter(t=>t.kind===kind))for(const reverse of [false,true]){
  let baseline=0
  for(const arena of [flat,generated]){
  const originalA=transition.points[reverse?transition.points.length-1:0]!,originalB=transition.points[reverse?0:transition.points.length-1]!
  const a=arena===flat?{x:0,y:0,z:0}:originalA,b=arena===flat?{x:originalB.x-originalA.x,y:0,z:0}:originalB,sign=Math.sign(b.x-a.x)
  const initial=createInitialSimulationSnapshot({rngSeed:0,arena,configuration:config}),player=initial.participants.find(p=>p.kind==='local-seat')!
  let state=parseSimulationSnapshot({...initial,participants:initial.participants.map(p=>p===player?{...p,position:{x:a.x,y:a.y+c.radius+c.halfHeight+c.skinWidth,z:a.z},velocity:{x:0,y:0,z:0},grounded:true,yaw:sign*Math.PI/2}:{...p,alive:false,health:0,deathTick:0,respawnTick:null})},config)
  const world=await createRapierCollisionWorld(arena,livingParticipantCollisionCapsules(state,profile),{skinWidth:c.skinWidth,autostepMaxHeight:m.autostepMaxHeight,autostepMinWidth:m.autostepMinWidth,snapToGroundDistance:m.snapToGroundDistance,maxSlopeClimbDegrees:m.maxSlopeClimbDegrees,minSlopeSlideDegrees:m.minSlopeSlideDegrees})
  let ticks=0,maxExcess=0,airborne=0
  try{
   for(;ticks<1200;ticks++){
    state=stepPlayableSimulation(state,createActionFrame(state.tick+1,[{participantId:player.semanticId,actionId:'move-forward',value:input},{participantId:player.semanticId,actionId:'move-right',value:input*diagonal}]),world,profile,config,arena)
    const p=state.participants.find(p=>p.semanticId===player.semanticId)!
    if(!p.grounded)airborne++
    if(sign*(p.position.x-b.x)>=0){maxExcess=p.position.y-c.radius-c.halfHeight-b.y;break}
   }
   results.push({kind:transition.kind,reverse,ticks,maxExcess,airborne})
   if(arena===flat)baseline=ticks
   expect(ticks,JSON.stringify(results)).toBeLessThan(baseline*1.15)
   expect(maxExcess,JSON.stringify(results)).toBeLessThan(c.radius)
   if(arena===generated&&kind==='stairs')expect(airborne,JSON.stringify(results)).toBe(0)
   for(let stop=0;stop<30;stop++)state=stepPlayableSimulation(state,createActionFrame(state.tick+1,[]),world,profile,config,arena)
   const stopped=state.participants.find(p=>p.semanticId===player.semanticId)!
   expect(stopped.grounded).toBe(true)
   state=stepPlayableSimulation(state,createActionFrame(state.tick+1,[{participantId:player.semanticId,actionId:'jump',value:1}]),world,profile,config,arena)
   const jumped=state.participants.find(p=>p.semanticId===player.semanticId)!
   expect(jumped.position.y).toBeGreaterThan(stopped.position.y)
   expect(jumped.grounded).toBe(false)
  }finally{world.dispose()}
  }
 }
 console.log('TRANSITION TEMPO',JSON.stringify(results))
},30000)
