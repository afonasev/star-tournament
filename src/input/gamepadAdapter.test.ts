import { describe, expect, it } from 'vitest'
import { GamepadAdapter } from './gamepadAdapter'

function pad(overrides: Partial<Gamepad> = {}): Gamepad {
  return {
    id: 'Pad A', index: 1, connected: true, mapping: 'standard', timestamp: 0,
    axes: [0.5, -0.25, -0.75, 0.25],
    buttons: Array.from({ length: 10 }, (_, index) => ({ pressed: index === 0 || index === 7 || index === 8 || index === 9, touched: false, value: index === 0 || index === 7 || index === 8 || index === 9 ? 1 : 0 })),
    vibrationActuator: null,
    hapticActuators: [],
    ...overrides,
  } as unknown as Gamepad
}

describe('GamepadAdapter', () => {
  it('normalizes standard controls without introducing an LB action', () => {
    const adapter = new GamepadAdapter({ participantId: 'local-2', binding: { kind: 'gamepad', gamepadId: 'Pad A', connectionSlot: 1 }, getGamepads: () => [null, pad()] })
    expect(adapter.drainActionFrame(1).actions).toEqual([
      { participantId: 'local-2', actionId: 'move-forward', value: 0.25 },
      { participantId: 'local-2', actionId: 'move-right', value: 0.5 },
      { participantId: 'local-2', actionId: 'look-yaw', value: -0.75 },
      { participantId: 'local-2', actionId: 'look-pitch', value: -0.25 },
      { participantId: 'local-2', actionId: 'jump', value: 1 },
      { participantId: 'local-2', actionId: 'fire', value: 1 },
    ])
    expect(adapter.drainUiActionSnapshot()).toEqual({ scoreboardHeld: true, pauseRequested: true })
    expect(adapter.drainUiActionSnapshot()).toEqual({ scoreboardHeld: true, pauseRequested: false })
  })

  it('clears held input and does not accept a different gamepad after disconnect', () => {
    let pads: readonly (Gamepad | null)[] = [null, pad()]
    const adapter = new GamepadAdapter({ participantId: 'local-2', binding: { kind: 'gamepad', gamepadId: 'Pad A', connectionSlot: 1 }, getGamepads: () => pads })
    adapter.drainActionFrame(1)
    pads = [null, pad({ id: 'Replacement' })]
    expect(adapter.isAvailable()).toBe(false)
    expect(adapter.drainActionFrame(2).actions.every(({ value }) => value === 0)).toBe(true)
    expect(adapter.drainUiActionSnapshot()).toEqual({ scoreboardHeld: false, pauseRequested: false })
  })
})
