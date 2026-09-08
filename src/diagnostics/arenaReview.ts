import { arenaDoorwayGaps } from '../arena/doorwayGaps'
// Dev-only visual fixture. Uses the shipping renderer; never claims physical input acceptance.
import { requireProceduralArena } from '../arena'
import { arenaFloorHeight } from '../arena/elevation'
import { arenaNavigation } from '../ai/navigation'
import { PROTOTYPE_V1_PROFILE as profile } from '../profiles'
import { COMBAT_SLICE_SCENARIO_V1 as scenario } from '../scenario'
import { prepareArenaGlbAssets } from '../render/arenaGlbAssets'
import { createFirstPersonRenderer } from '../render/firstPersonRenderer'
import { GRAPHICS_QUALITY_V1 } from '../presentation'
import {createRapierCollisionWorld} from '../collision/rapierWorld'
import {createAimProjectionQuery} from '../runtime/aimProjection'
import {createInitialSimulationSnapshot,parseSimulationSnapshot,localParticipant,type SimulationSnapshot} from '../simulation/playableSnapshot'
import {stepPlayableSimulation,livingParticipantCollisionCapsules} from '../simulation/playableStep'
import {createActionFrame} from '../input/actionFrame'
import {createMatchConfiguration,DEFAULT_MATCH_CONFIGURATION} from '../match'

