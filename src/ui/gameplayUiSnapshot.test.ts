import { describe, expect, it } from 'vitest'

import { DIAGNOSTIC_ARENA } from '../arena'
import type { ContentHash } from '../core/canonical'
import { PROTOTYPE_V1_PROFILE } from '../profiles/gameDesignProfile'
import { createInitialSimulationSnapshot, type SimulationSnapshot } from '../simulation/playableSnapshot'
import {
  GameplayUiProjector,
  createGameplayUiSnapshot,
  type GameplayUiSourceSnapshot,
} from './gameplayUiSnapshot'

function initialSource(): SimulationSnapshot {
  return createInitialSimulationSnapshot({
    rngSeed: 1,
    arena: { id: DIAGNOSTIC_ARENA.id, seed: DIAGNOSTIC_ARENA.seed, generatorVersion: DIAGNOSTIC_ARENA.generatorVersion, contentHash: DIAGNOSTIC_ARENA.contentHash as ContentHash },
  })
}

function baseUi() {
  return {
    status: 'running' as const,
    health: 100,
    maximumHealth: 100,
    weaponName: 'Двухстволка',
    ammo: 20,
    crosshairFeedback: 'idle' as const,
    matchPhase: 'running' as const,
    remainingTicks: 18_000,
    timerText: '05:00',
    scoreboardVisible: false,
    standings: { mode: 'ffa' as const, rows: [] },
    result: null,
  }
}

