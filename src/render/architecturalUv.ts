import * as THREE from 'three'

/** Split shared corners before projection: every triangle needs its own face UVs. */
export function architecturalUv(source: THREE.BufferGeometry, scale: THREE.Vector3, density: number): THREE.BufferGeometry {
  const geometry = source.index ? source.toNonIndexed() : source.clone()
  const position = geometry.getAttribute('position')
  const uv = new Float32Array(position.count * 2)
  const a = new THREE.Vector3(), b = new THREE.Vector3(), c = new THREE.Vector3()
  const normal = new THREE.Vector3(), edge = new THREE.Vector3()
  for (let i = 0; i < position.count; i += 3) {
    a.fromBufferAttribute(position, i).multiply(scale)
    b.fromBufferAttribute(position, i + 1).multiply(scale)
    c.fromBufferAttribute(position, i + 2).multiply(scale)
    normal.subVectors(b, a).cross(edge.subVectors(c, a))
    const ax = Math.abs(normal.x), ay = Math.abs(normal.y), az = Math.abs(normal.z)
    for (const [offset, point] of [a, b, c].entries()) {
      const u = ax >= ay && ax >= az ? point.z : point.x
      const v = ay >= ax && ay >= az ? point.z : point.y
      uv[(i + offset) * 2] = u * density
      uv[(i + offset) * 2 + 1] = v * density
    }
  }
  geometry.setAttribute('uv', new THREE.BufferAttribute(uv, 2))
  geometry.setAttribute('uv1', geometry.getAttribute('uv').clone())
  geometry.computeVertexNormals()
  return geometry
}

/** Fit an authored module into its canonical host; cached geometry is untouched. */
export function fitWallModule(object: THREE.Object3D, span: number, height: number, depth: number): void {
  object.updateMatrixWorld(true)
  const bounds = new THREE.Box3().setFromObject(object)
  const size = bounds.getSize(new THREE.Vector3())
  object.scale.set(span / size.x, height / size.y, depth / size.z)
  object.position.set(-((bounds.min.x+bounds.max.x)/2)*object.scale.x, -bounds.min.y*object.scale.y, -((bounds.min.z+bounds.max.z)/2)*object.scale.z)
}
