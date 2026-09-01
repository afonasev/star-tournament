import { useEffect, useRef, useState } from 'react'

import { generateProceduralArena, PROCEDURAL_FALLBACK_ARENA, type ArenaDefinition, type ProceduralArenaGenerationResult } from '../arena'
import { createMatchConfiguration, type MatchConfiguration } from '../match'
import { PROTOTYPE_V1_PROFILE } from '../profiles'
import { bootstrapBrowserRuntime, type BrowserApplication, type BrowserBootstrapDependencies } from '../runtime/bootstrapBrowserRuntime'
import { MatchSetupMenu } from './MatchSetupMenu'
import { UiPreview, type UiPreviewKind } from './UiPreview'

export interface StarTournamentAppProps {
  readonly bootstrap?: (mount: HTMLElement, dependencies: BrowserBootstrapDependencies) => Promise<BrowserApplication>
  readonly generateArena?: (options: Parameters<typeof generateProceduralArena>[0]) => ProceduralArenaGenerationResult
}

interface MatchSession {
  readonly configuration: MatchConfiguration
  readonly arena: ArenaDefinition
}

export function StarTournamentApp({ bootstrap = bootstrapBrowserRuntime, generateArena = generateProceduralArena }: StarTournamentAppProps) {
  const [session, setSession] = useState<MatchSession | null>(null)
  const [failedConfiguration, setFailedConfiguration] = useState<MatchConfiguration | null>(null)
  const [generationError, setGenerationError] = useState<{ seed: number; size: MatchConfiguration['arenaSize']; reason: string } | null>(null)
  const [sessionNumber, setSessionNumber] = useState(0)
  const hostRef = useRef<HTMLDivElement>(null)
  const preview = new URLSearchParams(window.location.search).get('uiPreview')

  useEffect(() => {
    const host = hostRef.current
    if (session === null || host === null) return
    let application: BrowserApplication | undefined
    let cancelled = false
    void bootstrap(host, {
      configuration: session.configuration,
      arena: session.arena,
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
    const generated = generateArena({ size: configuration.arenaSize, seed: configuration.arenaSeed, profile: PROTOTYPE_V1_PROFILE })
    if (!generated.ok) {
      const reason = generated.reports.at(-1)?.errors.map(({ code }) => code).join(', ') || 'неизвестная ошибка validation'
      setFailedConfiguration(configuration)
      setGenerationError({ seed: configuration.arenaSeed, size: configuration.arenaSize, reason })
      return
    }
    setGenerationError(null)
    setFailedConfiguration(null)
    setSession({ configuration, arena: generated.arena })
  }

  const useFallback = () => {
    const source = failedConfiguration
    if (source === null) return
    const { contentHash: _contentHash, ...payload } = source
    const configuration = createMatchConfiguration({ ...payload, arenaSize: PROCEDURAL_FALLBACK_ARENA.size, arenaSeed: PROCEDURAL_FALLBACK_ARENA.seed }, PROTOTYPE_V1_PROFILE)
    setGenerationError(null)
    setSession({ configuration, arena: PROCEDURAL_FALLBACK_ARENA })
  }

  if (preview === 'scoreboard-ffa' || preview === 'overtime' || preview === 'results-ffa' || preview === 'results-teams' || preview === 'generation-error') return <UiPreview kind={preview as UiPreviewKind} />
  if (session === null) return <MatchSetupMenu profile={PROTOTYPE_V1_PROFILE} onStart={start} generationError={generationError} onUseFallback={useFallback} />
  return <div key={sessionNumber} ref={hostRef} className="match-session-host" data-configuration-hash={session.configuration.contentHash} data-arena-hash={session.arena.contentHash} />
}
