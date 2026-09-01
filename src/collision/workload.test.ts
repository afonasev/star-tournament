import { describe, expect, it } from 'vitest'

import {
  COLLISION_WORKLOAD_INITIAL_CAPSULES,
  COLLISION_WORKLOAD_PARTICIPANTS,
  runCollisionBrowserBenchmark,
  runCollisionWorkload,
} from './workload'

describe('collision-workload-v1', () => {
  it('uses eight canonically ordered participants', () => {
    expect(COLLISION_WORKLOAD_INITIAL_CAPSULES).toHaveLength(COLLISION_WORKLOAD_PARTICIPANTS)
    expect(COLLISION_WORKLOAD_INITIAL_CAPSULES.map((capsule) => capsule.semanticId)).toEqual(
      [...COLLISION_WORKLOAD_INITIAL_CAPSULES].map((capsule) => capsule.semanticId).sort(),
    )
  })

  it('matches independent and mid-run reconstructed worlds at every checkpoint', async () => {
    const first = await runCollisionWorkload()
    const second = await runCollisionWorkload()
    const restored = await runCollisionWorkload({ restoreAtTick: 5_000 })

    expect(second.checkpointHashes).toEqual(first.checkpointHashes)
    expect(second.tickHashes).toEqual(first.tickHashes)
    expect(second.finalGameplayHash).toBe(first.finalGameplayHash)
    expect(second.finalCollisionHash).toBe(first.finalCollisionHash)
    expect(restored.checkpointHashes).toEqual(first.checkpointHashes)
    expect(restored.tickHashes.slice(5_000)).toEqual(first.tickHashes.slice(5_000))
    expect(restored.finalGameplayHash).toBe(first.finalGameplayHash)
    expect(restored.finalCollisionHash).toBe(first.finalCollisionHash)
  }, 60_000)

  it('is independent of zero, one, two, and four observer passes', async () => {
    const results = await Promise.all(
      [0, 1, 2, 4].map((observerPasses) => runCollisionWorkload({ tickCount: 200, observerPasses })),
    )
    expect(new Set(results.map((result) => result.finalGameplayHash))).toHaveLength(1)
    expect(new Set(results.map((result) => result.finalCollisionHash))).toHaveLength(1)
  }, 20_000)

  it('measures after warm-up with an injected clock outside hashes', async () => {
    let time = 0
    const summary = await runCollisionBrowserBenchmark(2, 4, () => {
      time += 0.25
      return time
    })
    expect(summary).toMatchObject({
      workloadId: 'collision-workload-v1',
      warmupSamples: 2,
      sampleCount: 4,
      p95Milliseconds: 0.25,
      p99Milliseconds: 0.25,
      maxMilliseconds: 0.25,
    })
  })
})
