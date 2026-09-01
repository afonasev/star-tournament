import { describe, expect, it } from 'vitest'

import {
  ACTION_FRAME_SCHEMA_VERSION,
  CANONICAL_ACTION_DESCRIPTORS,
  CANONICAL_ACTION_IDS,
  computeActionFrameHash,
  createActionFrame,
  parseActionFrame,
  serializeActionFrame,
} from './actionFrame'

describe('ActionFrame', () => {
  it('publishes the exact immutable canonical action registry and ranges', () => {
    expect(ACTION_FRAME_SCHEMA_VERSION).toBe(2)
    expect(CANONICAL_ACTION_IDS).toEqual([
      'move-forward',
      'move-right',
      'look-yaw',
      'look-pitch',
      'jump',
      'fire',
    ])
    expect(CANONICAL_ACTION_DESCRIPTORS).toEqual([
      { id: 'move-forward', minimum: -1, maximum: 1, valueKind: 'axis' },
      { id: 'move-right', minimum: -1, maximum: 1, valueKind: 'axis' },
      { id: 'look-yaw', minimum: -1, maximum: 1, valueKind: 'axis' },
      { id: 'look-pitch', minimum: -1, maximum: 1, valueKind: 'axis' },
      { id: 'jump', minimum: 0, maximum: 1, valueKind: 'pressed' },
      { id: 'fire', minimum: 0, maximum: 1, valueKind: 'pressed' },
    ])
    expect(Object.isFrozen(CANONICAL_ACTION_IDS)).toBe(true)
    expect(Object.isFrozen(CANONICAL_ACTION_DESCRIPTORS)).toBe(true)
    expect(CANONICAL_ACTION_DESCRIPTORS.every(Object.isFrozen)).toBe(true)
  })

  it('normalizes actions by participant and stable canonical action order', () => {
    const frame = createActionFrame(4, [
      { participantId: 'seat-b', actionId: 'fire', value: 1 },
      { participantId: 'seat-a', actionId: 'fire', value: 0 },
      { participantId: 'seat-a', actionId: 'move-right', value: 1 },
      { participantId: 'seat-a', actionId: 'look-yaw', value: -0.5 },
      { participantId: 'seat-a', actionId: 'move-forward', value: 0.75 },
    ])

    expect(frame.actions.map(({ participantId, actionId }) => `${participantId}/${actionId}`)).toEqual([
      'seat-a/move-forward',
      'seat-a/move-right',
      'seat-a/look-yaw',
      'seat-a/fire',
      'seat-b/fire',
    ])
    expect(Object.isFrozen(frame)).toBe(true)
    expect(Object.isFrozen(frame.actions)).toBe(true)
  })

  it('preserves pressed state so simulation can detect rising and falling edges', () => {
    const pressed = createActionFrame(1, [
      { participantId: 'local-1', actionId: 'jump', value: 1 },
      { participantId: 'local-1', actionId: 'fire', value: 1 },
    ])
    const held = createActionFrame(2, [
      { participantId: 'local-1', actionId: 'jump', value: 1 },
      { participantId: 'local-1', actionId: 'fire', value: 1 },
    ])
    const released = createActionFrame(3, [
      { participantId: 'local-1', actionId: 'jump', value: 0 },
      { participantId: 'local-1', actionId: 'fire', value: 0 },
    ])

    expect(pressed.actions.map(({ value }) => value)).toEqual([1, 1])
    expect(held.actions.map(({ value }) => value)).toEqual([1, 1])
    expect(released.actions.map(({ value }) => value)).toEqual([0, 0])
  })

  it('round-trips canonical data and preserves its content hash', () => {
    const source = createActionFrame(1, [
      { participantId: 'local-1', actionId: 'move-forward', value: 0.25 },
      { participantId: 'local-1', actionId: 'look-pitch', value: -0.5 },
      { participantId: 'local-1', actionId: 'jump', value: 0 },
    ])
    const serialized = serializeActionFrame(source)
    const restored = parseActionFrame(serialized)

    expect(restored).toEqual(source)
    expect(serializeActionFrame(restored)).toBe(serialized)
    expect(computeActionFrameHash(restored)).toBe(computeActionFrameHash(source))
    expect(computeActionFrameHash(source)).toMatch(/^fnv1a64-v1:[0-9a-f]{16}$/)
    expect(computeActionFrameHash(source)).toBe('fnv1a64-v1:a82ed41c61f2c626')
  })

  it('accepts every declared boundary and canonicalizes negative zero', () => {
    for (const descriptor of CANONICAL_ACTION_DESCRIPTORS) {
      expect(() =>
        createActionFrame(1, [
          { participantId: 'minimum', actionId: descriptor.id, value: descriptor.minimum },
          { participantId: 'maximum', actionId: descriptor.id, value: descriptor.maximum },
        ]),
      ).not.toThrow()
    }
    const frame = createActionFrame(1, [
      { participantId: 'local-1', actionId: 'look-yaw', value: -0 },
    ])
    expect(Object.is(frame.actions[0]?.value, -0)).toBe(false)
  })

  it('rejects invalid versions, unknown keys, values and duplicate stable keys', () => {
    expect(() => parseActionFrame({ schemaVersion: 1, tick: 1, actions: [] })).toThrow(
      /schemaVersion/,
    )
    expect(() =>
      parseActionFrame({ schemaVersion: 2, tick: 1, actions: [], hidden: true }),
    ).toThrow(/exactly/)
    expect(() =>
      parseActionFrame({
        schemaVersion: 2,
        tick: 1,
        actions: [
          { participantId: 'p1', actionId: 'move-forward', value: 1, hidden: true },
        ],
      }),
    ).toThrow(/exactly/)
    expect(() =>
      parseActionFrame({
        schemaVersion: 2,
        tick: 1,
        actions: [{ participantId: 'p1', actionId: 'diagnostic.signal', value: 1 }],
      }),
    ).toThrow(/must be one of/)
    expect(() =>
      createActionFrame(1, [
        { participantId: 'p1', actionId: 'move-right', value: 1 },
        { participantId: 'p1', actionId: 'move-right', value: -1 },
      ]),
    ).toThrow(/duplicate action key/)
    expect(() =>
      createActionFrame(1, [
        { participantId: 'p1', actionId: 'move-forward', value: Number.NaN },
      ]),
    ).toThrow(/finite/)
    expect(() =>
      createActionFrame(1, [
        { participantId: 'p1', actionId: 'look-yaw', value: 1.01 },
      ]),
    ).toThrow(/between -1 and 1/)
    expect(() =>
      createActionFrame(1, [
        { participantId: 'p1', actionId: 'fire', value: 0.5 },
      ]),
    ).toThrow(/pressed state 0 or 1/)
    expect(() => createActionFrame(0, [])).toThrow(/positive safe integer/)
  })
})
