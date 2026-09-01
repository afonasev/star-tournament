import { collisionCheckpointHash, COLLISION_ERROR_CODES, RUNTIME_COLLISION_COMPATIBILITY_IDENTITY, type CollisionCapsuleProjection, type CollisionCheckpointPayload } from '../collision/contracts'
import { initializeCollisionBackend } from '../collision/backendInitialization'
import { COLLISION_FIXTURE_ARENA, COLLISION_FIXTURE_ID } from '../collision/fixture'
import {
  createRapierCollisionWorld,
  type CollisionControllerConfiguration,
} from '../collision/rapierWorld'
import { runCollisionBrowserBenchmark } from '../collision/workload'
import type { ContentHash } from '../core/canonical'
import {
  KeyboardMouseAdapter,
  type KeyboardMouseAdapterOptions,
} from '../input/keyboardMouseAdapter'
import {
  PointerLockController,
  type PointerLockControllerOptions,
  type PointerLockLifecycleSnapshot,
} from '../input/pointerLockController'
import {
  PROTOTYPE_V1_PROFILE,
  validateGameDesignProfile,
  type GameDesignProfile,
  type ValidatedGameDesignProfile,
} from '../profiles/gameDesignProfile'
import {
  createFirstPersonRenderer,
  type FirstPersonRenderer,
  type FirstPersonRendererOptions,
} from '../render/firstPersonRenderer'
import { COMBAT_SLICE_SCENARIO_V1 } from '../scenario'
import {
  createInitialPlayableSimulationSnapshot,
  playableSimulationSnapshotHash,
  PLAYABLE_PLAYER_ID,
  SIMULATION_TICKS_PER_SECOND,
  stepPlayableSimulation,
  type PlayableCollisionPort,
  type PlayableSimulationSnapshot,
} from '../simulation'
import { createGameplayUi, type GameplayUi, type GameplayUiOptions } from '../ui/createGameplayUi'
import {
  GameplayUiProjector,
  type GameplayDebugSnapshot,
  type GameplayUiSnapshot,
  type GameplayUiStatus,
} from '../ui/gameplayUiSnapshot'
import { BrowserRuntime, type AnimationScheduler } from './browserRuntime'
import type { CollisionUiState } from './uiSnapshot'

export interface RuntimeCollisionWorld extends PlayableCollisionPort {
  checkpoint(tick: number): CollisionCheckpointPayload
  dispose(): void
}

export interface RuntimeInputAdapter {
  drainActionFrame(tick: number): ReturnType<KeyboardMouseAdapter['drainActionFrame']>
  clearInputState(): void
  dispose(): void
}

export interface RuntimePointerLockController {
  getSnapshot(): Readonly<PointerLockLifecycleSnapshot>
  requestStart(): Promise<boolean>
  requestResume(): Promise<boolean>
  dispose(): void
}

export interface BrowserApplication {
  readonly runtime?: BrowserRuntime<PlayableSimulationSnapshot, GameplayUiSnapshot>
  readonly requestControl: () => Promise<boolean>
  readonly dispose: () => void
}

interface BrowserShell {
  readonly playfield: HTMLDivElement
  readonly uiLayer: HTMLDivElement
}

function createShell(mount: HTMLElement): BrowserShell {
  const playfield = document.createElement('div')
  playfield.className = 'playfield'
  playfield.setAttribute('aria-label', 'Игровое поле')
  playfield.tabIndex = -1
  const uiLayer = document.createElement('div')
  uiLayer.className = 'ui-layer'
  mount.replaceChildren(playfield, uiLayer)
  return { playfield, uiLayer }
}

function toContentHash(hash: string): ContentHash {
  return hash as ContentHash
}

function collisionIdentityLabel(): string {
  const identity = RUNTIME_COLLISION_COMPATIBILITY_IDENTITY
  return `${identity.contractVersion} · ${identity.backendPackage}@${identity.backendVersion}`
}

