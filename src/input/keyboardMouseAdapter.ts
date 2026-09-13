import {
  createActionFrame,
  type ActionFrame,
  type CanonicalActionId,
  type NormalizedAction,
} from './actionFrame'

export const LOCAL_KEYBOARD_MOUSE_PARTICIPANT_ID = 'local-keyboard-mouse' as const

export const KEYBOARD_MOUSE_ACTION_IDS = Object.freeze({
  moveForward: 'move-forward',
  moveRight: 'move-right',
  lookYaw: 'look-yaw',
  lookPitch: 'look-pitch',
  jump: 'jump',
  fire: 'fire',
} as const satisfies Record<string, CanonicalActionId>)

export interface KeyboardMouseAdapterOptions {
  readonly pointerLockElement: Element
  readonly participantId?: string
  readonly document?: Document
  readonly window?: Window
}

export interface UiActionSnapshot {
  readonly scoreboardHeld: boolean
  readonly pauseRequested: boolean
  readonly scoreboardHeldSeatIds?: readonly string[]
}

type HeldKey = 'KeyW' | 'KeyA' | 'KeyS' | 'KeyD' | 'Space'

const HELD_KEYS = new Set<string>(['KeyW', 'KeyA', 'KeyS', 'KeyD', 'Space'])

function axis(positive: boolean, negative: boolean): number {
  return Number(positive) - Number(negative)
}

function normalizedAxis(value: number): number {
  return Math.max(-1, Math.min(1, value))
}

/**
 * Owns the physical keyboard/mouse state for the provisional single local seat.
 * It never reads or mutates simulation state and emits only serializable frames.
 */
export class KeyboardMouseAdapter {
  private readonly seatId: string
  private readonly document: Document
  private readonly window: Window
  private readonly heldKeys = new Set<HeldKey>()
  private accumulatedMouseX = 0
  private accumulatedMouseY = 0
  private primaryFireHeld = false
  private scoreboardHeld = false
  private pauseRequested = false
  private disposed = false

  constructor(private readonly options: KeyboardMouseAdapterOptions) {
    this.seatId = options.participantId ?? LOCAL_KEYBOARD_MOUSE_PARTICIPANT_ID
    if (this.seatId.length === 0) {
      throw new TypeError('KeyboardMouseAdapter participantId must be non-empty')
    }
    this.document = options.document ?? document
    this.window = options.window ?? window

    this.window.addEventListener('keydown', this.onKeyDown)
    this.window.addEventListener('keyup', this.onKeyUp)
    this.window.addEventListener('mousedown', this.onMouseDown)
    this.window.addEventListener('mouseup', this.onMouseUp)
    this.window.addEventListener('mousemove', this.onMouseMove)
    this.window.addEventListener('blur', this.onBlur)
    this.document.addEventListener('visibilitychange', this.onVisibilityChange)
    this.document.addEventListener('pointerlockchange', this.onPointerLockChange)
  }
  get participantId(): string { return this.seatId }

  isPointerLocked(): boolean {
    return !this.disposed && this.document.pointerLockElement === this.options.pointerLockElement
  }

  /**
   * Returns the complete action state for one simulation tick. Look deltas are
   * consumed atomically; held movement/jump/fire state remains until released.
   */
  drainActionFrame(tick: number): ActionFrame {
    const locked = this.isPointerLocked()
    const moveForward = locked
      ? axis(this.heldKeys.has('KeyW'), this.heldKeys.has('KeyS'))
      : 0
    const moveRight = locked
      ? axis(this.heldKeys.has('KeyD'), this.heldKeys.has('KeyA'))
      : 0
    const lookYaw = locked ? normalizedAxis(this.accumulatedMouseX) : 0
    // Browser movementY grows down-screen; simulation pitch grows upward.
    const lookPitch = locked && this.accumulatedMouseY !== 0
      ? normalizedAxis(-this.accumulatedMouseY)
      : 0
    const jump = locked && this.heldKeys.has('Space') ? 1 : 0
    const fire = locked && this.primaryFireHeld ? 1 : 0

    this.accumulatedMouseX = 0
    this.accumulatedMouseY = 0

    const actions: NormalizedAction[] = [
      { participantId: this.seatId, actionId: KEYBOARD_MOUSE_ACTION_IDS.moveForward, value: moveForward },
      { participantId: this.seatId, actionId: KEYBOARD_MOUSE_ACTION_IDS.moveRight, value: moveRight },
      { participantId: this.seatId, actionId: KEYBOARD_MOUSE_ACTION_IDS.lookYaw, value: lookYaw },
      { participantId: this.seatId, actionId: KEYBOARD_MOUSE_ACTION_IDS.lookPitch, value: lookPitch },
      { participantId: this.seatId, actionId: KEYBOARD_MOUSE_ACTION_IDS.jump, value: jump },
      { participantId: this.seatId, actionId: KEYBOARD_MOUSE_ACTION_IDS.fire, value: fire },
    ]
    return createActionFrame(tick, actions)
  }

