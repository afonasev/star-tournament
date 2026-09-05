/** Generate geometry-only participant GLB source assets without a DCC/runtime dependency. */
import { mkdirSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'
import * as THREE from 'three'
import { RoundedBoxGeometry } from 'three/examples/jsm/geometries/RoundedBoxGeometry.js'
import { deflateSync } from 'node:zlib'

const out = resolve(import.meta.dirname, '../public/assets/participants')
mkdirSync(out, { recursive: true })
const materials = [
  ['shell', [0.88, 0.92, 0.95, 1], 0.18, 0.43], ['joint', [0.025, 0.07, 0.12, 1], 0.82, 0.2],
  ['trim', [0.15, 0.26, 0.34, 1], 0.75, 0.22], ['identity', [0.1, 0.8, 1, 1], 0.35, 0.18], ['muzzle-dark', [0.01, 0.015, 0.02, 1], 0.4, 0.2], ['rubber', [0.035, 0.055, 0.07, 1], 0.02, 0.85],
]
const mat = Object.fromEntries(materials.map(([name], index) => [name, index]))

// Fixed authoring texel resolution and bevels are asset construction invariants,
// not gameplay, camera or balance controls. No runtime random source is used.
function surfacePng(kind) {
  const size = 128, raw = Buffer.alloc(size * (size * 4 + 1))
  for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
    const noise = ((x * 73 + y * 151 + x * y * 7) % 17) - 8
    let value = 238 + noise * .35
    if (kind === 0) {
      if (x < 3 || y < 3 || x > 124 || y > 124) value = 140
      else if (x < 5 || y < 5) value = 255
      if (y > 91 && y < 96 && x > 16 && x < 60) value = 110
      if (y > 101 && y < 105 && x > 16 && x < 42) value = 160
      if (x > 98 && x < 113 && y > 19 && y < 47) value = (y % 5 < 2) ? 100 : 225
    } else if (kind === 1) value = 216 + (y % 3) * 9 + noise
    else value = 170 + ((x + y) % 12 < 4 ? -48 : 24) + noise
    const i = y * (size * 4 + 1) + 1 + x * 4
    raw[i] = raw[i + 1] = raw[i + 2] = Math.max(0, Math.min(255, value)); raw[i + 3] = 255
  }
  const crc = (bytes) => { let n = 0xffffffff; for (const b of bytes) { n ^= b; for (let k = 0; k < 8; k++) n = (n >>> 1) ^ (n & 1 ? 0xedb88320 : 0) } return (n ^ 0xffffffff) >>> 0 }
  const chunk = (type, bytes) => { const tag = Buffer.from(type), head = Buffer.alloc(4), tail = Buffer.alloc(4); head.writeUInt32BE(bytes.length); tail.writeUInt32BE(crc(Buffer.concat([tag, bytes]))); return Buffer.concat([head, tag, bytes, tail]) }
  const header = Buffer.alloc(13); header.writeUInt32BE(size); header.writeUInt32BE(size, 4); header[8] = 8; header[9] = 6
  return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]), chunk('IHDR', header), chunk('IDAT', deflateSync(raw)), chunk('IEND', Buffer.alloc(0))])
}

