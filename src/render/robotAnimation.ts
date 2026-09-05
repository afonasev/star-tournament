import * as THREE from 'three'

export type RobotAnimationState = 'idle' | 'walk' | 'run' | 'strafe-left' | 'strafe-right' | 'jump-takeoff' | 'jump-airborne' | 'jump-landing' | 'aim' | 'fire' | 'hit' | 'destroyed'

const REQUIRED_JOINTS = [
  'robot:rig:pelvis', 'robot:rig:spine', 'robot:rig:head', 'robot:rig:weapon-mount',
  'robot:rig:shoulder:left', 'robot:rig:elbow:left', 'robot:rig:wrist:left',
  'robot:rig:shoulder:right', 'robot:rig:elbow:right', 'robot:rig:wrist:right',
  'robot:rig:hip:left', 'robot:rig:knee:left', 'robot:rig:ankle:left',
  'robot:rig:hip:right', 'robot:rig:knee:right', 'robot:rig:ankle:right',
] as const

type JointName = (typeof REQUIRED_JOINTS)[number]
type Pose = Map<JointName, THREE.Euler>

export interface RobotAnimationController {
  readonly weaponMount: THREE.Object3D
  present: (state: RobotAnimationState, seconds: number) => void
}

function neutralPose(): Pose { return new Map(REQUIRED_JOINTS.map((name) => [name, new THREE.Euler()])) }

function targetPose(state: RobotAnimationState, seconds: number): Pose {
  const pose = neutralPose(); const joint = (name: JointName) => pose.get(name)!
  const pulse = Math.sin(seconds * Math.PI * 2)
  const leftLeg = joint('robot:rig:hip:left'); const rightLeg = joint('robot:rig:hip:right')
  const leftShoulder = joint('robot:rig:shoulder:left'); const rightShoulder = joint('robot:rig:shoulder:right')
  if (state === 'idle') { joint('robot:rig:spine').z = pulse * .018; joint('robot:rig:head').y = pulse * .045 }
  const stride = state === 'run' ? .65 : state === 'walk' || state.startsWith('strafe') ? .42 : 0
  if (stride !== 0) {
    const side = state === 'strafe-left' ? -1 : state === 'strafe-right' ? 1 : 0
    leftLeg.x = pulse * stride; rightLeg.x = -pulse * stride
    joint('robot:rig:knee:left').x = Math.max(0, -pulse) * stride * .8; joint('robot:rig:knee:right').x = Math.max(0, pulse) * stride * .8
    leftShoulder.x = -pulse * stride * .72; rightShoulder.x = pulse * stride * .72; joint('robot:rig:pelvis').z = side * .08
  }
  if (state.startsWith('jump-')) {
    const crouch = state === 'jump-takeoff' ? -.28 : state === 'jump-landing' ? .32 : .08
    leftLeg.x = rightLeg.x = crouch; joint('robot:rig:knee:left').x = joint('robot:rig:knee:right').x = -crouch * 1.5
  }
  if (state === 'aim' || state === 'fire') {
    leftShoulder.x = rightShoulder.x = -.62; joint('robot:rig:elbow:left').x = joint('robot:rig:elbow:right').x = -.48
    joint('robot:rig:weapon-mount').x = state === 'fire' ? .26 : -.12
  }
  if (state === 'hit') joint('robot:rig:spine').z = Math.sin(seconds * 20) * .15
  if (state === 'destroyed') {
    joint('robot:rig:pelvis').z = .25; joint('robot:rig:spine').z = -.42
    leftShoulder.x = .35; rightShoulder.x = -.35; leftLeg.x = .32; rightLeg.x = -.24
  }
  return pose
}

function transitionRate(state: RobotAnimationState): number { return state === 'fire' || state === 'hit' ? 22 : state === 'destroyed' ? 9 : 12 }

export function createRobotAnimationController(model: THREE.Object3D): RobotAnimationController | undefined {
  const semanticJoint = (name: string) => {
    const direct = model.getObjectByName(name); if (direct !== undefined) return direct
    let authored: THREE.Object3D | undefined; model.traverse((object) => { if (object.userData.name === name) authored = object }); return authored
  }
  const joints = new Map(REQUIRED_JOINTS.map((name) => [name, semanticJoint(name)]))
  if ([...joints.values()].some((joint) => joint === undefined)) return undefined
  const joint = (name: JointName) => joints.get(name)!
  let previousSeconds: number | undefined
  const present = (state: RobotAnimationState, seconds: number) => {
    const target = targetPose(state, seconds)
    const elapsed = previousSeconds === undefined ? 0 : Math.min(.1, Math.max(0, seconds - previousSeconds))
    const alpha = previousSeconds === undefined ? 1 : 1 - Math.exp(-transitionRate(state) * elapsed)
    for (const name of REQUIRED_JOINTS) joint(name).rotation.set(
      THREE.MathUtils.lerp(joint(name).rotation.x, target.get(name)!.x, alpha),
      THREE.MathUtils.lerp(joint(name).rotation.y, target.get(name)!.y, alpha),
      THREE.MathUtils.lerp(joint(name).rotation.z, target.get(name)!.z, alpha),
    )
    previousSeconds = seconds
  }
  return { weaponMount: joint('robot:rig:weapon-mount'), present }
}
