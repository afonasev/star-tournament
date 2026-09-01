import { useMemo, useState } from 'react'

import {
  DEFAULT_MATCH_CONFIGURATION,
  TEAM_COLORS,
  createMatchConfiguration,
  type MatchConfiguration,
  type MatchMode,
} from '../match'
import type { ValidatedGameDesignProfile } from '../profiles'
import type { ArenaSize } from '../arena'

export interface ArenaGenerationErrorView {
  readonly seed: number
  readonly size: ArenaSize
  readonly reason: string
}

export interface MatchSetupMenuProps {
  readonly profile: ValidatedGameDesignProfile
  readonly onStart: (configuration: MatchConfiguration) => void
  readonly generationError?: ArenaGenerationErrorView | null
  readonly onUseFallback?: () => void
}

function autoSeed(): number {
  const values = new Uint32Array(1)
  globalThis.crypto.getRandomValues(values)
  return values[0]!
}

function parseSeedDraft(value: string): number | null {
  if (value.trim() === '') return null
  if (!/^[0-9]+$/.test(value.trim())) throw new RangeError('Seed должен быть целым числом от 0 до 4294967295.')
  const parsed = Number(value)
  if (!Number.isSafeInteger(parsed) || parsed < 0 || parsed > 0xffffffff) throw new RangeError('Seed должен быть целым числом от 0 до 4294967295.')
  return parsed
}

export function MatchSetupMenu({ profile, onStart, generationError, onUseFallback }: MatchSetupMenuProps) {
  const [mode, setMode] = useState<MatchMode>('ffa')
  const [arenaSize, setArenaSize] = useState<ArenaSize>('medium')
  const [seedDraft, setSeedDraft] = useState('')
  const [seedError, setSeedError] = useState<string | null>(null)
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
        arenaSize,
        arenaSeed: 0,
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
  }, [arenaSize, colors, durationMinutes, mode, names, profile, scoreTargetPoints, targetEnabled, teams])

  const start = (forceNewSeed = false, resolvedSeed?: number) => {
    if (candidate === null) return
    try {
      const manual = forceNewSeed || resolvedSeed !== undefined ? null : parseSeedDraft(seedDraft)
      const arenaSeed = resolvedSeed ?? manual ?? autoSeed()
      setSeedError(null)
      const { contentHash: _contentHash, ...payload } = candidate
      onStart(createMatchConfiguration({ ...payload, arenaSeed }, profile))
    } catch (error) {
      setSeedError(error instanceof Error ? error.message : 'Некорректный seed.')
    }
  }

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
          <label>Размер арены<select aria-label="Размер арены" value={arenaSize} onChange={(event) => setArenaSize(event.target.value as ArenaSize)}><option value="small">Маленькая</option><option value="medium">Средняя</option><option value="large">Большая</option></select></label>
          <label>Seed (необязательно)<input aria-label="Seed арены" inputMode="numeric" placeholder="Автоматически" value={seedDraft} onChange={(event) => { setSeedDraft(event.target.value); setSeedError(null) }} /></label>
          <label>Длительность, мин<input aria-label="Длительность матча" type="number" min={profile.rules.match.duration.minimumMinutes} max={profile.rules.match.duration.maximumMinutes} step="1" value={durationMinutes} onChange={(event) => setDurationMinutes(Number(event.target.value))} /></label>
          <label className="match-menu__check"><input aria-label="Включить цель по очкам" type="checkbox" checked={targetEnabled} onChange={(event) => setTargetEnabled(event.target.checked)} />Цель по очкам</label>
          {targetEnabled && <label>Очки<input aria-label="Цель по очкам" type="number" min={profile.rules.match.scoreTarget.minimumPoints} max={profile.rules.match.scoreTarget.maximumPoints} step={profile.rules.match.scoreTarget.stepPoints} value={scoreTargetPoints} onChange={(event) => setScoreTargetPoints(Number(event.target.value))} /></label>}
        </div>
        {seedError !== null && <p className="match-menu__validation" role="alert">{seedError}</p>}
        {generationError != null && <div className="match-menu__generation-error" role="alert"><strong>Арена не создана</strong><p>{generationError.size} · seed {generationError.seed}: {generationError.reason}</p><div><button type="button" onClick={() => start(false, generationError.seed)}>Повторить seed</button><button type="button" onClick={() => { setSeedDraft(''); start(true) }}>Новый seed</button>{onUseFallback && <button type="button" onClick={onUseFallback}>Запустить запасную арену</button>}</div></div>}
        {candidate === null && <p className="match-menu__validation" role="alert">Проверьте состав команд, имена и диапазоны матча.</p>}
        <button className="match-menu__start" type="button" disabled={candidate === null} onClick={() => start(false)}>Начать матч</button>
      </section>
    </main>
  )
}
