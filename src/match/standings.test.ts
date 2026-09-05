import { describe, expect, it } from 'vitest'

import { DIAGNOSTIC_ARENA } from '../arena'
import type { ContentHash } from '../core/canonical'
import { createInitialSimulationSnapshot } from '../simulation/playableSnapshot'
import { DEFAULT_MATCH_CONFIGURATION, TEAM_COLORS, createMatchConfiguration } from './matchConfiguration'
import { projectStandings } from './standings'

function participants() {
  const snapshot = createInitialSimulationSnapshot({
    rngSeed: 1,
    arena: { id: DIAGNOSTIC_ARENA.id, seed: DIAGNOSTIC_ARENA.seed, generatorVersion: DIAGNOSTIC_ARENA.generatorVersion, contentHash: DIAGNOSTIC_ARENA.contentHash as ContentHash },
  })
  return snapshot.participants.map((participant, index) => ({
    ...participant,
    statistics: {
      kills: 9 - index,
      assists: index,
      deaths: index + 1,
      damageDealt: 10.25 + index,
      damageReceived: 20.5 + index,
      score: participant.semanticId === 'robot-right' ? 700 : 500,
    },
  }))
}

describe('standings projector', () => {
  it('formats each difficulty without changing base names or adding duplicate suffixes on Repeat', () => {
    const source=participants().slice(1).map((p,i)=>({...p,kind:'bot' as const,difficulty:(['easy','normal','hard'] as const)[i]!,name:'Очень длинное имя пилота'}))
    const first=projectStandings('ffa',source)
    expect(projectStandings('ffa',source)).toEqual(first)
    if(first.mode!=='ffa')throw new Error('expected ffa')
    expect(first.rows.map(p=>p.name).sort()).toEqual(['Очень длинное имя пилота · Салага','Очень длинное имя пилота · Боец','Очень длинное имя пилота · Ветеран'].sort())
    expect(source.every(p=>p.name==='Очень длинное имя пилота')).toBe(true)
  })
  it('copies every authoritative column and uses semantic id as the FFA tie-break without mutation', () => {
    const source = participants()
    const before = structuredClone(source)
    const projection = projectStandings('ffa', source)
    expect(projection.mode).toBe('ffa')
    if (projection.mode !== 'ffa') return
    expect(projection.rows.map(({ semanticId }) => semanticId)).toEqual(['robot-right', 'local-player-1', 'robot-center', 'robot-left'])
    expect(projection.rows[0]).toMatchObject({ kind: 'stationary-fixture', kills: 6, assists: 3, deaths: 4, damageDealt: 13.25, damageReceived: 23.5, score: 700 })
    expect(projection.rows.find(({ semanticId }) => semanticId === 'local-player-1')).toMatchObject({ kind: 'local-seat' })
    expect(source).toEqual(before)
    expect(Object.isFrozen(projection.rows)).toBe(true)
  })

  it('groups teams in stable red/blue order and only sums authoritative participant scores', () => {
    const configuration = createMatchConfiguration({
      schemaVersion: DEFAULT_MATCH_CONFIGURATION.schemaVersion,
      id: DEFAULT_MATCH_CONFIGURATION.id,
      mode: 'teams',
      arenaSize: DEFAULT_MATCH_CONFIGURATION.arenaSize,
      arenaSeed: DEFAULT_MATCH_CONFIGURATION.arenaSeed,
      durationMinutes: 5,
      scoreTargetPoints: null,
      participants: DEFAULT_MATCH_CONFIGURATION.participants.map((participant, index) => ({
        ...participant,
        team: index % 2 === 0 ? 'team-a' : 'team-b',
        color: index % 2 === 0 ? TEAM_COLORS['team-a'] : TEAM_COLORS['team-b'],
      })),
    })
    const byId = new Map(configuration.participants.map((participant) => [participant.semanticId, participant]))
    const source = participants().map((participant) => ({ ...participant, ...byId.get(participant.semanticId) }))
    const projection = projectStandings('teams', source)
    expect(projection.mode).toBe('teams')
    if (projection.mode !== 'teams') return
    expect(projection.teams.map(({ team }) => team)).toEqual(['team-a', 'team-b'])
    expect(projection.teams.map(({ totalScore }) => totalScore)).toEqual([1_000, 1_200])
    expect(projection.teams[0]?.participants.map(({ semanticId }) => semanticId)).toEqual(['local-player-1', 'robot-left'])
  })
})
