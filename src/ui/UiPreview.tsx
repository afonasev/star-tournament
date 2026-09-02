import { DIAGNOSTIC_ARENA } from '../arena'
import type { ContentHash } from '../core/canonical'
import { DEFAULT_MATCH_CONFIGURATION, TEAM_COLORS, createMatchConfiguration } from '../match'
import { PROTOTYPE_V1_PROFILE } from '../profiles'
import { createInitialSimulationSnapshot, type SimulationSnapshot } from '../simulation/playableSnapshot'
import { GameplayHud } from './GameplayHud'
import { GameplayUiProjector } from './gameplayUiSnapshot'
import { MatchSetupMenu } from './MatchSetupMenu'

export type UiPreviewKind = 'scoreboard-ffa' | 'overtime' | 'results-ffa' | 'results-teams' | 'generation-error'

function configuration(kind: UiPreviewKind) {
  if (kind !== 'results-teams') return DEFAULT_MATCH_CONFIGURATION
  return createMatchConfiguration({
    schemaVersion: DEFAULT_MATCH_CONFIGURATION.schemaVersion,
    id: DEFAULT_MATCH_CONFIGURATION.id,
    mode: 'teams',
    arenaSize: DEFAULT_MATCH_CONFIGURATION.arenaSize,
    arenaSeed: DEFAULT_MATCH_CONFIGURATION.arenaSeed,
    durationMinutes: 5,
    scoreTargetPoints: 1_000,
    participants: DEFAULT_MATCH_CONFIGURATION.participants.map((participant, index) => ({ ...participant, team: index % 2 === 0 ? 'team-a' : 'team-b', color: index % 2 === 0 ? TEAM_COLORS['team-a'] : TEAM_COLORS['team-b'] })),
  })
}

export function UiPreview({ kind }: { readonly kind: UiPreviewKind }) {
  if (kind === 'generation-error') return <MatchSetupMenu profile={PROTOTYPE_V1_PROFILE} onStart={() => undefined} onUseFallback={() => undefined} generationError={{ size: 'medium', seed: 1398030674, reason: 'MODE_FAIRNESS_FAILED · route redundancy below profile limit' }} />
  const matchConfiguration = configuration(kind)
  const initial = createInitialSimulationSnapshot({ rngSeed: 1, configuration: matchConfiguration, arena: { id: DIAGNOSTIC_ARENA.id, seed: DIAGNOSTIC_ARENA.seed, generatorVersion: DIAGNOSTIC_ARENA.generatorVersion, contentHash: DIAGNOSTIC_ARENA.contentHash as ContentHash } })
  const participants = initial.participants.map((participant, index) => ({ ...participant, statistics: { ...participant.statistics, kills: index === 0 ? 8 : 4 - index, assists: index, deaths: index + 1, damageDealt: 1240.4 - index * 130.15, damageReceived: 320.2 + index * 90.35, score: index === 0 ? 1_600 : kind === 'overtime' && index === 1 ? 1_600 : 800 - index * 100 } }))
  const finished = kind === 'results-ffa' || kind === 'results-teams'
  const overtime = kind === 'overtime'
  const winnerId = kind === 'results-teams' ? 'team-a' : 'local-player-1'
  const result = finished ? { winnerType: kind === 'results-teams' ? 'team' as const : 'participant' as const, winnerId, trigger: 'score-limit' as const, finalTick: 720, finalStandings: participants.map(({ semanticId }) => semanticId) } : null
  const source = { ...initial, tick: 720, participants, match: { ...initial.match, phase: finished ? 'finished' as const : overtime ? 'overtime' as const : 'running' as const, finishTrigger: finished || overtime ? 'score-limit' as const : null, elapsedTicks: 720, remainingTicks: finished || overtime ? 0 : initial.match.remainingTicks - 720, result } } as SimulationSnapshot
  const snapshot = new GameplayUiProjector({ profile: PROTOTYPE_V1_PROFILE }).project(source, finished ? 'results' : 'running', undefined, undefined, kind === 'scoreboard-ffa')
  return <div className="ui-preview"><GameplayHud snapshot={snapshot} /></div>
}
