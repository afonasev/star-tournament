import { describe, expect, it } from 'vitest'

import { DIAGNOSTIC_ARENA } from '../arena'
import type { ContentHash } from '../core/canonical'
import { DEFAULT_MATCH_CONFIGURATION, createMatchConfiguration } from '../match'
import { PROTOTYPE_V1_PROFILE } from '../profiles'
import { COMBAT_SLICE_SCENARIO_V1 } from '../scenario'
import { createInitialSimulationSnapshot, type SimulationEvent, type SimulationSnapshot } from './playableSnapshot'
import { reduceMatchTick } from './matchReducer'

function nextSnapshot(source: SimulationSnapshot, reduction: ReturnType<typeof reduceMatchTick>): SimulationSnapshot {
  return {
    ...source,
    tick: source.tick + 1,
    participants: reduction.participants,
    corpses: reduction.corpses,
    match: reduction.match,
    events: reduction.events,
  } as SimulationSnapshot
}

function snapshot(configuration = DEFAULT_MATCH_CONFIGURATION): SimulationSnapshot {
  return createInitialSimulationSnapshot({ rngSeed: 7, arena: { id: DIAGNOSTIC_ARENA.id, seed: DIAGNOSTIC_ARENA.seed, generatorVersion: DIAGNOSTIC_ARENA.generatorVersion, contentHash: DIAGNOSTIC_ARENA.contentHash as ContentHash }, configuration })
}

function damage(tick: number, attackerId: string, targetId: string, amount: number, lethal = false): SimulationEvent[] {
  const targetLifeId = `${targetId}:life:1`
  return [
    { kind: 'target-damaged', eventId: `${tick}:damage:${attackerId}:${targetId}`, tick, attackerId, targetId, targetLifeId, damage: amount, healthAfter: Math.max(0, 100 - amount), zones: ['torso'] },
    ...(lethal ? [{ kind: 'target-destroyed' as const, eventId: `${tick}:death:${targetId}`, tick, killerId: attackerId, targetId, targetLifeId }] : []),
  ]
}

