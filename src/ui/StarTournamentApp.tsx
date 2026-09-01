import { useEffect, useRef, useState } from 'react'

import type { MatchConfiguration } from '../match'
import { PROTOTYPE_V1_PROFILE } from '../profiles'
import { bootstrapBrowserRuntime, type BrowserApplication, type BrowserBootstrapDependencies } from '../runtime/bootstrapBrowserRuntime'
import { MatchSetupMenu } from './MatchSetupMenu'
import { UiPreview, type UiPreviewKind } from './UiPreview'

export interface StarTournamentAppProps {
  readonly bootstrap?: (mount: HTMLElement, dependencies: BrowserBootstrapDependencies) => Promise<BrowserApplication>
}

export function StarTournamentApp({ bootstrap = bootstrapBrowserRuntime }: StarTournamentAppProps) {
  const [configuration, setConfiguration] = useState<MatchConfiguration | null>(null)
  const [sessionNumber, setSessionNumber] = useState(0)
  const hostRef = useRef<HTMLDivElement>(null)
  const preview = new URLSearchParams(window.location.search).get('uiPreview')

  useEffect(() => {
    const host = hostRef.current
    if (configuration === null || host === null) return
    let application: BrowserApplication | undefined
    let cancelled = false
    void bootstrap(host, {
      configuration,
      onRepeat: () => setSessionNumber((value) => value + 1),
      onMainMenu: () => setConfiguration(null),
    }).then((created) => {
      if (cancelled) created.dispose()
      else application = created
    })
    return () => {
      cancelled = true
      application?.dispose()
    }
  }, [bootstrap, configuration, sessionNumber])

  if (preview === 'scoreboard-ffa' || preview === 'overtime' || preview === 'results-ffa' || preview === 'results-teams') return <UiPreview kind={preview as UiPreviewKind} />
  if (configuration === null) return <MatchSetupMenu profile={PROTOTYPE_V1_PROFILE} onStart={setConfiguration} />
  return <div key={sessionNumber} ref={hostRef} className="match-session-host" data-configuration-hash={configuration.contentHash} />
}
