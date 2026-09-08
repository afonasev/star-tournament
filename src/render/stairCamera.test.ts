import {expect,it} from 'vitest'
import {StairCameraSmoothing} from './stairCamera'
import {PROTOTYPE_V1_PROFILE as profile} from '../profiles'

const pose=(tick:number,y:number)=>({tick,lifeId:'life-1',grounded:true,alive:true,position:{x:0,y,z:0},onStairs:true,aspect:16/9})
it('smooths a step without changing simulation position and settles after stopping',()=>{
 const camera=new StairCameraSmoothing(),start=pose(0,1),step=pose(1,1.3)
 expect(camera.update(start,profile,60)).toBe(0)
 const offset=camera.update(step,profile,60)
 expect(offset).toBeLessThan(0);expect(offset).toBeGreaterThan(-.3)
 expect(step.position.y).toBe(1.3)
 expect(Math.abs(camera.update({...pose(61,1.3),onStairs:false},profile,60))).toBeLessThan(.001)
})
it('keeps the complete camera near plane inside the capsule on ascent and descent',()=>{
 const c=profile.rules.player.capsule,p=profile.rules.camera
 for(const aspect of [1,16/9,32/9])for(const sign of [-1,1]){
  const camera=new StairCameraSmoothing();camera.update({...pose(0,1),aspect},profile,60)
  const offset=camera.update({...pose(1,1+sign*.3),aspect},profile,60)
  const radius=p.nearPlane*Math.sqrt(1+Math.tan(p.fieldOfViewDegrees*Math.PI/360)**2*(1+aspect**2))
  expect(Math.abs(profile.rules.player.eyeHeight+offset)+radius).toBeLessThanOrEqual(c.radius+c.halfHeight)
 }
})
it('resets on jump, death, new life, rewind and teleport',()=>{
 for(const changed of [{grounded:false},{alive:false},{lifeId:'life-2'},{tick:0},{position:{x:20,y:1.3,z:0}}]){
  const camera=new StairCameraSmoothing();camera.update(pose(0,1),profile,60);camera.update(pose(1,1.3),profile,60)
  expect(camera.update({...pose(2,1.3),...changed},profile,60)).toBe(0)
 }
})
