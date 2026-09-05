/** Generate textured participant GLB source assets without a DCC/runtime dependency. */
import { mkdirSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'
import * as THREE from 'three'
import { deflateSync } from 'node:zlib'

const out = resolve(import.meta.dirname, '../public/assets/participants')
mkdirSync(out, { recursive: true })
const materials = [
  ['shell', [0.88, 0.92, 0.95, 1], 0.18, 0.43], ['joint', [0.025, 0.07, 0.12, 1], 0.82, 0.2],
  ['trim', [0.15, 0.26, 0.34, 1], 0.75, 0.22], ['identity', [0.1, 0.8, 1, 1], 0.35, 0.18], ['muzzle-dark', [0.01, 0.015, 0.02, 1], 0.4, 0.2], ['rubber', [0.035, 0.055, 0.07, 1], 0.02, 0.85], ['hand-metal', [.62,.68,.72,1], .85,.42], ['hardware', [.48, .55, .60, 1], .85, .28],
]
const mat = Object.fromEntries(materials.map(([name], index) => [name, index]))

// Fixed authoring texel resolution and bevels are asset construction invariants,
// not gameplay, camera or balance controls. No runtime random source is used.
function surfacePng(kind, closeup = false) {
  const size = closeup && kind === 1 ? 256 : 128, raw = Buffer.alloc(size * (size * 4 + 1))
  for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
    const noise = ((x * 73 + y * 151 + x * y * 7) % 17) - 8
    let value = 238 + noise * .35
    if (kind === 0) {
      if (x > 12 && x < 16 && y > 26 && y < 80) value = 184
      if (x > 18 && x < 20 && y > 26 && y < 58) value = 208
      if (y > 91 && y < 96 && x > 16 && x < 60) value = 110
      if (y > 101 && y < 105 && x > 16 && x < 42) value = 160
      if (x > 98 && x < 113 && y > 19 && y < 47) value = (y % 5 < 2) ? 100 : 225
    } else if (kind === 1) value = 216 + (y % 3) * 9 + noise
    else value = 170 + ((x + y) % 12 < 4 ? -48 : 24) + noise
    if (closeup && kind === 1) value = 215 + Math.sin(y * 2.1) * 12 + noise * .8 - ((y % 37 === 0 && x > 40 && x < 150) ? 35 : 0)
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
  const images = [0, 1, 2].map((kind) => ({ bufferView: add(surfacePng(kind, name.startsWith('first-person') || name.startsWith('light-sport-robot'))), mimeType: 'image/png' }))
  const key = name.replace(/-lod[01]\.glb$/, '')
  const contracts = {
    'light-sport-robot': ['feet-center', [1.2, 2.2, .8]],
    'double-barrel-shotgun': ['hand-grip-center', [.55, .35, 1.4]],
    'first-person-shotgun': ['camera-weapon-grip', [1.3, .8, 1.6]],
  }
  const [pivot, attachmentBoundsMeters] = contracts[key]
  const json = { asset: { version: '2.0', generator: 'star-tournament equipment-surfaces-v4', extras: { units: 'meters', up: '+Y', front: '+Z', pivot, attachmentBoundsMeters } }, scene: 0, scenes: [{ nodes: nodes.map((_, i) => i) }], nodes, meshes, materials: materials.map(([name, colour, metallic, roughness]) => ({ name, pbrMetallicRoughness: { baseColorFactor: colour, metallicFactor: metallic, roughnessFactor: roughness, ...(['identity', 'muzzle-dark'].includes(name) ? {} : { baseColorTexture: { index: name === 'shell' ? 0 : ['trim', 'joint', 'hardware', 'hand-metal'].includes(name) ? 1 : 2 } }) }, emissiveFactor: name === 'identity' ? colour.slice(0, 3) : [0, 0, 0] })), images, textures: images.map((_, source) => ({ source, sampler: 0 })), samplers: [{ magFilter: 9729, minFilter: 9987, wrapS: 10497, wrapT: 10497 }], buffers: [{ byteLength: length }], bufferViews: views, accessors }
  const encoded = new TextEncoder().encode(JSON.stringify(json)); const jsonLength = (encoded.length + 3) & ~3
  const file = new Uint8Array(12 + 8 + jsonLength + 8 + length); const view = new DataView(file.buffer)
  view.setUint32(0, 0x46546c67, true); view.setUint32(4, 2, true); view.setUint32(8, file.length, true); view.setUint32(12, jsonLength, true); view.setUint32(16, 0x4e4f534a, true); file.set(encoded, 20); file.fill(0x20, 20 + encoded.length, 20 + jsonLength); view.setUint32(20 + jsonLength, length, true); view.setUint32(24 + jsonLength, 0x004e4942, true)
  let offset = 28 + jsonLength; for (const chunk of bin) { file.set(chunk, offset); offset += chunk.length }
  writeFileSync(resolve(out, name), file)
}

// Polygon cuts are authored contour dimensions, not tunable simulation values.
function polygonShell(size, taper = .82) {
  const [w, h, d] = size, cut = .22, shape = new THREE.Shape()
  const points = [[-.5*taper+cut*.5,-.5],[.5*taper-cut*.5,-.5],[.5*taper,-.5+cut],[.5,.5-cut],[.5-cut,.5],[-.5+cut,.5],[-.5,.5-cut],[-.5*taper,-.5+cut]]
  points.forEach(([x,y],i)=>i ? shape.lineTo(x*w,y*h):shape.moveTo(x*w,y*h));shape.closePath()
  const bevel = Math.min(w,h,d)*.08
  const g = new THREE.ExtrudeGeometry(shape,{depth:d-bevel*2,bevelEnabled:true,bevelThickness:bevel,bevelSize:bevel,bevelSegments:1,steps:1,curveSegments:1})
  g.translate(0,0,-d/2+bevel)
  // Equipment prints use local face scale rather than architectural world units.
  const uv=g.getAttribute('uv'); for(let i=0;i<uv.count;i++)uv.setXY(i,uv.getX(i)/w+.5,uv.getY(i)/h+.5)
  return g
}
function equipmentBox(name, material, size) {
  const [x,y,z]=size
  if(name.includes('shoulder:cap')) {
    const g=new THREE.SphereGeometry(.5,16,8,0,Math.PI*2,0,Math.PI/2);g.scale(x,y*1.8,z);return g
  }
  if(name.includes('identity:shoulder')) {const g=new THREE.SphereGeometry(.5,16,10);g.scale(x,y,z);return g}
  if(/robot:(arm:(upper-shell|forearm)|leg:(thigh|shin))/.test(name)) {
    const g=new THREE.CylinderGeometry(.40,.5,y,8,1);g.rotateY(Math.PI/8);g.scale(x,1,z);return g
  }
  if(material==='shell' || name==='robot:pelvis' || name==='weapon:grip' || name.startsWith('robot:foot:')) {
    if(x<y*.5 && x<z*.5){const g=polygonShell([z,y,x]);g.rotateY(Math.PI/2);return g}
    if(y<x*.5 && y<z*.5){const g=polygonShell([x,z,y]);g.rotateX(Math.PI/2);return g}
    return polygonShell(size,name.includes('torso:core-shell')?.62:.8)
  }
  return new THREE.BoxGeometry(...size)
}

function kit() {
  const objects = []
  const add = (name, material, geometry, position = [0, 0, 0], rotation = [0, 0, 0]) => { geometry.rotateX(rotation[0]); geometry.rotateY(rotation[1]); geometry.rotateZ(rotation[2]); geometry.translate(...position); objects.push({ name, material, geometry }); return objects }
  return { objects, segment: (n,m,a,b,ra,rb,segments=12) => {
    const from=new THREE.Vector3(...a),to=new THREE.Vector3(...b),direction=to.clone().sub(from)
    const g=new THREE.CylinderGeometry(rb,ra,direction.length(),segments)
    g.applyQuaternion(new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0,1,0),direction.normalize()))
    return add(n,m,g,from.add(to).multiplyScalar(.5).toArray())
  }, hose: (n, m, points, radius) => add(n, m, new THREE.TubeGeometry(new THREE.CatmullRomCurve3(points.map(p=>new THREE.Vector3(...p))), 10, radius, 6, false)), box: (n, m, p, s, r = [0, 0, 0]) => add(n, m, equipmentBox(n,m,s), p, r), cylinder: (n, m, p, radius, depth, r = [0, 0, 0], seg = 12) => add(n, m, new THREE.CylinderGeometry(radius, radius, depth, seg), p, r), sphere: (n, m, p, s, seg = 14) => { const g = new THREE.SphereGeometry(s[0], seg, Math.max(8, seg >> 1)); g.scale(1, s[1] / s[0], s[2] / s[0]); return add(n, m, g, p) }, torus: (n, m, p, major, minor, r = [0, 0, 0], seg = 14) => add(n, m, new THREE.TorusGeometry(major, minor, 6, seg), p, r) }
}

