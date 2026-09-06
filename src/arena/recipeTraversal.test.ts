import { describe, expect, it } from 'vitest'
import { createRapierCollisionWorld } from '../collision/rapierWorld'
import { PROTOTYPE_V1_PROFILE as profile } from '../profiles'
import { arenaNavigation } from '../ai/navigation'
import { requireProceduralArena } from './proceduralArena'
import { arenaFloorHeight } from './elevation'
import { arenaContentPayload, createArenaDefinition } from './arenaDefinition'
import { DIAGNOSTIC_ARENA } from './diagnosticArena'

const capsule = profile.rules.player.capsule
const offset = capsule.halfHeight+capsule.radius
const controller = { skinWidth:capsule.skinWidth, autostepMaxHeight:profile.rules.player.movement.autostepMaxHeight,autostepMinWidth:profile.rules.player.movement.autostepMinWidth,snapToGroundDistance:profile.rules.player.movement.snapToGroundDistance,maxSlopeClimbDegrees:profile.rules.player.movement.maxSlopeClimbDegrees,minSlopeSlideDegrees:profile.rules.player.movement.minSlopeSlideDegrees }

describe('recipe physical queries', () => {
  it.each(['small','medium','large'] as const)('walks both directions across %s atrium without jumping', async size => {
    const arena=requireProceduralArena({size,seed:1})
    const rules=profile.rules.arenaGenerator.presets[size]
    const edge=rules.rampWidthMeters/2+rules.rampLengthMeters+0.5
    const world=await createRapierCollisionWorld(arena,[{semanticId:'walker',position:{x:-edge,y:offset+capsule.skinWidth,z:0},radius:capsule.radius,halfHeight:capsule.halfHeight}],controller)
    try {
      let position={x:-edge,y:offset,z:0}, maxY=position.y
      for (const direction of [1,-1]) {
        for(let tick=0;tick<Math.ceil(2*edge/0.05)+30;tick++) {
          const result=world.moveCapsule('walker',{x:direction*0.05,y:-0.03,z:0})
          position={...result.position};maxY=Math.max(maxY,position.y)
          if(direction*position.x>=edge) break
        }
        expect(direction*position.x).toBeGreaterThanOrEqual(edge-0.1)
      }
      expect(maxY-offset).toBeGreaterThan(rules.elevationDeltaMeters-0.03)
      expect(position.y-offset).toBeLessThan(0.1)
      const route=arenaNavigation(arena,profile).route({x:-edge,y:0,z:0},{x:edge,y:0,z:0})
      expect(route.some(p=>p.y>=rules.elevationDeltaMeters-1e-6)).toBe(true)
      for(const point of route) expect(point.y).toBeCloseTo(arenaFloorHeight(arena,point.x,point.z)!,6)
    } finally { world.dispose() }
  })
  it('passes a projectile through movement barriers and blocks it at the window', async () => {
    const payload=arenaContentPayload(DIAGNOSTIC_ARENA)
    payload.schemaVersion=4
    payload.surfaces=[payload.surfaces.find(s=>s.id==='floor')!,...['movement-barrier','window'].map((classification,index)=>({id:classification,kind:'box' as const,center:{x:0,y:1,z:index*3},size:{x:4,y:2,z:0.5},orientation:{x:0,y:0,z:0,w:1},material:'wall' as const,semantics:{classification:classification as 'movement-barrier'|'window',movement:true,projectile:index===1,sector:'fixture'}}))]
    const arena=createArenaDefinition(payload)
    const world=await createRapierCollisionWorld(arena,[{semanticId:'walker',position:{x:0,y:offset+capsule.skinWidth,z:-2},radius:capsule.radius,halfHeight:capsule.halfHeight}],controller)
    try {
      const ray=world.raycast({x:0,y:1,z:-2},{x:0,y:0,z:1},10,['walker'])
      expect(ray.map(h=>h.semanticId)).toEqual(['window'])
      const movement=world.moveCapsule('walker',{x:0,y:0,z:4})
      expect(movement.position.z).toBeLessThan(-0.25)
      expect(movement.contacts.some(c=>c.semanticId==='movement-barrier')).toBe(true)
    } finally { world.dispose() }
  })
})

it('physically validates every spawn route in each recipe and size', async () => {
  const {validateArenaPhysicalRoutes}=await import('./physicalValidation')
  for(const size of ['small','medium','large'] as const) for(let seed=0;seed<8;seed++) await validateArenaPhysicalRoutes(requireProceduralArena({size,seed}),profile)
},60000)
