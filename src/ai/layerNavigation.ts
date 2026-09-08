import { arenaSupportHeight } from '../arena/elevation'
import { supportsNavigation, type ArenaBoxSurface, type ArenaDefinition } from '../arena/arenaDefinition'
import type { CollisionVector3 as Vec } from '../collision/contracts'
import type { ValidatedGameDesignProfile } from '../profiles'
import { simulationMath } from '../core/simulationMath'
import type { NavigationCapabilities } from './navigation'

export interface SupportWaypoint extends Vec { readonly supportSurfaceId: string }
export interface NavigationTransition { id: string; surfaceId: string; points: readonly SupportWaypoint[] }
interface Bounds { id: string; x: number; y: number; z: number; hx: number; hy: number; hz: number }
const length = (a: Vec, b: Vec) => simulationMath.hypot(a.x-b.x, a.y-b.y, a.z-b.z)
// Geometry epsilon only absorbs double precision shared-face roundoff.
const EPS = 1e-6
function bounds(s: ArenaBoxSurface): Bounds {
  const {x,y,z,w}=s.orientation
  return {id:s.id,...s.center,
    hx:(Math.abs(1-2*(y*y+z*z))*s.size.x+Math.abs(2*(x*y-z*w))*s.size.y+Math.abs(2*(x*z+y*w))*s.size.z)/2,
    hy:(Math.abs(2*(x*y+z*w))*s.size.x+Math.abs(1-2*(x*x+z*z))*s.size.y+Math.abs(2*(y*z-x*w))*s.size.z)/2,
    hz:(Math.abs(2*(x*z-y*w))*s.size.x+Math.abs(2*(y*z+x*w))*s.size.y+Math.abs(1-2*(x*x+y*y))*s.size.z)/2}
}
function intersects(a: Vec,b: Vec,box: Bounds,radius: number,height: number): boolean {
  let lo=0,hi=1
  for(const axis of ['x','y','z'] as const) {
    const minimum=box[axis]-box[axis==='x'?'hx':axis==='y'?'hy':'hz']-(axis==='y'?height:radius)
    const maximum=box[axis]+box[axis==='x'?'hx':axis==='y'?'hy':'hz']+(axis==='y'?-EPS:radius)
    const delta=b[axis]-a[axis]
    if(Math.abs(delta)<EPS) {if(a[axis]<minimum||a[axis]>maximum)return false;continue}
    const first=(minimum-a[axis])/delta,last=(maximum-a[axis])/delta
    lo=Math.max(lo,Math.min(first,last));hi=Math.min(hi,Math.max(first,last))
    if(lo>hi)return false
  }
  return true
}

