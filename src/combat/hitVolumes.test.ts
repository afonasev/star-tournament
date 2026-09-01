import { describe, expect, it } from 'vitest'

import type { RobotTargetDefinition } from '../scenario'
import { intersectTargetHitVolumes } from './hitVolumes'

const target: RobotTargetDefinition = {
  semanticId: 'robot-a',
  name: 'Robot A',
  participantKind: 'stationary-fixture',
  position: { x: 0, y: 0, z: 0 },
  yawRadians: 0,
  hitVolumes: [
    { semanticId: 'head', kind: 'sphere', zone: 'head', centerOffset: { x: 0, y: 2, z: 0 }, radius: 0.25 },
    { semanticId: 'torso', kind: 'box', zone: 'torso', centerOffset: { x: 0, y: 1.2, z: 0 }, halfExtents: { x: 0.4, y: 0.5, z: 0.25 } },
    { semanticId: 'arm-left', kind: 'capsule', zone: 'limb', startOffset: { x: -0.6, y: 1.6, z: 0 }, endOffset: { x: -0.6, y: 0.8, z: 0 }, radius: 0.15 },
    { semanticId: 'arm-right', kind: 'capsule', zone: 'limb', startOffset: { x: 0.6, y: 1.6, z: 0 }, endOffset: { x: 0.6, y: 0.8, z: 0 }, radius: 0.15 },
    { semanticId: 'leg-left', kind: 'capsule', zone: 'limb', startOffset: { x: -0.2, y: 0.7, z: 0 }, endOffset: { x: -0.2, y: 0.1, z: 0 }, radius: 0.16 },
    { semanticId: 'leg-right', kind: 'capsule', zone: 'limb', startOffset: { x: 0.2, y: 0.7, z: 0 }, endOffset: { x: 0.2, y: 0.1, z: 0 }, radius: 0.16 },
  ],
}

describe('analytic target hit volumes', () => {
  it('hits head sphere, torso box, and limb capsule with ordered distances', () => {
    const head = intersectTargetHitVolumes({ x: 0, y: 2, z: 3 }, { x: 0, y: 0, z: -1 }, 10, [target])
    expect(head[0]).toMatchObject({ targetId: 'robot-a', volumeId: 'head', zone: 'head' })
    expect(head[0]?.distance).toBeCloseTo(2.75)

    const torso = intersectTargetHitVolumes({ x: 0, y: 1.2, z: 3 }, { x: 0, y: 0, z: -1 }, 10, [target])
    expect(torso[0]).toMatchObject({ volumeId: 'torso', zone: 'torso' })
    expect(torso[0]?.normal).toEqual({ x: 0, y: 0, z: 1 })

    const limb = intersectTargetHitVolumes({ x: 0.6, y: 1.2, z: 3 }, { x: 0, y: 0, z: -1 }, 10, [target])
    expect(limb[0]).toMatchObject({ volumeId: 'arm-right', zone: 'limb' })
  })

  it('supports misses, inclusive range boundaries, and inactive filtering', () => {
    expect(intersectTargetHitVolumes({ x: 3, y: 2, z: 3 }, { x: 0, y: 0, z: -1 }, 10, [target])).toEqual([])
    const exact = intersectTargetHitVolumes({ x: 0, y: 2, z: 3 }, { x: 0, y: 0, z: -1 }, 2.75, [target])
    expect(exact).toHaveLength(1)
    expect(intersectTargetHitVolumes(
      { x: 0, y: 2, z: 3 },
      { x: 0, y: 0, z: -1 },
      10,
      [target],
      new Set(),
    )).toEqual([])
  })

  it('applies target yaw and canonical semantic ordering for equal distance', () => {
    const rotated = { ...target, semanticId: 'robot-b', yawRadians: Math.PI / 2 }
    const tied = { ...target, semanticId: 'robot-a' }
    const hits = intersectTargetHitVolumes(
      { x: 3, y: 1.2, z: 0 },
      { x: -1, y: 0, z: 0 },
      10,
      [rotated, tied],
    )
    const nearestPerTarget = hits.filter((hit, index) => index === hits.findIndex((candidate) => candidate.targetId === hit.targetId))
    expect(nearestPerTarget.map((hit) => hit.targetId)).toEqual(['robot-a', 'robot-b'])
  })
})
