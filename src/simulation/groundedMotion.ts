import { arenaSupportHeight } from '../arena/elevation'
import { supportsNavigation, type ArenaDefinition } from '../arena/arenaDefinition'
import type { CollisionVector3 } from '../collision/contracts'
import type { ValidatedGameDesignProfile } from '../profiles'

/** Preserve XZ intent on the contacted walkable plane; KCC still resolves the whole move. */
export function groundedTranslation(arena:ArenaDefinition,profile:ValidatedGameDesignProfile,position:CollisionVector3,desired:CollisionVector3):CollisionVector3 {
  const c=profile.rules.player.capsule,m=profile.rules.player.movement,feet=position.y-c.radius-c.halfHeight
  let best:{error:number;x:number;y:number;z:number}|undefined
  for(const s of arena.surfaces){
    if(!supportsNavigation(s)||s.semantics?.movement===false)continue
    const q=s.orientation,n={x:2*(q.x*q.y-q.z*q.w),y:1-2*(q.x*q.x+q.z*q.z),z:2*(q.y*q.z+q.x*q.w)}
    if(n.y<=0||Math.acos(Math.min(1,n.y))*180/Math.PI>m.maxSlopeClimbDegrees)continue
    const y=arenaSupportHeight(arena,s.id,position.x,position.z)
    if(y===undefined)continue
    const error=Math.abs(feet-y-c.radius*(1/n.y-1))
    if(error<=m.snapToGroundDistance+c.skinWidth*4&&(!best||error<best.error))best={...n,error}
  }
  if(!best)return {...desired,y:-c.skinWidth}
  // A small inward NORMAL component supplies contact, rather than replacing uphill Y
  // with a downward world-axis probe (which repeatedly dissipated horizontal speed).
  const rise=-(best.x*desired.x+best.z*desired.z)/best.y
  return {x:desired.x-best.x*c.skinWidth,y:rise-best.y*c.skinWidth,z:desired.z-best.z*c.skinWidth}
}
