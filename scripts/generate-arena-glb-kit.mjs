import { mkdirSync, writeFileSync } from 'node:fs'

const output = new URL('../public/assets/arena/', import.meta.url)
mkdirSync(output, { recursive: true })

function glb(name, color, scale) {
  const positions = new Float32Array([
    -scale[0] / 2, 0, 0, scale[0] / 2, 0, 0, scale[0] / 2, scale[1], 0,
    -scale[0] / 2, 0, 0, scale[0] / 2, scale[1], 0, -scale[0] / 2, scale[1], 0,
  ])
  const indices = new Uint16Array([0, 1, 2, 3, 4, 5])
  const binary = Buffer.concat([Buffer.from(positions.buffer), Buffer.from(indices.buffer)])
  const json = Buffer.from(JSON.stringify({ asset: { version: '2.0', generator: 'star-tournament clean-future-sport-glb-v1' }, scenes: [{ nodes: [0] }], scene: 0, nodes: [{ mesh: 0 }], meshes: [{ primitives: [{ attributes: { POSITION: 0 }, indices: 1, material: 0 }] }], materials: [{ pbrMetallicRoughness: { baseColorFactor: color, metallicFactor: 0.16, roughnessFactor: 0.32 }, emissiveFactor: color.slice(0, 3) }], buffers: [{ byteLength: binary.length }], bufferViews: [{ buffer: 0, byteOffset: 0, byteLength: positions.byteLength, target: 34962 }, { buffer: 0, byteOffset: positions.byteLength, byteLength: indices.byteLength, target: 34963 }], accessors: [{ bufferView: 0, componentType: 5126, count: 6, type: 'VEC3', min: [-scale[0] / 2, 0, 0], max: [scale[0] / 2, scale[1], 0] }, { bufferView: 1, componentType: 5123, count: 6, type: 'SCALAR' }] }))
  const pad = (value) => Buffer.concat([value, Buffer.alloc((4 - value.length % 4) % 4, 0x20)])
  const jsonChunk = pad(json); const binaryChunk = Buffer.concat([binary, Buffer.alloc((4 - binary.length % 4) % 4)])
  const header = Buffer.alloc(12); header.writeUInt32LE(0x46546c67, 0); header.writeUInt32LE(2, 4); header.writeUInt32LE(12 + 8 + jsonChunk.length + 8 + binaryChunk.length, 8)
  const jsonHeader = Buffer.alloc(8); jsonHeader.writeUInt32LE(jsonChunk.length, 0); jsonHeader.writeUInt32LE(0x4e4f534a, 4)
  const binHeader = Buffer.alloc(8); binHeader.writeUInt32LE(binaryChunk.length, 0); binHeader.writeUInt32LE(0x004e4942, 4)
  writeFileSync(new URL(name, output), Buffer.concat([header, jsonHeader, jsonChunk, binHeader, binaryChunk]))
}

for (const [key, color, scale] of [
  ['portal-facade', [0.08, 0.75, 0.84, 1], [4, 4]], ['wall-bay', [0.92, 0.96, 0.97, 1], [3.2, 3.4]], ['floor-route-guide', [0.72, 1, 0.4, 1], [3.2, 0.08]], ['central-landmark', [1, 0.53, 0.3, 1], [2.4, 0.2]],
]) { glb(`${key}-lod0.glb`, color, scale); glb(`${key}-lod1.glb`, color, scale) }
