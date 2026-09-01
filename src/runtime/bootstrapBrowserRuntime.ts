import { DIAGNOSTIC_ARENA } from '../arena/diagnosticArena'
import type { ContentHash } from '../core/canonical'
import { createActionFrame } from '../input/actionFrame'
import { PROTOTYPE_V1_PROFILE } from '../profiles/gameDesignProfile'
import {
  createDiagnosticRenderer,
  WebGlUnavailableError,
} from '../render/diagnosticRenderer'
import {
  createInitialSimulationSnapshot,
  simulationSnapshotHash,
  stepSimulation,
  type SimulationSnapshot,
} from '../simulation'
import { createDiagnosticUi } from '../ui/createDiagnosticUi'
import { BrowserRuntime } from './browserRuntime'
import { freezeUiSnapshot, type RuntimeStatus, type UiSnapshot } from './uiSnapshot'

export interface BrowserApplication {
  readonly runtime?: BrowserRuntime<SimulationSnapshot>
  readonly dispose: () => void
}

function createShell(mount: HTMLElement): {
  readonly playfield: HTMLDivElement
  readonly uiLayer: HTMLDivElement
} {
  const playfield = document.createElement('div')
  playfield.className = 'playfield'
  playfield.setAttribute('aria-label', 'Игровое поле')
  const uiLayer = document.createElement('div')
  uiLayer.className = 'ui-layer'
  mount.replaceChildren(playfield, uiLayer)
  return { playfield, uiLayer }
}

function toContentHash(hash: string): ContentHash {
  // Both shipped inputs were validated before export; this narrows their
  // intentionally general serialized interface for the simulation contract.
  return hash as ContentHash
}

function createUiSnapshot(state: SimulationSnapshot, status: RuntimeStatus): UiSnapshot {
  return {
    status,
    tick: state.tick,
    stateHash: simulationSnapshotHash(state),
    profileId: state.profile.id,
    profileHash: state.profile.contentHash,
    arenaId: state.arena.id,
    arenaHash: state.arena.contentHash,
  }
}

export function bootstrapBrowserRuntime(mount: HTMLElement): BrowserApplication {
  const { playfield, uiLayer } = createShell(mount)
  const ui = createDiagnosticUi(uiLayer)
  const initialState = createInitialSimulationSnapshot({
    rngSeed: DIAGNOSTIC_ARENA.seed,
    profile: {
      id: PROTOTYPE_V1_PROFILE.id,
      revision: PROTOTYPE_V1_PROFILE.revision,
      contentHash: toContentHash(PROTOTYPE_V1_PROFILE.contentHash),
    },
    arena: {
      id: DIAGNOSTIC_ARENA.id,
      seed: DIAGNOSTIC_ARENA.seed,
      generatorVersion: DIAGNOSTIC_ARENA.generatorVersion,
      contentHash: toContentHash(DIAGNOSTIC_ARENA.contentHash),
    },
  })

  ui.update(createUiSnapshot(initialState, 'starting'))

  let renderer: ReturnType<typeof createDiagnosticRenderer>
  try {
    renderer = createDiagnosticRenderer({
      container: playfield,
      arena: DIAGNOSTIC_ARENA,
    })
  } catch (error) {
    const message = error instanceof WebGlUnavailableError
      ? error.message
      : 'Неизвестная ошибка инициализации 3D-сцены'
    ui.update(
      freezeUiSnapshot({
        ...createUiSnapshot(initialState, 'error'),
        errorMessage: message,
      }),
    )
    const dispose = () => {
      window.removeEventListener('pagehide', dispose)
      ui.dispose()
      mount.replaceChildren()
    }
    window.addEventListener('pagehide', dispose, { once: true })
    return { dispose }
  }

  let runtime: BrowserRuntime<SimulationSnapshot>
  const disposePresentation = () => {
    window.removeEventListener('pagehide', onPageHide)
    renderer.dispose()
    ui.dispose()
    mount.replaceChildren()
  }
  const onPageHide = () => runtime.dispose()

  runtime = new BrowserRuntime<SimulationSnapshot>({
    initialState,
    step: (state) => stepSimulation(state, createActionFrame(state.tick + 1, [])),
    render: (state) => renderer.render(state),
    snapshotForUi: createUiSnapshot,
    publishUiSnapshot: ui.update,
    disposePresentation,
  })
  window.addEventListener('pagehide', onPageHide, { once: true })
  runtime.start()

  return {
    runtime,
    dispose: () => runtime.dispose(),
  }
}
