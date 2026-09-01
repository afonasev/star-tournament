import { describe, expect, it } from 'vitest'

import { createActionFrame, parseActionFrame, serializeActionFrame } from './actionFrame'

describe('ActionFrame', () => {
  it('normalizes actions by participant and action key', () => {
    const frame = createActionFrame(4, [
      { participantId: 'seat-b', actionId: 'move.z', value: -1 },
      { participantId: 'seat-a', actionId: 'move.z', value: 0.5 },
      { participantId: 'seat-a', actionId: 'move.x', value: 1 },
    ])

    expect(frame.actions.map(({ participantId, actionId }) => `${participantId}/${actionId}`)).toEqual([
      'seat-a/move.x',
      'seat-a/move.z',
      'seat-b/move.z',
    ])
    expect(Object.isFrozen(frame)).toBe(true)
    expect(Object.isFrozen(frame.actions)).toBe(true)
  })

  it('round-trips through the versioned parser', () => {
    const source = createActionFrame(1, [
      { participantId: 'local-1', actionId: 'diagnostic.signal', value: 0.25 },
    ])
    expect(parseActionFrame(serializeActionFrame(source))).toEqual(source)
  })

  it('rejects invalid versions, values, and duplicate stable keys', () => {
    expect(() => parseActionFrame({ schemaVersion: 2, tick: 1, actions: [] })).toThrow(
      /schemaVersion/,
    )
    expect(() =>
      createActionFrame(1, [
        { participantId: 'p1', actionId: 'move.x', value: 1 },
        { participantId: 'p1', actionId: 'move.x', value: -1 },
      ]),
    ).toThrow(/duplicate action key/)
    expect(() =>
      createActionFrame(1, [{ participantId: 'p1', actionId: 'move.x', value: Number.NaN }]),
    ).toThrow(/finite/)
    expect(() => createActionFrame(0, [])).toThrow(/positive safe integer/)
  })
})