function robot(lod) {
  const k = kit(), detail = lod === 0, pair = [-1, 1]
  k.box('robot:pelvis', 'joint', [0, .83, 0], [.56, .3, .34]); k.box('robot:pelvis:armour-front', 'shell', [0, .86, .19], [.48, .18, .06]); k.box('robot:torso:core-shell', 'shell', [0, 1.42, 0], [.65, .70, .40]); k.cylinder('robot:torso:spine', 'joint', [0, 1.34, -.20], .075, .65); k.cylinder('robot:identity:core', 'identity', [0, 1.38, .255], .145, .055, [Math.PI/2,0,0], detail?24:12); k.box('robot:head', 'shell', [0, 2.05, 0], [.45, .38, .39]); k.cylinder('robot:head:jaw', 'joint', [0, 1.91, .02], .165, .11, [0,0,0], 8); k.box('robot:identity:visor', 'identity', [0, 2.06, .215], [.32, .075, .036]); k.box('robot:identity:rear-beacon', 'identity', [0, 1.85, -.23], [.11, .28, .07])
  for (const [index, y] of [1.08, 1.22, 1.5, 1.62].entries()) k.box(`robot:torso:armour-layer:${index}`, 'shell', [0, y, .275], [.58 - index * .06, .11, .035])
  for (const side of pair) {
    const x = side * .48
    k.box(`robot:identity:shoulder:${side}`, 'identity', [x, 1.72, 0], [.32, .23, .38]); k.torus(`robot:joint:shoulder-collar:${side}`, 'joint', [x, 1.59, 0], .115, .028, [0, 0, 0], detail ? 18 : 10)
    k.cylinder(`robot:arm:upper-joint:${side}`, 'joint', [x, 1.53, 0], .072, .28); k.box(`robot:arm:upper-shell:${side}`, 'shell', [x, 1.41, 0], [.21, .28, .24]); k.sphere(`robot:arm:elbow:${side}`, 'joint', [x, 1.15, .02], [.115, .1, .11]); k.torus(`robot:joint:elbow-collar:${side}`, 'joint', [x, 1.15, .02], .11, .018, [Math.PI / 2, 0, 0], detail ? 16 : 10)
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
    k.box(`robot:pectoral:${side}`, 'shell', [side * .205, 1.61, .28], [.28, .22, .09], [0, side * -.16, side * -.09])
    k.box(`robot:cheek:${side}`, 'trim', [side * .225, 1.99, .18], [.07, .19, .16], [0, side * -.25, 0])
    k.cylinder(`robot:ear:hub:${side}`, 'trim', [side * .27, 2.07, 0], .115, .09, [0, 0, Math.PI / 2])
    k.box(`robot:thigh:armour:${side}`, 'shell', [side * .25, .53, .16], [.18, .24, .07], [0, side * .12, 0])
    k.box(`robot:knee:shield:${side}`, 'shell', [side * .23, .25, .19], [.22, .16, .09])
    k.box(`robot:shoulder:cap:${side}`, 'shell', [side * .49, 1.83, .02], [.34, .13, .42], [0, 0, side * -.12])
    k.box(`robot:forearm:inset:${side}`, 'trim', [side * .48, 1, .198], [.085, .20, .025])
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
  for(let i=0;i<(detail?7:4);i++) {
    k.box(`robot:spine:vertebra:${i}`, 'hardware', [0,1.10+i*.085,-.275],[.17,.045,.07],[.1,0,i%2?.12:-.12])
  }
  k.cylinder('robot:back:service-hatch','trim',[0,1.58,-.26],.13,.065,[Math.PI/2,0,0],detail?24:12)
  k.torus('robot:back:hatch-rim','hardware',[0,1.58,-.305],.105,.014)
  for(const side of pair) {
    k.hose(`robot:back:power-hose:${side}`,'trim',[[side*.08,1.7,-.26],[side*.28,1.55,-.29],[side*.30,1.18,-.28],[side*.15,1.04,-.2]],.018)
    k.cylinder(`robot:elbow:axle:${side}`,'hardware',[side*.51,1.15,.02],.065,.24,[0,0,Math.PI/2],12)
    k.cylinder(`robot:knee:axle:${side}`,'hardware',[side*.25,.25,.04],.07,.29,[0,0,Math.PI/2],12)
    k.hose(`robot:leg:hydraulic:${side}`,'hardware',[[side*.31,.67,-.07],[side*.34,.48,-.1],[side*.31,.30,-.08]],.022)
    k.cylinder(`robot:head:ear-inset:${side}`,'hardware',[side*.32,2.07,0],.075,.02,[0,0,Math.PI/2],detail?20:10)
    for(let i=0;i<(detail?4:2);i++) {
      k.box(`robot:helmet:chevron:${side}:${i}`,'trim',[side*(.045+i*.034),2.205,.025],[.014,.012,.22],[0,0,side*.15])
      k.box(`robot:forearm:louver:${side}:${i}`,'hardware',[side*.48,.91+i*.045,.22],[.12,.012,.023],[.3,0,0])
      k.box(`robot:toe:segment:${side}:${i}`,'shell',[side*.23,-.06,.10+i*.046],[.24,.045,.035],[.12,0,0])
    }
    if(detail) {
      for(let i=0;i<6;i++){const a=i*Math.PI/3;k.cylinder(`robot:ear:screw:${side}:${i}`,'joint',[side*.333,2.07+Math.cos(a)*.054,Math.sin(a)*.054],.009,.015,[0,0,Math.PI/2],6)}
      for(let i=0;i<4;i++)k.cylinder(`robot:knuckle:${side}:${i}`,'hardware',[side*(.31+i*.035),.80,.27],.019,.025,[Math.PI/2,0,0],8)
    }
  }
  // Surface-only detailing: dimensions are authoring invariants, never hit-volume changes.
  for(const side of pair) {
    for(const [part,x,y,z,w,h] of [
      ['chest',side*.205,1.61,.333,.20,.15],
      ['thigh',side*.25,.53,.201,.12,.17],
      ['knee',side*.23,.25,.239,.15,.09],
    ]) {
      for(const edge of [-1,1]) {
        k.box(`robot:seam:${part}:${side}:${edge}`,'joint',[x+edge*w/2,y,z],[.005,h,.005])
        k.box(`robot:seam:edge:${part}:${side}:${edge}`,'hardware',[x+edge*(w/2-.008),y,z+.001],[.003,h*.85,.004])
      }
      if(detail)for(const dx of [-1,1])for(const dy of [-1,1]) {
        const p=[x+dx*w*.32,y+dy*h*.35,z+.006]
        k.cylinder(`robot:socket:${part}:${side}:${dx}:${dy}`,'joint',p,.012,.007,[Math.PI/2,0,0],8)
        k.cylinder(`robot:screw:${part}:${side}:${dx}:${dy}`,'hardware',[p[0],p[1],p[2]+.004],.007,.005,[Math.PI/2,0,0],6)
      }
    }
    for(const [part,x,y,z,r] of [['forearm',side*.48,.90,.06,.112],['shin',side*.23,.04,0,.115]]) {
      k.torus(`robot:seal:${part}:${side}`,'joint',[x,y,z],r,.007,[Math.PI/2,0,0],detail?20:12)
      k.torus(`robot:seal:lip:${part}:${side}`,'hardware',[x,y+.012,z],r,.004,[Math.PI/2,0,0],detail?20:12)
    }
    k.box(`robot:shin:metal-inlay:${side}`,'hardware',[side*.23,.045,.139],[.045,.15,.008])
    k.box(`robot:helmet:temple-seam:${side}`,'joint',[side*.14,2.12,.20],[.005,.10,.008])
    k.box(`robot:helmet:temple-edge:${side}`,'hardware',[side*.15,2.12,.20],[.004,.085,.009])
    if(detail) {
      for(let i=0;i<3;i++)k.box(`robot:neck:seal:${side}:${i}`,'joint',[side*.09,1.79+i*.025,.085],[.055,.006,.028])
      for(let i=0;i<4;i++)k.box(`robot:back:service-slot:${side}:${i}`,'joint',[side*.07,1.56+i*.018,-.300],[.034,.006,.012])
      k.cylinder(`robot:back:connector:${side}`,'hardware',[side*.15,1.08,-.28],.031,.035,[Math.PI/2,0,0],12)
      k.cylinder(`robot:back:connector-core:${side}`,'joint',[side*.15,1.08,-.301],.019,.01,[Math.PI/2,0,0],8)
    }
  }
  // Authored slender silhouette; the feet origin, height and simulation capsule stay unchanged.
  for(const object of k.objects) {
    const position=object.geometry.getAttribute('position')
    for(let i=0;i<position.count;i++) {
      const y=position.getY(i), waist=1-.10*Math.max(0,1-Math.abs(y-1.05)/.35)
      position.setXYZ(i,position.getX(i)*.76*waist,y,position.getZ(i)*.78)
    }
    object.geometry.computeVertexNormals()
  }
  writer(`light-sport-robot-lod${lod}.glb`, k.objects)
}

function addGripHands(k, detail) {
  // These are authored attachment landmarks in the same +Z-forward weapon space.
  // Elbows extend toward the player and below the camera frustum, never floating in view.
  const poses = [
    {side:-1, elbow:[-.85,-1.30,-.65], wrist:[-.19,-.24,-.09], palm:[-.105,-.20,-.025]},
    {side:1, elbow:[1.10,-1.30,-.60], wrist:[.27,-.17,.64], palm:[.10,-.095,.76]},
  ]
  for(const {side,elbow,wrist,palm} of poses) {
    k.segment(`viewmodel:forearm:${side}`,'shell',elbow,wrist,.155,.105,detail?14:8)
    const axis=new THREE.Vector3(...wrist).sub(new THREE.Vector3(...elbow))
    // Authoring dimensions: seams follow the tapered shell, independent of gameplay controls.
    const direction=axis.clone().normalize(), radial=new THREE.Vector3(0,0,1).cross(direction).normalize(), tangent=direction.clone().cross(radial)
    const point=(t,angle,radius)=>new THREE.Vector3(...elbow).addScaledVector(axis,t).addScaledVector(radial,Math.cos(angle)*radius).addScaledVector(tangent,Math.sin(angle)*radius).toArray()
    for(const t of (detail?[.36,.56,.76,.89]:[.56,.89])) {
      const radius=.155-.05*t
      k.segment(`viewmodel:seam:${side}:${t}`,'joint',point(t-.004,0,0),point(t+.004,0,0),radius+.003,radius+.003,detail?28:16)
      k.segment(`viewmodel:seam:lip:${side}:${t}`,'hand-metal',point(t+.005,0,0),point(t+.012,0,0),radius+.004,radius+.004,detail?28:16)
    }
    for(let a=0;a<(detail?6:4);a++) {
      const angle=a*Math.PI*2/(detail?6:4)
      k.segment(`viewmodel:panel:groove:${side}:${a}`,'joint',point(.38,angle,.139),point(.87,angle,.115),.003,.003,6)
      for(const t of (detail?[.42,.68,.85]:[.68])) {
        const radius=.155-.05*t
        k.segment(`viewmodel:fastener:socket:${side}:${a}:${t}`,'joint',point(t,angle,radius-.002),point(t,angle,radius+.005),.012,.012,8)
        k.segment(`viewmodel:fastener:head:${side}:${a}:${t}`,'hand-metal',point(t,angle,radius+.004),point(t,angle,radius+.007),.007,.007,6)
      }
      if(a%2===0)k.segment(`viewmodel:metal:inlay:${side}:${a}`,'hand-metal',point(.59,angle+.12,.133),point(.82,angle+.12,.121),.015,.012,8)
    }
    const cuffStart=new THREE.Vector3(...wrist).addScaledVector(axis,-.11).toArray()
    k.segment(`viewmodel:wrist:${side}`,'hand-metal',cuffStart,wrist,.12,.11,detail?16:8)
    k.segment(`viewmodel:wrist:link:${side}`,'hardware',wrist,palm,.10,.09,detail?12:8)
    k.sphere(`viewmodel:wrist:ball:${side}`,'hardware',wrist,[.112,.112,.112],detail?12:8)
    k.box(`viewmodel:hand:${side}`,'shell',palm,side<0?[.12,.19,.19]:[.27,.10,.22],[0,side<0?-.12:.1,side<0?-.1:0])
    const middle=new THREE.Vector3(...elbow).lerp(new THREE.Vector3(...wrist),.70)
    k.segment(`viewmodel:forearm:brace:${side}`,'trim',middle.clone().add(new THREE.Vector3(side*.10,0,0)).toArray(),[wrist[0]+side*.10,wrist[1]-.025,wrist[2]],.028,.018,8)
    for(let finger=0;finger<4;finger++) {
      const points=side<0
        ? [[-.15,-.135-finger*.047,.055],[-.035,-.135-finger*.047,.108],[.073,-.14-finger*.047,.069],[.083,-.14-finger*.047,-.002]]
        : [[.205,-.10,.665+finger*.061],[.248,-.016,.665+finger*.061],[.19,.024,.665+finger*.061],[.13,.02,.665+finger*.061]]
      for(let segment=0;segment<3;segment++)k.segment(`viewmodel:hand:finger:${side}:${finger}:${segment}`,segment===1?'shell':'hardware',points[segment],points[segment+1],.023,.021,detail?8:6)
      if(detail)k.sphere(`viewmodel:knuckle:${side}:${finger}`,'shell',points[1],[.029,.029,.03],10)
    }
    const thumb=side<0?[[-.11,-.11,-.085],[-.03,-.075,-.115],[.054,-.10,-.06]]:[[.015,-.095,.87],[-.11,-.028,.87],[-.13,.022,.83]]
    for(let i=0;i<2;i++)k.segment(`viewmodel:thumb:${side}:${i}`,'shell',thumb[i],thumb[i+1],.034,.028,detail?10:6)
    if(detail)k.hose(`viewmodel:forearm:cable:${side}`,'trim',[elbow,[middle.x+side*.12,middle.y,middle.z],wrist],.015)
  }
}

function shotgun(lod, viewmodel = false) {
  const k = kit(), detail = lod === 0, scale = viewmodel ? 1 : .72
  const v = (n) => n * scale; k.box('weapon:receiver', 'shell', [0, v(.14), v(.36)], [v(.42), v(.28), v(.64)]); k.box('weapon:receiver:top-armour', 'shell', [0, v(.31), v(.35)], [v(.25), v(.08), v(.40)]); k.box('weapon:energy-chamber', 'identity', [0, v(.17), v(.18)], [v(.36), v(.24), v(.32)]); k.box('weapon:grip', 'rubber', [0, v(-.22), v(-.03)], [v(.17), v(.34), v(.19)], [.18, 0, 0]); k.box('weapon:owner-indicator', 'identity', [0, v(.31), v(.32)], [v(.24), v(.06), v(.3)])
  for (const side of [-1, 1]) {
    const x = v(side * .17); k.cylinder(`weapon:barrel-shroud:${side}`, 'joint', [x, v(.1), v(.88)], v(.105), v(1.1), [Math.PI / 2, 0, 0], detail ? 20 : 12); k.cylinder(`weapon:muzzle-opening:${side}`, 'muzzle-dark', [x, v(.1), v(1.44)], v(.071), v(.016), [Math.PI / 2, 0, 0], detail ? 16 : 10); k.cylinder(`weapon:inner-bore:${side}`, 'muzzle-dark', [x, v(.1), v(1.37)], v(.056), v(.14), [Math.PI / 2, 0, 0], detail ? 16 : 10)
    const ringCount = detail ? 5 : 2; for (let ring = 0; ring < ringCount; ring++) k.torus(`weapon:barrel:ring:${side}:${ring}`, 'shell', [x, v(.1), v(.54 + ring * .18)], v(.11), v(.012), [0, 0, 0], detail ? 16 : 10)
    const ventCount = detail ? 6 : 2; for (let vent = 0; vent < ventCount; vent++) k.box(`weapon:barrel:vent:${side}:${vent}`, 'trim', [x + v((vent % 2 ? .08 : -.08)), v(.18), v(.63 + Math.floor(vent / 2) * .19)], [v(.035), v(.025), v(.06)])
    k.box(`weapon:barrel-rail:${side}`, 'trim', [x, v(.22), v(.9)], [v(.045), v(.025), v(.88)]); if (detail) k.box(`weapon:barrel-rail:side:${side}`, 'trim', [x + v(side * .11), v(.1), v(.91)], [v(.025), v(.05), v(.84)])
  }
  k.box('weapon:bridge', 'shell', [0, v(.12), v(.88)], [v(.16), v(.09), v(.78)]); const coilCount = detail ? 9 : 3; for (let coil = 0; coil < coilCount; coil++) { const z = v(.02 + coil * .055); k.torus(`weapon:energy:coil:${coil}`, 'identity', [0, v(.17), z], v(.11), v(.009), [0, 0, 0], detail ? 16 : 10) }
  if (detail) for (let rail = 0; rail < 4; rail++) k.box(`weapon:receiver:rail:${rail}`, 'trim', [v((rail % 2 ? .18 : -.18)), v(.31), v(.22 + Math.floor(rail / 2) * .26)], [v(.035), v(.035), v(.22)])
  if (viewmodel) addGripHands(k, detail)

  for (const side of [-1, 1]) {
    k.box(`weapon:side-plate:${side}`, 'shell', [v(side * .25), v(.16), v(.33)], [v(.045), v(.18), v(.40)], [0, 0, side * -.08])
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
  // Visible rear breech: this contour is prominent in the first-person view.
  k.cylinder('weapon:breech:rear-housing','trim',[0,v(.14),v(-.005)],v(.16),v(.07),[Math.PI/2,0,0],detail?16:8)
  k.torus('weapon:breech:seal','hardware',[0,v(.14),v(-.044)],v(.127),v(.012))
  k.cylinder('weapon:breech:core','identity',[0,v(.14),v(-.048)],v(.080),v(.012),[Math.PI/2,0,0],detail?20:10)
  for(const side of [-1,1]) {
    k.hose(`weapon:feed-line:${side}`,'hardware',[[v(side*.2),v(.1),v(.05)],[v(side*.30),v(.01),v(.36)],[v(side*.24),v(.06),v(.64)]],v(.018))
    for(let i=0;i<(detail?5:2);i++) {
      const z=v(.57+i*.16)
      k.box(`weapon:shroud:segment:${side}:${i}`,'shell',[v(side*.235),v(.11),z],[v(.10),v(.13),v(.10)],[0,0,side*.22])
      if(detail)k.cylinder(`weapon:shroud:pin:${side}:${i}`,'hardware',[v(side*.289),v(.12),z],v(.014),v(.015),[0,0,Math.PI/2],6)
    }
    k.cylinder(`weapon:receiver:hinge:${side}`,'hardware',[v(side*.245),v(.15),v(.16)],v(.052),v(.022),[0,0,Math.PI/2],detail?16:8)

  }
  // The camera-attached model is authored at a compact presentation scale; its
  // manifest bounds remain a maximum attachment envelope, not collision data.
  if (viewmodel) for (const object of k.objects) object.geometry.scale(.42, .42, .42)
  writer(`${viewmodel ? 'first-person-shotgun' : 'double-barrel-shotgun'}-lod${lod}.glb`, k.objects)
}

for (const lod of [0, 1]) robot(lod)
for (const lod of [0, 1]) shotgun(lod)
for (const lod of [0, 1]) shotgun(lod, true)
