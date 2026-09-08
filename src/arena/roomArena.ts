import { createArenaDefinition, type ArenaBoxSurface, type ArenaDefinitionPayload, type ArenaRegion, type ArenaSize } from './arenaDefinition'
import { rampBox } from './elevation'
import type { GameDesignProfile, ValidatedGameDesignProfile } from '../profiles'
import type { ContentHash } from '../core/canonical'

export const ROOM_GENERATOR_VERSION='arena-rooms-v1'
const recipes=['wide-bypass-hall','bypass-gallery','fire-balconies','dual-combat-decks','basement-service-loop'] as const
export interface RoomPlan {id:string; layer:number; x:number; z:number; width:number; depth:number; floor:number; height:number; kind:'room'|'hall'|'tunnel'}
const EPS=1e-6 // Shared-face numerical tolerance, not a layout tuning value.
function box(id:string,x:number,y:number,z:number,w:number,h:number,d:number,kind:'wall'|'floor'|'slab',sector:string):ArenaBoxSurface {
 return {id,kind:'box',center:{x,y,z},size:{x:w,y:h,z:d},orientation:{x:0,y:0,z:0,w:1},material:kind==='floor'?'floor':'wall',semantics:{classification:kind,movement:true,projectile:true,sector}}
}

/** A bounded graph catalogue, compiled before collision and presentation consume it. */
export function compileRoomArena(size:ArenaSize,seed:number,profile:GameDesignProfile|ValidatedGameDesignProfile){
 const r=profile.rules.arenaGenerator.presets[size],t=r.wallThicknessMeters,slab=r.slabThicknessMeters
 const pool=recipes.flatMap((id,index)=>Array.from({length:index===1||index===2?r.openLayoutWeight:r.closedLayoutWeight},()=>id))
 const recipe=pool[seed%pool.length]!,variant=(Math.floor(seed/pool.length)+seed%2+recipes.indexOf(recipe))%r.minimumStructuralVariants
 const open=recipe==='bypass-gallery'||recipe==='fire-balconies'
 const columns=variant===2||size==='large'?5:3
 const w=r.widthMeters,d=r.depthMeters,cellW=w/columns,mid=r.corridorWidthMeters+t
 const corridorZ=(variant-1)*t
 const rows=[(d-mid)/2+corridorZ,mid,(d-mid)/2-corridorZ],zs=[-d/2+rows[0]!/2,corridorZ,d/2-rows[2]!/2]
 const rise=r.hallCeilingMeters+r.ceilingVariationMeters+2*slab
 const apron=r.rampLengthMeters+r.corridorWidthMeters+2*t,outer=w/2+apron
 const payload:ArenaDefinitionPayload={schemaVersion:5,id:`procedural-${size}-${seed.toString(16).padStart(8,'0')}`,seed,size,generatorVersion:ROOM_GENERATOR_VERSION,generatorProfile:{id:profile.id,revision:profile.revision,contentHash:profile.contentHash as ContentHash},recipeId:recipe,regions:[],links:[],surfaces:[],spawnRegions:[],spawnAnchors:[],attachments:[]}
 const surfaces=payload.surfaces,plans:RoomPlan[]=[],roomAt=new Map<string,RoomPlan>()
 for(let layer=0;layer<2;layer++)for(let row=0;row<3;row++)for(let col=0;col<columns;col++){
  const middle=col===(columns-1)/2&&row===1
  const kind=middle?'hall':row===1?'tunnel':'room'
  const base=kind==='hall'?r.hallCeilingMeters:kind==='tunnel'?r.tunnelCeilingMeters:r.roomCeilingMeters
  const noise=((seed+col+row+layer)%3)/2
  const id=middle?(layer?'combat-upper':'combat-center'):`room-${layer}-${row}-${col}`
  const room:RoomPlan={id,layer,x:-w/2+(col+.5)*cellW,z:zs[row]!,width:cellW,depth:rows[row]!,floor:layer*rise,height:base+noise*r.ceilingVariationMeters,kind}
  plans.push(room);roomAt.set(`${layer}/${row}/${col}`,room)
  payload.regions.push({id,role:kind==='tunnel'?'route':'combat',center:{x:room.x,y:room.floor,z:room.z},size:{x:cellW-t,y:room.height,z:room.depth-t},supportSurfaceId:layer?'floor-upper-rooms':'floor'})
  surfaces.push(box(`ceiling:${id}`,room.x,room.floor+room.height+slab/2,room.z,cellW,slab,room.depth,'slab',kind))
 }
 surfaces.push(box('floor',0,-.5,0,2*outer,1,d,'floor','hall'),box('floor-upper-rooms',0,rise-slab/2,0,w,slab,d,'floor','upper-combat'))
 const wall=(id:string,axis:'x'|'z',plane:number,start:number,end:number,floor:number,height:number,sector:string)=>{
  if(end-start<EPS||height<EPS)return
  surfaces.push(box(id,axis==='x'?(start+end)/2:plane,floor+height/2,axis==='x'?plane:(start+end)/2,axis==='x'?end-start:t,height,axis==='x'?t:end-start,'wall',sector))
 }
 const boundary=(id:string,axis:'x'|'z',plane:number,start:number,end:number,floor:number,height:number,door:number|undefined,clearHeight:number,sector:string)=>{
  if(door===undefined){wall(id,axis,plane,start,end,floor,height,sector);return}
  const half=r.doorwayWidthMeters/2
  wall(`${id}:left`,axis,plane,start,door-half,floor,height,sector)
  wall(`${id}:right`,axis,plane,door+half,end,floor,height,sector)
  wall(`${id}:lintel`,axis,plane,door-half,door+half,floor+clearHeight,height-clearHeight,sector)
 }
 // Shared boundaries are emitted once. The catalogue removes symmetric spokes,
 // changing the graph (not just a mirror) while preserving the outer cycle.
 for(let layer=0;layer<2;layer++)for(let row=0;row<3;row++)for(let col=0;col<columns;col++){
  const a=roomAt.get(`${layer}/${row}/${col}`)!
  for(const [dr,dc] of [[0,1],[1,0]]){
   const b=roomAt.get(`${layer}/${row+dr!}/${col+dc!}`);if(!b)continue
   const cut=variant===0?(layer===0&&dr===1&&col===(columns-1)/2):(variant===1&&layer===1&&dc===1&&row===1&&(col===(columns-1)/2||col===(columns-1)/2-1))
   const axis=dc?'z':'x',plane=dc?a.x+cellW/2:a.z+a.depth/2
   const start=dc?a.z-a.depth/2:a.x-cellW/2,end=dc?a.z+a.depth/2:a.x+cellW/2
   const door=dc?a.z:a.x
   boundary(`room-wall:${a.id}:${b.id}`,axis,plane,start,end,a.floor,Math.max(a.height,b.height),cut?undefined:door,Math.min(a.height,b.height),a.kind)
   if(!cut)payload.links.push({id:`door:${a.id}:${b.id}`,fromRegionId:a.id,toRegionId:b.id,exposure:a.kind==='tunnel'||b.kind==='tunnel'?'covered':'open'})
  }
 }
 // Main building perimeter: side doors connect lower rooms to side halls;
 // upper doors connect only to the two declared ramps.
 for(let layer=0;layer<2;layer++){
  for(let col=0;col<columns;col++)for(const row of [0,2]){
   const a=roomAt.get(`${layer}/${row}/${col}`)!
   wall(`room-shell:${a.id}`,'x',row===0?-d/2:d/2,a.x-cellW/2,a.x+cellW/2,a.floor,a.height,a.kind)
  }
  for(const side of [-1,1])for(let row=0;row<3;row++){
   const a=roomAt.get(`${layer}/${row}/${side<0?0:columns-1}`)!
   const door=layer?(row===1?a.z:undefined):(row!==1?a.z:undefined)
   if(open&&layer===1&&(side===1||recipe==='fire-balconies')&&row!==1){
    wall(`balcony-guard:${a.id}`,'z',side*w/2,a.z-a.depth/2,a.z+a.depth/2,a.floor,r.barrierHighMeters,'gallery')
   }else boundary(`room-shell:side:${a.id}`,'z',side*w/2,a.z-a.depth/2,a.z+a.depth/2,a.floor,a.height,door,a.height,a.kind)
  }
 }
 // Stepped outer shell includes side halls and both ramp vestibules.
 const inset=r.perimeterInsetMeters,outline=[[-outer+inset,-d/2],[outer-inset,-d/2],[outer-inset,-d/2+inset],[outer,-d/2+inset],[outer,d/2-inset],[outer-inset,d/2-inset],[outer-inset,d/2],[-outer+inset,d/2],[-outer+inset,d/2-inset],[-outer,d/2-inset],[-outer,-d/2+inset],[-outer+inset,-d/2+inset]]
 const sideHeight=rise+r.hallCeilingMeters
 outline.forEach((p,i)=>{const q=outline[(i+1)%outline.length]!,axis=p[1]===q[1]?'x':'z';wall(`wall-perimeter-${i}`,axis,axis==='x'?p[1]!:p[0]!,Math.min(axis==='x'?p[0]!:p[1]!,axis==='x'?q[0]!:q[1]!),Math.max(axis==='x'?p[0]!:p[1]!,axis==='x'?q[0]!:q[1]!),0,sideHeight,'hall')})
 for(const sign of [-1,1]){
  const side=sign<0?'west':'east',sx=sign*(w/2+apron/2)
  surfaces.push(box(`ceiling:side-${side}`,sx,sideHeight+slab/2,0,apron,slab,d,'slab',open?'hall':'tunnel'))
  const lowerId=`landing-lower-${side}`,upper=roomAt.get(`1/1/${sign<0?0:columns-1}`)!
  const lowerX=sign*(w/2+r.rampLengthMeters+r.corridorWidthMeters/2)
  payload.regions.push({id:lowerId,role:'route',center:{x:lowerX,y:0,z:corridorZ},size:{x:r.corridorWidthMeters,y:sideHeight,z:r.corridorWidthMeters},supportSurfaceId:'floor'})
  const ramp=sign<0?rampBox(`ramp-${side}-0`,-w/2-r.rampLengthMeters,-w/2,0,rise,corridorZ,r.rampWidthMeters,slab):rampBox(`ramp-${side}-0`,w/2,w/2+r.rampLengthMeters,rise,0,corridorZ,r.rampWidthMeters,slab)
  surfaces.push(ramp)
  for(const edge of [-1,1])wall(`room-ramp-wall:${side}:${edge}`,'x',corridorZ+edge*(r.rampWidthMeters+t)/2,Math.min(sign*w/2,sign*(w/2+r.rampLengthMeters)),Math.max(sign*w/2,sign*(w/2+r.rampLengthMeters)),0,sideHeight,'tunnel')
  payload.links.push({id:`link-upper-${side}`,fromRegionId:lowerId,toRegionId:upper.id,exposure:'covered',transitionSurfaceId:ramp.id})
  for(const row of [0,2]){
   const a=roomAt.get(`0/${row}/${sign<0?0:columns-1}`)!
   payload.links.push({id:`side:${side}:${row}`,fromRegionId:a.id,toRegionId:lowerId,exposure:'covered'})
  }
 }
 // Four paired spawn rooms preserve the existing team allocation ordering.
 for(const [side,row,col] of [['east',0,columns-1],['north',0,0],['south',2,columns-1],['west',2,0]] as const){
  const spawnLayer=recipe==='basement-service-loop'?1:0
  const spawnSupport=spawnLayer?'floor-upper-rooms':'floor'
  const room=roomAt.get(`${spawnLayer}/${row}/${col}`)!,id=`spawn-region-${side}`,regionId=`spawn-${side}`
  const inward=col===0?1:-1,offset=r.minimumSpawnSeparationMeters/2
  payload.regions.push({id:regionId,role:'spawn',center:{x:room.x+inward*offset,y:room.floor,z:room.z},size:{x:room.width-t,y:room.height,z:room.depth-t},supportSurfaceId:spawnSupport})
  const anchorIds:string[]=[]
  const coords=[[-1,-1],[-1,1],[1,-1],[1,1],[0,0]]
  for(let i=0;i<r.spawnSlotCount/4;i++){
   const [dx,dz]=coords[i]!,aid=`spawn-${side}-${String(i).padStart(2,'0')}`
   anchorIds.push(aid);payload.spawnAnchors.push({id:aid,spawnRegionId:id,position:{x:room.x+dx!*offset,y:room.floor,z:room.z+dz!*offset},yawRadians:side==='east'?Math.PI:0,supportSurfaceId:spawnSupport})
  }
  payload.spawnRegions.push({id,regionId,anchorIds})
  const horizontal=roomAt.get(`${spawnLayer}/${row}/${col+inward}`)!,vertical=roomAt.get(`${spawnLayer}/1/${col}`)!
  payload.links.push({id:`spawn-link:${side}:open`,fromRegionId:regionId,toRegionId:horizontal.id,exposure:'open'},{id:`spawn-link:${side}:covered`,fromRegionId:regionId,toRegionId:vertical.id,exposure:'covered'})
 }
 // Lamps derive from each local wall; no second presentation/physics shell.
 payload.attachments=surfaces.filter(s=>s.semantics?.classification==='wall'&&!s.id.includes(':lintel')).map(s=>({id:`lamp:${s.id}`,surfaceId:s.id,kind:'lamp'}))
 if(recipe==='basement-service-loop'){
  for(const s of surfaces){s.center.y-=rise;if(s.semantics&&s.center.y<0)s.semantics.sector='basement'}
  for(const r of payload.regions)r.center.y-=rise
  for(const a of payload.spawnAnchors)a.position.y-=rise
 }
 return createArenaDefinition(payload)
}

export function roomRegions(arena:{regions:readonly ArenaRegion[]}){
 return arena.regions.filter(r=>r.id.startsWith('room-')||r.id==='combat-center'||r.id==='combat-upper')
}
