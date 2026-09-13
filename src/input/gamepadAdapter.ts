import { createActionFrame, type ActionFrame, type NormalizedAction } from './actionFrame'
import { KEYBOARD_MOUSE_ACTION_IDS, type UiActionSnapshot } from './keyboardMouseAdapter'
import type { DeviceBindingDescriptor } from '../match'

export interface GamepadAdapterOptions {
  readonly participantId: string
  readonly binding: Extract<DeviceBindingDescriptor, { readonly kind: 'gamepad' }>
  readonly getGamepads?: () => readonly (Gamepad | null)[]
}

function clamp(value: number | undefined): number {
  return Math.max(-1, Math.min(1, value ?? 0))
}

function pressed(gamepad: Gamepad, index: number): boolean {
  return gamepad.buttons[index]?.pressed === true || (gamepad.buttons[index]?.value ?? 0) >= 1
}

/**
 * Translates one explicitly bound browser gamepad into device-neutral frames.
 * It owns no simulation state and refuses to silently substitute another pad.
 */
export class GamepadAdapter {
  private readonly getGamepads: () => readonly (Gamepad | null)[]
  private viewHeld = false
  private previousMenuPressed = false
  private pauseRequested = false
  private unavailable = false

  constructor(private readonly options: GamepadAdapterOptions) {
    if (options.participantId.trim().length === 0) throw new TypeError('GamepadAdapter participantId must be non-empty')
    this.getGamepads = options.getGamepads ?? (() => navigator.getGamepads())
  }

  describeUnavailable(): string { return this.options.participantId }
  get participantId(): string { return this.options.participantId }

  isAvailable(): boolean {
    const gamepad = this.getGamepads()[this.options.binding.connectionSlot]
    return gamepad !== null && gamepad !== undefined && gamepad.connected && gamepad.id === this.options.binding.gamepadId
  }

  drainActionFrame(tick: number): ActionFrame {
    const gamepad = this.getGamepads()[this.options.binding.connectionSlot]
    const available = gamepad !== null && gamepad !== undefined && gamepad.connected && gamepad.id === this.options.binding.gamepadId
    if (!available) {
      this.unavailable = true
      this.clearInputState()
      return createActionFrame(tick, this.actions(0, 0, 0, 0, 0, 0))
    }
    this.unavailable = false
    const view = pressed(gamepad, 8)
    const menu = pressed(gamepad, 9)
    if (menu && !this.previousMenuPressed) this.pauseRequested = true
    this.previousMenuPressed = menu
    this.viewHeld = view
    // Standard mapping: left/right stick, RT (button 7), A (button 0).
    return createActionFrame(tick, this.actions(
      -clamp(gamepad.axes[1]),
      clamp(gamepad.axes[0]),
      clamp(gamepad.axes[2]),
      -clamp(gamepad.axes[3]),
      pressed(gamepad, 0) ? 1 : 0,
      pressed(gamepad, 7) ? 1 : 0,
    ))
  }

  drainUiActionSnapshot(): Readonly<UiActionSnapshot> {
    const snapshot = Object.freeze({ scoreboardHeld: !this.unavailable && this.viewHeld, pauseRequested: this.pauseRequested })
    this.pauseRequested = false
    return snapshot
  }

  clearInputState(): void {
    this.viewHeld = false
    this.previousMenuPressed = false
    this.pauseRequested = false
  }

  dispose(): void { this.clearInputState() }

  private actions(moveForward: number, moveRight: number, lookYaw: number, lookPitch: number, jump: number, fire: number): NormalizedAction[] {
    return [
      { participantId: this.options.participantId, actionId: KEYBOARD_MOUSE_ACTION_IDS.moveForward, value: moveForward },
      { participantId: this.options.participantId, actionId: KEYBOARD_MOUSE_ACTION_IDS.moveRight, value: moveRight },
      { participantId: this.options.participantId, actionId: KEYBOARD_MOUSE_ACTION_IDS.lookYaw, value: lookYaw },
      { participantId: this.options.participantId, actionId: KEYBOARD_MOUSE_ACTION_IDS.lookPitch, value: lookPitch },
      { participantId: this.options.participantId, actionId: KEYBOARD_MOUSE_ACTION_IDS.jump, value: jump },
      { participantId: this.options.participantId, actionId: KEYBOARD_MOUSE_ACTION_IDS.fire, value: fire },
    ]
  }
}
