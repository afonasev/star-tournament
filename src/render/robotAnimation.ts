import * as THREE from 'three'

export type RobotAnimationState = 'idle' | 'walk' | 'run' | 'strafe-left' | 'strafe-right' | 'jump-takeoff' | 'jump-airborne' | 'jump-landing' | 'aim' | 'fire' | 'hit' | 'destroyed'

const REQUIRED_JOINTS = [
  'robot:rig:pelvis', 'robot:rig:spine', 'robot:rig:head', 'robot:rig:weapon-mount',
  'robot:rig:shoulder:left', 'robot:rig:elbow:left', 'robot:rig:wrist:left',
  'robot:rig:shoulder:right', 'robot:rig:elbow:right', 'robot:rig:wrist:right',
  'robot:rig:hip:left', 'robot:rig:knee:left', 'robot:rig:ankle:left',
  'robot:rig:hip:right', 'robot:rig:knee:right', 'robot:rig:ankle:right',
] as const

export interface RobotAnimationController {
  readonly weaponMount: THREE.Object3D
  apply: (state: RobotAnimationState, seconds: number) => void
}

export function createRobotAnimationController(model: THREE.Object3D): RobotAnimationController | undefined {
  // GLTFLoader sanitises `:` from Object3D.name, while preserving the authored
  // semantic node name in userData.name. Hand-authored test rigs still use name.
  const semanticJoint = (name: string) => {
    const direct = model.getObjectByName(name)
    if (direct !== undefined) return direct
    let authored: THREE.Object3D | undefined
    model.traverse((object) => { if (object.userData.name === name) authored = object })
    return authored
  }
  const joints = new Map(REQUIRED_JOINTS.map((name) => [name, semanticJoint(name)]))
  if ([...joints.values()].some((joint) => joint === undefined)) return undefined
  const joint = (name: (typeof REQUIRED_JOINTS)[number]) => joints.get(name)!
  const apply = (state: RobotAnimationState, seconds: number) => {
    for (const value of joints.values()) value!.rotation.set(0, 0, 0)
    const pulse = Math.sin(seconds * Math.PI * 2)
    const stride = state === 'run' ? 0.65 : state === 'walk' || state.startsWith('strafe') ? 0.42 : 0
    const leftLeg = joint('robot:rig:hip:left'); const rightLeg = joint('robot:rig:hip:right')
    const leftShoulder = joint('robot:rig:shoulder:left'); const rightShoulder = joint('robot:rig:shoulder:right')
    if (state === 'idle') {
      joint('robot:rig:spine').rotation.z = pulse * 0.018
      joint('robot:rig:head').rotation.y = pulse * 0.045
    }
    if (stride !== 0) {
      const side = state === 'strafe-left' ? -1 : state === 'strafe-right' ? 1 : 0
      leftLeg.rotation.x = pulse * stride; rightLeg.rotation.x = -pulse * stride
      joint('robot:rig:knee:left').rotation.x = Math.max(0, -pulse) * stride * .8
      joint('robot:rig:knee:right').rotation.x = Math.max(0, pulse) * stride * .8
      leftShoulder.rotation.x = -pulse * stride * .72; rightShoulder.rotation.x = pulse * stride * .72
      joint('robot:rig:pelvis').rotation.z = side * .08
    }
    if (state.startsWith('jump-')) {
      const crouch = state === 'jump-takeoff' ? -.28 : state === 'jump-landing' ? .32 : .08
      leftLeg.rotation.x = rightLeg.rotation.x = crouch
      joint('robot:rig:knee:left').rotation.x = joint('robot:rig:knee:right').rotation.x = -crouch * 1.5
    }
    if (state === 'aim' || state === 'fire') {
      leftShoulder.rotation.x = rightShoulder.rotation.x = -.62
      joint('robot:rig:elbow:left').rotation.x = joint('robot:rig:elbow:right').rotation.x = -.48
      joint('robot:rig:weapon-mount').rotation.x = state === 'fire' ? .26 : -.12
    }
    if (state === 'hit') joint('robot:rig:spine').rotation.z = Math.sin(seconds * 20) * .15
    if (state === 'destroyed') {
      joint('robot:rig:pelvis').rotation.z = .25
      joint('robot:rig:spine').rotation.z = -.42
      leftShoulder.rotation.x = .35; rightShoulder.rotation.x = -.35
      leftLeg.rotation.x = .32; rightLeg.rotation.x = -.24
    }
  }
  return { weaponMount: joint('robot:rig:weapon-mount'), apply }
}
