/** Generate geometry-only participant GLB source assets without a DCC/runtime dependency. */
import { mkdirSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'
import * as THREE from 'three'

const out = resolve(import.meta.dirname, '../public/assets/participants')
mkdirSync(out, { recursive: true })
const materials = [
  ['shell', [0.78, 0.84, 0.88, 1], 0.65, 0.25], ['joint', [0.025, 0.07, 0.12, 1], 0.82, 0.2],
  ['trim', [0.15, 0.26, 0.34, 1], 0.75, 0.22], ['identity', [0.1, 0.8, 1, 1], 0.35, 0.18], ['muzzle-dark', [0.01, 0.015, 0.02, 1], 0.4, 0.2],
]
const mat = Object.fromEntries(materials.map(([name], index) => [name, index]))

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
    if (indexArray) primitive.indices = accessor(indexArray, 1, indexArray instanceof Uint32Array ? 5125 : 5123, 34963)
    meshes.push({ name: item.name, primitives: [primitive] }); nodes.push({ name: item.name, mesh: meshes.length - 1 })
  }
  const key = name.replace(/-lod[01]\.glb$/, '')
  const contracts = {
    'light-sport-robot': ['feet-center', [1.2, 2.2, .8]],
    'double-barrel-shotgun': ['hand-grip-center', [.55, .35, 1.4]],
    'first-person-shotgun': ['camera-weapon-grip', [1.3, .8, 1.6]],
  }
  const [pivot, attachmentBoundsMeters] = contracts[key]
  const json = { asset: { version: '2.0', generator: 'star-tournament participant asset generator v1', extras: { units: 'meters', up: '+Y', front: '+Z', pivot, attachmentBoundsMeters } }, scene: 0, scenes: [{ nodes: nodes.map((_, i) => i) }], nodes, meshes, materials: materials.map(([name, colour, metallic, roughness]) => ({ name, pbrMetallicRoughness: { baseColorFactor: colour, metallicFactor: metallic, roughnessFactor: roughness }, emissiveFactor: name === 'identity' ? colour.slice(0, 3) : [0, 0, 0] })), buffers: [{ byteLength: length }], bufferViews: views, accessors }
  const encoded = new TextEncoder().encode(JSON.stringify(json)); const jsonLength = (encoded.length + 3) & ~3
  const file = new Uint8Array(12 + 8 + jsonLength + 8 + length); const view = new DataView(file.buffer)
  view.setUint32(0, 0x46546c67, true); view.setUint32(4, 2, true); view.setUint32(8, file.length, true); view.setUint32(12, jsonLength, true); view.setUint32(16, 0x4e4f534a, true); file.set(encoded, 20); file.fill(0x20, 20 + encoded.length, 20 + jsonLength); view.setUint32(20 + jsonLength, length, true); view.setUint32(24 + jsonLength, 0x004e4942, true)
  let offset = 28 + jsonLength; for (const chunk of bin) { file.set(chunk, offset); offset += chunk.length }
  writeFileSync(resolve(out, name), file)
}

function kit() {
  const objects = []
  const add = (name, material, geometry, position = [0, 0, 0], rotation = [0, 0, 0]) => { geometry.rotateX(rotation[0]); geometry.rotateY(rotation[1]); geometry.rotateZ(rotation[2]); geometry.translate(...position); objects.push({ name, material, geometry }); return objects }
  return { objects, box: (n, m, p, s, r = [0, 0, 0]) => add(n, m, new THREE.BoxGeometry(...s, 2, 2, 2), p, r), cylinder: (n, m, p, radius, depth, r = [0, 0, 0], seg = 12) => add(n, m, new THREE.CylinderGeometry(radius, radius, depth, seg), p, r), sphere: (n, m, p, s, seg = 14) => { const g = new THREE.SphereGeometry(s[0], seg, Math.max(8, seg >> 1)); g.scale(1, s[1] / s[0], s[2] / s[0]); return add(n, m, g, p) }, torus: (n, m, p, major, minor, r = [0, 0, 0], seg = 14) => add(n, m, new THREE.TorusGeometry(major, minor, 6, seg), p, r) }
}

