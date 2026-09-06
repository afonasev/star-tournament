import { arenaDoorwayGaps } from '../arena/doorwayGaps'
// Dev-only visual fixture. Uses the shipping renderer; never claims physical input acceptance.
import { requireProceduralArena } from '../arena'
import { arenaFloorHeight } from '../arena/elevation'
import { PROTOTYPE_V1_PROFILE as profile } from '../profiles'
import { COMBAT_SLICE_SCENARIO_V1 as scenario } from '../scenario'
import { prepareArenaGlbAssets } from '../render/arenaGlbAssets'
import { createFirstPersonRenderer } from '../render/firstPersonRenderer'
import { GRAPHICS_QUALITY_V1 } from '../presentation'

const params=new URLSearchParams(location.search)
const seed=Number(params.get('seed')??1)
const size=params.get('size')==='large'?'large':params.get('size')==='small'?'small':'medium'
const arena=requireProceduralArena({size,seed})
const assets=await prepareArenaGlbAssets()
const renderer=createFirstPersonRenderer({container:document.querySelector('#scene')!,arena,scenario,profile,pixelBudget:2073600,arenaGlbAssets:assets,graphicsQuality:GRAPHICS_QUALITY_V1.high})
const selector=document.querySelector<HTMLSelectElement>('#view')!
let tick=0
function render() {
  const view=selector.value
  const subject = view==='window' ? arena.surfaces.find(s=>s.semantics?.classification==='window') : view==='low'||view==='high' ? arena.surfaces.filter(s=>s.semantics?.classification==='movement-barrier').sort((a,b)=>a.size.y-b.size.y)[view==='low'?0:arena.surfaces.filter(s=>s.semantics?.classification==='movement-barrier').length-1] : view==='niche' ? arena.surfaces.find(s=>s.id===arena.attachments?.find(a=>a.kind==='relief-niche')?.surfaceId) : undefined
  let x=-profile.rules.arenaGenerator.presets[size].rampWidthMeters/2-profile.rules.arenaGenerator.presets[size].rampLengthMeters-1,z=0
  let target={x:0,y:1,z:0}
  if(view==='ramp') { z=3;target={x:-profile.rules.arenaGenerator.presets[size].rampWidthMeters/2,y:profile.rules.arenaGenerator.presets[size].elevationDeltaMeters,z:0} }
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
    const gap=arenaDoorwayGaps(arena.surfaces)[0]!
    x=gap.horizontalAlongX?gap.spanCenter:gap.planeCoordinate-Math.sign(gap.planeCoordinate)*3
    z=gap.horizontalAlongX?gap.planeCoordinate-Math.sign(gap.planeCoordinate)*3:gap.spanCenter
    target={x:gap.horizontalAlongX?gap.spanCenter:gap.planeCoordinate,y:profile.rules.player.capsule.halfHeight+profile.rules.player.capsule.radius+profile.rules.player.eyeHeight,z:gap.horizontalAlongX?gap.planeCoordinate:gap.spanCenter}
  }
  const y=(arenaFloorHeight(arena,x,z)??0)+profile.rules.player.capsule.halfHeight+profile.rules.player.capsule.radius
  const yaw=Math.atan2(target.x-x,-(target.z-z))
  const pitch=Math.atan2(target.y-y-profile.rules.player.eyeHeight,Math.hypot(target.x-x,target.z-z))
  renderer.render({tick:++tick,participants:[{semanticId:'local-player-1',kind:'local-seat',position:{x,y,z},yaw,pitch,weapon:{semanticId:'double-barrel-shotgun',ammo:20}}],events:[],corpses:[]})
  document.querySelector('#status')!.textContent=`VISUAL FIXTURE · ${arena.recipeId} · ${size} · seed ${seed} · ${arena.contentHash} · ${view} · muted`
}
selector.addEventListener('change',render)
render()
window.addEventListener('beforeunload',()=>{renderer.dispose();assets.dispose()})
