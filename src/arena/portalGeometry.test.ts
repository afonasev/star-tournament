import { expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import kit from './portalKit.json'
import { requireProceduralArena } from './proceduralArena'
import { createArenaDefinition, arenaContentPayload } from './arenaDefinition'
import { DIAGNOSTIC_ARENA } from './diagnosticArena'
import { createRapierCollisionWorld } from '../collision/rapierWorld'
import { PROTOTYPE_V1_PROFILE as profile } from '../profiles'

it.each([0,1])('shares every authored GLB LOD%s box with the canonical portal contract', lod => {
  const bytes=readFileSync(new URL(`../../public/assets/arena/portal-facade-lod${lod}.glb`,import.meta.url))
  const asset=JSON.parse(bytes.subarray(20,20+bytes.readUInt32LE(12)).toString())
  expect(asset.nodes).toHaveLength(kit.parts.length)
  asset.nodes.forEach((node: {translation: number[];scale: number[];rotation?: number[]},i:number)=>{
    expect(node.translation).toEqual(kit.parts[i]![1])
    expect(node.scale).toEqual(kit.parts[i]![2])
    expect(node.rotation).toEqual(kit.parts[i]![3])
  })
})

it.each(['small','medium','large'] as const)('blocks the visible portal pillar and arch rays for %s', async size => {
  const generated=requireProceduralArena({size,seed:1})
  const parts=generated.surfaces.filter(s=>s.id.startsWith('portal-solid:'))
  const pillar=parts.find(s=>s.id.endsWith(':0'))!
  const payload=arenaContentPayload(DIAGNOSTIC_ARENA)
  payload.surfaces=[payload.surfaces.find(s=>s.id==='floor')!,...parts]
  const arena=createArenaDefinition(payload)
  const c=profile.rules.player.capsule,m=profile.rules.player.movement
  const horizontal=Math.abs(pillar.orientation.y)<0.5
  const normal=horizontal?{x:0,y:0,z:1}:{x:1,y:0,z:0}
  const start={x:pillar.center.x-normal.x*3,y:c.radius+c.halfHeight+c.skinWidth,z:pillar.center.z-normal.z*3}
  const world=await createRapierCollisionWorld(arena,[{semanticId:'walker',position:start,radius:c.radius,halfHeight:c.halfHeight}],{skinWidth:c.skinWidth,autostepMaxHeight:m.autostepMaxHeight,autostepMinWidth:m.autostepMinWidth,snapToGroundDistance:m.snapToGroundDistance,maxSlopeClimbDegrees:m.maxSlopeClimbDegrees,minSlopeSlideDegrees:m.minSlopeSlideDegrees})
  try {
    const moved=world.moveCapsule('walker',{x:normal.x*4,y:0,z:normal.z*4})
    expect(moved.contacts.some(contact=>contact.semanticId.startsWith('portal-solid:'))).toBe(true)
    expect(Math.hypot(moved.position.x-start.x,moved.position.z-start.z)).toBeLessThan(3)
    for(const part of parts) {
      const q=part.orientation
      const front={x:2*(q.x*q.z+q.y*q.w),y:2*(q.y*q.z-q.x*q.w),z:1-2*(q.x*q.x+q.y*q.y)}
      const origin={x:part.center.x-front.x*2,y:part.center.y-front.y*2,z:part.center.z-front.z*2}
      expect(world.raycast(origin,front,4,['walker']).some(hit=>hit.semanticId.startsWith('portal-solid:'))).toBe(true)
    }
  } finally {world.dispose()}
})
