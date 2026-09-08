import { arenaNavigation } from '../ai/navigation'
import { validateArenaPhysicalRoutes as validateLegacyRoutes } from './legacyPhysicalValidationV1'
import type { SupportWaypoint } from '../ai/layerNavigation'
import { createRapierCollisionWorld } from '../collision/rapierWorld'
import type { ValidatedGameDesignProfile } from '../profiles'
import { arenaSupportHeight } from './elevation'
import type { ArenaDefinition, ArenaVector3 } from './arenaDefinition'

const completed = new Set<string>()
/** Disposable derived worlds never advance match RNG/state. */
export async function validateArenaPhysicalRoutes(arena:ArenaDefinition,profile:ValidatedGameDesignProfile):Promise<void> {
  if(arena.generatorVersion==='arena-layers-v1')return validateLegacyRoutes(arena,profile)
  const key=`${arena.contentHash}:${profile.contentHash}`
  if (!arena.recipeId || completed.has(key)) return
  const c=profile.rules.player.capsule,m=profile.rules.player.movement
  const nav=arenaNavigation(arena,profile)
  const verifySupport=(id:string,point:ArenaVector3,support:string|undefined)=>{
    if(arena.schemaVersion===5&&!support)throw new Error(`Missing support: ${id}`)
    if(support){const y=arenaSupportHeight(arena,support,point.x,point.z)
      if(y===undefined||Math.abs(y-point.y)>1e-6)throw new Error(`Declared support mismatch: ${id}/${support}`)}
  }
  for(const r of arena.regions)verifySupport(r.id,r.center,r.supportSurfaceId)
  for(const a of arena.spawnAnchors)verifySupport(a.id,a.position,a.supportSurfaceId)
  const world=await createRapierCollisionWorld(arena,[],{skinWidth:c.skinWidth,autostepMaxHeight:m.autostepMaxHeight,autostepMinWidth:m.autostepMinWidth,snapToGroundDistance:m.snapToGroundDistance,maxSlopeClimbDegrees:m.maxSlopeClimbDegrees,minSlopeSlideDegrees:m.minSlopeSlideDegrees})
  // Acceptance resolution/tolerances derive from capsule and numerical contact skin.
  const stride=c.radius/4,offset=c.halfHeight+c.radius
  const walk=(id:string,start:ArenaVector3,route:readonly ArenaVector3[])=>{
    if(!route.length)throw new Error(`No physical route: ${arena.size}/${arena.seed}/${id}`)
    let position={x:start.x,y:start.y+offset+c.skinWidth,z:start.z}
    const capsuleId='acceptance-capsule'
    world.projectCapsules([{semanticId:capsuleId,position,radius:c.radius,halfHeight:c.halfHeight}])
    for(const target of [start,...route]){
      const limit=Math.ceil(Math.hypot(target.x-position.x,target.z-position.z)/stride)*3+60
      let movement=world.moveCapsule(capsuleId,{x:0,y:-c.skinWidth,z:0})
      for(let step=0;step<limit;step++){
        position={...movement.position}
        const dx=target.x-position.x,dz=target.z-position.z,d=Math.hypot(dx,dz)
        if(d<=c.skinWidth&&movement.grounded)break
        movement=world.moveCapsule(capsuleId,{x:d?dx/d*Math.min(stride,d):0,y:-c.skinWidth,z:d?dz/d*Math.min(stride,d):0})
      }
      position={...movement.position}
      const support=(target as Partial<SupportWaypoint>).supportSurfaceId
      const surface=arena.surfaces.find(s=>s.id===support)
      const ny=surface?1-2*(surface.orientation.x**2+surface.orientation.z**2):1
      const tangent=c.radius*(1/ny-1)
      if(Math.hypot(target.x-position.x,target.z-position.z)>stride*2||!movement.grounded||Math.abs(position.y-offset-target.y)>tangent+c.skinWidth*4)
        throw new Error(`Capsule route blocked: ${arena.size}/${arena.seed}/${id}/target=${JSON.stringify(target)}/at=${JSON.stringify(position)}/grounded=${movement.grounded}/contacts=${movement.contacts.map(c=>c.semanticId)}`)
      // Rapier shape intersection includes touching support within its contact
      // tolerance; support height/contact is checked independently below.
      const supports=world.overlapCapsule({semanticId:'support-probe',position:{...position,y:position.y-tangent-c.skinWidth*2},radius:c.radius,halfHeight:c.halfHeight},[capsuleId])
        .filter(hit=>{const y=arenaSupportHeight(arena,hit.semanticId,target.x,target.z);return y!==undefined&&Math.abs(y-target.y)<=c.skinWidth*2}).map(hit=>hit.semanticId)
      const overlaps=world.overlapCapsule({semanticId:capsuleId,position,radius:c.radius,halfHeight:c.halfHeight},[capsuleId,...supports])
      if(overlaps.length)
        throw new Error(`Capsule/headroom penetration: ${id}/${support}/${JSON.stringify(position)}/${overlaps.map(hit=>hit.semanticId)}`)
      if(support) {
        const identity=supports.some(id=>{
          if(id===support)return true
          // Shared ramp endpoint may be supported by either coincident face.
          const actual=arenaSupportHeight(arena,id,target.x,target.z)
          const expected=arenaSupportHeight(arena,support,target.x,target.z)
          return actual!==undefined&&expected!==undefined&&Math.abs(actual-expected)<=c.skinWidth*2
        })
        if(!identity)throw new Error(`Physical support mismatch: ${id}/${support}/${supports}`)
      }
    }
  }
  try{
    for(const anchor of arena.spawnAnchors)for(const region of arena.regions.filter(r=>r.role==='combat'))for(const reverse of [false,true]){
      const a={...anchor.position,supportSurfaceId:anchor.supportSurfaceId},b={...region.center,supportSurfaceId:region.supportSurfaceId}
      const start=reverse?b:a,goal=reverse?a:b
      walk(`${anchor.id}/${region.id}/${reverse}`,start,nav.route(start,goal))
    }
    for(const link of arena.links.filter(l=>l.transitionSurfaceId)){
      const transition=nav.transitions?.find(t=>t.id===link.id)
      if(!transition)throw new Error(`Invalid physical transition endpoints: ${link.id}/${link.transitionSurfaceId}`)
      const first=transition.points[0]!,last=transition.points.at(-1)!
      const dx=last.x-first.x,dz=last.z-first.z,d=Math.hypot(dx,dz)
      const ramp=arena.surfaces.find(s=>s.id===transition.surfaceId)!
      if(ramp.size.z+1e-6<6*(c.radius+c.skinWidth))throw new Error(`Transition narrower than three capsules: ${link.id}`)
      for(const lane of [-1,0,1])for(const reverse of [false,true]){
        const lateral=lane*2*(c.radius+c.skinWidth)
        const points=transition.points.map(p=>({...p,x:p.x-dz/d*lateral,z:p.z+dx/d*lateral}))
        if(reverse)points.reverse()
        const start=reverse?last:first,end=reverse?first:last
        // Sweep sideways across the landing, turn into the chosen ramp lane,
        // then turn back to the exit centre instead of testing a line only.
        walk(`${link.id}/lane=${lane}/reverse=${reverse}`,start,[...points,end])
      }
    }
    completed.add(key)
  }finally{world.dispose()}
}
