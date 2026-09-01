import { deepFreeze } from '../core/deepFreeze'
import { allocateArenaSpawns, type ArenaDefinition } from '../arena'
import type { MatchConfiguration, MatchTeam } from '../match'
import type { ValidatedGameDesignProfile } from '../profiles'
import type { CombatSliceScenario } from '../scenario'
import type {
  MatchFinalResult,
  MatchFinishTrigger,
  MatchParticipantState,
  ParticipantCorpseState,
  SimulationEvent,
  SimulationMatchState,
  SimulationSnapshot,
  TargetDamagedEvent,
  TargetDestroyedEvent,
} from './playableSnapshot'

export interface MatchTickReduction {
  readonly participants: readonly MatchParticipantState[]
  readonly corpses: readonly ParticipantCorpseState[]
  readonly match: SimulationMatchState
  readonly events: readonly SimulationEvent[]
}

function compareId(first: MatchParticipantState, second: MatchParticipantState): number {
  return first.semanticId.localeCompare(second.semanticId, 'en')
}

function updateParticipant(
  participants: MatchParticipantState[],
  semanticId: string,
  update: (participant: MatchParticipantState) => MatchParticipantState,
): void {
  const index = participants.findIndex((participant) => participant.semanticId === semanticId)
  if (index < 0) throw new TypeError(`Unknown match participant ${semanticId}`)
  participants[index] = update(participants[index]!)
}

function killAward(
  participant: MatchParticipantState,
  tick: number,
  profile: ValidatedGameDesignProfile,
): Pick<MatchParticipantState, 'statistics' | 'killChain'> {
  const maximumGapTicks = Math.ceil(profile.rules.scoring.killChain.maximumGapSeconds * 60)
  const continues = participant.killChain.lastKillTick !== null && tick - participant.killChain.lastKillTick <= maximumGapTicks
  const length = continues ? participant.killChain.length + 1 : 1
  const totals = profile.rules.scoring.killChain.cumulativeTotals
  const awardedInChain = length <= totals.length
    ? totals[length - 1]!
    : totals[totals.length - 1]! + (length - totals.length) * profile.rules.scoring.killChain.postFiveIncrementPoints
  const scoreDelta = continues ? awardedInChain - participant.killChain.awardedInChain : awardedInChain
  return {
    statistics: { ...participant.statistics, kills: participant.statistics.kills + 1, score: participant.statistics.score + scoreDelta },
    killChain: { length, lastKillTick: tick, awardedInChain },
  }
}

function applyDamage(
  participants: MatchParticipantState[],
  event: TargetDamagedEvent,
): void {
  const victim = participants.find(({ semanticId }) => semanticId === event.targetId)
  if (victim === undefined || !victim.alive || victim.lifeId !== event.targetLifeId) return
  const actualDamage = Math.min(event.damage, victim.health)
  if (actualDamage <= 0) return
  updateParticipant(participants, event.attackerId, (attacker) => ({
    ...attacker,
    statistics: { ...attacker.statistics, damageDealt: attacker.statistics.damageDealt + actualDamage },
  }))
  updateParticipant(participants, event.targetId, (current) => {
    const ledger = current.damageLedger.filter(({ attackerId }) => attackerId !== event.attackerId)
    ledger.push({ attackerId: event.attackerId, lastDamageTick: event.tick })
    ledger.sort((first, second) => first.attackerId.localeCompare(second.attackerId, 'en'))
    const health = Math.max(0, current.health - actualDamage)
    return {
      ...current,
      health,
      alive: health > 0,
      statistics: { ...current.statistics, damageReceived: current.statistics.damageReceived + actualDamage },
      damageLedger: ledger,
    }
  })
}

