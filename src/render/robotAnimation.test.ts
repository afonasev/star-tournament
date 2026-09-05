import * as THREE from 'three'
import { describe, expect, it } from 'vitest'

import { createRobotAnimationController, type RobotAnimationState } from './robotAnimation'

const names = [
  'robot:rig:pelvis', 'robot:rig:spine', 'robot:rig:head', 'robot:rig:weapon-mount',
  'robot:rig:shoulder:left', 'robot:rig:elbow:left', 'robot:rig:wrist:left',
  'robot:rig:shoulder:right', 'robot:rig:elbow:right', 'robot:rig:wrist:right',
  'robot:rig:hip:left', 'robot:rig:knee:left', 'robot:rig:ankle:left',
  'robot:rig:hip:right', 'robot:rig:knee:right', 'robot:rig:ankle:right',
] as const

function rig(): THREE.Group {
  const root = new THREE.Group()
  for (const name of names) { const joint = new THREE.Group(); joint.name = name; root.add(joint) }
  return root
}

describe('robot animation controller', () => {
  it('keeps a missing GLB rig inert', () => {
    expect(createRobotAnimationController(new THREE.Group())).toBeUndefined()
  })

  it('uses the authored semantic name retained by GLTFLoader', () => {
    const model = rig()
    for (const joint of model.children) { joint.userData.name = joint.name; joint.name = joint.name.replaceAll(':', '') }
    expect(createRobotAnimationController(model)).toBeDefined()
  })

  it.each(['idle', 'walk', 'run', 'strafe-left', 'strafe-right', 'jump-takeoff', 'jump-airborne', 'jump-landing', 'aim', 'fire', 'hit', 'destroyed'] as const)('applies %s only to semantic joints', (state: RobotAnimationState) => {
    const model = rig(); const controller = createRobotAnimationController(model)!
    controller.apply(state, .25)
    expect(model.getObjectByName('robot:rig:weapon-mount')).toBe(controller.weaponMount)
    expect(model.rotation.toArray()).toEqual([0, 0, 0, 'XYZ'])
    expect([...model.children].some((joint) => joint.rotation.toArray().some((value) => typeof value === 'number' && value !== 0))).toBe(true)
  })

  it('returns to neutral pose before each state', () => {
    const model = rig(); const controller = createRobotAnimationController(model)!
    controller.apply('destroyed', .1); controller.apply('idle', 0)
    expect(model.getObjectByName('robot:rig:pelvis')!.rotation.z).toBe(0)
    expect(model.getObjectByName('robot:rig:spine')!.rotation.z).toBe(0)
  })
})