function controllerConfiguration(
  profile: ValidatedGameDesignProfile,
): CollisionControllerConfiguration {
  const movement = profile.rules.player.movement
  return {
    skinWidth: profile.rules.player.capsule.skinWidth,
    autostepMaxHeight: movement.autostepMaxHeight,
    autostepMinWidth: movement.autostepMinWidth,
    snapToGroundDistance: movement.snapToGroundDistance,
    maxSlopeClimbDegrees: movement.maxSlopeClimbDegrees,
    minSlopeSlideDegrees: movement.minSlopeSlideDegrees,
  }
}

function playerCapsule(
  snapshot: PlayableSimulationSnapshot,
  profile: ValidatedGameDesignProfile,
): CollisionCapsuleProjection {
  return {
    semanticId: snapshot.player.semanticId,
    position: snapshot.player.position,
    radius: profile.rules.player.capsule.radius,
    halfHeight: profile.rules.player.capsule.halfHeight,
  }
}

function errorCode(error: unknown): string {
  return error !== null && typeof error === 'object' && 'code' in error
    ? String(error.code)
    : COLLISION_ERROR_CODES.backendInitializationFailed
}

export interface BrowserBootstrapDependencies {
  readonly profile?: GameDesignProfile | ValidatedGameDesignProfile
  readonly createCollisionWorld?: (
    arena: typeof COLLISION_FIXTURE_ARENA,
    capsules: readonly CollisionCapsuleProjection[],
    configuration: CollisionControllerConfiguration,
  ) => Promise<RuntimeCollisionWorld>
  readonly runBenchmark?: typeof runCollisionBrowserBenchmark
  readonly createRenderer?: (
    options: FirstPersonRendererOptions,
  ) => FirstPersonRenderer
  readonly createUi?: (mount: HTMLElement, options: GameplayUiOptions) => GameplayUi
  readonly createInput?: (options: KeyboardMouseAdapterOptions) => RuntimeInputAdapter
  readonly createPointerLockController?: (
    options: PointerLockControllerOptions,
  ) => RuntimePointerLockController
  readonly initializeBackend?: () => Promise<unknown>
  readonly scheduler?: AnimationScheduler
  readonly now?: () => number
  readonly locationSearch?: string
}

