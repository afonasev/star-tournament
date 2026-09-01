import { describe, expect, it, vi } from 'vitest'

import {
  BrowserRuntime,
  MAX_CATCH_UP_TICKS,
  type AnimationScheduler,
} from './browserRuntime'
import type { RuntimeStatus } from './uiSnapshot'

interface TestState {
  readonly tick: number
}

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
    const [handle, callback] = entry
    this.callbacks.delete(handle)
    callback(timeMilliseconds)
  }

  get pendingCount(): number {
    return this.callbacks.size
  }
}

function createRuntime(scheduler: ManualScheduler, disposePresentation = vi.fn()) {
  const statuses: RuntimeStatus[] = []
  const runtime = new BrowserRuntime<TestState>({
    initialState: { tick: 0 },
    scheduler,
    step: (state) => ({ tick: state.tick + 1 }),
    render: vi.fn(),
    snapshotForUi: (state, status) => ({
      status,
      tick: state.tick,
      stateHash: `state-${state.tick}`,
      profileId: 'prototype-v1',
      profileHash: 'profile-hash',
      arenaId: 'diagnostic-arena',
      arenaHash: 'arena-hash',
      collision: { status: 'ready', identity: 'collision-test', fixtureId: 'fixture', fixtureStatus: 'passed' },
    }),
    publishUiSnapshot: (snapshot) => statuses.push(snapshot.status),
    disposePresentation,
  })
  return { runtime, statuses, disposePresentation }
}

describe('BrowserRuntime', () => {
  it('steps only in fixed increments and render cadence does not add ticks', () => {
    const scheduler = new ManualScheduler()
    const { runtime } = createRuntime(scheduler)

    runtime.start()
    scheduler.frame(0)
    scheduler.frame(8)
    expect(runtime.getState().tick).toBe(0)
    scheduler.frame(17)
    expect(runtime.getState().tick).toBe(1)
    scheduler.frame(34)
    expect(runtime.getState().tick).toBe(2)
  })

  it('reaches the same state at the same wall time across render cadences', () => {
    const sparseScheduler = new ManualScheduler()
    const denseScheduler = new ManualScheduler()
    const sparse = createRuntime(sparseScheduler).runtime
    const dense = createRuntime(denseScheduler).runtime

    sparse.start()
    dense.start()
    sparseScheduler.frame(0)
    denseScheduler.frame(0)
    for (const time of [20, 40, 60, 80, 100, 120]) {
      denseScheduler.frame(time)
    }
    sparseScheduler.frame(120)

    expect(sparse.getState()).toEqual(dense.getState())
  })

  it('caps catch-up work after a long wall-clock gap', () => {
    const scheduler = new ManualScheduler()
    const { runtime } = createRuntime(scheduler)

    runtime.start()
    scheduler.frame(0)
    scheduler.frame(1_000)

    expect(runtime.getState().tick).toBe(MAX_CATCH_UP_TICKS)
  })

  it('stops scheduling and never ticks after stop', () => {
    const scheduler = new ManualScheduler()
    const { runtime, statuses } = createRuntime(scheduler)

    runtime.start()
    scheduler.frame(0)
    runtime.stop()

    expect(runtime.isRunning()).toBe(false)
    expect(scheduler.pendingCount).toBe(0)
    expect(statuses).toEqual(['running', 'stopped'])
  })

  it('publishes periodic gameplay UI at the configured cadence instead of every tick', () => {
    const scheduler = new ManualScheduler()
    const publishedTicks: number[] = []
    const runtime = new BrowserRuntime<TestState, { readonly status: RuntimeStatus; readonly tick: number }>({
      initialState: { tick: 0 },
      scheduler,
      uiUpdateIntervalSeconds: 0.1,
      step: (state) => ({ tick: state.tick + 1 }),
      render: vi.fn(),
      snapshotForUi: (state, status) => ({ status, tick: state.tick }),
      publishUiSnapshot: (snapshot) => publishedTicks.push(snapshot.tick),
    })

    runtime.start()
    for (let time = 0; time <= 1_000; time += 10) scheduler.frame(time)

    expect(runtime.getState().tick).toBe(60)
    expect(publishedTicks.length).toBeGreaterThanOrEqual(10)
    expect(publishedTicks.length).toBeLessThanOrEqual(11)
    expect(publishedTicks[0]).toBe(0)
    expect(publishedTicks.at(-1)).toBeGreaterThanOrEqual(54)
  })

  it('rejects a non-positive UI publication interval', () => {
    const scheduler = new ManualScheduler()
    expect(() => new BrowserRuntime<TestState>({
      initialState: { tick: 0 },
      scheduler,
      uiUpdateIntervalSeconds: 0,
      step: (state) => state,
      render: vi.fn(),
      snapshotForUi: (state, status) => ({
        status,
        tick: state.tick,
        stateHash: 'state',
        profileId: 'profile',
        profileHash: 'profile-hash',
        arenaId: 'arena',
        arenaHash: 'arena-hash',
        collision: { status: 'ready', identity: 'collision-test', fixtureId: 'fixture', fixtureStatus: 'passed' },
      }),
      publishUiSnapshot: vi.fn(),
    })).toThrow(/greater than zero/)
  })

  it('disposes presentation once and rejects restart', () => {
    const scheduler = new ManualScheduler()
    const { runtime, disposePresentation } = createRuntime(scheduler)

    runtime.start()
    runtime.dispose()
    runtime.dispose()

    expect(disposePresentation).toHaveBeenCalledTimes(1)
    expect(scheduler.pendingCount).toBe(0)
    expect(() => runtime.start()).toThrow(/уничтоженный/)
  })

  it('does not reschedule when dispose is called re-entrantly from render', () => {
    const scheduler = new ManualScheduler()
    let runtime: BrowserRuntime<TestState>
    runtime = new BrowserRuntime<TestState>({
      initialState: { tick: 0 },
      scheduler,
      step: (state) => ({ tick: state.tick + 1 }),
      render: () => runtime.dispose(),
      snapshotForUi: (state, status) => ({
        status,
        tick: state.tick,
        stateHash: 'state',
        profileId: 'profile',
        profileHash: 'profile-hash',
        arenaId: 'arena',
        arenaHash: 'arena-hash',
        collision: { status: 'ready', identity: 'collision-test', fixtureId: 'fixture', fixtureStatus: 'passed' },
      }),
      publishUiSnapshot: () => undefined,
    })

    runtime.start()
    scheduler.frame(0)

    expect(runtime.isRunning()).toBe(false)
    expect(scheduler.pendingCount).toBe(0)
  })

  it('publishes immutable UI snapshots', () => {
    const scheduler = new ManualScheduler()
    let published: Readonly<{ status: RuntimeStatus }> | undefined
    const runtime = new BrowserRuntime<TestState>({
      initialState: { tick: 0 },
      scheduler,
      step: (state) => ({ tick: state.tick + 1 }),
      render: vi.fn(),
      snapshotForUi: (state, status) => ({
        status,
        tick: state.tick,
        stateHash: 'state',
        profileId: 'profile',
        profileHash: 'profile-hash',
        arenaId: 'arena',
        arenaHash: 'arena-hash',
        collision: { status: 'ready', identity: 'collision-test', fixtureId: 'fixture', fixtureStatus: 'passed' },
      }),
      publishUiSnapshot: (snapshot) => {
        published = snapshot
      },
    })

    runtime.start()
    expect(Object.isFrozen(published)).toBe(true)
  })
})
