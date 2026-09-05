import { readFileSync } from 'node:fs'
import { Box3, Vector3 } from 'three'
import { expect, it } from 'vitest'

it.each(['lod0', 'lod1'])('keeps both authored grip chains connected in %s', (lod) => {
  const bytes = readFileSync(new URL(`../../public/assets/participants/first-person-shotgun-${lod}.glb`, import.meta.url))
  const asset = JSON.parse(bytes.subarray(20, 20 + bytes.readUInt32LE(12)).toString()) as {
    nodes: { name: string; mesh: number }[]
    meshes: { primitives: { attributes: { POSITION: number } }[] }[]
    accessors: { min: number[]; max: number[] }[]
  }
  const bounds = (name: string) => {
    const node = asset.nodes.find((item) => item.name === name)
    expect(node, name).toBeDefined()
    const accessor = asset.accessors[asset.meshes[node!.mesh].primitives[0].attributes.POSITION]
    return new Box3(new Vector3().fromArray(accessor.min), new Vector3().fromArray(accessor.max))
  }
  // Conservative spatial regression: catches separated pieces, while visual QA verifies the actual surface contact.
  const touches = (a: string, b: string) => expect(bounds(a).intersectsBox(bounds(b)), `${a} touches ${b}`).toBe(true)
  for (const side of [-1, 1]) {
    touches(`viewmodel:forearm:${side}`, `viewmodel:wrist:${side}`)
    touches(`viewmodel:wrist:${side}`, `viewmodel:wrist:link:${side}`)
    touches(`viewmodel:wrist:link:${side}`, `viewmodel:hand:${side}`)
    for (let finger = 0; finger < 4; finger++) {
      touches(`viewmodel:hand:${side}`, `viewmodel:hand:finger:${side}:${finger}:0`)
      for (let segment = 0; segment < 2; segment++) touches(`viewmodel:hand:finger:${side}:${finger}:${segment}`, `viewmodel:hand:finger:${side}:${finger}:${segment + 1}`)
    }
    touches(`viewmodel:hand:${side}`, `viewmodel:thumb:${side}:0`)
    touches(`viewmodel:thumb:${side}:0`, `viewmodel:thumb:${side}:1`)
  }
})