describe('createGameplayUiSnapshot', () => {
  it('returns detached deeply immutable presentation data', () => {
    const input = {
      ...baseUi(),
      debug: {
        tick: 1,
        stateHash: 'state',
        profileIdentity: 'profile',
        arenaIdentity: 'arena',
        collisionIdentity: 'collision',
        scenarioIdentity: 'scenario',
        configurationIdentity: 'configuration',
        matchPhase: 'running',
        remainingTicks: 17_999,
        playerPosition: { x: 1, y: 2, z: 3 },
        playerYawDegrees: 45,
        playerPitchDegrees: -10,
        playerGrounded: true,
        ammo: 20,
        targets: [{ semanticId: 'target-a', health: 100, active: true }],
        participantStatistics: [{ semanticId: 'local-player-1', kills: 0, assists: 0, deaths: 0, score: 0 }],
      },
    } as const

    const result = createGameplayUiSnapshot(input)

    expect(result).toEqual(input)
    expect(result).not.toBe(input)
    expect(Object.isFrozen(result)).toBe(true)
    expect(Object.isFrozen(result.debug)).toBe(true)
    expect(Object.isFrozen(result.debug?.playerPosition)).toBe(true)
    expect(Object.isFrozen(result.debug?.targets)).toBe(true)
    expect(Object.isFrozen(result.debug?.targets[0])).toBe(true)
    expect(Reflect.set(result, 'ammo', 0)).toBe(false)
  })

  it('rejects impossible HUD values before rendering', () => {
    const valid = baseUi()

    expect(() => createGameplayUiSnapshot({ ...valid, health: 101 })).toThrow(/maximumHealth/)
    expect(() => createGameplayUiSnapshot({ ...valid, ammo: -1 })).toThrow(/ammo/)
    expect(() => createGameplayUiSnapshot({ ...valid, weaponName: '' })).toThrow(/weaponName/)
    expect(() => createGameplayUiSnapshot({ ...valid, status: 'error' })).toThrow(/errorMessage/)
  })

  it('rejects invalid debug acceptance telemetry', () => {
    const valid = {
      ...baseUi(),
      debug: {
        tick: 1,
        stateHash: 'state',
        profileIdentity: 'profile',
        arenaIdentity: 'arena',
        collisionIdentity: 'collision',
        scenarioIdentity: 'scenario',
        configurationIdentity: 'configuration',
        matchPhase: 'running',
        remainingTicks: 17_999,
        playerPosition: { x: 0, y: 0, z: 0 },
        playerYawDegrees: 0,
        playerPitchDegrees: 0,
        playerGrounded: true,
        ammo: 20,
        targets: [{ semanticId: 'target-a', health: 100, active: true }],
        participantStatistics: [{ semanticId: 'local-player-1', kills: 0, assists: 0, deaths: 0, score: 0 }],
      },
    } as const

    expect(() => createGameplayUiSnapshot({
      ...valid,
      debug: { ...valid.debug, playerYawDegrees: Number.NaN },
    })).toThrow(/playerYawDegrees/)
    expect(() => createGameplayUiSnapshot({
      ...valid,
      debug: { ...valid.debug, targets: [{ semanticId: '', health: 100, active: true }] },
    })).toThrow(/semanticId/)
  })

  it('projects playable state and retains profile-timed hit and dry-fire feedback', () => {
    const projector = new GameplayUiProjector({ profile: PROTOTYPE_V1_PROFILE })
    const source = (
      tick: number,
      kind?: 'target-damaged' | 'dry-fire',
    ): GameplayUiSourceSnapshot => {
      const initial = initialSource()
      return {
        ...initial,
        tick,
        participants: initial.participants.map((participant) => participant.kind === 'local-seat'
          ? { ...participant, health: 70, weapon: { ...participant.weapon, ammo: 12 } }
          : participant),
        match: { ...initial.match, elapsedTicks: tick, remainingTicks: initial.match.remainingTicks - tick },
        events: kind === undefined ? [] : kind === 'dry-fire'
          ? [{ eventId: `event-${tick}`, tick, kind, shooterId: 'local-player-1' }]
          : [{ eventId: `event-${tick}`, tick, kind, attackerId: 'local-player-1', targetId: 'robot-center', targetLifeId: 'robot-center:life:1', damage: 10, healthAfter: 90, zones: ['torso'] }],
      }
    }

    const hit = projector.project(source(5, 'target-damaged'), 'running')
    expect(hit).toMatchObject({
      status: 'running',
      health: 70,
      maximumHealth: 100,
      ammo: 12,
      crosshairFeedback: 'hit',
    })
    expect(projector.project(source(14), 'running').crosshairFeedback).toBe('hit')
    expect(projector.project(source(15), 'running').crosshairFeedback).toBe('idle')

    expect(projector.project(source(20, 'dry-fire'), 'running').crosshairFeedback).toBe('dry-fire')
    expect(projector.project(source(1), 'start').crosshairFeedback).toBe('idle')
  })

  it('projects simulation timer, pause, overtime, scoreboard and finished result transitions', () => {
    const projector = new GameplayUiProjector({ profile: PROTOTYPE_V1_PROFILE })
    const running = initialSource()
    expect(projector.project({ ...running, match: { ...running.match, remainingTicks: 61 } }, 'running', undefined, undefined, true)).toMatchObject({
      status: 'running', matchPhase: 'running', timerText: '00:02', scoreboardVisible: true,
    })
    expect(projector.project(running, 'paused')).toMatchObject({ status: 'paused', timerText: '05:00' })
    const overtime = { ...running, match: { ...running.match, phase: 'overtime' as const, finishTrigger: 'time-limit' as const, remainingTicks: 0 } }
    expect(projector.project(overtime, 'running')).toMatchObject({ matchPhase: 'overtime', timerText: 'OVERTIME', result: null })
    const result = { winnerType: 'participant' as const, winnerId: 'local-player-1', trigger: 'time-limit' as const, finalTick: 10, finalStandings: ['local-player-1', 'robot-center', 'robot-left', 'robot-right'] }
    const finished = { ...running, tick: 10, match: { ...running.match, phase: 'finished' as const, finishTrigger: 'time-limit' as const, elapsedTicks: 10, result } }
    expect(projector.project(finished, 'running', undefined, undefined, true)).toMatchObject({ status: 'results', matchPhase: 'finished', scoreboardVisible: false, result })
  })
})