export async function bootstrapBrowserRuntime(
  mount: HTMLElement,
  dependencies: BrowserBootstrapDependencies = {},
): Promise<BrowserApplication> {
  const { playfield, uiLayer } = createShell(mount)
  const search = new URLSearchParams(dependencies.locationSearch ?? window.location.search)
  const debugEnabled = search.has('debug')
  const benchmarkEnabled = search.has('collisionBenchmark')
  const profileValidation = validateGameDesignProfile(
    dependencies.profile ?? PROTOTYPE_V1_PROFILE,
  )
  const profile = profileValidation.ok
    ? profileValidation.profile
    : PROTOTYPE_V1_PROFILE
  const projector = new GameplayUiProjector({
    profile,
    ticksPerSecond: SIMULATION_TICKS_PER_SECOND,
  })
  const createCollision = dependencies.createCollisionWorld ?? createRapierCollisionWorld
  const createRenderer = dependencies.createRenderer ?? createFirstPersonRenderer
  const createInput = dependencies.createInput ?? ((options) => new KeyboardMouseAdapter(options))
  const createPointerController = dependencies.createPointerLockController ??
    ((options) => new PointerLockController(options))
  const createUi = dependencies.createUi ?? createGameplayUi
  const initializeBackend = dependencies.initializeBackend ?? initializeCollisionBackend
  const runBenchmark = dependencies.runBenchmark ?? runCollisionBrowserBenchmark
  const now = dependencies.now ?? (() => performance.now())

  const initialState = createInitialPlayableSimulationSnapshot({
    rngSeed: COLLISION_FIXTURE_ARENA.seed,
    profile,
    scenario: COMBAT_SLICE_SCENARIO_V1,
    collision: RUNTIME_COLLISION_COMPATIBILITY_IDENTITY,
    arena: {
      id: COLLISION_FIXTURE_ARENA.id,
      seed: COLLISION_FIXTURE_ARENA.seed,
      generatorVersion: COLLISION_FIXTURE_ARENA.generatorVersion,
      contentHash: toContentHash(COLLISION_FIXTURE_ARENA.contentHash),
    },
  })

  let disposed = false
  let hasEverLocked = false
  let pointerState: Readonly<PointerLockLifecycleSnapshot> = Object.freeze({ status: 'paused' })
  let collisionState: CollisionUiState = {
    status: 'loading',
    identity: collisionIdentityLabel(),
    fixtureId: COLLISION_FIXTURE_ID,
    fixtureStatus: 'pending',
  }
  let collisionWorld: RuntimeCollisionWorld | undefined
  let renderer: FirstPersonRenderer | undefined
  let input: RuntimeInputAdapter | undefined
  let pointerController: RuntimePointerLockController | undefined
  let runtime: BrowserRuntime<PlayableSimulationSnapshot, GameplayUiSnapshot> | undefined

  function uiStatus(): GameplayUiStatus {
    if (pointerState.status === 'locked') return 'running'
    return hasEverLocked ? 'paused' : 'start'
  }

  function collisionDebugLabel(): string {
    const parts = [collisionState.identity, collisionState.status, collisionState.fixtureStatus]
    if (collisionState.checkpointHash) parts.push(collisionState.checkpointHash)
    if (collisionState.benchmark) {
      parts.push(
        `p95 ${collisionState.benchmark.p95Milliseconds.toFixed(3)} ms`,
        `p99 ${collisionState.benchmark.p99Milliseconds.toFixed(3)} ms`,
        `max ${collisionState.benchmark.maxMilliseconds.toFixed(3)} ms`,
      )
    }
    return parts.join(' · ')
  }

  function debugSnapshot(state: PlayableSimulationSnapshot): GameplayDebugSnapshot | undefined {
    if (!debugEnabled) return undefined
    return {
      tick: state.tick,
      stateHash: playableSimulationSnapshotHash(state),
      profileIdentity: `${state.profile.id}@${state.profile.revision} · ${state.profile.contentHash}`,
      arenaIdentity: `${state.arena.id} · ${state.arena.contentHash}`,
      collisionIdentity: collisionDebugLabel(),
      playerPosition: state.player.position,
      playerYawDegrees: state.player.yaw * (180 / Math.PI),
      playerPitchDegrees: state.player.pitch * (180 / Math.PI),
      playerGrounded: state.player.grounded,
      ammo: state.weapon.ammo,
      targets: state.targets.map((target) => ({
        semanticId: target.semanticId,
        health: target.health,
        active: target.active,
      })),
    }
  }

  function projectUi(
    state: PlayableSimulationSnapshot,
    status = uiStatus(),
    message?: string,
  ): Readonly<GameplayUiSnapshot> {
    return projector.project(state, status, debugSnapshot(state), message)
  }

  async function requestControl(): Promise<boolean> {
    if (disposed || pointerController === undefined) return false
    return hasEverLocked
      ? pointerController.requestResume()
      : pointerController.requestStart()
  }

  const ui = createUi(uiLayer, {
    onRequestControl: () => {
      void requestControl()
    },
  })
  ui.update(projectUi(initialState, 'start'))

  const onPageHide = () => {
    if (runtime !== undefined) runtime.dispose()
    else cleanup()
  }

  function cleanup(): void {
    if (disposed) return
    disposed = true
    window.removeEventListener('pagehide', onPageHide)
    pointerController?.dispose()
    input?.dispose()
    collisionWorld?.dispose()
    renderer?.dispose()
    ui.dispose()
    mount.replaceChildren()
  }
  window.addEventListener('pagehide', onPageHide, { once: true })

  if (!profileValidation.ok) {
    ui.update(projectUi(
      initialState,
      'error',
      `Profile validation failed (${profileValidation.errors.map((error) => error.code).join(', ')})`,
    ))
    return { requestControl, dispose: cleanup }
  }

  try {
    const coldStart = now()
    await initializeBackend()
    const cold = now() - coldStart
    const warmStart = now()
    await initializeBackend()
    const warm = now() - warmStart
    collisionWorld = await createCollision(
      COLLISION_FIXTURE_ARENA,
      [playerCapsule(initialState, profile)],
      controllerConfiguration(profile),
    )
    if (disposed) {
      collisionWorld.dispose()
      return { requestControl, dispose: cleanup }
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
    const code = errorCode(error)
    collisionState = {
      status: 'error',
      identity: collisionIdentityLabel(),
      fixtureId: COLLISION_FIXTURE_ID,
      fixtureStatus: 'failed',
      errorCode: code,
    }
    ui.update(projectUi(initialState, 'error', `Collision initialization failed (${code})`))
    return { requestControl, dispose: cleanup }
  }

  try {
    renderer = createRenderer({
      container: playfield,
      arena: COLLISION_FIXTURE_ARENA,
      scenario: COMBAT_SLICE_SCENARIO_V1,
      profile,
      ticksPerSecond: SIMULATION_TICKS_PER_SECOND,
    })
    renderer.render(initialState)
  } catch (error) {
    const message = error instanceof Error && error.name === 'WebGlUnavailableError'
      ? error.message
      : 'Неизвестная ошибка инициализации 3D-сцены'
    ui.update(projectUi(initialState, 'error', message))
    return { requestControl, dispose: cleanup }
  }

  input = createInput({
    pointerLockElement: playfield,
    participantId: PLAYABLE_PLAYER_ID,
  })

  runtime = new BrowserRuntime<PlayableSimulationSnapshot, GameplayUiSnapshot>({
    initialState,
    scheduler: dependencies.scheduler,
    publishUiAfterStep: true,
    step: (state) => {
      const frame = input!.drainActionFrame(state.tick + 1)
      return stepPlayableSimulation(state, frame, collisionWorld!, profile)
    },
    render: (state) => renderer?.render(state),
    snapshotForUi: (state) => projectUi(state),
    publishUiSnapshot: ui.update,
    disposePresentation: cleanup,
  })

  pointerController = createPointerController({
    lockElement: playfield,
    clearInputState: () => input?.clearInputState(),
    startRuntime: () => runtime?.start(),
    stopRuntime: () => runtime?.stop(),
    onStateChange: (snapshot) => {
      pointerState = snapshot
      if (snapshot.status === 'locked') hasEverLocked = true
      if (!disposed && runtime !== undefined) {
        ui.update(projectUi(runtime.getState(), uiStatus(), snapshot.errorMessage))
      }
    },
  })

  // The controller constructor synchronously pauses the runtime. This explicit
  // projection distinguishes the first start overlay from a later resume.
  pointerState = pointerController.getSnapshot()
  ui.update(projectUi(runtime.getState(), uiStatus(), pointerState.errorMessage))

  if (benchmarkEnabled) {
    playfield.dataset.collisionBenchmarkStatus = 'running'
    window.setTimeout(() => {
      void runBenchmark().then((benchmark) => {
        if (disposed || runtime === undefined) return
        collisionState = { ...collisionState, benchmark }
        playfield.dataset.collisionBenchmarkStatus = 'passed'
        playfield.dataset.collisionBenchmarkP95Milliseconds = String(benchmark.p95Milliseconds)
        playfield.dataset.collisionBenchmarkP99Milliseconds = String(benchmark.p99Milliseconds)
        playfield.dataset.collisionBenchmarkMaxMilliseconds = String(benchmark.maxMilliseconds)
        ui.update(projectUi(runtime.getState()))
      }).catch(() => {
        if (disposed || runtime === undefined) return
        collisionState = { ...collisionState, fixtureStatus: 'failed' }
        playfield.dataset.collisionBenchmarkStatus = 'failed'
        ui.update(projectUi(runtime.getState()))
      })
    }, 0)
  }

  return {
    runtime,
    requestControl,
    dispose: () => runtime?.dispose(),
  }
}