  /** Returns local-only UI intent without entering deterministic gameplay frames. */
  drainUiActionSnapshot(): Readonly<UiActionSnapshot> {
    const snapshot = Object.freeze({
      scoreboardHeld: this.isPointerLocked() && this.scoreboardHeld,
      pauseRequested: this.pauseRequested,
    })
    this.pauseRequested = false
    return snapshot
  }

  /** Clears every held and accumulated action in one synchronous operation. */
  clearInputState(): void {
    this.heldKeys.clear()
    this.accumulatedMouseX = 0
    this.accumulatedMouseY = 0
    this.primaryFireHeld = false
    this.scoreboardHeld = false
    this.pauseRequested = false
  }

  dispose(): void {
    if (this.disposed) return
    this.disposed = true
    this.clearInputState()
    this.window.removeEventListener('keydown', this.onKeyDown)
    this.window.removeEventListener('keyup', this.onKeyUp)
    this.window.removeEventListener('mousedown', this.onMouseDown)
    this.window.removeEventListener('mouseup', this.onMouseUp)
    this.window.removeEventListener('mousemove', this.onMouseMove)
    this.window.removeEventListener('blur', this.onBlur)
    this.document.removeEventListener('visibilitychange', this.onVisibilityChange)
    this.document.removeEventListener('pointerlockchange', this.onPointerLockChange)
  }

  private readonly onKeyDown = (event: KeyboardEvent): void => {
    if (!this.isPointerLocked()) return
    if (event.code === 'Tab') {
      event.preventDefault()
      this.scoreboardHeld = true
      return
    }
    if (event.code === 'Escape') {
      if (!event.repeat) this.pauseRequested = true
      return
    }
    if (!HELD_KEYS.has(event.code)) return
    event.preventDefault()
    this.heldKeys.add(event.code as HeldKey)
  }

  private readonly onKeyUp = (event: KeyboardEvent): void => {
    if (event.code === 'Tab') {
      if (this.isPointerLocked()) event.preventDefault()
      this.scoreboardHeld = false
      return
    }
    if (!HELD_KEYS.has(event.code)) return
    if (this.isPointerLocked()) event.preventDefault()
    this.heldKeys.delete(event.code as HeldKey)
  }

  private readonly onMouseDown = (event: MouseEvent): void => {
    if (!this.isPointerLocked() || event.button !== 0) return
    event.preventDefault()
    this.primaryFireHeld = true
  }

  private readonly onMouseUp = (event: MouseEvent): void => {
    if (event.button !== 0) return
    if (this.isPointerLocked()) event.preventDefault()
    this.primaryFireHeld = false
  }

  private readonly onMouseMove = (event: MouseEvent): void => {
    if (!this.isPointerLocked()) return
    this.accumulatedMouseX += event.movementX
    this.accumulatedMouseY += event.movementY
  }

  private readonly onBlur = (): void => {
    this.clearInputState()
  }

  private readonly onVisibilityChange = (): void => {
    if (this.document.hidden) this.clearInputState()
  }

  private readonly onPointerLockChange = (): void => {
    if (!this.isPointerLocked()) this.clearInputState()
  }
}
