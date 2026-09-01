// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import {
  PointerLockController,
  type PointerLockLifecycleSnapshot,
} from './pointerLockController'

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

function installExitPointerLock(): ReturnType<typeof vi.fn> {
  const exitPointerLock = vi.fn(() => {
    setPointerLockElement(null)
    document.dispatchEvent(new Event('pointerlockchange'))
  })
  Object.defineProperty(document, 'exitPointerLock', {
    configurable: true,
    value: exitPointerLock,
  })
  return exitPointerLock
}

describe('PointerLockController', () => {
  let lockElement: HTMLElement
  let controller: PointerLockController | undefined
  let calls: string[]
  let snapshots: Readonly<PointerLockLifecycleSnapshot>[]
  let requestPointerLock: ReturnType<typeof vi.fn>
  let getGamepads: ReturnType<typeof vi.fn>

  beforeEach(() => {
    calls = []
    snapshots = []
    lockElement = document.createElement('canvas')
    document.body.append(lockElement)
    setPointerLockElement(null)
    setDocumentHidden(false)
    installExitPointerLock()
    requestPointerLock = vi.fn(async () => undefined)
    Object.defineProperty(lockElement, 'requestPointerLock', {
      configurable: true,
      value: requestPointerLock,
    })
    getGamepads = vi.fn(() => {
      throw new Error('Gamepad API must not be read')
    })
    Object.defineProperty(navigator, 'getGamepads', {
      configurable: true,
      value: getGamepads,
    })
  })

  afterEach(() => {
    controller?.dispose()
    controller = undefined
    setPointerLockElement(null)
    setDocumentHidden(false)
    document.body.replaceChildren()
    vi.restoreAllMocks()
  })

  function createController(onRequestFailure = vi.fn()): PointerLockController {
    controller = new PointerLockController({
      lockElement,
      clearInputState: () => calls.push('clear'),
      stopRuntime: () => calls.push('stop'),
      startRuntime: () => calls.push('start'),
      onStateChange: (snapshot) => snapshots.push(snapshot),
      onRequestFailure,
    })
    return controller
  }

  it('stops gameplay before lock and starts only after confirmed request success', async () => {
    requestPointerLock.mockImplementation(async () => {
      calls.push('request')
      setPointerLockElement(lockElement)
      document.dispatchEvent(new Event('pointerlockchange'))
    })
    const subject = createController()

    const accepted = await subject.requestStart()

    expect(accepted).toBe(true)
    expect(calls).toEqual(['clear', 'stop', 'clear', 'stop', 'request', 'start'])
    expect(subject.getSnapshot()).toEqual({ status: 'locked' })
    expect(Object.isFrozen(subject.getSnapshot())).toBe(true)
  })

  it('reports synchronous and asynchronous request failure without starting runtime', async () => {
    const onFailure = vi.fn()
    requestPointerLock.mockRejectedValue(new DOMException('Denied', 'NotAllowedError'))
    const subject = createController(onFailure)

    expect(await subject.requestStart()).toBe(false)
    expect(subject.getSnapshot()).toMatchObject({ status: 'error' })
    expect(onFailure).toHaveBeenCalledTimes(1)
    expect(calls).not.toContain('start')
  })

  it('clears actions before stopping when lock is lost', async () => {
    requestPointerLock.mockImplementation(async () => {
      setPointerLockElement(lockElement)
      document.dispatchEvent(new Event('pointerlockchange'))
    })
    const subject = createController()
    await subject.requestStart()
    calls = []

    setPointerLockElement(null)
    document.dispatchEvent(new Event('pointerlockchange'))

    expect(calls).toEqual(['clear', 'stop'])
    expect(subject.getSnapshot()).toEqual({ status: 'paused' })
  })

  it('requires an explicit resume request after unlock', async () => {
    requestPointerLock.mockImplementation(async () => {
      setPointerLockElement(lockElement)
      document.dispatchEvent(new Event('pointerlockchange'))
    })
    const subject = createController()
    await subject.requestStart()
    setPointerLockElement(null)
    document.dispatchEvent(new Event('pointerlockchange'))
    const startsBeforeResume = calls.filter((call) => call === 'start').length

    expect(startsBeforeResume).toBe(1)
    await subject.requestResume()
    expect(calls.filter((call) => call === 'start')).toHaveLength(2)
    expect(requestPointerLock).toHaveBeenCalledTimes(2)
  })

  it.each([
    ['window blur', () => window.dispatchEvent(new Event('blur'))],
    ['hidden page', () => {
      setDocumentHidden(true)
      document.dispatchEvent(new Event('visibilitychange'))
    }],
  ])('pauses and exits lock on %s', async (_label, loseFocus) => {
    const exitPointerLock = installExitPointerLock()
    requestPointerLock.mockImplementation(async () => {
      setPointerLockElement(lockElement)
      document.dispatchEvent(new Event('pointerlockchange'))
    })
    const subject = createController()
    await subject.requestStart()
    calls = []

    loseFocus()

    expect(calls).toEqual(['clear', 'stop'])
    expect(exitPointerLock).toHaveBeenCalledTimes(1)
    expect(subject.getSnapshot()).toEqual({ status: 'paused' })
  })

  it('does not depend on Escape events and never reads the gamepad API', async () => {
    requestPointerLock.mockImplementation(async () => {
      setPointerLockElement(lockElement)
      document.dispatchEvent(new Event('pointerlockchange'))
    })
    const subject = createController()
    await subject.requestStart()
    calls = []

    window.dispatchEvent(new KeyboardEvent('keydown', { code: 'Escape' }))

    expect(subject.getSnapshot()).toEqual({ status: 'locked' })
    expect(calls).toEqual([])
    expect(getGamepads).not.toHaveBeenCalled()
  })

  it('handles the pointerlockerror event as a stable failed request', async () => {
    const onFailure = vi.fn()
    const subject = createController(onFailure)

    document.dispatchEvent(new Event('pointerlockerror'))
    document.dispatchEvent(new Event('pointerlockerror'))
    await new Promise((resolve) => window.setTimeout(resolve, 60))

    expect(subject.getSnapshot()).toMatchObject({ status: 'error' })
    expect(onFailure).toHaveBeenCalledTimes(1)
    expect(calls).not.toContain('start')
  })

  it('trusts committed pointer lock over a stale embedded-browser error event', async () => {
    const onFailure = vi.fn()
    const subject = createController(onFailure)
    setPointerLockElement(lockElement)

    document.dispatchEvent(new Event('pointerlockerror'))
    await new Promise((resolve) => window.setTimeout(resolve, 60))

    expect(subject.getSnapshot()).toEqual({ status: 'locked' })
    expect(onFailure).not.toHaveBeenCalled()
    expect(calls).toContain('start')
  })

  it('removes listeners and stops runtime on idempotent dispose', () => {
    const subject = createController()
    calls = []
    subject.dispose()
    subject.dispose()
    calls = []

    setPointerLockElement(lockElement)
    document.dispatchEvent(new Event('pointerlockchange'))
    window.dispatchEvent(new Event('blur'))

    expect(calls).toEqual([])
    expect(subject.getSnapshot()).toEqual({ status: 'disposed' })
  })
})
