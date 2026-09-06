import { useEffect, useRef, useState } from 'react'

import { generateProceduralArena, PROCEDURAL_FALLBACK_ARENA, type ArenaDefinition, type ProceduralArenaGenerationResult } from '../arena'
import { createMatchConfiguration, type MatchConfiguration } from '../match'
import { loadLocalProfileCatalog, persistLocalProfileCatalog, resolveSelectedProfile, PROTOTYPE_V1_PROFILE, type ValidatedGameDesignProfile } from '../profiles'
import { GRAPHICS_QUALITY_STORAGE_KEY, parseGraphicsQualityPreference, type GraphicsQualityProfile } from '../presentation'
import { bootstrapBrowserRuntime, type BrowserApplication, type BrowserBootstrapDependencies } from '../runtime/bootstrapBrowserRuntime'
import { MatchSetupMenu } from './MatchSetupMenu'
import { UiPreview, type UiPreviewKind } from './UiPreview'
import { RobotAnimationReview } from './RobotAnimationReview'
import { GameDesignLab } from './GameDesignLab'
import { loadRepositoryHistory } from './profileRepository'

export interface StarTournamentAppProps {
  readonly bootstrap?: (mount: HTMLElement, dependencies: BrowserBootstrapDependencies) => Promise<BrowserApplication>
  readonly generateArena?: (options: Parameters<typeof generateProceduralArena>[0]) => ProceduralArenaGenerationResult
}

interface MatchSession {
  readonly configuration: MatchConfiguration
  readonly arena: ArenaDefinition
  readonly graphicsQuality: GraphicsQualityProfile
  readonly profile: ValidatedGameDesignProfile
}

export function StarTournamentApp({ bootstrap = bootstrapBrowserRuntime, generateArena = generateProceduralArena }: StarTournamentAppProps) {
  const [graphicsQuality, setGraphicsQuality] = useState<GraphicsQualityProfile>(() => (
    parseGraphicsQualityPreference(globalThis.localStorage?.getItem(GRAPHICS_QUALITY_STORAGE_KEY)).profile
  ))
  const [session, setSession] = useState<MatchSession | null>(null)
  const [failedConfiguration, setFailedConfiguration] = useState<MatchConfiguration | null>(null)
  const [generationError, setGenerationError] = useState<{ seed: number; size: MatchConfiguration['arenaSize']; reason: string } | null>(null)
  const [sessionNumber, setSessionNumber] = useState(0)
  const [labOpen, setLabOpen] = useState(false)
  const [selectedProfile, setSelectedProfile] = useState<ValidatedGameDesignProfile>(() => {
    const catalog = loadLocalProfileCatalog(); return catalog.ok && resolveSelectedProfile(catalog.catalog).ok ? (resolveSelectedProfile(catalog.catalog) as { ok: true; profile: ValidatedGameDesignProfile }).profile : PROTOTYPE_V1_PROFILE
  })
  const hostRef = useRef<HTMLDivElement>(null)
  const preview = new URLSearchParams(window.location.search).get('uiPreview')
  const animationReview = new URLSearchParams(window.location.search).has('animationReview')

  useEffect(() => {
    void loadRepositoryHistory().then((history) => {
      if (history === null) return
      persistLocalProfileCatalog(history)
      const resolved = resolveSelectedProfile(history)
      if (resolved.ok) setSelectedProfile(resolved.profile)
    }).catch(() => undefined)
  }, [])

  useEffect(() => {
    const host = hostRef.current
    if (session === null || host === null) return
    let application: BrowserApplication | undefined
    let cancelled = false
    void bootstrap(host, {
      configuration: session.configuration,
      arena: session.arena,
      profile: session.profile,
      graphicsQuality: session.graphicsQuality,
      onRepeat: () => setSessionNumber((value) => value + 1),
      onMainMenu: () => { setSession(null); setGenerationError(null) },
    }).then((created) => {
      if (cancelled) created.dispose()
      else application = created
    })
    return () => {
      cancelled = true
      application?.dispose()
    }
  }, [bootstrap, session, sessionNumber])

  const start = (configuration: MatchConfiguration) => {
    const generated = generateArena({ size: configuration.arenaSize, seed: configuration.arenaSeed, profile: selectedProfile })
    if (!generated.ok) {
      const reason = generated.reports.at(-1)?.errors.map(({ code }) => code).join(', ') || 'неизвестная ошибка validation'
      setFailedConfiguration(configuration)
      setGenerationError({ seed: configuration.arenaSeed, size: configuration.arenaSize, reason })
      return
    }
    setGenerationError(null)
    setFailedConfiguration(null)
    setSession({ configuration, arena: generated.arena, graphicsQuality, profile: selectedProfile })
  }

  const useFallback = () => {
    const source = failedConfiguration
    if (source === null) return
    const { contentHash: _contentHash, ...payload } = source
    const configuration = createMatchConfiguration({ ...payload, arenaSize: PROCEDURAL_FALLBACK_ARENA.size, arenaSeed: PROCEDURAL_FALLBACK_ARENA.seed }, selectedProfile)
    setGenerationError(null)
    setSession({ configuration, arena: PROCEDURAL_FALLBACK_ARENA, graphicsQuality, profile: selectedProfile })
  }

  if (preview === 'scoreboard-ffa' || preview === 'overtime' || preview === 'results-ffa' || preview === 'results-teams' || preview === 'generation-error') return <UiPreview kind={preview as UiPreviewKind} />
  if (animationReview) return <RobotAnimationReview />
  if (session === null && labOpen) return <GameDesignLab selected={selectedProfile} onSelected={setSelectedProfile} onClose={() => setLabOpen(false)} />
  if (session === null) return <MatchSetupMenu profile={selectedProfile} onStart={start} onOpenGameDesignLab={() => setLabOpen(true)} generationError={generationError} onUseFallback={useFallback} onGraphicsQualityChange={setGraphicsQuality} />
  return <div key={sessionNumber} ref={hostRef} className="match-session-host" data-configuration-hash={session.configuration.contentHash} data-arena-hash={session.arena.contentHash} />
}
