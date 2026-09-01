import { deepFreeze } from '../core/deepFreeze'
import { SIMULATION_TICKS_PER_SECOND } from '../simulation'
import type { RuntimeStatus, UiSnapshot } from './uiSnapshot'

// These values protect only the browser scheduler and are deliberately not
// balance controls. The simulation package owns the compatibility tick rate.
export const MAX_WALL_CLOCK_DELTA_SECONDS = 0.25
export const MAX_CATCH_UP_TICKS = 8
export const UI_UPDATE_INTERVAL_SECONDS = 0.1

export interface AnimationScheduler {
  request(callback: FrameRequestCallback): number
  cancel(handle: number): void
}

export interface BrowserRuntimeOptions<State, UiState = UiSnapshot> {
  readonly initialState: State
  readonly step: (state: State, fixedDeltaSeconds: number) => State
  readonly render: (state: State, interpolationAlpha: number) => void
  readonly snapshotForUi: (state: State, status: RuntimeStatus) => UiState
  readonly publishUiSnapshot: (snapshot: Readonly<UiState>) => void
  readonly publishUiAfterStep?: boolean
  readonly scheduler?: AnimationScheduler
  readonly disposePresentation?: () => void
}

const browserAnimationScheduler: AnimationScheduler = {
  request: (callback) => window.requestAnimationFrame(callback),
  cancel: (handle) => window.cancelAnimationFrame(handle),
}

export class BrowserRuntime<State, UiState = UiSnapshot> {
  private readonly scheduler: AnimationScheduler
  private readonly fixedDeltaSeconds = 1 / SIMULATION_TICKS_PER_SECOND
  private state: State
  private status: RuntimeStatus = 'stopped'
  private accumulatorSeconds = 0
  private secondsSinceUiUpdate = 0
  private lastFrameTimeMilliseconds: number | null = null
  private animationHandle: number | null = null
  private disposed = false

  constructor(private readonly options: BrowserRuntimeOptions<State, UiState>) {
    this.state = options.initialState
    this.scheduler = options.scheduler ?? browserAnimationScheduler
  }

  start(): void {
    if (this.disposed) {
      throw new Error('Нельзя запустить уничтоженный browser runtime')
    }
    if (this.status === 'running') return

    this.status = 'running'
    this.lastFrameTimeMilliseconds = null
    this.publishUiSnapshot()
    this.animationHandle = this.scheduler.request(this.onAnimationFrame)
  }

  stop(): void {
    if (this.animationHandle !== null) {
      this.scheduler.cancel(this.animationHandle)
      this.animationHandle = null
    }
    this.lastFrameTimeMilliseconds = null
    if (this.status !== 'stopped') {
      this.status = 'stopped'
      this.publishUiSnapshot()
    }
  }

  dispose(): void {
    if (this.disposed) return
    this.stop()
    this.disposed = true
    this.options.disposePresentation?.()
  }

  getState(): State {
    return this.state
  }

  isRunning(): boolean {
    return this.status === 'running'
  }

  private readonly onAnimationFrame: FrameRequestCallback = (timeMilliseconds) => {
    if (this.status !== 'running' || this.disposed) return
    this.animationHandle = null

    if (this.lastFrameTimeMilliseconds === null) {
      this.lastFrameTimeMilliseconds = timeMilliseconds
    } else {
      const wallClockDeltaSeconds = Math.max(
        0,
        Math.min(
          (timeMilliseconds - this.lastFrameTimeMilliseconds) / 1_000,
          MAX_WALL_CLOCK_DELTA_SECONDS,
        ),
      )
      this.lastFrameTimeMilliseconds = timeMilliseconds
      this.accumulatorSeconds += wallClockDeltaSeconds
      this.secondsSinceUiUpdate += wallClockDeltaSeconds

      let catchUpTicks = 0
      while (
        this.accumulatorSeconds >= this.fixedDeltaSeconds &&
        catchUpTicks < MAX_CATCH_UP_TICKS
      ) {
        this.state = this.options.step(this.state, this.fixedDeltaSeconds)
        this.accumulatorSeconds -= this.fixedDeltaSeconds
        catchUpTicks += 1
      }

      if (this.accumulatorSeconds >= this.fixedDeltaSeconds) {
        this.accumulatorSeconds %= this.fixedDeltaSeconds
      }
      if (catchUpTicks > 0 && this.options.publishUiAfterStep) {
        this.publishUiSnapshot()
      }
    }

    this.options.render(this.state, this.accumulatorSeconds / this.fixedDeltaSeconds)
    if (this.secondsSinceUiUpdate >= UI_UPDATE_INTERVAL_SECONDS) {
      this.secondsSinceUiUpdate %= UI_UPDATE_INTERVAL_SECONDS
      this.publishUiSnapshot()
    }
    if (this.status === 'running' && !this.disposed) {
      this.animationHandle = this.scheduler.request(this.onAnimationFrame)
    }
  }

  private publishUiSnapshot(): void {
    this.options.publishUiSnapshot(
      deepFreeze(this.options.snapshotForUi(this.state, this.status)),
    )
  }
}
