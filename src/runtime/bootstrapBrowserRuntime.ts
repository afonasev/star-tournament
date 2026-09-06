import { validateArenaPhysicalRoutes } from '../arena/physicalValidation'
import { collisionCheckpointHash, COLLISION_ERROR_CODES, RUNTIME_COLLISION_COMPATIBILITY_IDENTITY, type CollisionCapsuleProjection, type CollisionCheckpointPayload } from '../collision/contracts'
import { initializeCollisionBackend } from '../collision/backendInitialization'
import { requireProceduralArena, type ArenaDefinition } from '../arena'
import {
  createRapierCollisionWorld,
  type CollisionControllerConfiguration,
} from '../collision/rapierWorld'
import { runCollisionBrowserBenchmark } from '../collision/workload'
import {
  KeyboardMouseAdapter,
  type KeyboardMouseAdapterOptions,
} from '../input/keyboardMouseAdapter'
import { createActionFrame, type NormalizedAction } from '../input/actionFrame'
import { DEFAULT_MATCH_CONFIGURATION, parseMatchConfiguration, type MatchConfiguration } from '../match'
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
  PRESENTATION_BALANCED_V1_PROFILE,
  validatePresentationProfile,
  type PresentationProfile,
  type ValidatedPresentationProfile,
} from '../presentation'
import {
  PERFORMANCE_REFERENCE_ID,
  PresentationPerformanceProbe,
  evaluateHardPerformanceGate,
  type HardPerformanceGateResult,
  type PerformancePhaseId,
  type PerformancePhaseMeasurement,
} from '../performance'
import {
  createFirstPersonRenderer,
  resolveLocalLightShadowBudget,
  type FirstPersonRenderer,
  type FirstPersonRendererOptions,
} from '../render/firstPersonRenderer'
import { GRAPHICS_QUALITY_V1, type GraphicsQualityProfile } from '../presentation'
import { prepareArenaGlbAssets, type ArenaGlbTemplateCache } from '../render/arenaGlbAssets'
import { COMBAT_SLICE_SCENARIO_V1 } from '../scenario'
import {
  createInitialPlayableSimulationSnapshot,
  playableSimulationSnapshotHash,
  PLAYABLE_PLAYER_ID,
  SIMULATION_TICKS_PER_SECOND,
  livingParticipantCollisionCapsules,
  stepPlayableSimulation,
  localParticipant,
  stationaryParticipants,
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
import { createPlayableSimulationReplay } from '../simulation/playableReplay'
import type { CollisionUiState } from './uiSnapshot'

export interface RuntimeCollisionWorld extends PlayableCollisionPort {
  checkpoint(tick: number): CollisionCheckpointPayload
  dispose(): void
}

export interface RuntimeInputAdapter {
  drainActionFrame(tick: number): ReturnType<KeyboardMouseAdapter['drainActionFrame']>
  drainUiActionSnapshot?: KeyboardMouseAdapter['drainUiActionSnapshot']
  clearInputState(): void
  dispose(): void
}

export interface RuntimePointerLockController {
  getSnapshot(): Readonly<PointerLockLifecycleSnapshot>
  requestStart(): Promise<boolean>
  requestResume(): Promise<boolean>
  pause(): void
  dispose(): void
}

export interface BrowserApplication {
  readonly runtime?: BrowserRuntime<PlayableSimulationSnapshot, GameplayUiSnapshot>
  readonly requestControl: () => Promise<boolean>
  readonly dispose: () => void
}

export interface BrowserPerformanceReport {
  readonly schemaVersion: 1
  readonly referenceId: typeof PERFORMANCE_REFERENCE_ID
  readonly mode: 'reference' | 'thermal-soak'
  readonly passed: boolean
  readonly environment: {
    readonly userAgent: string
    readonly viewportCssWidth: number
    readonly viewportCssHeight: number
    readonly devicePixelRatio: number
    readonly backingBufferWidth: number
    readonly backingBufferHeight: number
    readonly presentationProfileId: string
    readonly presentationProfileHash: string
    readonly graphicsQualityId: string
    readonly localLightShadowBudget: number
    readonly arenaSize: string
    readonly arenaSeed: number
    readonly arenaContentHash: string
  }
  readonly measurements: readonly PerformancePhaseMeasurement[]
  readonly gates: readonly HardPerformanceGateResult[]
}

interface BrowserShell {
  readonly playfield: HTMLDivElement
  readonly uiLayer: HTMLDivElement
}

type ActivePerformancePhase = Exclude<PerformancePhaseId, 'menu' | 'paused' | 'results'> | undefined

function benchmarkActions(tick: number, phase: ActivePerformancePhase): readonly NormalizedAction[] {
  if (phase === 'movement-jump') {
    return [
      { participantId: PLAYABLE_PLAYER_ID, actionId: 'move-forward', value: 1 },
      { participantId: PLAYABLE_PLAYER_ID, actionId: 'jump', value: tick % 60 === 1 ? 1 : 0 },
    ]
  }
  if (phase === 'combat-burst') {
    return [
      { participantId: PLAYABLE_PLAYER_ID, actionId: 'fire', value: tick % 20 === 1 ? 1 : 0 },
    ]
  }
  return []
}

function createBenchmarkInputAdapter(
  activePhase: () => ActivePerformancePhase,
): RuntimeInputAdapter {
  return {
    drainActionFrame: (tick) => createActionFrame(tick, benchmarkActions(tick, activePhase())),
    clearInputState: () => undefined,
    dispose: () => undefined,
  }
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

function errorCode(error: unknown): string {
  return error !== null && typeof error === 'object' && 'code' in error
    ? String(error.code)
    : COLLISION_ERROR_CODES.backendInitializationFailed
}

export interface BrowserBootstrapDependencies {
  readonly profile?: GameDesignProfile | ValidatedGameDesignProfile
  readonly presentationProfile?: PresentationProfile | ValidatedPresentationProfile
  /** Presentation preference; never participates in match or replay identity. */
  readonly graphicsQuality?: GraphicsQualityProfile
  readonly createCollisionWorld?: (
    arena: ArenaDefinition,
    capsules: readonly CollisionCapsuleProjection[],
    configuration: CollisionControllerConfiguration,
  ) => Promise<RuntimeCollisionWorld>
  readonly runBenchmark?: typeof runCollisionBrowserBenchmark
  readonly createRenderer?: (
    options: FirstPersonRendererOptions,
  ) => FirstPersonRenderer
  readonly prepareArenaGlbAssets?: () => Promise<ArenaGlbTemplateCache>
  readonly createUi?: (mount: HTMLElement, options: GameplayUiOptions) => GameplayUi
  readonly createInput?: (options: KeyboardMouseAdapterOptions) => RuntimeInputAdapter
  readonly createPointerLockController?: (
    options: PointerLockControllerOptions,
  ) => RuntimePointerLockController
  readonly initializeBackend?: () => Promise<unknown>
  readonly scheduler?: AnimationScheduler
  readonly now?: () => number
  readonly wait?: (milliseconds: number) => Promise<void>
  readonly performancePhaseDurationMilliseconds?: number
  readonly locationSearch?: string
  readonly configuration?: MatchConfiguration
  readonly arena?: ArenaDefinition
  readonly onRepeat?: () => void
  readonly onMainMenu?: () => void
}

export async function bootstrapBrowserRuntime(
  mount: HTMLElement,
  dependencies: BrowserBootstrapDependencies = {},
): Promise<BrowserApplication> {
  const { playfield, uiLayer } = createShell(mount)
  const search = new URLSearchParams(dependencies.locationSearch ?? window.location.search)
  const debugEnabled = search.has('debug')
  const benchmarkEnabled = search.has('collisionBenchmark')
  const performanceSoakSecondsRaw = search.get('performanceSoakSeconds')
  const performanceSoakSeconds = performanceSoakSecondsRaw === null
    ? undefined
    : Number(performanceSoakSecondsRaw)
  if (
    performanceSoakSeconds !== undefined &&
    (!Number.isFinite(performanceSoakSeconds) || performanceSoakSeconds <= 0 || performanceSoakSeconds > 3_600)
  ) {
    throw new RangeError('Performance soak duration must be between 0 and 3600 seconds')
  }
  const performanceBenchmarkEnabled = search.has('performanceBenchmark') ||
    performanceSoakSeconds !== undefined
  const profileValidation = validateGameDesignProfile(
    dependencies.profile ?? PROTOTYPE_V1_PROFILE,
  )
  const profile = profileValidation.ok
    ? profileValidation.profile
    : PROTOTYPE_V1_PROFILE
  const configuration = parseMatchConfiguration(dependencies.configuration ?? DEFAULT_MATCH_CONFIGURATION, profile)
  const arena = dependencies.arena ?? requireProceduralArena({ size: configuration.arenaSize, seed: configuration.arenaSeed, profile })
  const presentationProfileValidation = validatePresentationProfile(
    dependencies.presentationProfile ?? PRESENTATION_BALANCED_V1_PROFILE,
  )
  const presentationProfile = presentationProfileValidation.ok
    ? presentationProfileValidation.profile
    : PRESENTATION_BALANCED_V1_PROFILE
  const projector = new GameplayUiProjector({
    profile,
    ticksPerSecond: SIMULATION_TICKS_PER_SECOND,
  })
  const createCollision = dependencies.createCollisionWorld ?? createRapierCollisionWorld
  const createRenderer = dependencies.createRenderer ?? createFirstPersonRenderer
  let activePerformancePhase: ActivePerformancePhase
  const benchmarkFrames: ReturnType<typeof createActionFrame>[] = []
  const benchmarkCheckpoints: PlayableSimulationSnapshot[] = []
  const createInput = dependencies.createInput ?? ((options) => (
    performanceBenchmarkEnabled
      ? createBenchmarkInputAdapter(() => activePerformancePhase)
      : new KeyboardMouseAdapter(options)
  ))
  const createPointerController = dependencies.createPointerLockController ??
    ((options) => new PointerLockController(options))
  const createUi = dependencies.createUi ?? createGameplayUi
  const initializeBackend = dependencies.initializeBackend ?? initializeCollisionBackend
  const runBenchmark = dependencies.runBenchmark ?? runCollisionBrowserBenchmark
  const now = dependencies.now ?? (() => performance.now())
  const wait = dependencies.wait ?? ((milliseconds) => new Promise<void>((resolve) => {
    window.setTimeout(resolve, milliseconds)
  }))
  const performancePhaseDurationMilliseconds =
    dependencies.performancePhaseDurationMilliseconds ?? 2_000
  if (
    !Number.isFinite(performancePhaseDurationMilliseconds) ||
    performancePhaseDurationMilliseconds <= 0
  ) {
    throw new RangeError('Performance phase duration must be greater than zero')
  }
  const performanceProbe = performanceBenchmarkEnabled
    ? new PresentationPerformanceProbe(now)
    : undefined
  let performanceRecording = false
  const runtimePerformanceRecorder = performanceProbe === undefined ? undefined : {
    recordAnimationCallback: (durationMilliseconds: number) => {
      if (performanceRecording) performanceProbe.recordAnimationCallback(durationMilliseconds)
    },
    recordSimulationStep: (durationMilliseconds: number) => {
      if (performanceRecording) performanceProbe.recordSimulationStep(durationMilliseconds)
    },
    recordHudPublication: () => {
      if (performanceRecording) performanceProbe.recordHudPublication()
    },
  }

  const initialState = createInitialPlayableSimulationSnapshot({
    rngSeed: configuration.arenaSeed,
    profile,
    scenario: COMBAT_SLICE_SCENARIO_V1,
    collision: RUNTIME_COLLISION_COMPATIBILITY_IDENTITY,
    arena,
    configuration,
  })

  let disposed = false
  let hasEverLocked = false
  let pointerState: Readonly<PointerLockLifecycleSnapshot> = Object.freeze({ status: 'paused' })
  let collisionState: CollisionUiState = {
    status: 'loading',
    identity: collisionIdentityLabel(),
    fixtureId: arena.id,
    fixtureStatus: 'pending',
  }
  let collisionWorld: RuntimeCollisionWorld | undefined
  let renderer: FirstPersonRenderer | undefined
  let input: RuntimeInputAdapter | undefined
  let pointerController: RuntimePointerLockController | undefined
  let runtime: BrowserRuntime<PlayableSimulationSnapshot, GameplayUiSnapshot> | undefined
  let scoreboardVisible = false

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
    const player = localParticipant(state)
    return {
      tick: state.tick,
      stateHash: playableSimulationSnapshotHash(state, configuration),
      profileIdentity: `${state.profile.id}@${state.profile.revision} · ${state.profile.contentHash}`,
      arenaIdentity: `${state.arena.id} · ${state.arena.contentHash}`,
      collisionIdentity: collisionDebugLabel(),
      scenarioIdentity: `${state.scenario.id} · ${state.scenario.contentHash}`,
      configurationIdentity: `${state.match.configuration.id} · ${state.match.configuration.contentHash}`,
      matchPhase: state.match.phase,
      remainingTicks: state.match.remainingTicks,
      playerPosition: player.position,
      playerYawDegrees: player.yaw * (180 / Math.PI),
      playerPitchDegrees: player.pitch * (180 / Math.PI),
      playerGrounded: player.grounded,
      ammo: player.weapon.ammo,
      targets: stationaryParticipants(state).map((target) => ({
        semanticId: target.semanticId,
        health: target.health,
        active: target.alive,
      })),
      participantStatistics: state.participants.map(({ semanticId, statistics }) => ({ semanticId, kills: statistics.kills, assists: statistics.assists, deaths: statistics.deaths, score: statistics.score })),
    }
  }

  function projectUi(
    state: PlayableSimulationSnapshot,
    status = uiStatus(),
    message?: string,
  ): Readonly<GameplayUiSnapshot> {
    return projector.project(state, status, debugSnapshot(state), message, scoreboardVisible)
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
    onRepeat: dependencies.onRepeat,
    onMainMenu: dependencies.onMainMenu,
    onToggleFullscreen: () => {
      if (document.fullscreenElement === null) void mount.requestFullscreen?.()
      else void document.exitFullscreen?.()
    },
  })
  const arenaBadge = document.createElement('output')
  arenaBadge.className = 'arena-session-badge'
  arenaBadge.setAttribute('aria-label', 'Текущая арена')
  arenaBadge.textContent = `${arena.size.toUpperCase()} · SEED ${arena.seed} · ${arena.contentHash.slice(-8)}`
  mount.append(arenaBadge)
  ui.update(projectUi(initialState, 'start'))

  const onPageHide = () => {
    if (runtime !== undefined) runtime.dispose()
    else cleanup()
  }
  const onFullscreenChange = () => {
    if (document.fullscreenElement == null && pointerState.status === 'locked') pointerController?.pause()
  }

  function cleanup(): void {
    if (disposed) return
    disposed = true
    window.removeEventListener('pagehide', onPageHide)
    document.removeEventListener('fullscreenchange', onFullscreenChange)
    pointerController?.dispose()
    input?.dispose()
    collisionWorld?.dispose()
    renderer?.dispose()
    ui.dispose()
    mount.replaceChildren()
  }
  window.addEventListener('pagehide', onPageHide, { once: true })
  document.addEventListener('fullscreenchange', onFullscreenChange)

  if (!profileValidation.ok) {
    ui.update(projectUi(
      initialState,
      'error',
      `Profile validation failed (${profileValidation.errors.map((error) => error.code).join(', ')})`,
    ))
    return { requestControl, dispose: cleanup }
  }
  if (!presentationProfileValidation.ok) {
    ui.update(projectUi(
      initialState,
      'error',
      `Presentation profile validation failed (${presentationProfileValidation.errors.map((error) => error.code).join(', ')})`,
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
    await validateArenaPhysicalRoutes(arena,profile)
    collisionWorld = await createCollision(
      arena,
      livingParticipantCollisionCapsules(initialState, profile),
      controllerConfiguration(profile),
    )
    if (disposed) {
      collisionWorld.dispose()
      return { requestControl, dispose: cleanup }
    }
    collisionState = {
      status: 'ready',
      identity: collisionIdentityLabel(),
      fixtureId: arena.id,
      fixtureStatus: 'passed',
      checkpointHash: collisionCheckpointHash(collisionWorld.checkpoint(0)),
      initLatencyMilliseconds: { cold, warm },
    }
  } catch (error) {
    const code = errorCode(error)
    collisionState = {
      status: 'error',
      identity: collisionIdentityLabel(),
      fixtureId: arena.id,
      fixtureStatus: 'failed',
      errorCode: code,
    }
    ui.update(projectUi(initialState, 'error', `Collision initialization failed (${code})`))
    return { requestControl, dispose: cleanup }
  }

  try {
    // Test and diagnostic renderers own no GLB scene objects; loading shipping
    // assets for them would make their deterministic lifecycle tests depend on fetch.
    const arenaGlbAssets = dependencies.prepareArenaGlbAssets === undefined && dependencies.createRenderer !== undefined
      ? undefined
      : await (dependencies.prepareArenaGlbAssets ?? prepareArenaGlbAssets)()
    if (disposed) { arenaGlbAssets?.dispose(); return { requestControl, dispose: cleanup } }
    renderer = createRenderer({
      container: playfield,
      arena,
      scenario: COMBAT_SLICE_SCENARIO_V1,
      profile,
      pixelBudget: presentationProfile.render.backingBufferPixelBudget * Math.pow(dependencies.graphicsQuality?.renderScale ?? 1, 2),
      graphicsQuality: dependencies.graphicsQuality,
      presentationProfile,
      onWebGlSubmission: (rendererInfo) => {
        if (performanceRecording) {
          performanceProbe?.recordWebGlSubmission(rendererInfo)
        }
      },
      ticksPerSecond: SIMULATION_TICKS_PER_SECOND,
      arenaGlbAssets,
    })
    playfield.dataset.presentationProfileId = presentationProfile.id
    playfield.dataset.presentationProfileHash = presentationProfile.contentHash
    renderer.render(initialState)
  } catch (error) {
    const message = error instanceof Error && (error.name === 'WebGlUnavailableError' || error.name === 'ArenaGlbAssetError')
      ? error.message : 'Неизвестная ошибка инициализации 3D-сцены'
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
    uiUpdateIntervalSeconds: 1 / presentationProfile.ui.maximumPublicationsPerSecond,
    performanceRecorder: runtimePerformanceRecorder,
    now,
    step: (state) => {
      const frame = input!.drainActionFrame(state.tick + 1)
      if (performanceBenchmarkEnabled) benchmarkFrames.push(frame)
      const uiActions = input!.drainUiActionSnapshot?.()
      const next = stepPlayableSimulation(state, frame, collisionWorld!, profile, configuration, arena)
      if (performanceBenchmarkEnabled && [1, 2, 10, 60, 600].includes(next.tick)) benchmarkCheckpoints.push(next)
      if (uiActions !== undefined && uiActions.scoreboardHeld !== scoreboardVisible) {
        scoreboardVisible = uiActions.scoreboardHeld
        ui.update(projectUi(next))
      }
      if (uiActions?.pauseRequested) pointerController?.pause()
      if (next.match.phase === 'finished') {
        queueMicrotask(() => {
          if (disposed || runtime === undefined) return
          runtime.stop()
          input?.clearInputState()
          scoreboardVisible = false
          document.exitPointerLock?.()
          ui.update(projectUi(runtime.getState(), 'results'))
        })
      }
      return next
    },
    render: (state) => renderer?.render(state),
    snapshotForUi: (state) => projectUi(state),
    publishUiSnapshot: ui.update,
    disposePresentation: cleanup,
  })

  pointerController = createPointerController({
    lockElement: playfield,
    clearInputState: () => { input?.clearInputState(); scoreboardVisible = false },
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

  if (performanceBenchmarkEnabled && performanceProbe !== undefined) {
    const badge = document.createElement('output')
    badge.className = 'performance-benchmark-badge'
    badge.setAttribute('aria-label', 'Performance benchmark status')
    badge.textContent = 'PERFORMANCE · RUNNING'
    mount.append(badge)
    playfield.dataset.performanceBenchmarkStatus = 'running'

    const runPhase = async (
      phase: PerformancePhaseId,
    ): Promise<PerformancePhaseMeasurement | undefined> => {
      if (disposed || runtime === undefined) return undefined
      const settled = phase === 'menu' || phase === 'paused' || phase === 'results'
      activePerformancePhase = settled ? undefined : phase
      scoreboardVisible = phase === 'live-scoreboard'
      if (!settled) {
        pointerState = Object.freeze({ status: 'locked' })
        ui.update(projectUi(runtime.getState(), 'running'))
        runtime.start()
      }

      // Initial ResizeObserver delivery and the first RAF wall-clock anchor are
      // warm-up work, not part of the settled reference window.
      await wait(500)
      if (disposed || runtime === undefined) return undefined

      performanceProbe.startPhase(phase)
      performanceRecording = true
      performanceProbe.markMetricUnavailable('long-tasks')
      performanceProbe.markMetricUnavailable('thermal')

      const memory = performance as Performance & {
        readonly memory?: { readonly usedJSHeapSize?: number }
      }
      const initialHeapBytes = memory.memory?.usedJSHeapSize
      if (typeof initialHeapBytes === 'number') {
        performanceProbe.recordHeapSample(initialHeapBytes)
      } else {
        performanceProbe.markMetricUnavailable('heap')
      }

      await wait(
        settled
          ? 1_000
          : performanceSoakSeconds === undefined
            ? performancePhaseDurationMilliseconds
            : performanceSoakSeconds * 1_000,
      )
      if (disposed || runtime === undefined) return undefined

      if (!settled) runtime.stop()
      scoreboardVisible = false
      const finalHeapBytes = memory.memory?.usedJSHeapSize
      if (typeof finalHeapBytes === 'number') performanceProbe.recordHeapSample(finalHeapBytes)
      performanceRecording = false
      activePerformancePhase = undefined
      return performanceProbe.finishPhase()
    }

    void (async () => {
      const measurements: PerformancePhaseMeasurement[] = []
      const phases: readonly PerformancePhaseId[] = performanceSoakSeconds === undefined
        ? ['menu', 'paused', 'running-idle', 'live-scoreboard', 'movement-jump', 'combat-burst', 'overtime', 'results']
        : ['combat-burst']
      if (performanceSoakSeconds !== undefined) {
        badge.textContent = `PERFORMANCE · SOAK ${performanceSoakSeconds}s`
      }
      for (const phase of phases) {
        const measurement = await runPhase(phase)
        if (measurement === undefined) return
        measurements.push(measurement)
      }
      const gates = measurements.map((measurement) => evaluateHardPerformanceGate(measurement))
      const canvas = playfield.querySelector('canvas')
      const effectiveGraphicsQuality = dependencies.graphicsQuality ?? GRAPHICS_QUALITY_V1.balanced
      const report: BrowserPerformanceReport = Object.freeze({
        schemaVersion: 1,
        referenceId: PERFORMANCE_REFERENCE_ID,
        mode: performanceSoakSeconds === undefined ? 'reference' : 'thermal-soak',
        passed: gates.every((gate) => gate.passed),
        environment: Object.freeze({
          userAgent: navigator.userAgent,
          viewportCssWidth: playfield.clientWidth,
          viewportCssHeight: playfield.clientHeight,
          devicePixelRatio: window.devicePixelRatio || 1,
          backingBufferWidth: canvas?.width ?? 0,
          backingBufferHeight: canvas?.height ?? 0,
          presentationProfileId: presentationProfile.id,
          presentationProfileHash: presentationProfile.contentHash,
          graphicsQualityId: effectiveGraphicsQuality.id,
          localLightShadowBudget: resolveLocalLightShadowBudget(effectiveGraphicsQuality, presentationProfile),
          arenaSize: arena.size,
          arenaSeed: arena.seed,
          arenaContentHash: arena.contentHash,
        }),
        measurements: Object.freeze(measurements),
        gates: Object.freeze(gates),
      })
      const serializedReport = JSON.stringify(report)
      const reportWindow = window as typeof window & {
        __STAR_TOURNAMENT_PERFORMANCE_REPORT__?: BrowserPerformanceReport
      }
      reportWindow.__STAR_TOURNAMENT_PERFORMANCE_REPORT__ = report
      playfield.dataset.performanceBenchmarkStatus = report.passed ? 'passed' : 'failed'
      playfield.dataset.performanceBenchmarkReport = serializedReport
      playfield.dataset.aiReplay = JSON.stringify(createPlayableSimulationReplay(initialState, benchmarkFrames, configuration, arena))
      playfield.dataset.aiFinalHash = playableSimulationSnapshotHash(runtime!.getState(), configuration)
      playfield.dataset.aiFinalSnapshot = JSON.stringify(runtime!.getState())
      playfield.dataset.aiCheckpoints = JSON.stringify(benchmarkCheckpoints)
      badge.textContent = report.passed ? 'PERFORMANCE · PASS' : 'PERFORMANCE · FAIL'
      badge.dataset.status = report.passed ? 'passed' : 'failed'
      pointerState = Object.freeze({ status: 'paused' })
      ui.update(projectUi(runtime.getState(), 'paused'))
    })().catch((error: unknown) => {
      performanceRecording = false
      activePerformancePhase = undefined
      playfield.dataset.performanceBenchmarkStatus = 'failed'
      playfield.dataset.performanceBenchmarkError = error instanceof Error
        ? error.message
        : String(error)
      badge.textContent = 'PERFORMANCE · ERROR'
      badge.dataset.status = 'failed'
    })
  }

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
