import { describe, expect, it, vi } from 'vitest'

import {
  COLLISION_ERROR_CODES,
  CollisionContractError,
  RUNTIME_COLLISION_COMPATIBILITY_IDENTITY,
} from './contracts'
import {
  createCollisionBackendInitializer,
  type CollisionBackendModule,
} from './backendInitialization'

function successfulModule(): CollisionBackendModule {
  return {
    init: vi.fn(async () => undefined),
    version: vi.fn(() => '0.20.0'),
  }
}

describe('createCollisionBackendInitializer', () => {
  it('initializes one supported module and returns a frozen ready wrapper', async () => {
    const module = successfulModule()
    const loader = vi.fn(async () => module)
    const initialize = createCollisionBackendInitializer(loader)

    const ready = await initialize()

    expect(loader).toHaveBeenCalledTimes(1)
    expect(module.init).toHaveBeenCalledTimes(1)
    expect(module.version).toHaveBeenCalledTimes(1)
    expect(ready.identity).toBe(RUNTIME_COLLISION_COMPATIBILITY_IDENTITY)
    expect(ready.module).toBe(module)
    expect(Object.isFrozen(ready)).toBe(true)
  })

  it('shares the exact ready promise across concurrent initialization requests', async () => {
    let releaseInitialization: (() => void) | undefined
    const initializationGate = new Promise<void>((resolve) => {
      releaseInitialization = resolve
    })
    const module: CollisionBackendModule = {
      init: vi.fn(() => initializationGate),
      version: vi.fn(() => '0.20.0'),
    }
    const loader = vi.fn(async () => module)
    const initialize = createCollisionBackendInitializer(loader)

    const first = initialize()
    const second = initialize(RUNTIME_COLLISION_COMPATIBILITY_IDENTITY)
    const third = initialize()

    expect(first).toBe(second)
    expect(second).toBe(third)
    releaseInitialization?.()
    await expect(Promise.all([first, second, third])).resolves.toHaveLength(3)
    expect(loader).toHaveBeenCalledTimes(1)
    expect(module.init).toHaveBeenCalledTimes(1)
  })

  it('caches a deterministic initialization failure without leaking backend error text', async () => {
    const loader = vi.fn(async (): Promise<CollisionBackendModule> => {
      throw new Error(`volatile backend detail ${Math.random()}`)
    })
    const initialize = createCollisionBackendInitializer(loader)

    const first = initialize()
    const second = initialize()

    expect(first).toBe(second)
    await expect(first).rejects.toMatchObject({
      code: COLLISION_ERROR_CODES.backendInitializationFailed,
      message: 'Collision backend initialization failed',
    })
    await expect(second).rejects.not.toHaveProperty('message', expect.stringContaining('volatile'))
    expect(loader).toHaveBeenCalledTimes(1)
  })

  it('rejects an unsupported requested identity before loading and does not poison later init', async () => {
    const module = successfulModule()
    const loader = vi.fn(async () => module)
    const initialize = createCollisionBackendInitializer(loader)

    await expect(
      initialize({
        ...RUNTIME_COLLISION_COMPATIBILITY_IDENTITY,
        backendVersion: '0.21.0',
      }),
    ).rejects.toMatchObject({ code: COLLISION_ERROR_CODES.unsupportedIdentity })
    expect(loader).not.toHaveBeenCalled()

    await expect(initialize()).resolves.toMatchObject({
      identity: RUNTIME_COLLISION_COMPATIBILITY_IDENTITY,
    })
    expect(loader).toHaveBeenCalledTimes(1)
  })

  it('rejects a module reporting an incompatible runtime version with the stable code', async () => {
    const initialize = createCollisionBackendInitializer(async () => ({
      init: async () => undefined,
      version: () => '0.19.0',
    }))

    await expect(initialize()).rejects.toEqual(
      expect.objectContaining<Partial<CollisionContractError>>({
        code: COLLISION_ERROR_CODES.unsupportedIdentity,
      }),
    )
  })
})