function writer(name, objects) {
  const bin = [], views = [], accessors = [], meshes = [], nodes = []
  let length = 0
  const add = (array, target) => {
    const bytes = new Uint8Array(array.buffer, array.byteOffset, array.byteLength)
    const offset = length; bin.push(bytes); length += bytes.length
    while (length % 4) { bin.push(new Uint8Array([0])); length++ }
    views.push({ buffer: 0, byteOffset: offset, byteLength: bytes.length, target })
    return views.length - 1
  }
  const accessor = (array, itemSize, componentType, target, minMax = false) => {
    const view = add(array, target); const count = array.length / itemSize
    const result = { bufferView: view, componentType, count, type: itemSize === 3 ? 'VEC3' : itemSize === 2 ? 'VEC2' : 'SCALAR' }
    if (minMax) { result.min = Array.from({ length: itemSize }, (_, i) => Math.min(...Array.from({ length: count }, (_, n) => array[n * itemSize + i]))); result.max = Array.from({ length: itemSize }, (_, i) => Math.max(...Array.from({ length: count }, (_, n) => array[n * itemSize + i]))) }
    accessors.push(result); return accessors.length - 1
  }
  for (const item of objects) {
    const position = item.geometry.getAttribute('position').array
    const normal = item.geometry.getAttribute('normal').array
    const index = item.geometry.getIndex()
    const indexArray = index ? (position.length / 3 > 65535 ? new Uint32Array(index.array) : new Uint16Array(index.array)) : null
    const primitive = { attributes: { POSITION: accessor(position, 3, 5126, 34962, true), NORMAL: accessor(normal, 3, 5126, 34962) }, material: mat[item.material] }
    primitive.attributes.TEXCOORD_0 = accessor(item.geometry.getAttribute('uv').array, 2, 5126, 34962)
    if (indexArray) primitive.indices = accessor(indexArray, 1, indexArray instanceof Uint32Array ? 5125 : 5123, 34963)
    meshes.push({ name: item.name, primitives: [primitive] }); nodes.push({ name: item.name, mesh: meshes.length - 1 })
  }
  const images = [0, 1, 2].map((kind) => ({ bufferView: add(surfacePng(kind)), mimeType: 'image/png' }))
  const key = name.replace(/-lod[01]\.glb$/, '')
  const contracts = {
    'light-sport-robot': ['feet-center', [1.2, 2.2, .8]],
    'double-barrel-shotgun': ['hand-grip-center', [.55, .35, 1.4]],
    'first-person-shotgun': ['camera-weapon-grip', [1.3, .8, 1.6]],
  }
  const [pivot, attachmentBoundsMeters] = contracts[key]
  const json = { asset: { version: '2.0', generator: 'star-tournament equipment-surfaces-v1', extras: { units: 'meters', up: '+Y', front: '+Z', pivot, attachmentBoundsMeters } }, scene: 0, scenes: [{ nodes: nodes.map((_, i) => i) }], nodes, meshes, materials: materials.map(([name, colour, metallic, roughness]) => ({ name, pbrMetallicRoughness: { baseColorFactor: colour, metallicFactor: metallic, roughnessFactor: roughness, ...(['identity', 'muzzle-dark'].includes(name) ? {} : { baseColorTexture: { index: name === 'shell' ? 0 : ['trim', 'joint'].includes(name) ? 1 : 2 } }) }, emissiveFactor: name === 'identity' ? colour.slice(0, 3) : [0, 0, 0] })), images, textures: images.map((_, source) => ({ source, sampler: 0 })), samplers: [{ magFilter: 9729, minFilter: 9987, wrapS: 10497, wrapT: 10497 }], buffers: [{ byteLength: length }], bufferViews: views, accessors }
  const encoded = new TextEncoder().encode(JSON.stringify(json)); const jsonLength = (encoded.length + 3) & ~3
  const file = new Uint8Array(12 + 8 + jsonLength + 8 + length); const view = new DataView(file.buffer)
  view.setUint32(0, 0x46546c67, true); view.setUint32(4, 2, true); view.setUint32(8, file.length, true); view.setUint32(12, jsonLength, true); view.setUint32(16, 0x4e4f534a, true); file.set(encoded, 20); file.fill(0x20, 20 + encoded.length, 20 + jsonLength); view.setUint32(20 + jsonLength, length, true); view.setUint32(24 + jsonLength, 0x004e4942, true)
  let offset = 28 + jsonLength; for (const chunk of bin) { file.set(chunk, offset); offset += chunk.length }
  writeFileSync(resolve(out, name), file)
}

