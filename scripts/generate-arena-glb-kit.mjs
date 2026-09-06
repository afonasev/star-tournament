import { mkdirSync, writeFileSync, readFileSync } from 'node:fs'

const portalKit = JSON.parse(readFileSync(new URL('../src/arena/portalKit.json', import.meta.url), 'utf8'))
const output = new URL('../public/assets/arena/', import.meta.url)
mkdirSync(output, { recursive: true })
const vertices = new Float32Array([-0.5,-0.5,-0.5,0.5,-0.5,-0.5,0.5,0.5,-0.5,-0.5,0.5,-0.5,-0.5,-0.5,0.5,0.5,-0.5,0.5,0.5,0.5,0.5,-0.5,0.5,0.5])
const indices = new Uint16Array([0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,2,3,7,2,7,6,1,2,6,1,6,5,3,0,4,3,4,7])
const palette={white:[.92,.96,.97,1],cyan:[.08,.75,.84,1],lime:[.72,1,.4,1],orange:[1,.53,.3,1]}
// Collision proxies preserve the complete doorway kit. The renderer facade uses
// only its structural cyan pieces, each contracted slightly to leave a visible
// gap at curved joints instead of overlapping box faces.
const portalFacadeParts=portalKit.parts
  .filter(([material])=>material==='cyan')
  .map(([material,position,size,rotation])=>[material,position,[size[0]*.94,size[1]*.94,size[2]*.92],rotation])
const contracts={
  'wall-lamp':{units:'meters',up:'+Y',front:'+Z',pivot:'wall-base-center',attachmentBoundsMeters:[.8,.3,.16]},
  'portal-facade':{units:'meters',up:'+Y',front:'+Z',pivot:'doorway-plane-center',attachmentBoundsMeters:[4,4,.8]},
  'wall-bay':{units:'meters',up:'+Y',front:'+Z',pivot:'wall-base-center',attachmentBoundsMeters:[3.2,3.4,.4]},
  'floor-route-guide':{units:'meters',up:'+Y',front:'+Z',pivot:'floor-contact-center',attachmentBoundsMeters:[3.2,.08,.35]},
  'central-landmark':{units:'meters',up:'+Y',front:'+Z',pivot:'combat-region-center',attachmentBoundsMeters:[2.4,.2,2.4]},
}
const kits={
  'wall-lamp':[['white',[0,.15,0],[.8,.3,.12]],['cyan',[0,.15,.07],[.65,.14,.02]],['white',[-.3,.15,.08],[.03,.18,.02]],['white',[.3,.15,.08],[.03,.18,.02]]],
  'portal-facade':portalFacadeParts,
  'wall-bay':[['white',[0,1.65,0],[3.2,3.3,.25]],['cyan',[-1.3,1.7,.24],[.28,3.55,.34]],['cyan',[1.3,1.7,.24],[.28,3.55,.34]],['lime',[0,2.8,.24],[2.35,.12,.34]]],
  'floor-route-guide':[['lime',[0,.04,0],[3.2,.08,.32]],['cyan',[0,.08,0],[2.35,.035,.52]]],
  'central-landmark':[['orange',[0,.1,-1],[2.4,.2,.2]],['orange',[0,.1,1],[2.4,.2,.2]],['orange',[-1,.1,0],[.2,.2,2.4]],['orange',[1,.1,0],[.2,.2,2.4]],['lime',[0,.16,0],[1.5,.08,1.5]]],
}

function write(name, parts, contract){const bin=Buffer.concat([Buffer.from(vertices.buffer),Buffer.from(indices.buffer)]);const materials=Object.entries(palette).map(([name,color])=>({name,pbrMetallicRoughness:{baseColorFactor:color,metallicFactor:.15,roughnessFactor:.3},emissiveFactor:color.slice(0,3)}));const ids=Object.fromEntries(materials.map((m,i)=>[m.name,i]));const nodes=parts.map(([material,translation,scale,rotation])=>{const node={mesh:ids[material],translation,scale};if(rotation!==undefined)node.rotation=rotation;return node});const json=Buffer.from(JSON.stringify({asset:{version:'2.0',generator:'star-tournament clean-future-sport-glb-v1',extras:contract},scenes:[{nodes:parts.map((_,i)=>i)}],scene:0,nodes,meshes:materials.map((_,material)=>({primitives:[{attributes:{POSITION:0},indices:1,material}]})),materials,buffers:[{byteLength:bin.length}],bufferViews:[{buffer:0,byteOffset:0,byteLength:vertices.byteLength,target:34962},{buffer:0,byteOffset:vertices.byteLength,byteLength:indices.byteLength,target:34963}],accessors:[{bufferView:0,componentType:5126,count:8,type:'VEC3',min:[-.5,-.5,-.5],max:[.5,.5,.5]},{bufferView:1,componentType:5123,count:36,type:'SCALAR'}]}));const pad=(v,fill=0)=>Buffer.concat([v,Buffer.alloc((4-v.length%4)%4,fill)]);const jc=pad(json,32),bc=pad(bin),head=Buffer.alloc(12),jh=Buffer.alloc(8),bh=Buffer.alloc(8);head.writeUInt32LE(0x46546c67);head.writeUInt32LE(2,4);head.writeUInt32LE(12+8+jc.length+8+bc.length,8);jh.writeUInt32LE(jc.length);jh.writeUInt32LE(0x4e4f534a,4);bh.writeUInt32LE(bc.length);bh.writeUInt32LE(0x004e4942,4);writeFileSync(new URL(name,output),Buffer.concat([head,jh,jc,bh,bc]))}
for(const [key,parts] of Object.entries(kits)){write(`${key}-lod0.glb`,parts,contracts[key]);write(`${key}-lod1.glb`,key === 'wall-lamp' ? parts.slice(0,2) : parts,contracts[key])}
