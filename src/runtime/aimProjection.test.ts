import {expect,it} from 'vitest'
import {createAimProjectionQuery} from './aimProjection'
import {PROTOTYPE_V1_PROFILE as profile} from '../profiles'
import type {FirstPersonRenderSnapshot} from '../render/firstPersonRenderer'
import type {StaticRaycastPort} from '../combat/occlusion'

it('projects the authoritative centre hit and rejects cover exposed by camera smoothing without mutating state',()=>{
 const snapshot:FirstPersonRenderSnapshot={tick:10,participants:[{semanticId:'player',kind:'local-seat',position:{x:0,y:1,z:0},yaw:0,pitch:0,grounded:true,alive:true,weapon:{semanticId:'double-barrel-shotgun',ammo:20}}],events:[],corpses:[]}
 const before=JSON.stringify(snapshot),eye=1+profile.rules.player.eyeHeight
 const collision:StaticRaycastPort={raycast(origin,direction,range,excluded){
  expect(excluded).toContain('player')
  // Back wall is the authoritative hit. Lowered eye exposes a closer cover lip.
  const z=origin.y<eye-.1?-1:-4,distance=(z-origin.z)/direction.z
  return distance>=0&&distance<=range?[{semanticId:'wall',distance,point:{x:origin.x+direction.x*distance,y:origin.y+direction.y*distance,z},normal:{x:0,y:0,z:1}}]:[]
 }}
 const aim=createAimProjectionQuery(profile,collision)(snapshot)
 expect(aim.point).toEqual({x:0,y:eye,z:-4})
 expect(aim.isVisibleFrom({x:0,y:eye,z:0})).toBe(true)
 expect(aim.isVisibleFrom({x:0,y:eye-.2,z:0})).toBe(false)
 expect(JSON.stringify(snapshot)).toBe(before)
})