function kit() {
  const objects = []
  const add = (name, material, geometry, position = [0, 0, 0], rotation = [0, 0, 0]) => { geometry.rotateX(rotation[0]); geometry.rotateY(rotation[1]); geometry.rotateZ(rotation[2]); geometry.translate(...position); objects.push({ name, material, geometry }); return objects }
  return { objects, box: (n, m, p, s, r = [0, 0, 0]) => add(n, m, ((m === 'shell' || m === 'identity') ? new RoundedBoxGeometry(...s, 2, Math.min(...s) * .24) : new THREE.BoxGeometry(...s)), p, r), cylinder: (n, m, p, radius, depth, r = [0, 0, 0], seg = 12) => add(n, m, new THREE.CylinderGeometry(radius, radius, depth, seg), p, r), sphere: (n, m, p, s, seg = 14) => { const g = new THREE.SphereGeometry(s[0], seg, Math.max(8, seg >> 1)); g.scale(1, s[1] / s[0], s[2] / s[0]); return add(n, m, g, p) }, torus: (n, m, p, major, minor, r = [0, 0, 0], seg = 14) => add(n, m, new THREE.TorusGeometry(major, minor, 6, seg), p, r) }
}

function robot(lod) {
  const k = kit(), detail = lod === 0, pair = [-1, 1]
  k.box('robot:pelvis', 'joint', [0, .83, 0], [.56, .3, .34]); k.box('robot:pelvis:armour-front', 'shell', [0, .86, .19], [.48, .18, .06]); k.box('robot:torso:core-shell', 'shell', [0, 1.42, 0], [.65, .70, .40]); k.box('robot:torso:spine', 'joint', [0, 1.34, -.25], [.24, .7, .1]); k.box('robot:identity:core', 'identity', [0, 1.38, .255], [.38, .44, .05]); k.box('robot:head', 'shell', [0, 2.05, 0], [.45, .38, .39]); k.box('robot:head:jaw', 'joint', [0, 1.91, .05], [.38, .12, .32]); k.box('robot:identity:visor', 'identity', [0, 2.06, .215], [.4, .13, .036]); k.box('robot:identity:rear-beacon', 'identity', [0, 1.85, -.23], [.11, .28, .07])
  for (const [index, y] of [1.08, 1.22, 1.5, 1.62].entries()) k.box(`robot:torso:armour-layer:${index}`, 'shell', [0, y, .275], [.58 - index * .06, .11, .035])
  for (const side of pair) {
    const x = side * .48
    k.box(`robot:identity:shoulder:${side}`, 'identity', [x, 1.72, 0], [.32, .23, .38]); k.torus(`robot:joint:shoulder-collar:${side}`, 'joint', [x, 1.59, 0], .115, .028, [0, 0, 0], detail ? 18 : 10)
    k.cylinder(`robot:arm:upper-joint:${side}`, 'joint', [x, 1.53, 0], .1, .28); k.box(`robot:arm:upper-shell:${side}`, 'shell', [x, 1.41, 0], [.21, .28, .24]); k.sphere(`robot:arm:elbow:${side}`, 'joint', [x, 1.15, .02], [.115, .1, .11]); k.torus(`robot:joint:elbow-collar:${side}`, 'joint', [x, 1.15, .02], .11, .018, [Math.PI / 2, 0, 0], detail ? 16 : 10)
    k.box(`robot:arm:forearm:${side}`, 'shell', [x, .96, .06], [.24, .29, .26]); k.torus(`robot:joint:wrist-collar:${side}`, 'joint', [side * .36, .84, .13], .085, .018, [0, Math.PI / 2, 0], detail ? 16 : 10); k.sphere(`robot:hand:${side}`, 'joint', [side * .33, .82, .15], [.13, .1, .13])
    const fingerCount = detail ? 4 : 2; const segments = detail ? 3 : 1
    for (let finger = 0; finger < fingerCount; finger++) for (let segment = 0; segment < segments; segment++) k.box(`robot:hand:finger:${side}:${finger}:${segment}`, 'joint', [side * (.31 + finger * .035), .76 - segment * .045, .23], [.026, .055, .035], [0, 0, side * .12])
    k.sphere(`robot:leg:hip:${side}`, 'joint', [side * .23, .72, 0], [.13, .12, .13]); k.torus(`robot:joint:hip-collar:${side}`, 'joint', [side * .23, .69, .01], .12, .02, [Math.PI / 2, 0, 0], detail ? 16 : 10); k.box(`robot:leg:thigh:${side}`, 'shell', [side * .23, .52, 0], [.27, .33, .27]); k.sphere(`robot:leg:knee:${side}`, 'joint', [side * .23, .25, .04], [.13, .11, .13]); k.torus(`robot:joint:knee-collar:${side}`, 'joint', [side * .23, .25, .04], .12, .02, [Math.PI / 2, 0, 0], detail ? 16 : 10); k.box(`robot:leg:shin:${side}`, 'shell', [side * .23, .045, 0], [.25, .25, .28]); k.box(`robot:foot:${side}`, 'rubber', [side * .23, -.12, .09], [.32, .16, .5])
    const panelCount = detail ? 3 : 1
    for (let panel = 0; panel < panelCount; panel++) { k.box(`robot:arm:panel:${side}:${panel}`, 'trim', [x + side * (.125 + panel * .012), 1.31 - panel * .11, .15], [.035, .15, .022]); k.box(`robot:leg:panel:${side}:${panel}`, 'trim', [side * .23, .54 - panel * .17, .17], [.14, .06, .025]) }
    if (detail) { for (let cable = 0; cable < 3; cable++) k.cylinder(`robot:cable:${side}:${cable}`, 'trim', [x + side * (.13 - cable * .055), 1.15, -.16], .012, .46, [0, 0, side * .22], 8); for (let vent = 0; vent < 4; vent++) k.box(`robot:arm:vent:${side}:${vent}`, 'joint', [x + side * .14, 1.43 - vent * .06, .14], [.02, .018, .015]) }
  }
  const ventCount = detail ? 8 : 3
  for (let vent = 0; vent < ventCount; vent++) k.box(`robot:torso:vent:${vent}`, 'trim', [((vent % 2) - .5) * .18, 1.08 + Math.floor(vent / 2) * .07, -.25], [.06, .018, .024])
  k.torus('robot:core-ring', 'trim', [0, 1.38, .285], .18, .018, [0, 0, 0], detail ? 20 : 12)
  // Structural plate layering stays visible in both LODs; fasteners simplify at distance.
  k.cylinder('robot:neck:actuator', 'trim', [0, 1.85, 0], .13, .22)
  for (const side of pair) {
    k.box(`robot:pectoral:${side}`, 'shell', [side * .235, 1.61, .30], [.29, .25, .13], [0, side * -.16, side * -.09])
    k.box(`robot:cheek:${side}`, 'trim', [side * .225, 1.99, .18], [.07, .19, .16], [0, side * -.25, 0])
    k.cylinder(`robot:ear:hub:${side}`, 'trim', [side * .27, 2.07, 0], .115, .09, [0, 0, Math.PI / 2])
    k.box(`robot:thigh:armour:${side}`, 'shell', [side * .25, .53, .18], [.26, .30, .08], [0, side * .12, 0])
    k.box(`robot:knee:shield:${side}`, 'shell', [side * .23, .25, .19], [.22, .16, .09])
    k.box(`robot:shoulder:cap:${side}`, 'shell', [side * .49, 1.83, .02], [.34, .13, .42], [0, 0, side * -.12])
    k.box(`robot:forearm:inset:${side}`, 'trim', [side * .48, 1, .215], [.16, .25, .035])
    k.cylinder(`robot:forearm:piston:${side}`, 'trim', [side * .63, 1.03, .04], .035, .28)
    for (let i = 0; i < (detail ? 6 : 2); i++) {
      k.box(`robot:abdomen:rib:${side}:${i}`, 'trim', [side * .26, 1.07 + i * .063, .285], [.10, .025, .04])
      k.box(`robot:back:radiator:${side}:${i}`, 'trim', [side * .24, 1.2 + i * .07, -.30], [.18, .023, .09])
    }
    if (detail) for (const y of [1.70, 1.54, 1.10, .89, .62, .43]) {
      const x = side * (y > .7 ? .32 : .24), z = y > .7 ? .35 : .235
      k.cylinder(`robot:fastener:${side}:${y}`, 'trim', [x, y, z], .024, .012, [Math.PI / 2, 0, 0], 6)
      k.box(`robot:fastener:slot:${side}:${y}`, 'joint', [x, y, z + .008], [.026, .006, .004])
    }
  }
  writer(`light-sport-robot-lod${lod}.glb`, k.objects)
}

