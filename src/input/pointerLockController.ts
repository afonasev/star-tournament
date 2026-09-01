export type PointerLockLifecycleStatus =
  | 'paused'
  | 'requesting'
  | 'locked'
  | 'error'
  | 'disposed'

export interface PointerLockLifecycleSnapshot {
  readonly status: PointerLockLifecycleStatus
  readonly errorMessage?: string
}

export interface PointerLockControllerOptions {
  readonly lockElement: HTMLElement
  readonly clearInputState: () => void
  readonly startRuntime: () => void
  readonly stopRuntime: () => void
  readonly onStateChange?: (snapshot: Readonly<PointerLockLifecycleSnapshot>) => void
  readonly onRequestFailure?: (error: Error) => void
  readonly document?: Document
  readonly window?: Window
}

export class PointerLockRequestError extends Error {
  constructor(cause?: unknown) {
    super('Не удалось получить управление мышью. Кликните, чтобы попробовать снова.', { cause })
    this.name = 'PointerLockRequestError'
  }
}

// Embedded-browser pointer lock can commit just after emitting a stale error.
// This short technical settle window is lifecycle plumbing, not gameplay time.
export const POINTER_LOCK_ERROR_SETTLE_MILLISECONDS = 50

function freezeSnapshot(
  status: PointerLockLifecycleStatus,
  errorMessage?: string,
): Readonly<PointerLockLifecycleSnapshot> {
  return Object.freeze(errorMessage === undefined ? { status } : { status, errorMessage })
}

/**
 * Coordinates explicit click-to-lock requests with runtime pause/resume.
 * Pointer-lock state, not an Escape key event, is the lifecycle authority.
 */
export class PointerLockController {
  private readonly document: Document
  private readonly window: Window
  private snapshot: Readonly<PointerLockLifecycleSnapshot> = freezeSnapshot('paused')
  private requestInFlight: Promise<boolean> | null = null
  private failureSettleInFlight: Promise<boolean> | null = null
  private disposed = false

  constructor(private readonly options: PointerLockControllerOptions) {
    this.document = options.document ?? document
    this.window = options.window ?? window

    this.document.addEventListener('pointerlockchange', this.onPointerLockChange)
    this.document.addEventListener('pointerlockerror', this.onPointerLockError)
    this.document.addEventListener('visibilitychange', this.onVisibilityChange)
    this.window.addEventListener('blur', this.onWindowBlur)

    // Browser runtime must not advance before the first explicit lock request.
    this.options.clearInputState()
    this.options.stopRuntime()
    this.publish('paused')
  }

  getSnapshot(): Readonly<PointerLockLifecycleSnapshot> {
    return this.snapshot
  }

  isLocked(): boolean {
    return !this.disposed && this.document.pointerLockElement === this.options.lockElement
  }

  requestStart(): Promise<boolean> {
    return this.requestPointerLock()
  }

  requestResume(): Promise<boolean> {
    return this.requestPointerLock()
  }

  dispose(): void {
    if (this.disposed) return
    this.disposed = true
    this.document.removeEventListener('pointerlockchange', this.onPointerLockChange)
    this.document.removeEventListener('pointerlockerror', this.onPointerLockError)
    this.document.removeEventListener('visibilitychange', this.onVisibilityChange)
    this.window.removeEventListener('blur', this.onWindowBlur)
    this.options.clearInputState()
    this.options.stopRuntime()
    this.publish('disposed')
  }

  private requestPointerLock(): Promise<boolean> {
    if (this.disposed) return Promise.resolve(false)
    if (this.isLocked()) {
      this.activateLockedRuntime()
      return Promise.resolve(true)
    }
    if (this.requestInFlight) return this.requestInFlight

    this.options.clearInputState()
    this.options.stopRuntime()
    this.publish('requesting')

    this.requestInFlight = this.performPointerLockRequest()
    return this.requestInFlight
  }

  private async performPointerLockRequest(): Promise<boolean> {
    try {
      await this.options.lockElement.requestPointerLock()
      if (this.snapshot.status === 'error') return false
      if (this.isLocked()) this.activateLockedRuntime()
      return true
    } catch (cause) {
      return this.settlePointerLockFailure(cause)
    } finally {
      this.requestInFlight = null
    }
  }

  private readonly onPointerLockChange = (): void => {
    if (this.disposed) return
    if (this.isLocked()) {
      this.activateLockedRuntime()
    } else {
      if (this.snapshot.status === 'paused' || this.snapshot.status === 'error') return
      this.pauseRuntime()
    }
  }

  private readonly onPointerLockError = (): void => {
    if (this.disposed) return
    void this.settlePointerLockFailure()
  }

  private readonly onWindowBlur = (): void => {
    if (!this.disposed) this.pauseForFocusLoss()
  }

  private readonly onVisibilityChange = (): void => {
    if (!this.disposed && this.document.hidden) this.pauseForFocusLoss()
  }

  private pauseForFocusLoss(): void {
    this.pauseRuntime()
    if (this.document.pointerLockElement === this.options.lockElement) {
      try {
        this.document.exitPointerLock()
      } catch {
        // Runtime is already safely paused; browser UI owns lock release failure.
      }
    }
  }

  private activateLockedRuntime(): void {
    if (this.disposed || this.snapshot.status === 'locked') return
    this.publish('locked')
    this.options.startRuntime()
  }

  private pauseRuntime(): void {
    if (this.snapshot.status === 'paused') return
    // Ordering is intentional: no held action can leak into the final tick.
    this.options.clearInputState()
    this.options.stopRuntime()
    this.publish('paused')
  }

  private handleRequestFailure(cause?: unknown): void {
    if (this.snapshot.status === 'error') return
    this.options.clearInputState()
    this.options.stopRuntime()
    const error = cause instanceof PointerLockRequestError
      ? cause
      : new PointerLockRequestError(cause)
    this.publish('error', error.message)
    this.options.onRequestFailure?.(error)
  }

  private settlePointerLockFailure(cause?: unknown): Promise<boolean> {
    if (this.failureSettleInFlight !== null) return this.failureSettleInFlight
    const settlement = new Promise<boolean>((resolve) => {
      this.window.setTimeout(() => {
        if (this.disposed) {
          resolve(false)
        } else if (this.isLocked()) {
          this.activateLockedRuntime()
          resolve(true)
        } else {
          this.handleRequestFailure(cause)
          resolve(false)
        }
      }, POINTER_LOCK_ERROR_SETTLE_MILLISECONDS)
    }).finally(() => {
      if (this.failureSettleInFlight === settlement) this.failureSettleInFlight = null
    })
    this.failureSettleInFlight = settlement
    return settlement
  }

  private publish(status: PointerLockLifecycleStatus, errorMessage?: string): void {
    this.snapshot = freezeSnapshot(status, errorMessage)
    this.options.onStateChange?.(this.snapshot)
  }
}