/** Schema-5 visibility graph. Every ordinary edge belongs to exactly one support. */
export function layerNavigation(arena: ArenaDefinition,profile: ValidatedGameDesignProfile): NavigationCapabilities {
  const c=profile.rules.player.capsule
  const radius=c.radius, height=2*(c.radius+c.halfHeight)+c.skinWidth
  const margin=radius+c.skinWidth+profile.rules.bots.navigation.clearance+profile.rules.bots.navigation.waypointRadius
  const supports=arena.surfaces.filter(supportsNavigation).sort((a,b)=>a.id.localeCompare(b.id,'en'))
  const solids=arena.surfaces.filter(s=>s.semantics?.movement!==false).map(bounds)
  const supportById=new Map(supports.map(s=>[s.id,s]))
  const supportGeometry=new Map(supports.map(s=>[s.id,{surfaces:[s]}]))
  const blockersBySupport=new Map(supports.map(s=>{
    const b=bounds(s),flat=s.orientation.x===0&&s.orientation.z===0
    const low=flat?b.y+b.hy:b.y-b.hy,high=b.y+b.hy
    return [s.id,solids.filter(box=>box.id!==s.id&&box.y+box.hy>=low&&box.y-box.hy<=high+height)]
  }))
  const on=(id: string,p: Vec): SupportWaypoint|undefined => {
    const geometry=supportGeometry.get(id)
    const y=geometry&&arenaSupportHeight(geometry,id,p.x,p.z)
    return y===undefined?undefined:{x:p.x,y,z:p.z,supportSurfaceId:id}
  }
  const resolve=(p: Vec): SupportWaypoint|undefined => {
    const explicit=(p as Partial<SupportWaypoint>).supportSurfaceId
    if(explicit)return on(explicit,p)
    return supports.map(s=>on(s.id,p)).filter((v):v is SupportWaypoint=>!!v)
      .filter(v=>Math.abs(v.y-p.y)<=c.radius+c.skinWidth+profile.rules.player.movement.snapToGroundDistance)
      .sort((a,b)=>Math.abs(a.y-p.y)-Math.abs(b.y-p.y)||a.supportSurfaceId.localeCompare(b.supportSurfaceId,'en'))[0]
  }
  const clearOn=(a: SupportWaypoint,b: SupportWaypoint,ignore: readonly string[]=[]): boolean => {
    if(a.supportSurfaceId!==b.supportSurfaceId||!on(a.supportSurfaceId,a)||!on(a.supportSurfaceId,b))return false
    const s=supportById.get(a.supportSurfaceId)!
    // Convex supports need only endpoints; inset prevents a capsule following an open edge.
    for(const p of [a,b]) for(const [dx,dz] of [[radius,0],[-radius,0],[0,radius],[0,-radius]])
      if(arenaSupportHeight(supportGeometry.get(s.id)!,s.id,p.x+dx!,p.z+dz!)===undefined)return false
    return !blockersBySupport.get(s.id)!.some(box=>!ignore.includes(box.id)&&intersects(a,b,box,radius,height))
  }
  const clear=(a: Vec,b: Vec) => {const first=resolve(a),last=resolve(b);return !!first&&!!last&&clearOn(first,last)}
  const nodes:SupportWaypoint[]=[];const keys=new Map<string,number>()
  const add=(p:SupportWaypoint):number=>{
    const key=`${p.supportSurfaceId}:${p.x}:${p.y}:${p.z}`
    const found=keys.get(key);if(found!==undefined)return found
    keys.set(key,nodes.length);nodes.push(p);return nodes.length-1
  }
  for(const r of arena.regions){const p=r.supportSurfaceId&&on(r.supportSurfaceId,r.center);if(p&&clearOn(p,p))add(p)}
  for(const a of arena.spawnAnchors){const p=a.supportSurfaceId&&on(a.supportSurfaceId,a.position);if(p&&clearOn(p,p))add(p)}
  for(const s of supports) {
    if(s.semantics?.classification==='ramp')continue
    const base=on(s.id,s.center);if(!base)continue
    const relevant=solids.filter(box=>box.id!==s.id&&box.y+box.hy>base.y+EPS&&box.y-box.hy<base.y+height)
    for(const box of [bounds(s),...relevant]) for(const sx of [-1,1])for(const sz of [-1,1]) {
      const inset=box.id===s.id?-margin:margin
      const p=on(s.id,{x:box.x+sx*(box.hx+inset),y:base.y,z:box.z+sz*(box.hz+inset)})
      if(p&&clearOn(p,p))add(p)
    }
  }
  const transitions:NavigationTransition[]=[]
  const transitionEdges:Array<{a:number;b:number;points:SupportWaypoint[]}> = []
  for(const link of [...arena.links].sort((a,b)=>a.id.localeCompare(b.id,'en'))) {
    if(!link.transitionSurfaceId)continue
    const ramp=supports.find(s=>s.id===link.transitionSurfaceId&&s.semantics?.classification==='ramp')
    const from=arena.regions.find(r=>r.id===link.fromRegionId)?.supportSurfaceId
    const to=arena.regions.find(r=>r.id===link.toRegionId)?.supportSurfaceId
    if(!ramp||!from||!to||from===to)continue
    const q=ramp.orientation
    const along={x:1-2*(q.y*q.y+q.z*q.z),y:2*(q.x*q.y+q.z*q.w),z:2*(q.x*q.z-q.y*q.w)}
    const normal={x:2*(q.x*q.y-q.z*q.w),y:1-2*(q.x*q.x+q.z*q.z),z:2*(q.y*q.z+q.x*q.w)}
    const top={x:ramp.center.x+normal.x*ramp.size.y/2,y:ramp.center.y+normal.y*ramp.size.y/2,z:ramp.center.z+normal.z*ramp.size.y/2}
    const planar=simulationMath.hypot(along.x,along.z)
    if(planar<EPS||Math.abs(along.y)<EPS)continue
    const ends=[-1,1].map(sign=>({x:top.x+sign*along.x*ramp.size.x/2,y:top.y+sign*along.y*ramp.size.x/2,z:top.z+sign*along.z*ramp.size.x/2}))
    const landings=ends.map((p,index)=>{
      const sign=index===0?-1:1
      const outside={x:p.x+sign*along.x/planar*margin,y:p.y,z:p.z+sign*along.z/planar*margin}
      return [from,to].map(id=>on(id,outside)).find(v=>v&&Math.abs(v.y-p.y)<EPS)
    })
    const [a,b]=landings;if(!a||!b||a.supportSurfaceId===b.supportSurfaceId||!clearOn(a,a)||!clearOn(b,b))continue
    const points:SupportWaypoint[]=[a,...ends.map(p=>({...p,supportSurfaceId:ramp.id})),b]
    // An explicit transition still must be clear of every OTHER solid.
    if(points.slice(1).some((p,i)=>solids.some(box=>![ramp.id,from,to].includes(box.id)&&intersects(points[i]!,p,box,radius,height))))continue
    transitions.push({id:link.id,surfaceId:ramp.id,points})
    transitionEdges.push({a:add(a),b:add(b),points})
  }
  const edges=nodes.map(()=>[] as Array<{to:number;cost:number;points:SupportWaypoint[]}>)
  for(let i=0;i<nodes.length;i++)for(let j=i+1;j<nodes.length;j++)if(clearOn(nodes[i]!,nodes[j]!)) {
    const cost=length(nodes[i]!,nodes[j]!);edges[i]!.push({to:j,cost,points:[nodes[j]!]});edges[j]!.push({to:i,cost,points:[nodes[i]!]})
  }
  for(const e of transitionEdges) {
    const cost=e.points.slice(1).reduce((sum,p,i)=>sum+length(e.points[i]!,p),0)
    edges[e.a]!.push({to:e.b,cost,points:e.points.slice(1)})
    edges[e.b]!.push({to:e.a,cost,points:[...e.points].reverse().slice(1)})
  }
  return {destinations:nodes.filter(p=>arena.regions.some(r=>r.center.x===p.x&&r.center.z===p.z&&r.supportSurfaceId===p.supportSurfaceId)),clear,transitions,
    reached(a,b,tolerance){
      const p=resolve(a),target=resolve(b)
      if(!p||!target||length(a,b)>=tolerance)return false
      if(p.supportSurfaceId===target.supportSurfaceId)return true
      return transitions.some(t=>t.points.some(point=>length(point,b)<EPS)&&t.points.some(point=>point.supportSurfaceId===p.supportSurfaceId)&&Math.abs(p.y-target.y)<c.skinWidth*4)
    },
    canStep(a,b,target){
      if(clear(a,b))return true
      const start=resolve(a),goal=resolve(target)
      if(!start||!goal)return false
      const transition=transitions.find(t=>t.surfaceId===start.supportSurfaceId||t.surfaceId===goal.supportSurfaceId||t.points.some(p=>p.supportSurfaceId===goal.supportSurfaceId&&length(p,goal)<EPS))
      if(!transition)return false
      const first=transition.points[0]!,last=transition.points.at(-1)!,dx=last.x-first.x,dz=last.z-first.z,d=simulationMath.hypot(dx,dz)
      const ramp=supports.find(s=>s.id===transition.surfaceId)!
      const progress=((b.x-first.x)*dx+(b.z-first.z)*dz)/(d*d)
      const lateral=Math.abs((b.x-first.x)*dz-(b.z-first.z)*dx)/d
      if(progress<0||progress>1||lateral>ramp.size.z/2-radius-c.skinWidth)return false
      const ignored=transition.points.map(p=>p.supportSurfaceId)
      return !solids.some(box=>!ignored.includes(box.id)&&intersects(a,b,box,radius,height))
    },
    route(a,b) {
      const start=resolve(a),goal=resolve(b);if(!start||!goal)return []
      if(clearOn(start,goal))return [goal]
      const costs=nodes.map(p=>clearOn(start,p)?length(start,p):Infinity)
      const paths=nodes.map(p=>[p]);const visited=new Set<number>();let best:SupportWaypoint[]=[];let bestCost=Infinity
      // Resume on a selected ramp after replanning, without inventing a cross-layer edge.
      for(const t of transitions)if(start.supportSurfaceId===t.surfaceId)for(const sequence of [t.points,[...t.points].reverse()]) {
        const landing=sequence.at(-1)!,index=keys.get(`${landing.supportSurfaceId}:${landing.x}:${landing.y}:${landing.z}`)!
        const exit=sequence[sequence.length-2]!
        costs[index]=length(start,exit)+length(exit,landing);paths[index]=[exit,landing]
      }
      for(let step=0;step<nodes.length;step++) {
        let i=-1,cost=Infinity
        for(let j=0;j<nodes.length;j++)if(!visited.has(j)&&costs[j]!<cost){i=j;cost=costs[j]!}
        if(i<0||cost>=bestCost)break
        visited.add(i)
        if(cost+length(nodes[i]!,goal)<bestCost&&clearOn(nodes[i]!,goal)){best=[...paths[i]!,goal];bestCost=cost+length(nodes[i]!,goal)}
        // A moving target may stand inside a declared ramp, rather than at its exit.
        for(const t of transitions)if(goal.supportSurfaceId===t.surfaceId)for(const sequence of [t.points,[...t.points].reverse()]) {
          const landing=sequence[0]!,entry=sequence[1]!
          if(length(nodes[i]!,landing)>EPS)continue
          const total=cost+length(landing,entry)+length(entry,goal)
          if(total<bestCost){best=[...paths[i]!,entry,goal];bestCost=total}
        }
        for(const e of edges[i]!)if(cost+e.cost<costs[e.to]!){costs[e.to]=cost+e.cost;paths[e.to]=[...paths[i]!,...e.points]}
      }
      return best
    },
  }
}