function shotgun(lod, viewmodel = false) {
  const k = kit(), detail = lod === 0, scale = viewmodel ? 1 : .72
  const v = (n) => n * scale; k.box('weapon:receiver', 'shell', [0, v(.14), v(.36)], [v(.48), v(.32), v(.68)]); k.box('weapon:receiver:top-armour', 'shell', [0, v(.31), v(.35)], [v(.36), v(.1), v(.42)]); k.box('weapon:energy-chamber', 'identity', [0, v(.17), v(.18)], [v(.36), v(.24), v(.32)]); k.box('weapon:grip', 'rubber', [0, v(-.22), v(-.03)], [v(.22), v(.5), v(.24)], [.18, 0, 0]); k.box('weapon:owner-indicator', 'identity', [0, v(.31), v(.32)], [v(.24), v(.06), v(.3)])
  for (const side of [-1, 1]) {
    const x = v(side * .17); k.cylinder(`weapon:barrel-shroud:${side}`, 'joint', [x, v(.1), v(.88)], v(.105), v(1.1), [Math.PI / 2, 0, 0], detail ? 20 : 12); k.cylinder(`weapon:muzzle-opening:${side}`, 'muzzle-dark', [x, v(.1), v(1.44)], v(.071), v(.016), [Math.PI / 2, 0, 0], detail ? 16 : 10); k.cylinder(`weapon:inner-bore:${side}`, 'muzzle-dark', [x, v(.1), v(1.37)], v(.056), v(.14), [Math.PI / 2, 0, 0], detail ? 16 : 10)
    const ringCount = detail ? 5 : 2; for (let ring = 0; ring < ringCount; ring++) k.torus(`weapon:barrel:ring:${side}:${ring}`, 'shell', [x, v(.1), v(.54 + ring * .18)], v(.11), v(.012), [0, 0, 0], detail ? 16 : 10)
    const ventCount = detail ? 6 : 2; for (let vent = 0; vent < ventCount; vent++) k.box(`weapon:barrel:vent:${side}:${vent}`, 'trim', [x + v((vent % 2 ? .08 : -.08)), v(.18), v(.63 + Math.floor(vent / 2) * .19)], [v(.035), v(.025), v(.06)])
    k.box(`weapon:barrel-rail:${side}`, 'trim', [x, v(.22), v(.9)], [v(.09), v(.05), v(.96)]); if (detail) k.box(`weapon:barrel-rail:side:${side}`, 'trim', [x + v(side * .11), v(.1), v(.91)], [v(.025), v(.05), v(.84)])
  }
  k.box('weapon:bridge', 'shell', [0, v(.12), v(.88)], [v(.34), v(.12), v(.86)]); const coilCount = detail ? 9 : 3; for (let coil = 0; coil < coilCount; coil++) { const z = v(.02 + coil * .055); k.torus(`weapon:energy:coil:${coil}`, 'identity', [0, v(.17), z], v(.15), v(.012), [0, 0, 0], detail ? 16 : 10) }
  if (detail) for (let rail = 0; rail < 4; rail++) k.box(`weapon:receiver:rail:${rail}`, 'trim', [v((rail % 2 ? .18 : -.18)), v(.31), v(.22 + Math.floor(rail / 2) * .26)], [v(.035), v(.035), v(.22)])
  if (viewmodel) for (const side of [-1, 1]) { const x = side * .33; k.cylinder(`viewmodel:forearm:${side}`, 'shell', [x, -.4, .5], .12, .65, [.9, 0, side * .14], detail ? 16 : 10); k.torus(`viewmodel:wrist:${side}`, 'joint', [side * .19, -.15, .7], .105, .024, [.9, 0, 0], detail ? 16 : 10); k.sphere(`viewmodel:hand:${side}`, 'joint', [side * .14, -.06, .76], [.14, .11, .14]); const fingerCount = detail ? 4 : 2; const segments = detail ? 3 : 1; for (let finger = 0; finger < fingerCount; finger++) for (let segment = 0; segment < segments; segment++) k.box(`viewmodel:hand:finger:${side}:${finger}:${segment}`, 'joint', [side * (.12 + finger * .035), -.07 - segment * .04, .86], [.024, .05, .03], [0, 0, side * .1]); const panelCount = detail ? 4 : 1; for (let panel = 0; panel < panelCount; panel++) k.box(`viewmodel:forearm:panel:${side}:${panel}`, 'trim', [x, -.42 + panel * .08, .58], [.14, .08, .03]) }
  for (const side of [-1, 1]) {
    k.box(`weapon:side-plate:${side}`, 'shell', [v(side * .25), v(.16), v(.33)], [v(.06), v(.24), v(.52)], [0, 0, side * -.08])
    k.box(`weapon:service-port:${side}`, 'joint', [v(side * .286), v(.17), v(.34)], [v(.018), v(.12), v(.25)])
    k.box(`weapon:charge-window:${side}`, 'identity', [v(side * .299), v(.18), v(.34)], [v(.012), v(.035), v(.17)])
    for (let i = 0; i < (detail ? 8 : 3); i++) {
      k.box(`weapon:cooling-fin:${side}:${i}`, 'trim', [v(side * .17), v(.25), v(.58 + i * .07)], [v(.13), v(.038), v(.024)])
      if (detail) k.box(`weapon:grip:rib:${side}:${i}`, 'trim', [v(side * .115), v(-.39 + i * .045), v(-.03)], [v(.012), v(.012), v(.20)])
    }
    if (detail) for (const z of [.12, .52]) for (const y of [.08, .24]) {
      k.cylinder(`weapon:receiver:bolt:${side}:${z}:${y}`, 'trim', [v(side * .291), v(y), v(z)], v(.022), v(.013), [0, 0, Math.PI / 2], 6)
    }
    k.torus(`weapon:muzzle:lip:${side}`, 'trim', [v(side * .17), v(.1), v(1.44)], v(.086), v(.014))
  }
  k.box('weapon:sight:base', 'joint', [0, v(.385), v(.4)], [v(.14), v(.045), v(.33)])
  k.box('weapon:sight:blade', 'trim', [0, v(.425), v(.49)], [v(.04), v(.05), v(.07)])
  k.box('weapon:sight:dot', 'identity', [0, v(.445), v(.452)], [v(.017), v(.015), v(.006)])
  // The camera-attached model is authored at a compact presentation scale; its
  // manifest bounds remain a maximum attachment envelope, not collision data.
  if (viewmodel) for (const object of k.objects) object.geometry.scale(.42, .42, .42)
  writer(`${viewmodel ? 'first-person-shotgun' : 'double-barrel-shotgun'}-lod${lod}.glb`, k.objects)
}

for (const lod of [0, 1]) robot(lod)
for (const lod of [0, 1]) shotgun(lod)
for (const lod of [0, 1]) shotgun(lod, true)
