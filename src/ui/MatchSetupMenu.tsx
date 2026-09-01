import { useMemo, useState } from 'react'

import {
  DEFAULT_MATCH_CONFIGURATION,
  TEAM_COLORS,
  createMatchConfiguration,
  type MatchConfiguration,
  type MatchMode,
} from '../match'
import type { ValidatedGameDesignProfile } from '../profiles'

export interface MatchSetupMenuProps {
  readonly profile: ValidatedGameDesignProfile
  readonly onStart: (configuration: MatchConfiguration) => void
}

export function MatchSetupMenu({ profile, onStart }: MatchSetupMenuProps) {
  const [mode, setMode] = useState<MatchMode>('ffa')
  const [durationMinutes, setDurationMinutes] = useState(profile.rules.match.duration.defaultMinutes)
  const [targetEnabled, setTargetEnabled] = useState(false)
  const [scoreTargetPoints, setScoreTargetPoints] = useState(profile.rules.match.scoreTarget.defaultPoints)
  const [names, setNames] = useState(() => DEFAULT_MATCH_CONFIGURATION.participants.map(({ name }) => name))
  const [colors, setColors] = useState(() => DEFAULT_MATCH_CONFIGURATION.participants.map(({ color }) => color))
  const [teams, setTeams] = useState(() => DEFAULT_MATCH_CONFIGURATION.participants.map((_, index) => index % 2 === 0 ? 'red' as const : 'blue' as const))

  const candidate = useMemo(() => {
    try {
      return createMatchConfiguration({
        schemaVersion: DEFAULT_MATCH_CONFIGURATION.schemaVersion,
        id: DEFAULT_MATCH_CONFIGURATION.id,
        mode,
        durationMinutes,
        scoreTargetPoints: targetEnabled ? scoreTargetPoints : null,
        participants: DEFAULT_MATCH_CONFIGURATION.participants.map((participant, index) => ({
          ...participant,
          name: names[index] ?? '',
          color: mode === 'teams' ? TEAM_COLORS[teams[index]!] : colors[index]!,
          team: mode === 'teams' ? teams[index]! : null,
        })),
      }, profile)
    } catch {
      return null
    }
  }, [colors, durationMinutes, mode, names, profile, scoreTargetPoints, targetEnabled, teams])

  return (
    <main className="match-menu">
      <section className="match-menu__panel" aria-labelledby="match-menu-title">
        <p className="match-menu__eyebrow">STAR TOURNAMENT · LOCAL MATCH</p>
        <h1 id="match-menu-title">Настройка матча</h1>
        <div className="match-menu__mode" aria-label="Режим матча">
          <button type="button" data-active={mode === 'ffa'} onClick={() => setMode('ffa')}>Каждый за себя</button>
          <button type="button" data-active={mode === 'teams'} onClick={() => setMode('teams')}>Команды</button>
        </div>

        <div className="match-menu__roster">
          {DEFAULT_MATCH_CONFIGURATION.participants.map((participant, index) => (
            <fieldset key={participant.semanticId}>
              <legend>{index === 0 ? 'Вы' : `Манекен ${index}`}</legend>
              <label>Имя<input aria-label={`Имя участника ${index + 1}`} value={names[index]} onChange={(event) => setNames((current) => current.map((value, item) => item === index ? event.target.value : value))} /></label>
              {mode === 'ffa' ? (
                <label>Цвет<input aria-label={`Цвет участника ${index + 1}`} type="color" value={colors[index]} onChange={(event) => setColors((current) => current.map((value, item) => item === index ? event.target.value : value))} /></label>
              ) : (
                <label>Команда<select aria-label={`Команда участника ${index + 1}`} value={teams[index]} onChange={(event) => setTeams((current) => current.map((value, item) => item === index ? event.target.value as 'red' | 'blue' : value))}><option value="red">Красные</option><option value="blue">Синие</option></select></label>
              )}
            </fieldset>
          ))}
        </div>

        <div className="match-menu__rules">
          <label>Длительность, мин<input aria-label="Длительность матча" type="number" min={profile.rules.match.duration.minimumMinutes} max={profile.rules.match.duration.maximumMinutes} step="1" value={durationMinutes} onChange={(event) => setDurationMinutes(Number(event.target.value))} /></label>
          <label className="match-menu__check"><input aria-label="Включить цель по очкам" type="checkbox" checked={targetEnabled} onChange={(event) => setTargetEnabled(event.target.checked)} />Цель по очкам</label>
          {targetEnabled && <label>Очки<input aria-label="Цель по очкам" type="number" min={profile.rules.match.scoreTarget.minimumPoints} max={profile.rules.match.scoreTarget.maximumPoints} step={profile.rules.match.scoreTarget.stepPoints} value={scoreTargetPoints} onChange={(event) => setScoreTargetPoints(Number(event.target.value))} /></label>}
        </div>
        {candidate === null && <p className="match-menu__validation" role="alert">Проверьте состав команд, имена и диапазоны матча.</p>}
        <button className="match-menu__start" type="button" disabled={candidate === null} onClick={() => candidate && onStart(candidate)}>Начать матч</button>
      </section>
    </main>
  )
}
