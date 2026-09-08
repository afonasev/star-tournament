import { describe,expect,it } from 'vitest'
import { arenaContentPayload,createArenaDefinition } from '../arena/arenaDefinition'
import { DIAGNOSTIC_ARENA } from '../arena/diagnosticArena'
import { rampBox } from '../arena/elevation'
import { validateArenaPhysicalRoutes } from '../arena/physicalValidation'
import { PROTOTYPE_V1_PROFILE as profile } from '../profiles'
import { arenaNavigation } from './navigation'

function fixture() {
  const payload=arenaContentPayload(DIAGNOSTIC_ARENA)
  payload.schemaVersion=5;payload.recipeId='fixture';payload.generatorVersion='layer-fixture'
  payload.generatorProfile={id:profile.id,revision:profile.revision,contentHash:profile.contentHash as typeof payload.generatorProfile.contentHash}
  const floor=payload.surfaces.find(s=>s.id==='floor')!
  floor.size={x:40,y:1,z:20};floor.semantics={classification:'floor',movement:true,projectile:true,sector:'lower'}
  payload.surfaces=[floor,{...floor,id:'upper',center:{x:0,y:2.875,z:0},size:{x:8,y:0.25,z:10}},rampBox('west-ramp',-12,-4,0,3,0,4,0.25),rampBox('east-ramp',4,12,3,0,0,4,0.25)]
  payload.regions=[{id:'combat-center',role:'combat',center:{x:0,y:0,z:0},size:{x:8,y:1,z:8},supportSurfaceId:'floor'},
    {id:'combat-upper',role:'combat',center:{x:0,y:3,z:0},size:{x:8,y:1,z:8},supportSurfaceId:'upper'}]
  payload.links=['west','east'].map(side=>({id:side,fromRegionId:'combat-center',toRegionId:'combat-upper',exposure:'open',transitionSurfaceId:`${side}-ramp`}))
  payload.spawnRegions=[{id:'spawn-region',regionId:'combat-center',anchorIds:['spawn']}]
  payload.spawnAnchors=[{id:'spawn',spawnRegionId:'spawn-region',position:{x:0,y:0,z:6},yawRadians:0,supportSurfaceId:'floor'}]
  return createArenaDefinition(payload)
}
describe('support-layer navigation',()=>{
  it('does not create an ordinary edge between overlapping lower and upper XZ',()=>{
    const nav=arenaNavigation(fixture(),profile)
    const lower={x:0,y:0,z:0},upper={x:0,y:3,z:0}
    expect(nav.clear(lower,upper)).toBe(false)
    const route=nav.route(lower,upper)
    expect(route.length).toBeGreaterThan(3)
    expect(route.some(p=>'supportSurfaceId' in p&&String(p.supportSurfaceId).endsWith('-ramp'))).toBe(true)
    expect(route.at(-1)).toEqual({...upper,supportSurfaceId:'upper'})
    expect(JSON.stringify(nav.route(lower,upper))).toBe(JSON.stringify(route))
    expect(nav.reached?.(lower,route.at(-1)!,1)).toBe(false)
  })
  it('routes lower traffic around ramp footprint without borrowing an X breakpoint',()=>{
    const nav=arenaNavigation(fixture(),profile)
    const start={x:-15,y:0,z:0},goal={x:0,y:0,z:0}
    expect(nav.clear(start,goal)).toBe(false)
    const route=nav.route(start,goal)
    expect(route.length).toBeGreaterThan(1)
    let previous=start
    for(const point of route){expect(point.y).toBe(0);expect(nav.clear(previous,point)).toBe(true);previous=point}
    expect(nav.route({x:-15,y:0,z:7},{x:15,y:0,z:7})).toEqual([{x:15,y:0,z:7,supportSurfaceId:'floor'}])
  })
  it('rejects a missing declared transition rather than switching to the highest floor',()=>{
    const payload=arenaContentPayload(fixture());payload.links=[]
    expect(arenaNavigation(createArenaDefinition(payload),profile).route({x:0,y:0,z:0},{x:0,y:3,z:0})).toEqual([])
  })
  it('can pursue a target inside a ramp and resume from that ramp',()=>{
    const nav=arenaNavigation(fixture(),profile),target={x:-8,y:1.5,z:0}
    for(const start of [{x:0,y:0,z:0},{x:0,y:3,z:0}]) {
      const destination=nav.route(start,target).at(-1)!
      expect(destination).toMatchObject({x:target.x,z:target.z,supportSurfaceId:'west-ramp'})
      expect(destination.y).toBeCloseTo(target.y,6)
      expect(nav.route(target,start).at(-1)).toMatchObject(start)
    }
  })
  it('physically crosses both ramps in both directions and all three lateral lanes',async()=>{
    await validateArenaPhysicalRoutes(fixture(),profile)
  })
  it('rejects wrong feet Y and a low ceiling in the actual world',async()=>{
    const payload=arenaContentPayload(fixture());payload.regions[1]!.center.y=0
    await expect(validateArenaPhysicalRoutes(createArenaDefinition(payload),profile)).rejects.toThrow('Declared support mismatch')
    const blocked=arenaContentPayload(fixture());blocked.surfaces.push({...blocked.surfaces[0]!,id:'ceiling',center:{x:0,y:4.1,z:0},size:{x:8,y:0.25,z:10},material:'wall',semantics:{classification:'slab',movement:true,projectile:true,sector:'upper'}})
    await expect(validateArenaPhysicalRoutes(createArenaDefinition(blocked),profile)).rejects.toThrow()
  })
})
