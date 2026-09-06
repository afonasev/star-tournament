import { arenaNavigation } from '../ai/navigation'
import { createRapierCollisionWorld } from '../collision/rapierWorld'
import type { ValidatedGameDesignProfile } from '../profiles'
import { arenaSupportHeight } from './elevation'
import { supportsNavigation, type ArenaDefinition, type ArenaVector3 } from './arenaDefinition'

const completed = new Set<string>()
/** Physical acceptance uses disposable derived worlds and never advances match RNG/state. */
export async function validateArenaPhysicalRoutes(arena:ArenaDefinition,profile:ValidatedGameDesignProfile):Promise<void> {
  if (!arena.recipeId || completed.has(arena.contentHash)) return
  const verifyDeclaredSupport = (id: string, point: ArenaVector3, supportSurfaceId: string | undefined): void => {
    if (!supportSurfaceId) return
    const height = arenaSupportHeight(arena, supportSurfaceId, point.x, point.z)
    if (height === undefined || Math.abs(height - point.y) > 1e-6) {
      throw new Error(`Declared support mismatch: ${id}/${supportSurfaceId}/${JSON.stringify(point)}`)
    }
  }
  for (const region of arena.regions) verifyDeclaredSupport(region.id, region.center, region.supportSurfaceId)
  for (const anchor of arena.spawnAnchors) verifyDeclaredSupport(anchor.id, anchor.position, anchor.supportSurfaceId)
  for (const link of arena.links) {
    if (!link.transitionSurfaceId) continue
    const transition = arena.surfaces.find(surface => surface.id === link.transitionSurfaceId)
    if (!transition || !supportsNavigation(transition) || transition.semantics?.classification !== 'ramp') {
      throw new Error(`Invalid physical transition: ${link.id}/${link.transitionSurfaceId}`)
    }
  }
  const c=profile.rules.player.capsule,m=profile.rules.player.movement
  const world=await createRapierCollisionWorld(arena,[],{skinWidth:c.skinWidth,autostepMaxHeight:m.autostepMaxHeight,autostepMinWidth:m.autostepMinWidth,snapToGroundDistance:m.snapToGroundDistance,maxSlopeClimbDegrees:m.maxSlopeClimbDegrees,minSlopeSlideDegrees:m.minSlopeSlideDegrees})
  const nav=arenaNavigation(arena,profile)
  const center=arena.regions.find(r=>r.id==='combat-center')!.center
  // Acceptance sampling resolution is derived from capsule radius, not movement balance.
  const stride=c.radius/4
  try {
    for(const anchor of arena.spawnAnchors) for(const reverse of [false,true]) {
      const start=reverse ? center : anchor.position, goal=reverse ? anchor.position : center
      let position:ArenaVector3={...start,y:start.y+c.halfHeight+c.radius+c.skinWidth}
      world.projectCapsules([{semanticId:'acceptance-capsule',position,radius:c.radius,halfHeight:c.halfHeight}])
      const route=nav.route(start,goal)
      if (!route.length) throw new Error(`No physical route: ${anchor.id}`)
      let contactIds:string[]=[]
      for(const target of route) {
        const limit=Math.ceil(Math.hypot(target.x-position.x,target.z-position.z)/stride)*3+60
        for(let step=0;step<limit;step++) {
          const dx=target.x-position.x,dz=target.z-position.z,d=Math.hypot(dx,dz)
          if(d<=c.skinWidth) break
          const movement=world.moveCapsule('acceptance-capsule',{x:dx/d*Math.min(stride,d),y:-c.skinWidth,z:dz/d*Math.min(stride,d)})
          position={...movement.position};contactIds=movement.contacts.map(c=>c.semanticId)
        }
        if(Math.hypot(target.x-position.x,target.z-position.z)>stride*2) throw new Error(`Capsule route blocked: ${arena.size}/${arena.seed}/${anchor.id}/${reverse}/${JSON.stringify(target)}/at=${JSON.stringify(position)}/contacts=${contactIds}/route=${JSON.stringify(route)}`)
      }
    }
    completed.add(arena.contentHash)
  } finally {world.dispose()}
}
