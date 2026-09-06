import { BOT_DIFFICULTIES, BOT_LABELS, type BotDifficulty } from '../profiles/botRules'
import { VETTED_PARTICIPANT_COLORS } from '../match/participantPalette'
import { useMemo, useRef, useState } from 'react'

import {
  DEFAULT_MATCH_CONFIGURATION,
  TEAM_COLORS,
  createMatchConfiguration,
  type MatchConfiguration,
  type MatchMode,
  type MatchParticipantDescriptor,
} from '../match'
import type { ValidatedGameDesignProfile } from '../profiles'
import type { ArenaSize } from '../arena'
import {
  GRAPHICS_QUALITY_STORAGE_KEY,
  GRAPHICS_QUALITY_V1,
  parseGraphicsQualityPreference,
  resolveGraphicsQualityPreference,
  serializeGraphicsQualityPreference,
  type GraphicsQualityPreference,
  type GraphicsQualityProfile,
  type GraphicsQualityPreset,
} from '../presentation'

export interface ArenaGenerationErrorView {
  readonly seed: number
  readonly size: ArenaSize
  readonly reason: string
}

export interface MatchSetupMenuProps {
  readonly profile: ValidatedGameDesignProfile
  readonly onStart: (configuration: MatchConfiguration) => void
  readonly onOpenGameDesignLab?: () => void
  readonly generationError?: ArenaGenerationErrorView | null
  readonly onUseFallback?: () => void
  readonly onGraphicsQualityChange?: (quality: GraphicsQualityProfile) => void
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

export function MatchSetupMenu({ profile, onStart, onOpenGameDesignLab, generationError, onUseFallback, onGraphicsQualityChange }: MatchSetupMenuProps) {
  const [graphicsPreference, setGraphicsPreference] = useState<GraphicsQualityPreference>(() => {
    const resolved = parseGraphicsQualityPreference(globalThis.localStorage?.getItem(GRAPHICS_QUALITY_STORAGE_KEY))
    return { schemaId: 'graphics-quality-v1', preset: resolved.profile.id }
  })
  const [storedGraphicsFallbackReason] = useState(() => (
    parseGraphicsQualityPreference(globalThis.localStorage?.getItem(GRAPHICS_QUALITY_STORAGE_KEY)).fallbackReason
  ))
  const graphicsResolution = resolveGraphicsQualityPreference(graphicsPreference)
  const graphicsQuality = graphicsResolution.profile
  const [mode, setMode] = useState<MatchMode>('ffa')
  const [arenaSize, setArenaSize] = useState<ArenaSize>('medium')
  const [seedDraft, setSeedDraft] = useState('')
  const [seedError, setSeedError] = useState<string | null>(null)
  const [durationMinutes, setDurationMinutes] = useState(profile.rules.match.duration.defaultMinutes)
  const [targetEnabled, setTargetEnabled] = useState(false)
  const [scoreTargetPoints, setScoreTargetPoints] = useState(profile.rules.match.scoreTarget.defaultPoints)
  const nextBotId = useRef(4)
  const [roster, setRoster] = useState<MatchParticipantDescriptor[]>(() => DEFAULT_MATCH_CONFIGURATION.participants.map((p,index) => index===0 ? p : { ...p, kind:'bot', difficulty:'normal' }))
  const [teams, setTeams] = useState<Record<string,'team-a'|'team-b'>>(() => Object.fromEntries(DEFAULT_MATCH_CONFIGURATION.participants.map((p,i) => [p.semanticId,i%2===0?'team-a':'team-b'])))
  const update = (id:string, change:Partial<MatchParticipantDescriptor>) => setRoster(current => current.map(p=>p.semanticId===id?{...p,...change}:p))
  const addBot = () => {
    if(roster.length>=8) return
    const id = 'bot-'+nextBotId.current++
    const color = VETTED_PARTICIPANT_COLORS.find(c=>!roster.some(p=>p.color===c))!
    setRoster(current=>[...current,{semanticId:id,name:'Бот '+id.slice(4),kind:'bot',difficulty:'normal',color,team:null}])
    setTeams(current=>({...current,[id]:roster.length%2===0?'team-a':'team-b'}))
  }
  const candidate = useMemo(() => {
    try {
      return createMatchConfiguration({
        schemaVersion:DEFAULT_MATCH_CONFIGURATION.schemaVersion,id:DEFAULT_MATCH_CONFIGURATION.id,
        mode,arenaSize,arenaSeed:0,durationMinutes,scoreTargetPoints:targetEnabled?scoreTargetPoints:null,
        participants:roster.map(p=>({...p,color:mode==='teams'?TEAM_COLORS[teams[p.semanticId]!]:p.color,team:mode==='teams'?teams[p.semanticId]!:null})),
      },profile)
    } catch { return null }
  },[roster,teams,mode,arenaSize,durationMinutes,targetEnabled,scoreTargetPoints,profile])

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

  const saveGraphicsPreference = (next: GraphicsQualityPreference) => {
    const resolved = resolveGraphicsQualityPreference(next).profile
    globalThis.localStorage?.setItem(GRAPHICS_QUALITY_STORAGE_KEY, serializeGraphicsQualityPreference(next))
    setGraphicsPreference(next)
    onGraphicsQualityChange?.(resolved)
  }

  const selectGraphicsQuality = (value: string) => {
    if (!Object.hasOwn(GRAPHICS_QUALITY_V1, value)) return
    saveGraphicsPreference({ schemaId: 'graphics-quality-v1', preset: value as GraphicsQualityPreset })
  }

  const overrideGraphics = (overrides: NonNullable<GraphicsQualityPreference['overrides']>) => {
    saveGraphicsPreference({
      ...graphicsPreference,
      overrides: { ...graphicsPreference.overrides, ...overrides },
    })
  }

  return (
    <main className="match-menu">
      <section className="match-menu__panel" aria-labelledby="match-menu-title">
        <p className="match-menu__eyebrow">STAR TOURNAMENT · LOCAL MATCH</p>
        <h1 id="match-menu-title">Настройка матча</h1>
        <div className="match-menu__profile"><span>Профиль следующего матча</span><output aria-label="Профиль следующего матча">{profile.id} · v{profile.revision} · {profile.contentHash}</output>{onOpenGameDesignLab && <button type="button" onClick={onOpenGameDesignLab}>Лаборатория геймдизайна</button>}</div>
        <div className="match-menu__mode" aria-label="Режим матча">
          <button type="button" data-active={mode === 'ffa'} onClick={() => setMode('ffa')}>Каждый за себя</button>
          <button type="button" data-active={mode === 'teams'} onClick={() => setMode('teams')}>Команды</button>
        </div>

        <div className="match-menu__roster">
          {roster.map((participant,index) => (
            <fieldset key={participant.semanticId}>
              <legend>{participant.kind==='local-seat'?'Вы':participant.name}</legend>
              <label>Имя<input aria-label={`Имя участника ${index+1}`} value={participant.name} onChange={event=>update(participant.semanticId,{name:event.target.value})}/></label>
              {participant.kind==='bot' && <label>Сложность<select aria-label={`Сложность бота ${index}`} value={participant.difficulty} onChange={event=>update(participant.semanticId,{difficulty:event.target.value as BotDifficulty})}>{BOT_DIFFICULTIES.map(id=><option key={id} value={id}>{BOT_LABELS[id]}</option>)}</select></label>}
              {mode==='ffa'?<label>Цвет<input aria-label={`Цвет участника ${index+1}`} type="color" value={participant.color} onChange={event=>update(participant.semanticId,{color:event.target.value})}/></label>:<label>Команда<select aria-label={`Команда участника ${index+1}`} value={teams[participant.semanticId]} onChange={event=>setTeams(current=>({...current,[participant.semanticId]:event.target.value as 'team-a'|'team-b'}))}><option value="team-a">Team A</option><option value="team-b">Team B</option></select></label>}
              {participant.kind==='bot' && <button type="button" aria-label={`Удалить бота ${index}`} onClick={()=>setRoster(current=>current.filter(p=>p.semanticId!==participant.semanticId))}>Удалить</button>}
            </fieldset>
          ))}
        </div>
        <button type="button" disabled={roster.length>=8} onClick={addBot}>Добавить бота</button>

        <div className="match-menu__rules">
          <label>Размер арены<select aria-label="Размер арены" value={arenaSize} onChange={(event) => setArenaSize(event.target.value as ArenaSize)}><option value="small">Маленькая</option><option value="medium">Средняя</option><option value="large">Большая</option></select></label>
          <label>Seed (необязательно)<input aria-label="Seed арены" inputMode="numeric" placeholder="Автоматически" value={seedDraft} onChange={(event) => { setSeedDraft(event.target.value); setSeedError(null) }} /></label>
          <label>Длительность, мин<input aria-label="Длительность матча" type="number" min={profile.rules.match.duration.minimumMinutes} max={profile.rules.match.duration.maximumMinutes} step="1" value={durationMinutes} onChange={(event) => setDurationMinutes(Number(event.target.value))} /></label>
          <label className="match-menu__check"><input aria-label="Включить цель по очкам" type="checkbox" checked={targetEnabled} onChange={(event) => setTargetEnabled(event.target.checked)} />Цель по очкам</label>
          {targetEnabled && <label>Очки<input aria-label="Цель по очкам" type="number" min={profile.rules.match.scoreTarget.minimumPoints} max={profile.rules.match.scoreTarget.maximumPoints} step={profile.rules.match.scoreTarget.stepPoints} value={scoreTargetPoints} onChange={(event) => setScoreTargetPoints(Number(event.target.value))} /></label>}
        </div>
        <details className="match-menu__graphics">
          <summary>Графика · {graphicsQuality.label}</summary>
          <label>Пресет<select aria-label="Пресет графики" value={graphicsPreference.preset} onChange={(event) => selectGraphicsQuality(event.target.value)}>{Object.values(GRAPHICS_QUALITY_V1).map((item) => <option key={item.id} value={item.id}>{item.label}</option>)}</select></label>
          <label>Render scale<select aria-label="Render scale" value={graphicsQuality.renderScale} onChange={(event) => overrideGraphics({ renderScale: Number(event.target.value) })}>{[0.7, 1, 1.25, 1.5].map((value) => <option key={value} value={value}>×{value}</option>)}</select></label>
          <label>Текстуры<select aria-label="Качество текстур" value={graphicsQuality.textureTier} onChange={(event) => overrideGraphics({ textureTier: event.target.value as 'low' | 'high' })}><option value="low">Low</option><option value="high">High</option></select></label>
          <label>Geometry LOD<select aria-label="Geometry LOD" aria-describedby="geometry-lod-hint" value={graphicsQuality.geometryLod} onChange={(event) => overrideGraphics({ geometryLod: event.target.value as 'lod0' | 'lod1' })}><option value="lod1">LOD 1</option><option value="lod0">LOD 0</option></select></label>
          <label className="match-menu__check"><input aria-label="Decals" type="checkbox" aria-describedby="decals-hint" checked={graphicsQuality.decals} onChange={(event) => overrideGraphics({ decals: event.target.checked })} />Decals</label>
          <label className="match-menu__check"><input aria-label="Post-processing" type="checkbox" checked={graphicsQuality.postProcessing} disabled={graphicsPreference.preset !== 'ultra'} aria-describedby={graphicsPreference.preset !== 'ultra' ? 'post-processing-hint post-processing-reason' : 'post-processing-hint'} onChange={(event) => overrideGraphics({ postProcessing: event.target.checked })} />Post-processing</label>
          {graphicsPreference.preset !== 'ultra' && <p id="post-processing-reason">Post-processing доступен только в Ultra.</p>}
          <aside className="match-menu__graphics-hints" aria-label="Подсказки по качеству графики">
            <p id="geometry-lod-hint"><strong>Geometry LOD.</strong> LOD 0 использует более детальные модели: лучше силуэты и графика, но выше нагрузка. LOD 1 упрощает модели и обычно даёт больше производительности.</p>
            <p id="decals-hint"><strong>Decals.</strong> Добавляют разметку маршрутов, сектора и мелкие детали: арена выглядит богаче и понятнее. Отключите их, если важнее производительность или меньше расход texture budget.</p>
            <p id="post-processing-hint"><strong>Post-processing.</strong> Зарезервирован для финальных экранных эффектов Ultra, которые обычно улучшают картинку ценой производительности. В текущем renderer эффект ещё не подключён, поэтому переключатель пока не меняет изображение и нагрузку.</p>
          </aside>
          <output aria-label="Effective graphics quality">Effective: {graphicsQuality.label} · ×{graphicsQuality.renderScale} · {graphicsQuality.textureTier} · {graphicsQuality.geometryLod} · decals {graphicsQuality.decals ? 'on' : 'off'} · {graphicsQuality.textureBudgetMiB} MiB</output>
          {(storedGraphicsFallbackReason ?? graphicsResolution.fallbackReason) && <p role="status">{storedGraphicsFallbackReason ?? graphicsResolution.fallbackReason}</p>}
        </details>
        {seedError !== null && <p className="match-menu__validation" role="alert">{seedError}</p>}
        {generationError != null && <div className="match-menu__generation-error" role="alert"><strong>Арена не создана</strong><p>{generationError.size} · seed {generationError.seed}: {generationError.reason}</p><div><button type="button" onClick={() => start(false, generationError.seed)}>Повторить seed</button><button type="button" onClick={() => { setSeedDraft(''); start(true) }}>Новый seed</button>{onUseFallback && <button type="button" onClick={onUseFallback}>Запустить запасную арену</button>}</div></div>}
        {candidate === null && <p className="match-menu__validation" role="alert">Проверьте состав команд, имена и диапазоны матча.</p>}
        <button className="match-menu__start" type="button" disabled={candidate === null} onClick={() => start(false)}>Начать матч</button>
      </section>
    </main>
  )
}
