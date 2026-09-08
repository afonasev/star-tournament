import type { ValidatedGameDesignProfile } from '../profiles'
import type { ScenarioVector3 } from '../scenario'

export class StairCameraSmoothing {
 private tick=-1;private life:string|undefined;private previousY=0;private previousX=0;private previousZ=0;private offset=0
 update(input:{tick:number;lifeId?:string;grounded?:boolean;alive?:boolean;position:ScenarioVector3;onStairs:boolean;aspect:number},profile:ValidatedGameDesignProfile,ticksPerSecond:number):number {
  const c=profile.rules.player.capsule,eye=profile.rules.player.eyeHeight,camera=profile.rules.camera
  const dt=(input.tick-this.tick)/ticksPerSecond
  const reset=this.tick<0||dt<0||input.lifeId!==this.life||input.alive===false||!input.grounded||Math.abs(input.position.y-this.previousY)>c.radius+c.halfHeight||Math.hypot(input.position.x-this.previousX,input.position.z-this.previousZ)>profile.rules.player.movement.maximumGroundSpeed*Math.max(0,dt)+c.radius
  if(reset||!camera.stairSmoothingSeconds)this.offset=0
  else if(dt>0){
   const retained=Math.exp(-dt/camera.stairSmoothingSeconds)
   this.offset=(this.offset+(input.onStairs?this.previousY-input.position.y:0))*retained
  }
  const sphere=camera.nearPlane*Math.sqrt(1+Math.tan(camera.fieldOfViewDegrees*Math.PI/360)**2*(1+input.aspect**2))
  // A sphere enclosing the near plane remains inside the collision capsule.
  const reach=Math.max(0,c.halfHeight+c.radius-sphere),limit=camera.stairMaximumOffsetMeters
  this.offset=Math.max(-limit,Math.min(limit,this.offset))
  if(sphere>c.radius||eye>reach)this.offset=0
  else this.offset=Math.max(-reach-eye,Math.min(reach-eye,this.offset))
  this.tick=input.tick;this.life=input.lifeId;this.previousY=input.position.y;this.previousX=input.position.x;this.previousZ=input.position.z
  return this.offset
 }
}