function applyDeath(
  participants: MatchParticipantState[],
  corpses: ParticipantCorpseState[],
  event: TargetDestroyedEvent,
  profile: ValidatedGameDesignProfile,
): void {
  const victim = participants.find(({ semanticId }) => semanticId === event.targetId)
  if (victim === undefined || victim.lifeId !== event.targetLifeId || victim.health > 0 || victim.deathTick !== null) return
  const assistWindowTicks = Math.ceil(profile.rules.scoring.assist.windowSeconds * 60)
  for (const entry of victim.damageLedger) {
    if (entry.attackerId === event.killerId || entry.attackerId === event.targetId || event.tick - entry.lastDamageTick > assistWindowTicks) continue
    updateParticipant(participants, entry.attackerId, (assistant) => ({
      ...assistant,
      statistics: {
        ...assistant.statistics,
        assists: assistant.statistics.assists + 1,
        score: assistant.statistics.score + profile.rules.scoring.assist.points,
      },
    }))
  }
  updateParticipant(participants, event.killerId, (killer) => ({ ...killer, ...killAward(killer, event.tick, profile) }))
  corpses.push({
    corpseId: `${event.targetLifeId}:corpse`,
    participantId: victim.semanticId,
    lifeId: victim.lifeId,
    position: victim.position,
    yaw: victim.yaw,
    createdTick: event.tick,
    expiresTick: event.tick + Math.ceil(profile.rules.death.corpseSeconds * 60),
  })
  updateParticipant(participants, event.targetId, (current) => ({
    ...current,
    alive: false,
    health: 0,
    deathTick: event.tick,
    respawnTick: event.tick + Math.ceil(profile.rules.death.killcamSeconds * 60),
    corpseExpiresTick: event.tick + Math.ceil(profile.rules.death.corpseSeconds * 60),
    statistics: { ...current.statistics, deaths: current.statistics.deaths + 1 },
    killChain: { length: 0, lastKillTick: null, awardedInChain: 0 },
    damageLedger: [],
  }))
}

function respawnDueParticipants(
  participants: MatchParticipantState[],
  tick: number,
  profile: ValidatedGameDesignProfile,
  scenario: CombatSliceScenario,
  events: SimulationEvent[],
  arena?: ArenaDefinition,
): void {
  const fixtureById = new Map(scenario.targets.map((fixture) => [fixture.semanticId, fixture]))
  const due = [...participants].filter((participant) => participant.kind === 'stationary-fixture' && !participant.alive && participant.respawnTick !== null && participant.respawnTick <= tick).sort(compareId)
  const allocations = arena === undefined ? undefined : new Map(allocateArenaSpawns({
    arena,
    mode: participants.some(({ team }) => team !== null) ? 'teams' : 'ffa',
    participants: due,
    occupied: participants.filter(({ alive }) => alive).map(({ semanticId, position, team }) => ({ semanticId, position, team })),
    capsuleDiameterMeters: (profile.rules.player.capsule.radius + profile.rules.player.capsule.skinWidth) * 2,
    minimumOpponentSeparationMeters: profile.rules.arenaGenerator.presets[arena.size].minimumSpawnSeparationMeters,
  }).map((allocation) => [allocation.participantId, allocation]))
  for (const participant of due) {
    if (participant.kind !== 'stationary-fixture' || participant.alive || participant.respawnTick === null || participant.respawnTick > tick) continue
    const fixture = fixtureById.get(participant.semanticId)
    if (fixture === undefined) throw new TypeError(`Missing fixture anchor ${participant.semanticId}`)
    const allocation = allocations?.get(participant.semanticId)
    const lifeSequence = participant.lifeSequence + 1
    const lifeId = `${participant.semanticId}:life:${lifeSequence}`
    updateParticipant(participants, participant.semanticId, (current) => ({
      ...current,
      lifeSequence,
      lifeId,
      position: allocation?.position ?? fixture.position,
      velocity: { x: 0, y: 0, z: 0 },
      grounded: true,
      yaw: allocation?.yawRadians ?? fixture.yawRadians,
      pitch: 0,
      health: profile.rules.player.startingHealth,
      alive: true,
      deathTick: null,
      respawnTick: null,
      corpseExpiresTick: null,
      previousJumpPressed: false,
      weapon: {
        ...current.weapon,
        ammo: profile.rules.weapons.doubleBarrelShotgun.startingAmmo,
        shotSequence: 0,
        nextAllowedShotTick: 0,
        previousFirePressed: false,
      },
      damageLedger: [],
    }))
    events.push({ kind: 'participant-respawned', eventId: `tick-${tick}:respawn:${participant.semanticId}:${lifeSequence}`, tick, participantId: participant.semanticId, lifeId })
  }
}

interface Contender { readonly id: string; readonly score: number }

