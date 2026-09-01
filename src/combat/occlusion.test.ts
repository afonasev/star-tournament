import { describe, expect, it } from 'vitest'

import type { CollisionHit } from '../collision/contracts'
import type { RobotTargetDefinition } from '../scenario'
import { resolvePelletImpacts, type StaticRaycastPort } from './occlusion'

const target: RobotTargetDefinition = {
  semanticId: 'target',
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

const origin = { x: 0, y: 2, z: 3 }
const pellets = [{ pelletIndex: 0, direction: { x: 0, y: 0, z: -1 } }] as const
const staticHit = (distance: number, semanticId = 'wall'): CollisionHit => ({
  semanticId,
  distance,
  point: { x: 0, y: 2, z: 3 - distance },
  normal: { x: 0, y: 0, z: 1 },
})

describe('static shotgun occlusion', () => {
  it('selects cover before target and target before cover', () => {
    const cover: StaticRaycastPort = { raycast: () => [staticHit(2)] }
    expect(resolvePelletImpacts(origin, pellets, 10, [target], new Set(['target']), cover)[0])
      .toMatchObject({ target: null, surface: { semanticId: 'wall' } })

    const behind: StaticRaycastPort = { raycast: () => [staticHit(4)] }
    expect(resolvePelletImpacts(origin, pellets, 10, [target], new Set(['target']), behind)[0])
      .toMatchObject({ target: { targetId: 'target', zone: 'head' }, surface: null })
  })

  it('treats epsilon-equal cover as occluding', () => {
    const atTarget = 2.75
    const collision: StaticRaycastPort = { raycast: () => [staticHit(atTarget + 0.0000005)] }
    expect(resolvePelletImpacts(origin, pellets, 10, [target], new Set(['target']), collision)[0]?.target)
      .toBeNull()
  })

  it('is independent of backend callback order', () => {
    const ordered: StaticRaycastPort = { raycast: () => [staticHit(2, 'a'), staticHit(2, 'b')] }
    const reversed: StaticRaycastPort = { raycast: () => [staticHit(2, 'b'), staticHit(2, 'a')] }
    expect(resolvePelletImpacts(origin, pellets, 10, [target], new Set(['target']), ordered))
      .toEqual(resolvePelletImpacts(origin, pellets, 10, [target], new Set(['target']), reversed))
  })
})
