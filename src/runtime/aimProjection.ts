import { resolvePelletImpacts, type StaticRaycastPort } from '../combat/occlusion'
import { firstPersonBasisFromYawPitch } from '../combat/pelletPattern'
import { COMBAT_SLICE_SCENARIO_V1 } from '../scenario'
import type { FirstPersonRenderSnapshot, FirstPersonRendererOptions } from '../render/firstPersonRenderer'
import type { ValidatedGameDesignProfile } from '../profiles'

/** Read-only central combat ray. No spread RNG, damage, or collision-world mutation. */
export function createAimProjectionQuery(profile:ValidatedGameDesignProfile,collision:StaticRaycastPort):NonNullable<FirstPersonRendererOptions['aimQuery']> {
 return (snapshot:FirstPersonRenderSnapshot)=>{
  const player=snapshot.participants.find(p=>p.kind==='local-seat')!
  const offset=profile.rules.player.capsule.radius+profile.rules.player.capsule.halfHeight
  const targets=snapshot.participants.filter(p=>p.semanticId!==player.semanticId&&p.alive!==false).map(p=>({...COMBAT_SLICE_SCENARIO_V1.targets[0]!,semanticId:p.semanticId,name:p.semanticId,position:{...p.position,y:p.position.y-(p.kind==='stationary-fixture'?0:offset)},yawRadians:p.yaw}))
  const active=new Set(targets.map(p=>p.semanticId)),excluded=[player.semanticId,...active]
  const origin={...player.position,y:player.position.y+profile.rules.player.eyeHeight}
  const direction=firstPersonBasisFromYawPitch(player.yaw,'pitch' in player?player.pitch:0).forward,range=profile.rules.weapons.doubleBarrelShotgun.rangeMeters
  const hit=resolvePelletImpacts(origin,[{pelletIndex:0,direction}],range,targets,active,collision,excluded)[0]
  const point=hit?.position??{x:origin.x+direction.x*range,y:origin.y+direction.y*range,z:origin.z+direction.z*range}
  return {point,isVisibleFrom(camera){
    const d=Math.hypot(point.x-camera.x,point.y-camera.y,point.z-camera.z)
    if(d===0)return true
    const ray={x:(point.x-camera.x)/d,y:(point.y-camera.y)/d,z:(point.z-camera.z)/d}
    const blocker=resolvePelletImpacts(camera,[{pelletIndex:0,direction:ray}],d,targets,active,collision,excluded)[0]
    return !blocker||blocker.distance>=d-profile.rules.player.capsule.skinWidth*2
  }}
 }
}
