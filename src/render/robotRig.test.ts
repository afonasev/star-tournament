import { readFileSync } from 'node:fs'

import { describe, expect, it } from 'vitest'

interface GlbNode {
  readonly name: string
  readonly children?: readonly number[]
  readonly mesh?: number
  readonly translation?: readonly number[]
}

interface RobotGlb {
  readonly scenes: readonly { readonly nodes: readonly number[] }[]
  readonly scene: number
  readonly nodes: readonly GlbNode[]
  readonly skins?: readonly unknown[]
  readonly animations?: readonly unknown[]
}

function robotGlb(lod: 0 | 1): RobotGlb {
  const bytes = readFileSync(new URL(`../../public/assets/participants/light-sport-robot-lod${lod}.glb`, import.meta.url))
  return JSON.parse(bytes.subarray(20, 20 + bytes.readUInt32LE(12)).toString()) as RobotGlb
}

const REQUIRED_JOINTS = [
  'robot:rig:root', 'robot:rig:pelvis', 'robot:rig:spine', 'robot:rig:head', 'robot:rig:weapon-mount',
  'robot:rig:shoulder:left', 'robot:rig:elbow:left', 'robot:rig:wrist:left',
  'robot:rig:shoulder:right', 'robot:rig:elbow:right', 'robot:rig:wrist:right',
  'robot:rig:hip:left', 'robot:rig:knee:left', 'robot:rig:ankle:left',
  'robot:rig:hip:right', 'robot:rig:knee:right', 'robot:rig:ankle:right',
] as const

describe('light sport robot rigid animation rig', () => {
  it.each([0, 1] as const)('keeps a named, skinned-free hierarchy in LOD%s', (lod) => {
    const asset = robotGlb(lod)
    const indexByName = new Map(asset.nodes.map((node, index) => [node.name, index]))
    expect(asset.skins ?? []).toHaveLength(0)
    expect(asset.animations ?? []).toHaveLength(0)
    expect(asset.scenes[asset.scene]!.nodes.map((index) => asset.nodes[index]!.name)).toEqual(['robot:rig:root'])
    for (const joint of REQUIRED_JOINTS) expect(indexByName.get(joint)).toBeDefined()

    const hasChild = (parent: string, child: string) => asset.nodes[indexByName.get(parent)!]!.children?.includes(indexByName.get(child)!)
    expect(hasChild('robot:rig:pelvis', 'robot:rig:spine')).toBe(true)
    expect(hasChild('robot:rig:spine', 'robot:rig:head')).toBe(true)
    expect(hasChild('robot:rig:spine', 'robot:rig:weapon-mount')).toBe(true)
    expect(hasChild('robot:rig:shoulder:left', 'robot:rig:elbow:left')).toBe(true)
    expect(hasChild('robot:rig:elbow:left', 'robot:rig:wrist:left')).toBe(true)
    expect(hasChild('robot:rig:hip:right', 'robot:rig:knee:right')).toBe(true)
    expect(hasChild('robot:rig:knee:right', 'robot:rig:ankle:right')).toBe(true)
  })

  it('keeps role-defining rest-pose mesh nodes in both LODs', () => {
    for (const lod of [0, 1] as const) {
      const names = new Set(robotGlb(lod).nodes.map(({ name }) => name))
      for (const name of ['robot:identity:core', 'robot:identity:visor', 'robot:identity:shoulder:-1', 'robot:identity:shoulder:1', 'robot:foot:-1', 'robot:foot:1']) {
        expect(names.has(name)).toBe(true)
      }
    }
  })
})
