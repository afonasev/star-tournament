import { validateArenaPhysicalRoutes } from '../arena/physicalValidation'
import { requireProceduralArena, type ArenaDefinition, type ArenaSize } from '../arena'
import { createRapierCollisionWorld } from '../collision/rapierWorld'
import { createActionFrame, type ActionFrame } from '../input/actionFrame'
import { createMatchConfiguration, DEFAULT_MATCH_CONFIGURATION, TEAM_COLORS, type MatchConfiguration, type MatchMode } from '../match'
import { VETTED_PARTICIPANT_COLORS } from '../match/participantPalette'
import { PROTOTYPE_V1_PROFILE as profile } from '../profiles'
import type { BotDifficulty } from '../profiles/botRules'
import { createInitialSimulationSnapshot, parseSimulationSnapshot, simulationSnapshotHash, type SimulationSnapshot } from '../simulation/playableSnapshot'
import { livingParticipantCollisionCapsules, stepPlayableSimulation } from '../simulation/playableStep'
import { createPlayableSimulationReplay } from '../simulation/playableReplay'
import { distance } from './navigation'
import { arenaSupportHeight } from '../arena/elevation'
import { BOT_AI_VERSION } from './state'
import { AI_EVALUATION_V1 } from './evaluation'
export { AI_EVALUATION_V1, wilsonInterval } from './evaluation'
export { parsePlayableSimulationReplay } from '../simulation/playableReplay'
export { simulationSnapshotHash } from '../simulation/playableSnapshot'