describe('deterministic match reducer', () => {
  it('keeps exact dealt/received symmetry and records one death per life', () => {
    const source = snapshot()
    const first = reduceMatchTick(source, damage(1, 'local-player-1', 'robot-center', 100, true), PROTOTYPE_V1_PROFILE, DEFAULT_MATCH_CONFIGURATION, COMBAT_SLICE_SCENARIO_V1)
    const local = first.participants.find(({ semanticId }) => semanticId === 'local-player-1')!
    const victim = first.participants.find(({ semanticId }) => semanticId === 'robot-center')!
    expect(local.statistics).toMatchObject({ kills: 1, damageDealt: 100, score: 100 })
    expect(victim.statistics).toMatchObject({ deaths: 1, damageReceived: 100 })
    expect(victim).toMatchObject({ alive: false, deathTick: 1, health: 0 })
    expect(first.corpses).toEqual([expect.objectContaining({ participantId: 'robot-center', lifeId: 'robot-center:life:1', createdTick: 1, expiresTick: 601 })])

    const deadLife = nextSnapshot(source, first)
    const ignored = reduceMatchTick(deadLife, damage(2, 'local-player-1', 'robot-center', 50, true), PROTOTYPE_V1_PROFILE, DEFAULT_MATCH_CONFIGURATION, COMBAT_SLICE_SCENARIO_V1)
    expect(ignored.participants.find(({ semanticId }) => semanticId === 'local-player-1')?.statistics).toMatchObject({ kills: 1, damageDealt: 100 })
    expect(ignored.participants.find(({ semanticId }) => semanticId === 'robot-center')?.statistics).toMatchObject({ deaths: 1, damageReceived: 100 })
  })

  it('respawns a stationary fixture on the exact killcam tick while its corpse expires independently', () => {
    const source = snapshot()
    const killed = reduceMatchTick(source, damage(1, 'local-player-1', 'robot-center', 100, true), PROTOTYPE_V1_PROFILE, DEFAULT_MATCH_CONFIGURATION, COMBAT_SLICE_SCENARIO_V1)
    const dead = nextSnapshot(source, killed)
    const beforeDue = { ...dead, tick: 180, match: { ...dead.match, elapsedTicks: 180, remainingTicks: dead.match.remainingTicks - 179 } } as SimulationSnapshot
    const respawned = reduceMatchTick(beforeDue, [], PROTOTYPE_V1_PROFILE, DEFAULT_MATCH_CONFIGURATION, COMBAT_SLICE_SCENARIO_V1)
    const fixture = respawned.participants.find(({ semanticId }) => semanticId === 'robot-center')!
    expect(fixture).toMatchObject({ alive: true, health: 100, lifeId: 'robot-center:life:2', lifeSequence: 2, previousJumpPressed: false })
    expect(fixture.weapon).toMatchObject({ ammo: 20, shotSequence: 0, previousFirePressed: false })
    expect(fixture.position).toEqual(COMBAT_SLICE_SCENARIO_V1.targets.find(({ semanticId }) => semanticId === 'robot-center')?.position)
    expect(respawned.events).toContainEqual(expect.objectContaining({ kind: 'participant-respawned', participantId: 'robot-center', lifeId: 'robot-center:life:2' }))
    expect(respawned.corpses).toHaveLength(1)

    const beforeExpiry = { ...beforeDue, tick: 600, participants: respawned.participants, corpses: respawned.corpses, match: { ...respawned.match, elapsedTicks: 600, remainingTicks: source.match.remainingTicks - 600 } } as SimulationSnapshot
    expect(reduceMatchTick(beforeExpiry, [], PROTOTYPE_V1_PROFILE, DEFAULT_MATCH_CONFIGURATION, COMBAT_SLICE_SCENARIO_V1).corpses).toEqual([])
  })

  it('awards one assist inside the window and excludes killer/victim', () => {
    const source = snapshot()
    const assistedParticipants = source.participants.map((participant) => participant.semanticId === 'robot-center'
      ? { ...participant, damageLedger: [{ attackerId: 'robot-left', lastDamageTick: 1 }], health: 30 }
      : participant)
    const prior = { ...source, tick: 1, match: { ...source.match, elapsedTicks: 1, remainingTicks: source.match.remainingTicks - 1 }, participants: assistedParticipants } as SimulationSnapshot
    const result = reduceMatchTick(prior, damage(2, 'local-player-1', 'robot-center', 30, true), PROTOTYPE_V1_PROFILE, DEFAULT_MATCH_CONFIGURATION, COMBAT_SLICE_SCENARIO_V1)
    expect(result.participants.find(({ semanticId }) => semanticId === 'robot-left')?.statistics).toMatchObject({ assists: 1, score: 50 })
  })

  it('deduplicates repeated assist hits, includes the exact window boundary, and excludes stale/self/killer ledger entries', () => {
    const source = snapshot()
    const lethalTick = 301
    const participants = source.participants.map((participant) => participant.semanticId === 'robot-center'
      ? {
          ...participant,
          health: 20,
          damageLedger: [
            { attackerId: 'robot-left', lastDamageTick: 1 },
            { attackerId: 'robot-right', lastDamageTick: 0 },
            { attackerId: 'robot-center', lastDamageTick: 301 },
            { attackerId: 'local-player-1', lastDamageTick: 301 },
          ],
        }
      : participant)
    const prior = { ...source, tick: 300, participants, match: { ...source.match, elapsedTicks: 300, remainingTicks: source.match.remainingTicks - 300 } } as SimulationSnapshot
    const repeated = [
      ...damage(lethalTick, 'robot-left', 'robot-center', 1),
      ...damage(lethalTick, 'robot-left', 'robot-center', 1).map((event) => ({ ...event, eventId: `${event.eventId}:repeat` })),
      ...damage(lethalTick, 'local-player-1', 'robot-center', 18, true),
    ]
    const result = reduceMatchTick(prior, repeated, PROTOTYPE_V1_PROFILE, DEFAULT_MATCH_CONFIGURATION, COMBAT_SLICE_SCENARIO_V1)
    expect(result.participants.find(({ semanticId }) => semanticId === 'robot-left')?.statistics).toMatchObject({ assists: 1, score: 50 })
    expect(result.participants.find(({ semanticId }) => semanticId === 'robot-right')?.statistics).toMatchObject({ assists: 0, score: 0 })
    expect(result.participants.find(({ semanticId }) => semanticId === 'robot-center')?.statistics.assists).toBe(0)
    expect(result.participants.find(({ semanticId }) => semanticId === 'local-player-1')?.statistics.assists).toBe(0)
    expect(result.events.map(({ eventId }) => eventId)).toEqual([...result.events.map(({ eventId }) => eventId)].sort((a, b) => a.localeCompare(b, 'en')))
  })

  it('uses cumulative kill-chain deltas, resets after the gap, and supports six kills', () => {
    let current = snapshot()
    const expected = [100, 300, 500, 800, 1_200, 1_600]
    for (let index = 0; index < expected.length; index += 1) {
      const targetId = ['robot-center', 'robot-left', 'robot-right'][index % 3]!
      const participant = current.participants.find(({ semanticId }) => semanticId === targetId)!
      const prepared = { ...current, participants: current.participants.map((entry) => entry.semanticId === targetId ? { ...entry, health: 100, alive: true, deathTick: null, respawnTick: null, lifeId: `${targetId}:life:${index + 1}`, lifeSequence: index + 1 } : entry) } as SimulationSnapshot
      const tick = prepared.tick + 1
      const events = damage(tick, 'local-player-1', targetId, 100, true).map((event) => 'targetLifeId' in event ? { ...event, targetLifeId: `${targetId}:life:${index + 1}` } : event)
      const reduced = reduceMatchTick(prepared, events, PROTOTYPE_V1_PROFILE, DEFAULT_MATCH_CONFIGURATION, COMBAT_SLICE_SCENARIO_V1)
      current = { ...prepared, tick, participants: reduced.participants, match: reduced.match, events: reduced.events } as SimulationSnapshot
      expect(current.participants.find(({ semanticId }) => semanticId === 'local-player-1')?.statistics.score).toBe(expected[index])
      expect(participant.semanticId).toBe(targetId)
    }
  })

  it('resets only the active kill chain after the configured gap and on death', () => {
    const source = snapshot()
    const local = source.participants.find(({ semanticId }) => semanticId === 'local-player-1')!
    const prepared = {
      ...source,
      tick: 601,
      participants: source.participants.map((participant) => participant.semanticId === local.semanticId
        ? { ...participant, statistics: { ...participant.statistics, score: 550 }, killChain: { length: 3, lastKillTick: 0, awardedInChain: 500 } }
        : participant),
      match: { ...source.match, elapsedTicks: 601, remainingTicks: source.match.remainingTicks - 601 },
    } as SimulationSnapshot
    const afterGap = reduceMatchTick(prepared, damage(602, 'local-player-1', 'robot-center', 100, true), PROTOTYPE_V1_PROFILE, DEFAULT_MATCH_CONFIGURATION, COMBAT_SLICE_SCENARIO_V1)
    expect(afterGap.participants.find(({ semanticId }) => semanticId === local.semanticId)).toMatchObject({ statistics: { score: 650 }, killChain: { length: 1, awardedInChain: 100 } })

    const killedLocal = reduceMatchTick({ ...prepared, participants: afterGap.participants } as SimulationSnapshot, damage(602, 'robot-left', 'local-player-1', 100, true), PROTOTYPE_V1_PROFILE, DEFAULT_MATCH_CONFIGURATION, COMBAT_SLICE_SCENARIO_V1)
    expect(killedLocal.participants.find(({ semanticId }) => semanticId === local.semanticId)?.killChain).toEqual({ length: 0, lastKillTick: null, awardedInChain: 0 })
    expect(killedLocal.participants.find(({ semanticId }) => semanticId === local.semanticId)?.statistics.score).toBe(650)
  })

  it('enters overtime on a tied time limit and finishes only with a strict leader', () => {
    const source = snapshot()
    const lastRegulation = { ...source, match: { ...source.match, remainingTicks: 1, elapsedTicks: source.match.remainingTicks - 1 } } as SimulationSnapshot
    const tied = reduceMatchTick(lastRegulation, [], PROTOTYPE_V1_PROFILE, DEFAULT_MATCH_CONFIGURATION, COMBAT_SLICE_SCENARIO_V1)
    expect(tied.match).toMatchObject({ phase: 'overtime', finishTrigger: 'time-limit', result: null })
    const overtime = { ...lastRegulation, tick: 1, participants: tied.participants, match: tied.match, events: [] } as SimulationSnapshot
    const won = reduceMatchTick(overtime, damage(2, 'local-player-1', 'robot-center', 100, true), PROTOTYPE_V1_PROFILE, DEFAULT_MATCH_CONFIGURATION, COMBAT_SLICE_SCENARIO_V1)
    expect(won.match).toMatchObject({ phase: 'finished', result: { winnerId: 'local-player-1', trigger: 'time-limit' } })
  })

  it('uses team sums and score-limit precedence on the final regulation tick', () => {
    const teams = createMatchConfiguration({
      schemaVersion: DEFAULT_MATCH_CONFIGURATION.schemaVersion,
      id: DEFAULT_MATCH_CONFIGURATION.id,
      mode: 'teams',
      durationMinutes: DEFAULT_MATCH_CONFIGURATION.durationMinutes,
      scoreTargetPoints: 1_000,
      participants: DEFAULT_MATCH_CONFIGURATION.participants.map((participant, index) => ({
        ...participant,
        team: index % 2 === 0 ? 'red' : 'blue',
        color: index % 2 === 0 ? '#ef4444' : '#3b82f6',
      })),
    })
    const source = snapshot(teams)
    const participants = source.participants.map((participant) => participant.semanticId === 'local-player-1' ? { ...participant, statistics: { ...participant.statistics, score: 900 } } : participant)
    const prepared = { ...source, participants, match: { ...source.match, remainingTicks: 1 } } as SimulationSnapshot
    const result = reduceMatchTick(prepared, damage(1, 'local-player-1', 'robot-center', 100, true), PROTOTYPE_V1_PROFILE, teams, COMBAT_SLICE_SCENARIO_V1)
    expect(result.match).toMatchObject({ phase: 'finished', finishTrigger: 'score-limit', result: { winnerType: 'team', winnerId: 'red' } })
  })

  it('applies all score events before turning a tied score-limit tick into overtime', () => {
    const target = createMatchConfiguration({
      schemaVersion: DEFAULT_MATCH_CONFIGURATION.schemaVersion,
      id: DEFAULT_MATCH_CONFIGURATION.id,
      mode: 'ffa',
      durationMinutes: DEFAULT_MATCH_CONFIGURATION.durationMinutes,
      scoreTargetPoints: 1_000,
      participants: DEFAULT_MATCH_CONFIGURATION.participants,
    })
    const source = snapshot(target)
    const participants = source.participants.map((participant) => participant.semanticId === 'local-player-1' || participant.semanticId === 'robot-left'
      ? { ...participant, statistics: { ...participant.statistics, score: 900 } }
      : participant)
    const prepared = { ...source, participants } as SimulationSnapshot
    const events = [
      ...damage(1, 'local-player-1', 'robot-center', 100, true),
      ...damage(1, 'robot-left', 'robot-right', 100, true),
    ]
    const result = reduceMatchTick(prepared, events, PROTOTYPE_V1_PROFILE, target, COMBAT_SLICE_SCENARIO_V1)
    expect(result.match).toMatchObject({ phase: 'overtime', finishTrigger: 'score-limit', result: null })
    expect(result.participants.find(({ semanticId }) => semanticId === 'local-player-1')?.statistics.score).toBe(1_000)
    expect(result.participants.find(({ semanticId }) => semanticId === 'robot-left')?.statistics.score).toBe(1_000)
  })
})