const params=new URLSearchParams(location.search)
const seed=Number(params.get('seed')??1)
const size=params.get('size')==='large'?'large':params.get('size')==='small'?'small':'medium'
const arena=requireProceduralArena({size,seed})
const assets=await prepareArenaGlbAssets()
const c=profile.rules.player.capsule,m=profile.rules.player.movement
const world=await createRapierCollisionWorld(arena,[],{skinWidth:c.skinWidth,autostepMaxHeight:m.autostepMaxHeight,autostepMinWidth:m.autostepMinWidth,snapToGroundDistance:m.snapToGroundDistance,maxSlopeClimbDegrees:m.maxSlopeClimbDegrees,minSlopeSlideDegrees:m.minSlopeSlideDegrees})
const reticle=document.createElement('div');reticle.textContent='+';reticle.setAttribute('aria-label','Диагностический прицел');reticle.style.cssText='position:fixed;left:50%;top:50%;transform:translate(-50%,-50%);color:white;font:24px monospace;pointer-events:none';document.body.append(reticle)
const renderer=createFirstPersonRenderer({container:document.querySelector('#scene')!,arena,scenario,profile,pixelBudget:2073600,arenaGlbAssets:assets,graphicsQuality:GRAPHICS_QUALITY_V1.high,aimQuery:createAimProjectionQuery(profile,world),onAimProjection:p=>{reticle.style.left=`${p.x*100}%`;reticle.style.top=`${p.y*100}%`}})
const selector=document.querySelector<HTMLSelectElement>('#view')!
const transitions=arenaNavigation(arena,profile).transitions??[]
for(const t of transitions)for(const direction of ['up','down'])selector.add(new Option(`${t.kind==='stairs'?'Лестница':'Пандус'} ${t.id} · ${direction}`,`transition:${t.id}:${direction}`))
for(const [value,label] of [['upper','Верхняя опора'],['lower','Нижняя опора']])selector.add(new Option(label,value))
const rooms=arena.regions.filter(r=>r.id.startsWith('room-')||r.id==='combat-center'||r.id==='combat-upper')
const plan=document.createElement('div');plan.style.cssText='position:fixed;inset:100px 20px 20px;background:#101b2c;display:none';document.body.append(plan)
if(arena.generatorVersion.startsWith('arena-rooms-v')){
 for(const [value,label] of [['plan-lower','План нижнего этажа'],['plan-upper','План верхнего этажа']])selector.add(new Option(label,value))
 for(const r of rooms)selector.add(new Option(`${r.supportSurfaceId==='floor'?'Низ':'Верх'} · ${r.role==='route'?'Тоннель':'Комната'} · ${r.id} · потолок ${r.size.y.toFixed(2)} м`,`room:${r.id}`))
}
if(params.has('view'))selector.value=params.get('view')!
let tick=0
function render() {
  const view=selector.value
  plan.style.display=view.startsWith('plan-')?'block':'none'
  if(view.startsWith('plan-')){
    const upper=view==='plan-upper',floor=arena.surfaces.find(s=>s.id===(upper?'floor-upper-rooms':'floor'))!,height=floor.center.y+floor.size.y/2
    const base=arena.surfaces.find(s=>s.id==='floor')!,w=base.size.x,d=base.size.z
    const rect=(x:number,z:number,sx:number,sz:number,fill:string)=>`<rect x="${x-sx/2}" y="${z-sz/2}" width="${sx}" height="${sz}" fill="${fill}"/>`
    const local=rooms.filter(r=>r.supportSurfaceId===floor.id)
    plan.innerHTML=`<svg xmlns="http://www.w3.org/2000/svg" width="100%" height="100%" viewBox="${-w/2-1} ${-d/2-1} ${w+2} ${d+2}">${rect(0,0,floor.size.x,floor.size.z,'#213044')}${local.map(r=>rect(r.center.x,r.center.z,r.size.x,r.size.z,r.role==='route'?'#27695f':'#285b76')).join('')}${arena.surfaces.filter(s=>s.semantics?.classification==='wall'&&s.center.y-s.size.y/2<=height+.01&&s.center.y+s.size.y/2>height+.1).map(s=>rect(s.center.x,s.center.z,s.size.x,s.size.z,'#c4d3e3')).join('')}${arena.surfaces.filter(s=>s.semantics?.classification==='ramp').map(s=>rect(s.center.x,s.center.z,s.size.x*(1-2*s.orientation.z*s.orientation.z),s.size.z,'#e0a36e')).join('')}${local.map(r=>`<text x="${r.center.x}" y="${r.center.z}" fill="white" text-anchor="middle" font-size=".7">${r.role==='route'?'Тоннель':'Комната'} ${r.size.y.toFixed(2)} м</text>`).join('')}</svg>`
  }
  const subject = view==='window' ? arena.surfaces.find(s=>s.semantics?.classification==='window') : view==='low'||view==='high' ? arena.surfaces.filter(s=>s.semantics?.classification==='movement-barrier').sort((a,b)=>a.size.y-b.size.y)[view==='low'?0:arena.surfaces.filter(s=>s.semantics?.classification==='movement-barrier').length-1] : view==='niche' ? arena.surfaces.find(s=>s.id===arena.attachments?.find(a=>a.kind==='relief-niche')?.surfaceId) : undefined
  let x=-profile.rules.arenaGenerator.presets[size].rampWidthMeters/2-profile.rules.arenaGenerator.presets[size].rampLengthMeters-1,z=0
  let target={x:0,y:1,z:0}
  let supportY:number|undefined
  if(view.startsWith('room:')){
    const room=rooms.find(r=>r.id===view.slice(5))!
    x=room.center.x;z=room.center.z;supportY=room.center.y
    target={x:x+room.size.x,y:supportY+profile.rules.player.capsule.radius+profile.rules.player.capsule.halfHeight+profile.rules.player.eyeHeight,z}
  }
  if(view==='upper'||view==='lower') {
    const floor=arena.surfaces.find(s=>s.id==='floor')!
    const upper=arena.surfaces.find(s=>s.id.startsWith('floor-upper-')||s.id==='floor-main')
    x=0;z=0;supportY=view==='upper'&&upper?upper.center.y+upper.size.y/2:floor.center.y+floor.size.y/2
    target={x:floor.size.x/2,y:supportY,z:0}
  }
  if(view==='ramp') {
    const ramp=arena.surfaces.find(s=>s.id==='ramp-west-0')??arena.surfaces.find(s=>s.semantics?.classification==='ramp')
    const floor=arena.surfaces.find(s=>s.id==='floor')!
    if(ramp){
    x=ramp.center.x-profile.rules.arenaGenerator.presets[size].rampLengthMeters/2-profile.rules.arenaGenerator.presets[size].corridorWidthMeters/2;z=ramp.center.z;supportY=floor.center.y+floor.size.y/2
    target={x:ramp.center.x,y:ramp.center.y+profile.rules.player.eyeHeight,z:ramp.center.z}
    }
  }
  if(view.startsWith('transition:')){
    const t=transitions.find(t=>view===`transition:${t.id}:up`||view===`transition:${t.id}:down`)!
    const reverse=view.endsWith(':down'),a=reverse?t.points.at(-1)!:t.points[0]!,b=reverse?t.points[1]!:t.points.at(-2)!
    x=a.x;z=a.z;supportY=a.y
    target={x:b.x,y:b.y+profile.rules.player.eyeHeight,z:b.z}
  }
  if(subject) {
    // A square barrier inherits its facing from the adjacent room back wall.
    const host = subject.semantics?.classification === 'movement-barrier'
      ? arena.surfaces.filter(s => s.id.includes('-back-')).sort((a,b) => Math.hypot(a.center.x-subject.center.x,a.center.z-subject.center.z)-Math.hypot(b.center.x-subject.center.x,b.center.z-subject.center.z))[0] ?? subject
      : subject
    const horizontal=host.size.x>=host.size.z
    const side=horizontal?-Math.sign(subject.center.z||1):-Math.sign(subject.center.x||1)
    const distance = subject.semantics?.classification === 'movement-barrier' ? 0.8 : 3
    x=subject.center.x+(horizontal?0:side*(subject.size.x/2+distance))
    z=subject.center.z+(horizontal?side*(subject.size.z/2+distance):0)
    if (subject.semantics?.classification === 'movement-barrier' || view === 'niche') {
      const siblings=arena.surfaces.filter(s=>s.id.startsWith(host.id.replace(/-segment-\d+$/, '-segment-')))
      if(horizontal) { x=siblings.reduce((sum,s)=>sum+s.center.x,0)/siblings.length;z=subject.center.z+(view==='niche'?side*(subject.size.z/2+0.8):0) }
      else { z=siblings.reduce((sum,s)=>sum+s.center.z,0)/siblings.length;x=subject.center.x+(view==='niche'?side*(subject.size.x/2+0.8):0) }
    }
    target={...subject.center,y:Math.min(subject.center.y,profile.rules.player.capsule.halfHeight+profile.rules.player.capsule.radius+profile.rules.player.eyeHeight)}
  }
  if(view==='portal') {
    const gap=arenaDoorwayGaps(arena.surfaces)[0]
    if(gap){
    x=gap.horizontalAlongX?gap.spanCenter:gap.planeCoordinate-Math.sign(gap.planeCoordinate)*3
    z=gap.horizontalAlongX?gap.planeCoordinate-Math.sign(gap.planeCoordinate)*3:gap.spanCenter
    target={x:gap.horizontalAlongX?gap.spanCenter:gap.planeCoordinate,y:profile.rules.player.capsule.halfHeight+profile.rules.player.capsule.radius+profile.rules.player.eyeHeight,z:gap.horizontalAlongX?gap.planeCoordinate:gap.spanCenter}
    }
  }
  const y=(supportY??arenaFloorHeight(arena,x,z)??0)+profile.rules.player.capsule.halfHeight+profile.rules.player.capsule.radius
  const yaw=Math.atan2(target.x-x,-(target.z-z))
  const pitch=Math.atan2(target.y-y-profile.rules.player.eyeHeight,Math.hypot(target.x-x,target.z-z))
  renderer.render({tick:++tick,participants:[{semanticId:'local-player-1',kind:'local-seat',position:{x,y,z},yaw,pitch,weapon:{semanticId:'double-barrel-shotgun',ammo:20}}],events:[],corpses:[]})
  document.querySelector('#status')!.textContent=`VISUAL FIXTURE · ${arena.recipeId} · ${size} · seed ${seed} · ${arena.contentHash} · ${view} · muted`
}
selector.addEventListener('change',render)
let walking:SimulationSnapshot|undefined,animation=0
const {contentHash:_hash,...base}=DEFAULT_MATCH_CONFIGURATION
const configuration=createMatchConfiguration({...base,arenaSeed:seed,arenaSize:size})
for(const [label,finish] of [['Пройти до середины',false],['Завершить проход',true]] as const){
 const button=document.createElement('button');button.textContent=label;selector.parentElement!.append(button)
 button.addEventListener('click',()=>{
  cancelAnimationFrame(animation)
  const t=transitions.find(t=>selector.value===`transition:${t.id}:up`||selector.value===`transition:${t.id}:down`)
  if(!t)return
  const reverse=selector.value.endsWith(':down'),a=reverse?t.points.at(-1)!:t.points[0]!,b=reverse?t.points[0]!:t.points.at(-1)!,sign=Math.sign(b.x-a.x)
  if(!finish||!walking){
   const initial=createInitialSimulationSnapshot({rngSeed:seed,arena,configuration})
   walking=parseSimulationSnapshot({...initial,participants:initial.participants.map(p=>p.kind==='local-seat'?{...p,position:{x:a.x,y:a.y+c.radius+c.halfHeight+c.skinWidth,z:a.z},grounded:true,yaw:sign*Math.PI/2,velocity:{x:0,y:0,z:0}}:{...p,alive:false,health:0,deathTick:0,respawnTick:null})},configuration)
  }
  const goalX=finish?b.x:(a.x+b.x)/2
  function advance(){
   const before=localParticipant(walking!)
   world.projectCapsules(livingParticipantCollisionCapsules(walking!,profile))
   walking=stepPlayableSimulation(walking!,createActionFrame(walking!.tick+1,[{participantId:before.semanticId,actionId:'move-forward',value:1}]),world,profile,configuration,arena)
   renderer.render(walking)
   const p=localParticipant(walking),arrived=sign*(p.position.x-goalX)>=0
   document.querySelector('#status')!.textContent=`SIMULATION WALK · ${size} · seed ${seed} · ${t!.kind} · ${reverse?'down':'up'} · tick ${walking.tick} · feet ${(p.position.y-c.radius-c.halfHeight).toFixed(3)} · ${arrived?'ARRIVED':'RUNNING'} · muted`
   if(!arrived&&walking.tick<600)animation=requestAnimationFrame(advance)
  }
  animation=requestAnimationFrame(advance)
 })
}
selector.addEventListener('change',()=>{cancelAnimationFrame(animation);walking=undefined})
window.addEventListener('pagehide',()=>{cancelAnimationFrame(animation);world.dispose()},{once:true})
render()
window.addEventListener('beforeunload',()=>{renderer.dispose();assets.dispose()})
