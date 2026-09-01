// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it } from 'vitest'

import { parseActionFrame } from './actionFrame'
import {
  KeyboardMouseAdapter,
  KEYBOARD_MOUSE_ACTION_IDS,
  LOCAL_KEYBOARD_MOUSE_PARTICIPANT_ID,
} from './keyboardMouseAdapter'

function setPointerLockElement(value: Element | null): void {
  Object.defineProperty(document, 'pointerLockElement', {
    configurable: true,
    value,
  })
}

function setDocumentHidden(value: boolean): void {
  Object.defineProperty(document, 'hidden', {
    configurable: true,
    value,
  })
}

function valuesByAction(adapter: KeyboardMouseAdapter, tick: number): Record<string, number> {
  const frame = adapter.drainActionFrame(tick)
  return Object.fromEntries(frame.actions.map((action) => [action.actionId, action.value]))
}

describe('KeyboardMouseAdapter', () => {
  let lockElement: HTMLElement
  let adapter: KeyboardMouseAdapter

  beforeEach(() => {
    lockElement = document.createElement('canvas')
    document.body.append(lockElement)
    setPointerLockElement(lockElement)
    setDocumentHidden(false)
    adapter = new KeyboardMouseAdapter({ pointerLockElement: lockElement })
  })

  afterEach(() => {
    adapter.dispose()
    setPointerLockElement(null)
    setDocumentHidden(false)
    document.body.replaceChildren()
  })

  it('emits one complete canonical single-seat frame', () => {
    window.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyW', repeat: false }))
    window.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyD', repeat: false }))
    window.dispatchEvent(new KeyboardEvent('keydown', { code: 'Space', repeat: false }))
    window.dispatchEvent(new MouseEvent('mousedown', { button: 0 }))
    window.dispatchEvent(new MouseEvent('mousemove', { movementX: 12, movementY: -7 }))

    const frame = adapter.drainActionFrame(9)

    expect(parseActionFrame(frame)).toEqual(frame)
    expect(frame.actions.map((action) => action.actionId)).toEqual([
      KEYBOARD_MOUSE_ACTION_IDS.moveForward,
      KEYBOARD_MOUSE_ACTION_IDS.moveRight,
      KEYBOARD_MOUSE_ACTION_IDS.lookYaw,
      KEYBOARD_MOUSE_ACTION_IDS.lookPitch,
      KEYBOARD_MOUSE_ACTION_IDS.jump,
      KEYBOARD_MOUSE_ACTION_IDS.fire,
    ])
    expect(new Set(frame.actions.map((action) => action.participantId))).toEqual(
      new Set([LOCAL_KEYBOARD_MOUSE_PARTICIPANT_ID]),
    )
    expect(Object.fromEntries(frame.actions.map((action) => [action.actionId, action.value]))).toMatchObject({
      [KEYBOARD_MOUSE_ACTION_IDS.lookYaw]: 1,
      [KEYBOARD_MOUSE_ACTION_IDS.lookPitch]: 1,
    })
    expect(valuesByAction(adapter, 10)).toMatchObject({
      [KEYBOARD_MOUSE_ACTION_IDS.moveForward]: 1,
      [KEYBOARD_MOUSE_ACTION_IDS.moveRight]: 1,
      [KEYBOARD_MOUSE_ACTION_IDS.lookYaw]: 0,
      [KEYBOARD_MOUSE_ACTION_IDS.lookPitch]: 0,
      [KEYBOARD_MOUSE_ACTION_IDS.jump]: 1,
      [KEYBOARD_MOUSE_ACTION_IDS.fire]: 1,
    })
  })

  it('accumulates mouse movement and drains deltas once per tick', () => {
    window.dispatchEvent(new MouseEvent('mousemove', { movementX: 0.4, movementY: 0.3 }))
    window.dispatchEvent(new MouseEvent('mousemove', { movementX: -0.1, movementY: 0.5 }))

    const firstTick = valuesByAction(adapter, 1)
    expect(firstTick[KEYBOARD_MOUSE_ACTION_IDS.lookYaw]).toBeCloseTo(0.3)
    expect(firstTick[KEYBOARD_MOUSE_ACTION_IDS.lookPitch]).toBeCloseTo(-0.8)
    expect(valuesByAction(adapter, 2)).toMatchObject({
      [KEYBOARD_MOUSE_ACTION_IDS.lookYaw]: 0,
      [KEYBOARD_MOUSE_ACTION_IDS.lookPitch]: 0,
    })
  })

  it('treats repeated keydown as one held action and resolves opposing keys', () => {
    window.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyW' }))
    window.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyW', repeat: true }))
    expect(valuesByAction(adapter, 1)[KEYBOARD_MOUSE_ACTION_IDS.moveForward]).toBe(1)

    window.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyS' }))
    expect(valuesByAction(adapter, 2)[KEYBOARD_MOUSE_ACTION_IDS.moveForward]).toBe(0)

    window.dispatchEvent(new KeyboardEvent('keyup', { code: 'KeyW' }))
    expect(valuesByAction(adapter, 3)[KEYBOARD_MOUSE_ACTION_IDS.moveForward]).toBe(-1)
  })

  it('gates every gameplay action behind the configured pointer lock', () => {
    setPointerLockElement(null)
    window.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyW' }))
    window.dispatchEvent(new KeyboardEvent('keydown', { code: 'Space' }))
    window.dispatchEvent(new MouseEvent('mousedown', { button: 0 }))
    window.dispatchEvent(new MouseEvent('mousemove', { movementX: 20, movementY: 10 }))

    expect(Object.values(valuesByAction(adapter, 1))).toEqual([0, 0, 0, 0, 0, 0])
  })

  it.each([
    ['window blur', () => window.dispatchEvent(new Event('blur'))],
    ['hidden page', () => {
      setDocumentHidden(true)
      document.dispatchEvent(new Event('visibilitychange'))
    }],
    ['pointer unlock', () => {
      setPointerLockElement(null)
      document.dispatchEvent(new Event('pointerlockchange'))
    }],
  ])('atomically clears held and accumulated state on %s', (_label, clear) => {
    window.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyW' }))
    window.dispatchEvent(new KeyboardEvent('keydown', { code: 'Space' }))
    window.dispatchEvent(new MouseEvent('mousedown', { button: 0 }))
    window.dispatchEvent(new MouseEvent('mousemove', { movementX: 5, movementY: 6 }))

    clear()
    setPointerLockElement(lockElement)

    expect(Object.values(valuesByAction(adapter, 1))).toEqual([0, 0, 0, 0, 0, 0])
  })

  it('removes all physical-device listeners on dispose', () => {
    adapter.dispose()
    window.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyW' }))
    window.dispatchEvent(new MouseEvent('mousedown', { button: 0 }))
    window.dispatchEvent(new MouseEvent('mousemove', { movementX: 5, movementY: 6 }))

    expect(Object.values(valuesByAction(adapter, 1))).toEqual([0, 0, 0, 0, 0, 0])
  })
})
