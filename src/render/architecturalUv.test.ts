import { expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import * as THREE from 'three'
import { architecturalUv, fitWallModule } from './architecturalUv'

it('keeps nonzero texture area on every face of shared-corner GLB boxes', () => {
  const box = new THREE.BoxGeometry(1, 1, 1)
  const result = architecturalUv(box, new THREE.Vector3(3.2,3.55,0.34),0.5)
  const uv=result.getAttribute('uv')
  for(let i=0;i<uv.count;i+=3) {
    const area=(uv.getX(i+1)-uv.getX(i))*(uv.getY(i+2)-uv.getY(i))-(uv.getX(i+2)-uv.getX(i))*(uv.getY(i+1)-uv.getY(i))
    expect(Math.abs(area)).toBeGreaterThan(0.001)
  }
  expect(box.index).not.toBeNull()
  expect(result.index).toBeNull()
  box.dispose();result.dispose()
})

it('fits the actual wall-bay asset inside a narrow canonical wall without changing template geometry', () => {
  const bytes=readFileSync(new URL('../../public/assets/arena/wall-bay-lod0.glb',import.meta.url))
  const asset=JSON.parse(bytes.subarray(20,20+bytes.readUInt32LE(12)).toString())
  const group=new THREE.Group()
  for(const node of asset.nodes) {
    const mesh=new THREE.Mesh(new THREE.BoxGeometry(1,1,1))
    mesh.position.fromArray(node.translation);mesh.scale.fromArray(node.scale);group.add(mesh)
  }
  const geometry=group.children.map(c=>(c as THREE.Mesh).geometry)
  fitWallModule(group,0.7,4.5,0.7)
  const bounds=new THREE.Box3().setFromObject(group)
  expect(bounds.min.x).toBeCloseTo(-0.35,6)
  expect(bounds.max.x).toBeCloseTo(0.35,6)
  expect(bounds.min.y).toBeCloseTo(0,6)
  expect(bounds.max.y).toBeCloseTo(4.5,6)
  expect(bounds.min.z).toBeCloseTo(-0.35,6)
  expect(bounds.max.z).toBeCloseTo(0.35,6)
  expect(group.children.map(c=>(c as THREE.Mesh).geometry)).toEqual(geometry)
  geometry.forEach(g=>g.dispose())
})
