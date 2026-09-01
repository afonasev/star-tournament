import type { ContentHash } from '../core/canonical'
import { collisionCheckpointHash, COLLISION_ERROR_CODES, RUNTIME_COLLISION_COMPATIBILITY_IDENTITY } from '../collision/contracts'
import { initializeCollisionBackend } from '../collision/backendInitialization'
import { COLLISION_FIXTURE_ARENA, COLLISION_FIXTURE_ID } from '../collision/fixture'
import { createRapierCollisionWorld, type RapierCollisionWorld } from '../collision/rapierWorld'
import { runCollisionBrowserBenchmark } from '../collision/workload'
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
import { freezeUiSnapshot, type CollisionUiState, type RuntimeStatus, type UiSnapshot } from './uiSnapshot'

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

function createUiSnapshot(
  state: SimulationSnapshot,
  status: RuntimeStatus,
  collision: CollisionUiState,
): UiSnapshot {
  return {
    status,
    tick: state.tick,
    stateHash: simulationSnapshotHash(state),
    profileId: state.profile.id,
    profileHash: state.profile.contentHash,
    arenaId: state.arena.id,
    arenaHash: state.arena.contentHash,
    collision,
  }
}

function collisionIdentityLabel(): string {
  const identity = RUNTIME_COLLISION_COMPATIBILITY_IDENTITY
  return `${identity.contractVersion} · ${identity.backendPackage}@${identity.backendVersion}`
}

export interface BrowserBootstrapDependencies {
  readonly createCollisionWorld?: typeof createRapierCollisionWorld
  readonly runBenchmark?: typeof runCollisionBrowserBenchmark
  readonly createRenderer?: typeof createDiagnosticRenderer
  readonly initializeBackend?: typeof initializeCollisionBackend
  readonly now?: () => number
}

export async function bootstrapBrowserRuntime(
  mount: HTMLElement,
  dependencies: BrowserBootstrapDependencies = {},
): Promise<BrowserApplication> {
  const { playfield, uiLayer } = createShell(mount)
  const ui = createDiagnosticUi(uiLayer)
  const createCollision = dependencies.createCollisionWorld ?? createRapierCollisionWorld
  const runBenchmark = dependencies.runBenchmark ?? runCollisionBrowserBenchmark
  const createRenderer = dependencies.createRenderer ?? createDiagnosticRenderer
  const initializeBackend = dependencies.initializeBackend ?? initializeCollisionBackend
  const now = dependencies.now ?? (() => performance.now())
  const initialState = createInitialSimulationSnapshot({
    rngSeed: COLLISION_FIXTURE_ARENA.seed,
    profile: {
      id: PROTOTYPE_V1_PROFILE.id,
      revision: PROTOTYPE_V1_PROFILE.revision,
      contentHash: toContentHash(PROTOTYPE_V1_PROFILE.contentHash),
    },
    arena: {
      id: COLLISION_FIXTURE_ARENA.id,
      seed: COLLISION_FIXTURE_ARENA.seed,
      generatorVersion: COLLISION_FIXTURE_ARENA.generatorVersion,
      contentHash: toContentHash(COLLISION_FIXTURE_ARENA.contentHash),
    },
  })

  let collisionState: CollisionUiState = {
    status: 'loading',
    identity: collisionIdentityLabel(),
    fixtureId: COLLISION_FIXTURE_ID,
    fixtureStatus: 'pending',
  }
  ui.update(createUiSnapshot(initialState, 'starting', collisionState))

  let disposed = false
  let collisionWorld: RapierCollisionWorld | undefined
  let renderer: ReturnType<typeof createDiagnosticRenderer> | undefined
  let runtime: BrowserRuntime<SimulationSnapshot> | undefined

  const onPageHide = () => {
    if (runtime !== undefined) runtime.dispose()
    else cleanup()
  }

  const cleanup = () => {
    if (disposed) return
    disposed = true
    window.removeEventListener('pagehide', onPageHide)
    collisionWorld?.dispose()
    renderer?.dispose()
    ui.dispose()
    mount.replaceChildren()
  }
  window.addEventListener('pagehide', onPageHide, { once: true })

  try {
    const coldStart = now()
    await initializeBackend()
    const cold = now() - coldStart
    const warmStart = now()
    await initializeBackend()
    const warm = now() - warmStart
    collisionWorld = await createCollision(COLLISION_FIXTURE_ARENA, [])
    if (disposed) {
      collisionWorld.dispose()
      return { dispose: cleanup }
    }
    collisionState = {
      status: 'ready',
      identity: collisionIdentityLabel(),
      fixtureId: COLLISION_FIXTURE_ID,
      fixtureStatus: 'passed',
      checkpointHash: collisionCheckpointHash(collisionWorld.checkpoint(0)),
      initLatencyMilliseconds: { cold, warm },
    }
  } catch (error) {
    const errorCode = error !== null && typeof error === 'object' && 'code' in error
      ? String(error.code)
      : COLLISION_ERROR_CODES.backendInitializationFailed
    collisionState = {
      status: 'error',
      identity: collisionIdentityLabel(),
      fixtureId: COLLISION_FIXTURE_ID,
      fixtureStatus: 'failed',
      errorCode,
    }
    ui.update(freezeUiSnapshot({
      ...createUiSnapshot(initialState, 'error', collisionState),
      errorMessage: `Collision initialization failed (${errorCode})`,
    }))
    return { dispose: cleanup }
  }

  try {
    renderer = createRenderer({
      container: playfield,
      arena: COLLISION_FIXTURE_ARENA,
    })
  } catch (error) {
    const message = error instanceof WebGlUnavailableError
      ? error.message
      : 'Неизвестная ошибка инициализации 3D-сцены'
    ui.update(
      freezeUiSnapshot({
        ...createUiSnapshot(initialState, 'error', collisionState),
        errorMessage: message,
      }),
    )
    return { dispose: cleanup }
  }

  const disposePresentation = () => {
    cleanup()
  }

  runtime = new BrowserRuntime<SimulationSnapshot>({
    initialState,
    step: (state) => stepSimulation(state, createActionFrame(state.tick + 1, [])),
    render: (state) => renderer?.render(state),
    snapshotForUi: (state, status) => createUiSnapshot(state, status, collisionState),
    publishUiSnapshot: ui.update,
    disposePresentation,
  })
  runtime.start()

  if (new URLSearchParams(window.location.search).has('collisionBenchmark')) {
    window.setTimeout(() => {
      void runBenchmark().then((benchmark) => {
        if (disposed || runtime === undefined) return
        collisionState = { ...collisionState, benchmark }
        ui.update(freezeUiSnapshot(createUiSnapshot(runtime.getState(), 'running', collisionState)))
      }).catch(() => {
        if (disposed || runtime === undefined) return
        collisionState = { ...collisionState, fixtureStatus: 'failed' }
        ui.update(freezeUiSnapshot(createUiSnapshot(runtime.getState(), 'running', collisionState)))
      })
    }, 0)
  }

  return {
    runtime,
    dispose: () => runtime?.dispose(),
  }
}
