import { describe, expect, it } from 'vitest'
import { requireProceduralArena } from '../arena'
import { createActionFrame } from '../input/actionFrame'
import { createMatchConfiguration, DEFAULT_MATCH_CONFIGURATION } from '../match'
import { PROTOTYPE_V1_PROFILE as profile, validateGameDesignProfile } from '../profiles'
import { createInitialSimulationSnapshot, parseSimulationSnapshot, simulationSnapshotHash } from '../simulation/playableSnapshot'
import { botConfiguration, createAiRunner, runAiMatch } from './headless'
import { arenaNavigation, distance } from './navigation'
import { observeBot, planBot } from './planner'
import { createBotState, parseBotState } from './state'

describe('bot AI contracts',()=>{
  it.each((['small','medium','large'] as const).flatMap(size=>[0,2,3,4,6].map(seed=>({seed,size}))))('pursues a declared combat layer through normal simulation on $size/$seed',async ({seed,size})=>{
    const configuration=botConfiguration(seed,['normal','normal'],'ffa',size)
    const arena=requireProceduralArena({seed,size})
    const nav=arenaNavigation(arena,profile)
    const goalRegion=arena.regions.find(r=>r.id===(arena.recipeId==='basement-service-loop'?'combat-center':'combat-upper'))!
    const initial=createInitialSimulationSnapshot({rngSeed:seed,arena,configuration})
    const self=initial.participants[0]!
    const feet={x:self.position.x,y:self.position.y-profile.rules.player.capsule.radius-profile.rules.player.capsule.halfHeight,z:self.position.z}
    const goal={...goalRegion.center,supportSurfaceId:goalRegion.supportSurfaceId}
    const restored=parseSimulationSnapshot({...initial,participants:initial.participants.map((p,i)=>i===0?{...p,botState:{...p.botState!,intent:'pursue',goal,route:nav.route(feet,goal)}}:{...p,alive:false,health:0,deathTick:0,respawnTick:3600})},configuration)
    const runner=await createAiRunner(configuration,arena,restored)
    let arrived=false,recoveries=0,previousRecovery=0
    const progress:unknown[]=[]
    try{
      for(let tick=0;tick<1800;tick++){
        const bot=runner.step().participants[0]!
        if(tick%60===0&&progress.length<16)progress.push({tick,position:bot.position,goal:bot.botState!.goal,route:bot.botState!.route.slice(0,2)})
        if(bot.botState!.recoveryTick>previousRecovery)recoveries++
        previousRecovery=bot.botState!.recoveryTick
        const feet={x:bot.position.x,y:bot.position.y-profile.rules.player.capsule.radius-profile.rules.player.capsule.halfHeight,z:bot.position.z}
        if(bot.grounded&&nav.reached!(feet,goal,profile.rules.bots.navigation.waypointRadius)){arrived=true;break}
      }
      expect(arrived,JSON.stringify({progress,position:runner.snapshot.participants[0]!.position,state:runner.snapshot.participants[0]!.botState,recoveries})).toBe(true)
      expect(recoveries).toBe(0)
    }finally{runner.dispose()}
  },60000)
  it.each([10036,10060])('regression %i: neither bot loses combat after wall contact or rear damage',async(seed)=>{
    const result=await runAiMatch(botConfiguration(seed,['easy','normal'],'ffa','large'))
    expect(result.summary.outcome).toBe('finished')
    expect(result.summary.metrics.every(m=>m.shots>0&&m.maximumIdleTicks<900)).toBe(true)
  },60000)
  it('validates per-bot difficulty and explicit headless roster',()=>{
    const configuration=botConfiguration(101,['easy','normal','hard'])
    expect(configuration.participants.map(p=>p.difficulty)).toEqual(['easy','normal','hard'])
    const {contentHash:_hash,...payload}=configuration
    const {execution:_execution,...browser}=payload
    expect(()=>createMatchConfiguration(browser)).toThrow(/local-seat/)
    expect(()=>createMatchConfiguration({...payload,participants:payload.participants.map(p=>({...p,difficulty:'expert'}))} as never)).toThrow(/difficulty/)
    expect(()=>createMatchConfiguration({...payload,participants:[...payload.participants,DEFAULT_MATCH_CONFIGURATION.participants[0]!]})).toThrow()
  })
  it('rejects malformed bot memory and negative refill without weakening validation',()=>{
    const state=createBotState(1,'b',{x:0,y:0,z:0})
    expect(parseBotState(JSON.parse(JSON.stringify(state)),'bot')).toEqual(state)
    expect(()=>parseBotState({...state,decisionTick:-1},'bot')).toThrow()
    expect(()=>parseBotState({...state,route:[{x:NaN,y:0,z:0}]},'bot')).toThrow()
    const altered=JSON.parse(JSON.stringify(profile));altered.rules.weapons.doubleBarrelShotgun.emptyRefillAmmo=0
    const result=validateGameDesignProfile(altered)
    expect(result.ok).toBe(false)
    if(!result.ok)expect(result.errors.some(e=>e.paths.includes('rules.weapons.doubleBarrelShotgun.emptyRefillAmmo'))).toBe(true)
    const cross=JSON.parse(JSON.stringify(profile));cross.rules.bots.easy.memorySeconds=0.5;cross.rules.bots.easy.decisionSeconds=1
    const invalid=validateGameDesignProfile(cross)
    expect(invalid.ok).toBe(false)
    if(!invalid.ok)expect(invalid.errors.some(e=>e.code==='BOT_CONTRACT_INVALID')).toBe(true)
  })
  it('routes between all spawn pairs without crossing canonical solids',()=>{
    for(const size of ['small','medium','large'] as const){
      const arena=requireProceduralArena({seed:101,size});const nav=arenaNavigation(arena,profile)
      for(const start of arena.spawnAnchors)for(const end of arena.spawnAnchors){
        const route=nav.route(start.position,end.position)
        expect(route.length,`${size} ${start.id} -> ${end.id}`).toBeGreaterThan(0)
        let previous=start.position
        for(const waypoint of route){expect(nav.clear(previous,waypoint)).toBe(true);previous=waypoint}
      }
    }
  })
  it('cannot observe hidden movement and accepts a test goal through the extension contract',async()=>{
    const runner=await createAiRunner(botConfiguration(102,['normal','normal']))
    try{
      const self=runner.snapshot.participants[0]!
      const blocked={...runner.world,raycast:()=>[{distance:0}]} as unknown as typeof runner.world
      const a=observeBot(runner.snapshot,self,blocked,profile)
      const moved={...runner.snapshot,participants:runner.snapshot.participants.map((p,i)=>i===1?{...p,position:{x:999,y:0,z:999}}:p)}
      const b=observeBot(moved,self,blocked,profile)
      expect(a).toEqual(b)
      const nav=arenaNavigation(runner.arena,profile)
      const result=planBot(a,self.botState!,nav,profile,{goals:[()=>[{id:'test-pickup',position:nav.destinations[0]!,utility:100}]]})
      expect(result.state.intent).toBe('test-pickup')
      const noFire=planBot({...a,enemies:[{id:'target',lifeId:'target:1',position:nav.destinations[1]!,tick:0}]},self.botState!,nav,profile,{weapons:[{id:self.weapon.semanticId,aimPoint:t=>t.position,ready:()=>false}]})
      expect(noFire.actions.find(a=>a.actionId==='fire')?.value).toBe(0)
    }finally{runner.dispose()}
  // Runner initialization physically validates the complete room/stair route corpus.
  },30000)
  it('replays identical actions after a checkpoint in a newly constructed world',async()=>{
    const configuration=botConfiguration(101,['easy','normal','hard'])
    const original=await createAiRunner(configuration)
    let restored:Awaited<ReturnType<typeof createAiRunner>>|undefined
    try{
      for(let i=0;i<400;i++)original.step()
      const checkpoint=JSON.parse(JSON.stringify(original.snapshot))
      restored=await createAiRunner(configuration,original.arena,checkpoint)
      for(let i=0;i<180;i++){
        original.step();restored.step()
        expect(simulationSnapshotHash(restored.snapshot,configuration)).toBe(simulationSnapshotHash(original.snapshot,configuration))
      }
      expect(()=>original.step(createActionFrame(original.snapshot.tick+1,[{participantId:'bot-1',actionId:'fire',value:1}]))).toThrow(/External actions/)
      const invalid={...checkpoint,participants:checkpoint.participants.map((p:Record<string,unknown>,i:number)=>i===0?{...p,difficulty:'easy-typo'}:p)}
      expect(()=>parseSimulationSnapshot(invalid,configuration)).toThrow()
    }finally{original.dispose();restored?.dispose()}
  },15000)
  it('scans after unseen damage without acquiring hidden coordinates and bounds the turn',async()=>{
    const runner=await createAiRunner(botConfiguration(10036,['easy','normal'],'ffa','large'))
    try {
      const original=runner.snapshot.participants[0]!
      const self={...original,health:80}
      const before={...self.botState!,lastHealth:100}
      const observation={tick:10,self,enemies:[],reports:[],allies:[]}
      const result=planBot(observation,before,arenaNavigation(runner.arena,profile),profile)
      expect(result.state.alertYaw).toBe(self.yaw+Math.PI)
      expect(result.state.memory).toEqual([])
      const look=result.actions.find(a=>a.actionId==='look-yaw')!.value
      expect(Math.abs(look*profile.rules.camera.horizontalSensitivity)).toBeLessThanOrEqual(profile.rules.bots.easy.aimDegreesPerSecond/60)
      expect(result.actions.find(a=>a.actionId==='fire')!.value).toBe(0)
    } finally {runner.dispose()}
  })
  it('runs actual combat with movement, bounded jumping, damage, death and respawn',async()=>{
    const result=await runAiMatch(botConfiguration(101,['easy','normal','hard']))
    expect(result.summary.outcome).toBe('finished')
    expect(result.summary.metrics.every(m=>m.distanceMeters>10&&m.shots>0&&m.airborneTicks<result.summary.ticks/2)).toBe(true)
    expect(result.snapshot.participants.some(p=>p.lifeSequence>1)).toBe(true)
    expect(result.summary.statistics.reduce((n,p)=>n+p.damageDealt,0)).toBeGreaterThan(0)
    expect(result.snapshot.participants.every(p=>p.weapon.ammo>0)).toBe(true)
  },15000)
  it('supports nearby allies temporarily, separates, then selects its own goal',async()=>{
    const runner=await createAiRunner(botConfiguration(103,['hard','hard','hard','hard'],'teams'))
    try{
      const self=runner.snapshot.participants[0]!;const nav=arenaNavigation(runner.arena,profile)
      const allyPosition=nav.destinations.find(p=>distance(self.position,p)>3&&distance(self.position,p)<18)!
      const observation={tick:1,self,enemies:[],reports:[],allies:[{id:'human-ally',position:allyPosition}]}
      let chosen:ReturnType<typeof planBot>|undefined
      for(let seed=1;seed<100;seed++){
        const candidate=planBot(observation,createBotState(seed,self.semanticId,self.position),nav,profile)
        if(candidate.state.intent==='support'){chosen=candidate;break}
      }
      expect(chosen).toBeDefined()
      const expired=planBot({...observation,tick:chosen!.state.supportTick+1,allies:[]},chosen!.state,nav,profile)
      expect(expired.state.intent).not.toBe('support')
    }finally{runner.dispose()}
  },30000)
})
