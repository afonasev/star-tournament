import { simulationMath } from '../core/simulationMath'
import type { ArenaDefinition } from '../arena'
import type { CollisionVector3 as Vec } from '../collision/contracts'
import type { NormalizedAction } from '../input/actionFrame'
import type { ValidatedGameDesignProfile } from '../profiles'
import type { BotTuning } from '../profiles/botRules'
import type { MatchParticipantState, SimulationSnapshot } from '../simulation/playableSnapshot'
import type { PlayableCollisionPort } from '../simulation/playableStep'
import { nextRandomUint32 } from '../simulation/rng'
import { SIMULATION_TICKS_PER_SECOND as HZ } from '../simulation/step'
import { arenaNavigation, distance, type NavigationCapabilities } from './navigation'
import { type BotState, type Sighting } from './state'

export interface BotObservation {
  tick: number
  self: MatchParticipantState
  enemies: Sighting[]
  allies: Array<{ id: string; position: Vec }>
  reports: Sighting[]
}
export type ObservationProvider = (snapshot: SimulationSnapshot, self: MatchParticipantState, collision: PlayableCollisionPort, profile: ValidatedGameDesignProfile) => BotObservation
export const observeBot: ObservationProvider = (snapshot, self, collision, profile) => {
  const tuning=profile.rules.bots[self.difficulty!]
  const eye={...self.position,y:self.position.y+profile.rules.player.eyeHeight}
  const exclude=snapshot.participants.map(p=>p.semanticId)
  const enemies:Sighting[]=[];const allies:BotObservation['allies']=[];const reports:Sighting[]=[]
  for(const p of snapshot.participants) {
    if(p.semanticId===self.semanticId || !p.alive) continue
    if(self.team!==null && p.team===self.team) {
      allies.push({id:p.semanticId,position:p.position})
      for(const report of p.botState?.memory??[]) if(snapshot.tick-report.tick>=Math.ceil(profile.rules.bots.cooperation.communicationSeconds*HZ)) reports.push(report)
      continue
    }
    const delta={x:p.position.x-eye.x,y:p.position.y+profile.rules.player.eyeHeight-eye.y,z:p.position.z-eye.z}
    const length=simulationMath.hypot(delta.x,delta.y,delta.z)
    const angle=Math.abs(wrap(simulationMath.atan2(delta.x,-delta.z)-self.yaw))
    if(length===0 || angle>tuning.fieldOfViewDegrees*Math.PI/360) continue
    const direction={x:delta.x/length,y:delta.y/length,z:delta.z/length}
    if(collision.raycast(eye,direction,length,exclude).some(h=>h.distance<length)) continue
    enemies.push({id:p.semanticId,lifeId:p.lifeId,position:p.position,tick:snapshot.tick})
  }
  return {tick:snapshot.tick+1,self,enemies,allies,reports}
}
export interface BotGoal { id: string; position: Vec; utility: number }
export type GoalEvaluator = (observation: BotObservation, state: BotState, tuning: BotTuning) => readonly BotGoal[]
export interface WeaponPolicy {
  readonly id: string
  aimPoint(target: Sighting, profile: ValidatedGameDesignProfile): Vec
  ready(self: MatchParticipantState, tick: number, aimError: number, tuning: BotTuning): boolean
}
export const SHOTGUN_POLICY: WeaponPolicy = {
  id:'double-barrel-shotgun',
  aimPoint:(target,profile)=>({...target.position,y:target.position.y+profile.rules.player.eyeHeight}),
  ready:(self,tick,error,tuning)=>tick>=self.weapon.nextAllowedShotTick && !self.weapon.previousFirePressed && Math.abs(error)<=tuning.fireToleranceDegrees*Math.PI/180,
}
export interface BotExtensions { goals?: readonly GoalEvaluator[]; weapons?: readonly WeaponPolicy[] }
const wrap=(a:number)=>simulationMath.atan2(simulationMath.sin(a),simulationMath.cos(a))
const clamp=(a:number,n=1)=>Math.max(-n,Math.min(n,a))
export function planBot(observation: BotObservation, previous: BotState, nav: NavigationCapabilities, profile: ValidatedGameDesignProfile, extensions: BotExtensions = {}): { state: BotState; actions: NormalizedAction[] } {
  const {self,tick}=observation;const tuning=profile.rules.bots[self.difficulty!];const common=profile.rules.bots
  const state:BotState={...previous,route:[...previous.route],memory:previous.memory.filter(m=>tick-m.tick<=tuning.memorySeconds*HZ)}
  const random=()=>{const r=nextRandomUint32(state.rng);state.rng=r.state;return r.value/0x100000000}
  const personality=1+(state.personality*2-1)*common.cooperation.personalitySpread
  const memory=new Map(state.memory.map(m=>[m.id,m]))
  for(const m of [...observation.reports,...observation.enemies]) if(tick-m.tick<=tuning.memorySeconds*HZ && m.tick>=(memory.get(m.id)?.tick??-1)) memory.set(m.id,m)
  state.memory=[...memory.values()].sort((a,b)=>a.id.localeCompare(b.id,'en'))
  const visible=[...observation.enemies].sort((a,b)=>distance(self.position,a.position)-distance(self.position,b.position)||a.id.localeCompare(b.id,'en'))
  const target=visible.find(m=>m.id===state.targetId)??visible[0]
  // Own health is observable; this scan does not reveal an unseen attacker's position.
  if (!target && self.health < state.lastHealth && tick >= state.alertTick) {
    state.alertYaw=self.yaw+Math.PI
    state.alertTick=tick+Math.ceil(tuning.memorySeconds*HZ)
  }
  state.lastHealth=self.health
  if(target && state.targetId!==target.id) {state.targetId=target.id;state.noticedTick=tick;state.retreatTick=0}
  if(!target && !state.memory.some(m=>m.id===state.targetId)) state.targetId=''
  if(tick>=state.decisionTick) {
    state.decisionTick=tick+Math.ceil(tuning.decisionSeconds*HZ)
    let goal:BotGoal|undefined
    if(target) goal={id:'attack',position:target.position,utility:1}
    else {
      const remembered=state.memory.find(m=>m.id===state.targetId)??state.memory[0]
      if(remembered) goal={id:'pursue',position:remembered.position,utility:1}
      else if(state.intent==='pursue' && distance(self.position,state.goal)>common.navigation.waypointRadius && state.route.length) goal={id:'pursue',position:state.goal,utility:1}
    }
    if(!target && tick>=state.supportTick && observation.allies.length && random()<tuning.supportChance) {
      const ally=observation.allies.find(a=>distance(self.position,a.position)>common.cooperation.separationMeters && distance(self.position,a.position)<common.cooperation.supportDistanceMeters)
      if(ally) {goal={id:'support',position:ally.position,utility:1};state.supportTick=tick+Math.ceil(common.cooperation.supportSeconds*HZ)}
    } else if(!goal && state.intent==='support' && tick<state.supportTick) goal={id:'support',position:state.goal,utility:1}
    for(const evaluate of extensions.goals??[]) for(const candidate of evaluate(observation,state,tuning)) if(!goal||candidate.utility>goal.utility) goal=candidate
    if(!goal) {
      if(state.intent!=='search'||distance(self.position,state.goal)<common.navigation.waypointRadius||!state.route.length) {
        const choices=nav.destinations.filter(p=>distance(self.position,p)>tuning.preferredDistanceMeters)
        state.goal=choices[Math.floor(random()*choices.length)]??nav.destinations[0]??self.position
      }
      goal={id:'search',position:state.goal,utility:0}
    }
    state.intent=goal.id;state.goal=goal.position
    state.route=nav.route(self.position,state.goal)
  }
  if(tick-state.progressTick>=common.navigation.stuckSeconds*HZ) {
    if(distance(self.position,state.progressPosition)<common.navigation.progressMeters) {
      state.recoveryTick=tick+Math.ceil(common.navigation.recoverySeconds*HZ)
      state.strafeSign=random()<0.5?-1:1
      state.route=[];state.intent='search';state.decisionTick=0
    }
    state.progressPosition=self.position;state.progressTick=tick
  }
  while(state.route.length && distance(self.position,state.route[0]!)<common.navigation.waypointRadius) state.route.shift()
  // Pull the route taut only over collision-clear segments.
  while(state.route.length>1 && nav.clear(self.position,state.route[1]!)) state.route.shift()
  const destination=state.route[0]??self.position
  let dx=destination.x-self.position.x;let dz=destination.z-self.position.z
  let len=simulationMath.hypot(dx,dz);if(len>0){dx/=len;dz/=len}
  let jump=false
  if(tick>=state.strafeTick) {
    state.strafeTick=tick+Math.ceil(tuning.strafeSeconds*personality*HZ)
    state.strafeSign=random()<0.5?-1:1
    if(target && self.grounded && tick>=state.jumpTick && random()<tuning.jumpChance) {
      // Ballistic landing estimate uses the same movement profile.
      const flight=2*profile.rules.player.movement.jumpSpeed/profile.rules.player.movement.gravity
      const landing={x:self.position.x+self.velocity.x*flight,y:self.position.y,z:self.position.z+self.velocity.z*flight}
      if(nav.clear(self.position,landing)) {jump=true;state.jumpTick=tick+Math.ceil(tuning.jumpCooldownSeconds*personality*HZ)}
    }
  }
  if(target && nav.clear(self.position,target.position)) {
    const d=distance(self.position,target.position);const x=(target.position.x-self.position.x)/(d||1);const z=(target.position.z-self.position.z)/(d||1)
    let approach=clamp((d-tuning.preferredDistanceMeters*personality)/tuning.preferredDistanceMeters)
    if(self.health/profile.rules.player.startingHealth<tuning.retreatHealthRatio && state.retreatTick===0) state.retreatTick=tick+Math.ceil(tuning.retreatSeconds*HZ)
    if(tick<state.retreatTick) {approach=-1;state.intent='retreat'}
    dx=x*approach-z*state.strafeSign*tuning.strafeWeight;dz=z*approach+x*state.strafeSign*tuning.strafeWeight
  }
  for(const ally of observation.allies) {
    const d=distance(self.position,ally.position)
    if(d>0 && d<common.cooperation.separationMeters) {const force=1-d/common.cooperation.separationMeters;dx+=(self.position.x-ally.position.x)/d*force;dz+=(self.position.z-ally.position.z)/d*force}
  }
  if(tick<state.recoveryTick) {dx=simulationMath.cos(self.yaw)*state.strafeSign-simulationMath.sin(self.yaw);dz=simulationMath.sin(self.yaw)*state.strafeSign+simulationMath.cos(self.yaw)}
  len=simulationMath.hypot(dx,dz);if(len>1){dx/=len;dz/=len}
  const probe=profile.rules.player.capsule.radius+common.navigation.clearance
  if(!nav.clear(self.position,{x:self.position.x+dx*probe,y:self.position.y,z:self.position.z+dz*probe})) {
    if(nav.clear(self.position,{x:self.position.x+dx*probe,y:self.position.y,z:self.position.z})) dz=0
    else if(nav.clear(self.position,{x:self.position.x,y:self.position.y,z:self.position.z+dz*probe})) dx=0
    else {dx=-dx;dz=-dz}
  }
  const policy=extensions.weapons?.find(w=>w.id===self.weapon.semanticId)??SHOTGUN_POLICY
  const aim=target?policy.aimPoint(target,profile):destination
  const aimYaw=!target && tick<state.alertTick?state.alertYaw:target||len>0?simulationMath.atan2(aim.x-self.position.x,-(aim.z-self.position.z)):self.yaw
  const error=tuning.aimErrorDegrees*Math.PI/180*simulationMath.sin(tick/HZ/tuning.aimPeriodSeconds*Math.PI*2+state.personality*Math.PI*2)
  const deltaYaw=wrap(aimYaw+(target?error:0)-self.yaw)
  const turn=tuning.aimDegreesPerSecond/HZ*Math.PI/180
  const yawStep=clamp(deltaYaw,Math.min(turn,profile.rules.camera.horizontalSensitivity*Math.PI/180))
  const pitch=target?simulationMath.atan2(aim.y-self.position.y-profile.rules.player.eyeHeight,distance(self.position,aim)):0
  const pitchStep=clamp(pitch+(target?error:0)-self.pitch,Math.min(turn,profile.rules.camera.verticalSensitivity*Math.PI/180))
  const nextYaw=self.yaw+yawStep
  const values={ 'move-forward':clamp(dx*simulationMath.sin(nextYaw)-dz*simulationMath.cos(nextYaw)), 'move-right':clamp(dx*simulationMath.cos(nextYaw)+dz*simulationMath.sin(nextYaw)), 'look-yaw':clamp(yawStep/(profile.rules.camera.horizontalSensitivity*Math.PI/180)), 'look-pitch':clamp(pitchStep/(profile.rules.camera.verticalSensitivity*Math.PI/180)), jump:jump?1:0, fire:0 }
  const allyInLine=target && observation.allies.some(a=>{const d=distance(self.position,a.position);return d<distance(self.position,target.position)&&Math.abs(wrap(simulationMath.atan2(a.position.x-self.position.x,-(a.position.z-self.position.z))-aimYaw))<simulationMath.atan2(profile.rules.player.capsule.radius,d)})
  if(target && !allyInLine && tick-state.noticedTick>=tuning.reactionSeconds*HZ && policy.ready(self,tick,wrap(aimYaw-nextYaw),tuning)) values.fire=1
  return {state,actions:Object.entries(values).map(([actionId,value])=>({participantId:self.semanticId,actionId:actionId as NormalizedAction['actionId'],value}))}
}
export function advanceBots(snapshot: SimulationSnapshot, collision: PlayableCollisionPort, profile: ValidatedGameDesignProfile, arena: ArenaDefinition): {states: Map<string,BotState>;actions:NormalizedAction[]} {
  const states=new Map<string,BotState>();const actions:NormalizedAction[]=[]
  const nav=arenaNavigation(arena,profile)
  for(const self of snapshot.participants) if(self.kind==='bot' && self.alive) {
    const planned=planBot(observeBot(snapshot,self,collision,profile),self.botState!,nav,profile)
    states.set(self.semanticId,planned.state);actions.push(...planned.actions)
  }
  return {states,actions}
}