function contenders(configuration: MatchConfiguration, participants: readonly MatchParticipantState[]): readonly Contender[] {
  if (configuration.mode === 'ffa') return participants.map(({ semanticId, statistics }) => ({ id: semanticId, score: statistics.score }))
  const totals: Record<MatchTeam, number> = { red: 0, blue: 0 }
  for (const participant of participants) if (participant.team !== null) totals[participant.team] += participant.statistics.score
  return (['red', 'blue'] as const).map((id) => ({ id, score: totals[id] }))
}

function rankedParticipants(participants: readonly MatchParticipantState[]): readonly string[] {
  return [...participants].sort((first, second) => second.statistics.score - first.statistics.score || compareId(first, second)).map(({ semanticId }) => semanticId)
}

function evaluateMatch(
  snapshot: SimulationSnapshot,
  participants: readonly MatchParticipantState[],
  configuration: MatchConfiguration,
  tick: number,
): SimulationMatchState {
  if (snapshot.match.phase === 'finished') return snapshot.match
  const elapsedTicks = snapshot.match.elapsedTicks + 1
  const remainingTicks = snapshot.match.phase === 'running' ? Math.max(0, snapshot.match.remainingTicks - 1) : 0
  const values = contenders(configuration, participants)
  const maximum = Math.max(...values.map(({ score }) => score))
  const leaders = values.filter(({ score }) => score === maximum)
  const scoreReached = configuration.scoreTargetPoints !== null && maximum >= configuration.scoreTargetPoints
  const timeReached = snapshot.match.phase === 'running' && remainingTicks === 0
  const trigger: MatchFinishTrigger | null = snapshot.match.finishTrigger ?? (scoreReached ? 'score-limit' : timeReached ? 'time-limit' : null)
  const resolutionDue = trigger !== null || snapshot.match.phase === 'overtime'
  if (resolutionDue && leaders.length === 1) {
    const result: MatchFinalResult = {
      winnerType: configuration.mode === 'ffa' ? 'participant' : 'team',
      winnerId: leaders[0]!.id,
      trigger: trigger ?? snapshot.match.finishTrigger ?? 'time-limit',
      finalTick: tick,
      finalStandings: rankedParticipants(participants),
    }
    return { ...snapshot.match, phase: 'finished', finishTrigger: result.trigger, elapsedTicks, remainingTicks, result }
  }
  if (resolutionDue) return { ...snapshot.match, phase: 'overtime', finishTrigger: trigger, elapsedTicks, remainingTicks: 0, result: null }
  return { ...snapshot.match, elapsedTicks, remainingTicks }
}

export function reduceMatchTick(
  snapshot: SimulationSnapshot,
  eventsInput: readonly SimulationEvent[],
  profile: ValidatedGameDesignProfile,
  configuration: MatchConfiguration,
  scenario: CombatSliceScenario,
  arena?: ArenaDefinition,
): MatchTickReduction {
  if (snapshot.match.phase === 'finished') return deepFreeze({ participants: snapshot.participants, corpses: snapshot.corpses, match: snapshot.match, events: snapshot.events })
  const tick = snapshot.tick + 1
  const participants = snapshot.participants.map((participant) => ({
    ...participant,
    weapon: { ...participant.weapon },
    statistics: { ...participant.statistics },
    killChain: { ...participant.killChain },
    damageLedger: participant.damageLedger.map((entry) => ({ ...entry })),
  }))
  const corpses = snapshot.corpses.filter(({ expiresTick }) => expiresTick > tick).map((corpse) => ({ ...corpse }))
  const events = [...eventsInput].sort((first, second) => first.eventId.localeCompare(second.eventId, 'en'))
  for (const event of events) if (event.kind === 'target-damaged') applyDamage(participants, event)
  for (const event of events) if (event.kind === 'target-destroyed') applyDeath(participants, corpses, event, profile)
  respawnDueParticipants(participants, tick, profile, scenario, events, arena)
  participants.sort(compareId)
  corpses.sort((first, second) => first.corpseId.localeCompare(second.corpseId, 'en'))
  return deepFreeze({ participants, corpses, match: evaluateMatch(snapshot, participants, configuration, tick), events: events.sort((first, second) => first.eventId.localeCompare(second.eventId, 'en')) })
}