function robot(lod) {
  const k = kit(), detail = lod === 0, pair = [-1, 1]
  k.box('robot:pelvis', 'joint', [0, .83, 0], [.56, .3, .34]); k.box('robot:pelvis:armour-front', 'shell', [0, .86, .19], [.48, .18, .06]); k.box('robot:torso:core-shell', 'shell', [0, 1.35, 0], [.78, .96, .48]); k.box('robot:torso:spine', 'joint', [0, 1.34, -.25], [.24, .7, .1]); k.box('robot:identity:core', 'identity', [0, 1.38, .255], [.38, .44, .05]); k.box('robot:head', 'shell', [0, 2.05, 0], [.5, .4, .42]); k.box('robot:head:jaw', 'joint', [0, 1.91, .05], [.38, .12, .32]); k.box('robot:identity:visor', 'identity', [0, 2.06, .215], [.4, .13, .036]); k.box('robot:identity:rear-beacon', 'identity', [0, 1.85, -.23], [.11, .28, .07])
  for (const [index, y] of [1.08, 1.22, 1.5, 1.62].entries()) k.box(`robot:torso:armour-layer:${index}`, 'shell', [0, y, .275], [.58 - index * .06, .11, .035])
  for (const side of pair) {
    const x = side * .48
    k.box(`robot:identity:shoulder:${side}`, 'identity', [x, 1.72, 0], [.4, .26, .48]); k.torus(`robot:joint:shoulder-collar:${side}`, 'joint', [x, 1.59, 0], .115, .028, [0, 0, 0], detail ? 18 : 10)
    k.cylinder(`robot:arm:upper-joint:${side}`, 'joint', [x, 1.53, 0], .1, .28); k.box(`robot:arm:upper-shell:${side}`, 'shell', [x, 1.39, 0], [.24, .38, .26]); k.sphere(`robot:arm:elbow:${side}`, 'joint', [x, 1.15, .02], [.115, .1, .11]); k.torus(`robot:joint:elbow-collar:${side}`, 'joint', [x, 1.15, .02], .11, .018, [Math.PI / 2, 0, 0], detail ? 16 : 10)
    k.box(`robot:arm:forearm:${side}`, 'shell', [x, .99, .06], [.26, .38, .28]); k.torus(`robot:joint:wrist-collar:${side}`, 'joint', [side * .36, .84, .13], .085, .018, [0, Math.PI / 2, 0], detail ? 16 : 10); k.sphere(`robot:hand:${side}`, 'joint', [side * .33, .82, .15], [.13, .1, .13])
    const fingerCount = detail ? 4 : 2; const segments = detail ? 3 : 1
    for (let finger = 0; finger < fingerCount; finger++) for (let segment = 0; segment < segments; segment++) k.box(`robot:hand:finger:${side}:${finger}:${segment}`, 'joint', [side * (.31 + finger * .035), .76 - segment * .045, .23], [.026, .055, .035], [0, 0, side * .12])
    k.sphere(`robot:leg:hip:${side}`, 'joint', [side * .23, .72, 0], [.13, .12, .13]); k.torus(`robot:joint:hip-collar:${side}`, 'joint', [side * .23, .69, .01], .12, .02, [Math.PI / 2, 0, 0], detail ? 16 : 10); k.box(`robot:leg:thigh:${side}`, 'shell', [side * .23, .51, 0], [.3, .44, .3]); k.sphere(`robot:leg:knee:${side}`, 'joint', [side * .23, .25, .04], [.13, .11, .13]); k.torus(`robot:joint:knee-collar:${side}`, 'joint', [side * .23, .25, .04], .12, .02, [Math.PI / 2, 0, 0], detail ? 16 : 10); k.box(`robot:leg:shin:${side}`, 'shell', [side * .23, .09, 0], [.28, .4, .3]); k.box(`robot:foot:${side}`, 'joint', [side * .23, -.12, .09], [.32, .16, .5])
    const panelCount = detail ? 3 : 1
    for (let panel = 0; panel < panelCount; panel++) { k.box(`robot:arm:panel:${side}:${panel}`, 'trim', [x + side * (.125 + panel * .012), 1.31 - panel * .11, .15], [.035, .15, .022]); k.box(`robot:leg:panel:${side}:${panel}`, 'trim', [side * .23, .54 - panel * .17, .17], [.14, .06, .025]) }
    if (detail) { for (let cable = 0; cable < 3; cable++) k.cylinder(`robot:cable:${side}:${cable}`, 'trim', [x + side * (.13 - cable * .055), 1.15, -.16], .012, .46, [0, 0, side * .22], 8); for (let vent = 0; vent < 4; vent++) k.box(`robot:arm:vent:${side}:${vent}`, 'joint', [x + side * .14, 1.43 - vent * .06, .14], [.02, .018, .015]) }
  }
  const ventCount = detail ? 8 : 3
  for (let vent = 0; vent < ventCount; vent++) k.box(`robot:torso:vent:${vent}`, 'trim', [((vent % 2) - .5) * .18, 1.08 + Math.floor(vent / 2) * .07, -.25], [.06, .018, .024])
  k.torus('robot:core-ring', 'trim', [0, 1.38, .285], .18, .018, [Math.PI / 2, 0, 0], detail ? 20 : 12)
  writer(`light-sport-robot-lod${lod}.glb`, k.objects)
}

