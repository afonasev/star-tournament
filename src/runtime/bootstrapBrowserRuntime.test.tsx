// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest'
import { cleanup, fireEvent, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import {
  COLLISION_CHECKPOINT_SCHEMA_VERSION,
  COLLISION_ERROR_CODES,
  CollisionContractError,
  RUNTIME_COLLISION_COMPATIBILITY_IDENTITY,
  type CollisionCapsuleProjection,
  type CollisionCheckpointPayload,
  type CollisionVector3,
} from '../collision/contracts'
import type { CollisionControllerConfiguration } from '../collision/rapierWorld'
import { createActionFrame } from '../input/actionFrame'
import type { PointerLockControllerOptions, PointerLockLifecycleSnapshot } from '../input/pointerLockController'
import { PROTOTYPE_V1_PROFILE } from '../profiles'
import type { FirstPersonRenderer } from '../render/firstPersonRenderer'
import { WebGlUnavailableError } from '../render/diagnosticRenderer'
import { PLAYABLE_PLAYER_ID } from '../simulation'
import {
  bootstrapBrowserRuntime,
  type BrowserApplication,
  type BrowserBootstrapDependencies,
  type RuntimeCollisionWorld,
  type RuntimeInputAdapter,
  type RuntimePointerLockController,
} from './bootstrapBrowserRuntime'
import type { AnimationScheduler } from './browserRuntime'

class ManualScheduler implements AnimationScheduler {
  private nextHandle = 1
  private callbacks = new Map<number, FrameRequestCallback>()

  request(callback: FrameRequestCallback): number {
    const handle = this.nextHandle++
    this.callbacks.set(handle, callback)
    return handle
  }

  cancel(handle: number): void {
    this.callbacks.delete(handle)
  }

  frame(timeMilliseconds: number): void {
    const entry = this.callbacks.entries().next().value as
      | [number, FrameRequestCallback]
      | undefined
    if (!entry) throw new Error('No animation frame is scheduled')
    this.callbacks.delete(entry[0])
    entry[1](timeMilliseconds)
  }

  get pendingCount(): number {
    return this.callbacks.size
  }
}

class FakePointerController implements RuntimePointerLockController {
  readonly requestStart = vi.fn(async () => this.lock())
  readonly requestResume = vi.fn(async () => this.lock())
  readonly dispose = vi.fn(() => {
    this.state = Object.freeze({ status: 'disposed' })
    this.options.clearInputState()
    this.options.stopRuntime()
    this.options.onStateChange?.(this.state)
  })
  private state: Readonly<PointerLockLifecycleSnapshot> = Object.freeze({ status: 'paused' })

  constructor(private readonly options: PointerLockControllerOptions) {
    options.clearInputState()
    options.stopRuntime()
    options.onStateChange?.(this.state)
  }

  getSnapshot(): Readonly<PointerLockLifecycleSnapshot> {
    return this.state
  }

  pause(): void {
    this.options.clearInputState()
    this.options.stopRuntime()
    this.state = Object.freeze({ status: 'paused' })
    this.options.onStateChange?.(this.state)
  }

  fail(message = 'Pointer lock failed'): void {
    this.options.clearInputState()
    this.options.stopRuntime()
    this.state = Object.freeze({ status: 'error', errorMessage: message })
    this.options.onStateChange?.(this.state)
  }

  private lock(): boolean {
    this.state = Object.freeze({ status: 'locked' })
    this.options.onStateChange?.(this.state)
    this.options.startRuntime()
    return true
  }
}

function fakeRenderer(): FirstPersonRenderer {
  return {
    arenaId: 'collision-fixture-v1',
    arenaHash: 'fnv1a64-v1:arena',
    scenarioId: 'combat-slice-scenario-v1',
    scenarioHash: 'fnv1a64-v1:scenario',
    profileId: 'prototype-v1',
    profileHash: PROTOTYPE_V1_PROFILE.contentHash,
    render: vi.fn(),
    resize: vi.fn(),
    dispose: vi.fn(),
  }
}

function fakeCollisionWorld(
  arena: Parameters<NonNullable<BrowserBootstrapDependencies['createCollisionWorld']>>[0],
  initialCapsules: readonly CollisionCapsuleProjection[],
): RuntimeCollisionWorld {
  let capsules = [...initialCapsules]
  return {
    projectCapsules: vi.fn((next) => {
      capsules = [...next]
    }),
    moveCapsule: vi.fn((semanticId: string, movement: CollisionVector3) => {
      const capsule = capsules.find((candidate) => candidate.semanticId === semanticId)
      if (capsule === undefined) throw new Error(`Unknown capsule ${semanticId}`)
      return {
        position: {
          x: capsule.position.x + movement.x,
          y: capsule.position.y,
          z: capsule.position.z + movement.z,
        },
        grounded: true,
        contacts: [],
      }
    }),
    raycast: vi.fn(() => []),
    checkpoint: vi.fn((tick: number): CollisionCheckpointPayload => ({
      schemaVersion: COLLISION_CHECKPOINT_SCHEMA_VERSION,
      tick,
      compatibility: RUNTIME_COLLISION_COMPATIBILITY_IDENTITY,
      arena: {
        id: arena.id,
        seed: arena.seed,
        generatorVersion: arena.generatorVersion,
        contentHash: arena.contentHash as CollisionCheckpointPayload['arena']['contentHash'],
      },
      fixture: { id: 'runtime-test-controller', version: 1 },
      capsules,
    })),
    dispose: vi.fn(),
  }
}

function harness(actionValue = 0) {
  const scheduler = new ManualScheduler()
  const renderer = fakeRenderer()
  const input: RuntimeInputAdapter = {
    drainActionFrame: vi.fn((tick: number) => createActionFrame(tick, actionValue === 0 ? [] : [
      { participantId: PLAYABLE_PLAYER_ID, actionId: 'move-forward', value: actionValue },
    ])),
    clearInputState: vi.fn(),
    dispose: vi.fn(),
  }
  let collision: RuntimeCollisionWorld | undefined
  let pointer: FakePointerController | undefined
  const createCollisionWorld = vi.fn<
    NonNullable<BrowserBootstrapDependencies['createCollisionWorld']>
  >(async (arena, capsules) => {
    collision = fakeCollisionWorld(arena, capsules)
    return collision
  })
  const createInput = vi.fn<
    NonNullable<BrowserBootstrapDependencies['createInput']>
  >(() => input)
  const createPointerLockController = vi.fn<
    NonNullable<BrowserBootstrapDependencies['createPointerLockController']>
  >((options) => {
    pointer = new FakePointerController(options)
    return pointer
  })
  const dependencies: BrowserBootstrapDependencies = {
    scheduler,
    initializeBackend: async () => undefined,
    createCollisionWorld,
    createRenderer: () => renderer,
    createInput,
    createPointerLockController,
    now: () => 10,
  }
  return {
    scheduler,
    renderer,
    input,
    createCollisionWorld,
    createInput,
    createPointerLockController,
    dependencies,
    get pointer() { return pointer },
    get collision() { return collision },
  }
}

let application: BrowserApplication | undefined

afterEach(() => {
  application?.dispose()
  application = undefined
  cleanup()
  document.body.replaceChildren()
  window.history.replaceState({}, '', '/')
  vi.restoreAllMocks()
})

describe('playable browser bootstrap', () => {
  it('surfaces a stable collision error without starting partial presentation', async () => {
    const mount = document.createElement('div')
    document.body.append(mount)
    const test = harness()
    test.createCollisionWorld.mockRejectedValue(new CollisionContractError(
      COLLISION_ERROR_CODES.backendInitializationFailed,
      'volatile backend detail must not reach UI',
    ))

    application = await bootstrapBrowserRuntime(mount, test.dependencies)

    expect(application.runtime).toBeUndefined()
    expect(test.createInput).not.toHaveBeenCalled()
    expect(test.createPointerLockController).not.toHaveBeenCalled()
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent(
      'COLLISION_BACKEND_INITIALIZATION_FAILED',
    ))
    expect(screen.queryByText(/volatile backend detail/)).not.toBeInTheDocument()
  })

  it('surfaces profile validation errors before collision or renderer initialization', async () => {
    const mount = document.createElement('div')
    document.body.append(mount)
    const test = harness()
    const invalidProfile = JSON.parse(JSON.stringify(PROTOTYPE_V1_PROFILE)) as unknown as {
      rules: { camera?: unknown }
    }
    delete invalidProfile.rules.camera

    application = await bootstrapBrowserRuntime(mount, {
      ...test.dependencies,
      profile: invalidProfile as never,
    })

    expect(application.runtime).toBeUndefined()
    expect(test.createCollisionWorld).not.toHaveBeenCalled()
    expect(test.renderer.render).not.toHaveBeenCalled()
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent(
      'Profile validation failed',
    ))
    expect(screen.getByRole('alert')).toHaveTextContent('MISSING_FIELD')
  })

  it('constructs the configured player capsule but does not tick before explicit lock', async () => {
    const mount = document.createElement('div')
    document.body.append(mount)
    const test = harness()

    application = await bootstrapBrowserRuntime(mount, test.dependencies)

    expect(application.runtime?.isRunning()).toBe(false)
    expect(application.runtime?.getState().tick).toBe(0)
    expect(test.scheduler.pendingCount).toBe(0)
    expect(test.createInput).toHaveBeenCalledWith(expect.objectContaining({
      participantId: PLAYABLE_PLAYER_ID,
      pointerLockElement: expect.any(HTMLElement),
    }))
    const [, capsules, configuration] = test.createCollisionWorld.mock.calls[0] as [
      unknown,
      readonly CollisionCapsuleProjection[],
      CollisionControllerConfiguration,
    ]
    expect(capsules).toEqual([expect.objectContaining({
      semanticId: PLAYABLE_PLAYER_ID,
      radius: PROTOTYPE_V1_PROFILE.rules.player.capsule.radius,
      halfHeight: PROTOTYPE_V1_PROFILE.rules.player.capsule.halfHeight,
    })])
    expect(configuration).toEqual({
      skinWidth: 0.01,
      autostepMaxHeight: 0.45,
      autostepMinWidth: 0.25,
      snapToGroundDistance: 0.2,
      maxSlopeClimbDegrees: 45,
      minSlopeSlideDegrees: 60,
    })
    await waitFor(() => expect(screen.getByRole('button', { name: 'Начать игру' })).toBeInTheDocument())
  })

  it('starts from the HUD, drains one canonical frame per fixed tick and renders state', async () => {
    const mount = document.createElement('div')
    document.body.append(mount)
    const test = harness(1)
    application = await bootstrapBrowserRuntime(mount, test.dependencies)

    fireEvent.click(await screen.findByRole('button', { name: 'Начать игру' }))
    expect(application.runtime?.isRunning()).toBe(true)
    expect(test.scheduler.pendingCount).toBe(1)

    test.scheduler.frame(0)
    test.scheduler.frame(17)

    expect(application.runtime?.getState().tick).toBe(1)
    expect(application.runtime?.getState().player.position.z).toBeLessThan(8)
    expect(test.input.drainActionFrame).toHaveBeenCalledTimes(1)
    expect(test.input.drainActionFrame).toHaveBeenCalledWith(1)
    expect(test.renderer.render).toHaveBeenCalledWith(application.runtime?.getState())
    await waitFor(() => expect(screen.getByLabelText('Здоровье игрока')).toHaveTextContent('100'))
  })

  it('pauses on lock loss, shows resume and restarts without leaking ticks', async () => {
    const mount = document.createElement('div')
    document.body.append(mount)
    const test = harness()
    application = await bootstrapBrowserRuntime(mount, test.dependencies)
    await application.requestControl()
    test.scheduler.frame(0)
    test.scheduler.frame(17)
    expect(application.runtime?.getState().tick).toBe(1)

    test.pointer!.pause()

    expect(application.runtime?.isRunning()).toBe(false)
    expect(test.scheduler.pendingCount).toBe(0)
    expect(test.input.clearInputState).toHaveBeenCalled()
    await waitFor(() => expect(screen.getByRole('button', { name: 'Продолжить' })).toBeInTheDocument())

    await application.requestControl()
    expect(test.pointer!.requestResume).toHaveBeenCalledTimes(1)
    expect(application.runtime?.isRunning()).toBe(true)
  })

  it('keeps pointer-lock request failures retryable instead of replacing the game', async () => {
    const mount = document.createElement('div')
    document.body.append(mount)
    const test = harness()
    application = await bootstrapBrowserRuntime(mount, test.dependencies)

    test.pointer!.fail('Pointer lock failed')

    expect(await screen.findByText('Pointer lock failed')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Начать игру' })).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('keeps debug and collision benchmark diagnostics behind their explicit queries', async () => {
    const mount = document.createElement('div')
    document.body.append(mount)
    const test = harness()
    const runBenchmark = vi.fn(async () => ({
      schemaVersion: 1 as const,
      workloadId: 'collision-workload-v1' as const,
      warmupSamples: 100,
      sampleCount: 10_000,
      p95Milliseconds: 0.4,
      p99Milliseconds: 0.5,
      maxMilliseconds: 1.6,
    }))

    application = await bootstrapBrowserRuntime(mount, {
      ...test.dependencies,
      locationSearch: '?debug=1&collisionBenchmark=1',
      runBenchmark,
    })

    expect(await screen.findByLabelText('Отладочная информация')).toHaveTextContent('DEBUG · TICK 0')
    await waitFor(() => expect(runBenchmark).toHaveBeenCalledTimes(1))
    await waitFor(() => expect(screen.getByLabelText('Игровое поле')).toHaveAttribute(
      'data-collision-benchmark-status',
      'passed',
    ))
    expect(screen.getByLabelText('Игровое поле')).toHaveAttribute(
      'data-collision-benchmark-p95-milliseconds',
      '0.4',
    )
  })

  it('surfaces WebGL errors and disposes every owned resource once on pagehide', async () => {
    const errorMount = document.createElement('div')
    document.body.append(errorMount)
    const failing = harness()
    application = await bootstrapBrowserRuntime(errorMount, {
      ...failing.dependencies,
      createRenderer: () => {
        throw new WebGlUnavailableError()
      },
    })
    expect(application.runtime).toBeUndefined()
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent(
      'Браузер не предоставил совместимый WebGL-контекст',
    ))
    application.dispose()
    application = undefined
    cleanup()
    errorMount.remove()

    const mount = document.createElement('div')
    document.body.append(mount)
    const test = harness()
    application = await bootstrapBrowserRuntime(mount, test.dependencies)

    window.dispatchEvent(new Event('pagehide'))
    window.dispatchEvent(new Event('pagehide'))

    expect(application.runtime?.isRunning()).toBe(false)
    expect(test.pointer!.dispose).toHaveBeenCalledTimes(1)
    expect(test.input.dispose).toHaveBeenCalledTimes(1)
    expect(test.collision!.dispose).toHaveBeenCalledTimes(1)
    expect(test.renderer.dispose).toHaveBeenCalledTimes(1)
    expect(mount).toBeEmptyDOMElement()
  })
})