export function botConfiguration(seed: number, difficulties: readonly BotDifficulty[], mode: MatchMode='ffa', size: ArenaSize='small'): MatchConfiguration {
  const {contentHash:_hash,...base}=DEFAULT_MATCH_CONFIGURATION
  return createMatchConfiguration({...base,execution:'headless',arenaSeed:seed,arenaSize:size,mode,durationMinutes:1,
    participants:difficulties.map((difficulty,i)=>({semanticId:`bot-${i+1}`,name:`Бот ${i+1}`,kind:'bot',difficulty,
      color:mode==='ffa'?VETTED_PARTICIPANT_COLORS[i]!:TEAM_COLORS[i%2===0?'team-a':'team-b'],team:mode==='ffa'?null:i%2===0?'team-a':'team-b'}))})
}
export async function createAiRunner(configuration:MatchConfiguration, arena: ArenaDefinition = requireProceduralArena({seed:configuration.arenaSeed,size:configuration.arenaSize,profile}), restored?:SimulationSnapshot) {
  let snapshot=restored?parseSimulationSnapshot(restored,configuration):createInitialSimulationSnapshot({rngSeed:configuration.arenaSeed,arena,configuration})
  await validateArenaPhysicalRoutes(arena,profile)
  const initialSnapshot=snapshot
  const m=profile.rules.player.movement
  const world=await createRapierCollisionWorld(arena,livingParticipantCollisionCapsules(snapshot,profile),{skinWidth:profile.rules.player.capsule.skinWidth,autostepMaxHeight:m.autostepMaxHeight,autostepMinWidth:m.autostepMinWidth,snapToGroundDistance:m.snapToGroundDistance,maxSlopeClimbDegrees:m.maxSlopeClimbDegrees,minSlopeSlideDegrees:m.minSlopeSlideDegrees})
  return {configuration,arena,initialSnapshot,world,get snapshot(){return snapshot},step(frame:ActionFrame=createActionFrame(snapshot.tick+1,[])){snapshot=stepPlayableSimulation(snapshot,frame,world,profile,configuration,arena);return snapshot},dispose(){world.dispose()}}
}
export interface AiMetrics { id:string; distanceMeters:number; shots:number; hitShots:number; jumps:number; airborneTicks:number; idleTicks:number; maximumIdleTicks:number; supportTicks:number; closeAllyTicks:number; maximumCloseAllyTicks:number; firstContactTick:number|null; visitedSupportIds:string[]; transitionVisits:Record<string,number>; recoveryCount:number }
export async function runAiMatch(configuration:MatchConfiguration,tickBudget:number=AI_EVALUATION_V1.tickBudget, checkpointEvery:number=AI_EVALUATION_V1.checkpointEveryTicks) {
  const start=performance.now();const runner=await createAiRunner(configuration)
  const initialized=performance.now()
  const metrics:AiMetrics[]=configuration.participants.map(p=>({id:p.semanticId,distanceMeters:0,shots:0,hitShots:0,jumps:0,airborneTicks:0,idleTicks:0,maximumIdleTicks:0,supportTicks:0,closeAllyTicks:0,maximumCloseAllyTicks:0,firstContactTick:null,visitedSupportIds:[],transitionVisits:{},recoveryCount:0}))
  const lastSupport=new Map<string,string>()
  const idle=new Map<string,number>();const close=new Map<string,number>();const checkpoints:Array<{tick:number;hash:string}>=[]
  const frames:ActionFrame[]=[]
  try {
    while(runner.snapshot.tick<tickBudget && runner.snapshot.match.phase!=='finished') {
      const before=runner.snapshot;const frame=createActionFrame(before.tick+1,[]);const after=runner.step(frame);frames.push(frame)
      for(const metric of metrics) {
        const p=after.participants.find(p=>p.semanticId===metric.id)!;const old=before.participants.find(p=>p.semanticId===metric.id)!
        if(p.alive&&p.grounded){
          const feet=p.position.y-profile.rules.player.capsule.halfHeight-profile.rules.player.capsule.radius
          const support=runner.arena.surfaces.map(s=>({id:s.id,y:arenaSupportHeight(runner.arena,s.id,p.position.x,p.position.z)}))
            .filter((s):s is {id:string;y:number}=>s.y!==undefined&&Math.abs(s.y-feet)<profile.rules.player.capsule.radius)
            .sort((a,b)=>Math.abs(a.y-feet)-Math.abs(b.y-feet)||a.id.localeCompare(b.id,'en'))[0]?.id
          if(support){
            if(!metric.visitedSupportIds.includes(support)){metric.visitedSupportIds.push(support);metric.visitedSupportIds.sort()}
            if(lastSupport.get(metric.id)!==support&&runner.arena.links.some(l=>l.transitionSurfaceId===support))metric.transitionVisits[support]=(metric.transitionVisits[support]??0)+1
            lastSupport.set(metric.id,support)
          }
        }
        if((p.botState?.recoveryTick??0)>(old.botState?.recoveryTick??0))metric.recoveryCount++
        if(p.alive&&old.alive) metric.distanceMeters+=distance(old.position,p.position)
        const notMoving=p.alive&&old.alive&&distance(old.position,p.position)<profile.rules.player.capsule.skinWidth
        const span=notMoving?(idle.get(metric.id)??0)+1:0;idle.set(metric.id,span);metric.idleTicks+=notMoving?1:0;metric.maximumIdleTicks=Math.max(metric.maximumIdleTicks,span)
        metric.airborneTicks+=p.alive&&!p.grounded?1:0;metric.jumps+=p.alive&&old.grounded&&!p.grounded&&p.velocity.y>0?1:0
        metric.supportTicks+=p.alive&&p.botState?.intent==='support'?1:0
        const nearby=p.alive&&p.team!==null&&after.participants.some(a=>a.semanticId!==p.semanticId&&a.alive&&a.team===p.team&&distance(a.position,p.position)<profile.rules.bots.cooperation.separationMeters)
        const closeSpan=nearby?(close.get(metric.id)??0)+1:0;close.set(metric.id,closeSpan);metric.closeAllyTicks+=nearby?1:0;metric.maximumCloseAllyTicks=Math.max(metric.maximumCloseAllyTicks,closeSpan)
        if(p.botState?.memory.length&&metric.firstContactTick===null)metric.firstContactTick=after.tick
        metric.shots+=after.events.filter(e=>e.kind==='shotgun-shot'&&e.shooterId===metric.id).length
        metric.hitShots+=after.events.some(e=>e.kind==='target-damaged'&&e.attackerId===metric.id)?1:0
      }
      if(after.tick%checkpointEvery===0)checkpoints.push({tick:after.tick,hash:simulationSnapshotHash(after,configuration)})
    }
    const end=performance.now();const snapshot=runner.snapshot
    const stalled=metrics.some(m=>m.shots===0||m.maximumIdleTicks>AI_EVALUATION_V1.maximumIdleTicks)
    const outcome=snapshot.match.phase==='finished'?(stalled?'stall':'finished'):'timeout'
    return {summary:{aiVersion:BOT_AI_VERSION,profile:snapshot.profile,arena:snapshot.arena,configuration,outcome,result:snapshot.match.result,ticks:snapshot.tick,wallMilliseconds:end-start,simulationMilliseconds:end-initialized,speedup:(snapshot.tick/60)/((end-initialized)/1000),metrics,statistics:snapshot.participants.map(p=>({id:p.semanticId,difficulty:p.difficulty,...p.statistics})),checkpoints,finalHash:simulationSnapshotHash(snapshot,configuration)},snapshot,replay:createPlayableSimulationReplay(runner.initialSnapshot,frames,configuration,runner.arena)}
  } finally {runner.dispose()}
}