function shotgun(lod, viewmodel = false) {
  const k = kit(), detail = lod === 0, scale = viewmodel ? 1 : .72
  const v = (n) => n * scale; k.box('weapon:receiver', 'shell', [0, v(.14), v(.36)], [v(.48), v(.32), v(.68)]); k.box('weapon:receiver:top-armour', 'shell', [0, v(.31), v(.35)], [v(.36), v(.1), v(.42)]); k.box('weapon:energy-chamber', 'identity', [0, v(.17), v(.18)], [v(.36), v(.24), v(.32)]); k.box('weapon:grip', 'joint', [0, v(-.22), v(-.03)], [v(.22), v(.5), v(.24)], [.18, 0, 0]); k.box('weapon:owner-indicator', 'identity', [0, v(.31), v(.32)], [v(.24), v(.06), v(.3)])
  for (const side of [-1, 1]) {
    const x = v(side * .17); k.cylinder(`weapon:barrel-shroud:${side}`, 'joint', [x, v(.1), v(.88)], v(.105), v(1.1), [Math.PI / 2, 0, 0], detail ? 20 : 12); k.cylinder(`weapon:muzzle-opening:${side}`, 'muzzle-dark', [x, v(.1), v(1.44)], v(.071), v(.016), [Math.PI / 2, 0, 0], detail ? 16 : 10); k.cylinder(`weapon:inner-bore:${side}`, 'muzzle-dark', [x, v(.1), v(1.37)], v(.056), v(.14), [Math.PI / 2, 0, 0], detail ? 16 : 10)
    const ringCount = detail ? 5 : 2; for (let ring = 0; ring < ringCount; ring++) k.torus(`weapon:barrel:ring:${side}:${ring}`, 'shell', [x, v(.1), v(.54 + ring * .18)], v(.11), v(.012), [Math.PI / 2, 0, 0], detail ? 16 : 10)
    const ventCount = detail ? 6 : 2; for (let vent = 0; vent < ventCount; vent++) k.box(`weapon:barrel:vent:${side}:${vent}`, 'trim', [x + v((vent % 2 ? .08 : -.08)), v(.18), v(.63 + Math.floor(vent / 2) * .19)], [v(.035), v(.025), v(.06)])
    k.box(`weapon:barrel-rail:${side}`, 'trim', [x, v(.22), v(.9)], [v(.09), v(.05), v(.96)]); if (detail) k.box(`weapon:barrel-rail:side:${side}`, 'trim', [x + v(side * .11), v(.1), v(.91)], [v(.025), v(.05), v(.84)])
  }
  k.box('weapon:bridge', 'shell', [0, v(.12), v(.88)], [v(.34), v(.12), v(.86)]); const coilCount = detail ? 9 : 3; for (let coil = 0; coil < coilCount; coil++) { const z = v(.02 + coil * .055); k.torus(`weapon:energy:coil:${coil}`, 'identity', [0, v(.17), z], v(.15), v(.012), [Math.PI / 2, 0, 0], detail ? 16 : 10) }
  if (detail) for (let rail = 0; rail < 4; rail++) k.box(`weapon:receiver:rail:${rail}`, 'trim', [v((rail % 2 ? .18 : -.18)), v(.31), v(.22 + Math.floor(rail / 2) * .26)], [v(.035), v(.035), v(.22)])
  if (viewmodel) for (const side of [-1, 1]) { const x = side * .33; k.cylinder(`viewmodel:forearm:${side}`, 'shell', [x, -.4, .5], .12, .65, [.9, 0, side * .14], detail ? 16 : 10); k.torus(`viewmodel:wrist:${side}`, 'joint', [side * .19, -.15, .7], .105, .024, [.9, 0, 0], detail ? 16 : 10); k.sphere(`viewmodel:hand:${side}`, 'joint', [side * .14, -.06, .76], [.14, .11, .14]); const fingerCount = detail ? 4 : 2; const segments = detail ? 3 : 1; for (let finger = 0; finger < fingerCount; finger++) for (let segment = 0; segment < segments; segment++) k.box(`viewmodel:hand:finger:${side}:${finger}:${segment}`, 'joint', [side * (.12 + finger * .035), -.07 - segment * .04, .86], [.024, .05, .03], [0, 0, side * .1]); const panelCount = detail ? 4 : 1; for (let panel = 0; panel < panelCount; panel++) k.box(`viewmodel:forearm:panel:${side}:${panel}`, 'trim', [x, -.42 + panel * .08, .58], [.14, .08, .03]) }
  // The camera-attached model is authored at a compact presentation scale; its
  // manifest bounds remain a maximum attachment envelope, not collision data.
  if (viewmodel) for (const object of k.objects) object.geometry.scale(.42, .42, .42)
  writer(`${viewmodel ? 'first-person-shotgun' : 'double-barrel-shotgun'}-lod${lod}.glb`, k.objects)
}

for (const lod of [0, 1]) robot(lod)
for (const lod of [0, 1]) shotgun(lod)
for (const lod of [0, 1]) shotgun(lod, true)
