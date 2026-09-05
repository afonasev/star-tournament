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
    controller.present(state, .25)
    expect(model.getObjectByName('robot:rig:weapon-mount')).toBe(controller.weaponMount)
    expect(model.rotation.toArray()).toEqual([0, 0, 0, 'XYZ'])
    expect([...model.children].some((joint) => joint.rotation.toArray().some((value) => typeof value === 'number' && value !== 0))).toBe(true)
  })

  it('returns to the neutral idle target over subsequent presentation ticks', () => {
    const model = rig(); const controller = createRobotAnimationController(model)!
    controller.present('destroyed', .1)
    for (let seconds = .1 + 1 / 60; seconds < .8; seconds += 1 / 60) controller.present('idle', seconds)
    expect(model.getObjectByName('robot:rig:pelvis')!.rotation.z).toBeCloseTo(0, 3)
    expect(Math.abs(model.getObjectByName('robot:rig:spine')!.rotation.z)).toBeLessThan(.03)
  })

  it('blends a state transition instead of teleporting a joint', () => {
    const model = rig(); const controller = createRobotAnimationController(model)!
    controller.present('idle', 0); controller.present('destroyed', 1 / 60)
    expect(model.getObjectByName('robot:rig:spine')!.rotation.z).toBeGreaterThan(-.42)
    expect(model.getObjectByName('robot:rig:spine')!.rotation.z).toBeLessThan(0)
    expect(model.rotation.toArray()).toEqual([0, 0, 0, 'XYZ'])
  })

  it('converges similarly with 60 Hz and 120 Hz presentation steps', () => {
    const atRate = (step: number) => {
      const model = rig(); const controller = createRobotAnimationController(model)!
      controller.present('aim', 0)
      for (let seconds = step; seconds <= .5; seconds += step) controller.present('destroyed', seconds)
      return model.getObjectByName('robot:rig:spine')!.rotation.z
    }
    expect(atRate(1 / 60)).toBeCloseTo(atRate(1 / 120), 2)
  })
})
