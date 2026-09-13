import { describe, expect, it } from 'vitest'
import { createActionFrame } from './actionFrame'
import { InputCoordinator, type LocalInputAdapter } from './inputCoordinator'

function adapter(participantId: string, value: number): LocalInputAdapter {
  return {
    participantId,
    drainActionFrame: (tick) => createActionFrame(tick, [{ participantId, actionId: 'move-forward', value }]),
    drainUiActionSnapshot: () => ({ scoreboardHeld: participantId === 'b', pauseRequested: participantId === 'a' }),
    clearInputState: () => undefined,
    dispose: () => undefined,
  }
}

describe('InputCoordinator', () => {
  it('combines independently normalized frames into one ordered deterministic tick', () => {
    const coordinator = new InputCoordinator([adapter('b', -1), adapter('a', 1)])
    expect(coordinator.drainActionFrame(4).actions).toEqual([
      { participantId: 'a', actionId: 'move-forward', value: 1 },
      { participantId: 'b', actionId: 'move-forward', value: -1 },
    ])
    expect(coordinator.drainUiActionSnapshot()).toEqual({ scoreboardHeld: true, scoreboardHeldSeatIds: ['b'], pauseRequested: true })
  })
})
