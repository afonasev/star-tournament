import { BOT_LABELS } from '../profiles/botRules'
import { deepFreeze } from '../core/deepFreeze'
import type { MatchParticipantState } from '../simulation/playableSnapshot'
import { TEAM_COLORS, type MatchMode, type MatchParticipantKind, type MatchTeam } from './matchConfiguration'

export interface StandingsParticipantRow {
  readonly rank: number
  readonly semanticId: string
  readonly name: string
  readonly kind: MatchParticipantKind
  readonly color: string
  readonly team: MatchTeam | null
  readonly kills: number
  readonly assists: number
  readonly deaths: number
  readonly damageDealt: number
  readonly damageReceived: number
  readonly score: number
}

export interface TeamStandingsGroup {
  readonly team: MatchTeam
  readonly color: string
  readonly totalScore: number
  readonly participants: readonly StandingsParticipantRow[]
}

export type StandingsProjection =
  | { readonly mode: 'ffa'; readonly rows: readonly StandingsParticipantRow[] }
  | { readonly mode: 'teams'; readonly teams: readonly TeamStandingsGroup[] }

function sortedRows(participants: readonly MatchParticipantState[]): readonly StandingsParticipantRow[] {
  return [...participants]
    .sort((first, second) => second.statistics.score - first.statistics.score || first.semanticId.localeCompare(second.semanticId, 'en'))
    .map((participant, index) => ({
      rank: index + 1,
      semanticId: participant.semanticId,
      name: participant.kind === 'bot' ? `${participant.name} · ${BOT_LABELS[participant.difficulty!]}` : participant.name,
      kind: participant.kind,
      color: participant.color,
      team: participant.team,
      kills: participant.statistics.kills,
      assists: participant.statistics.assists,
      deaths: participant.statistics.deaths,
      damageDealt: participant.statistics.damageDealt,
      damageReceived: participant.statistics.damageReceived,
      score: participant.statistics.score,
    }))
}

export function projectStandings(mode: MatchMode, participants: readonly MatchParticipantState[]): Readonly<StandingsProjection> {
  const rows = sortedRows(participants)
  if (mode === 'ffa') return deepFreeze({ mode, rows })
  const teams = (['team-a', 'team-b'] as const).map((team): TeamStandingsGroup => {
    const teamRows = rows.filter((row) => row.team === team).map((row, index) => ({ ...row, rank: index + 1 }))
    return {
      team,
      color: TEAM_COLORS[team],
      totalScore: teamRows.reduce((total, row) => total + row.score, 0),
      participants: teamRows,
    }
  })
  return deepFreeze({ mode, teams })
}
