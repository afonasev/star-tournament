import { describe, expect, it } from 'vitest'

import { summarizeCollisionBenchmark } from './benchmark'

describe('collision benchmark summary', () => {
  it('uses nearest-rank p95/p99 and keeps results immutable', () => {
    const samples = Array.from({ length: 100 }, (_, index) => index + 1)
    const summary = summarizeCollisionBenchmark(samples, 20)

    expect(summary).toEqual({
      schemaVersion: 1,
      workloadId: 'collision-workload-v1',
      warmupSamples: 20,
      sampleCount: 100,
      p95Milliseconds: 95,
      p99Milliseconds: 99,
      maxMilliseconds: 100,
    })
    expect(Object.isFrozen(summary)).toBe(true)
  })

  it('does not mutate the raw sample order', () => {
    const samples = [2, 0.5, 1]
    expect(summarizeCollisionBenchmark(samples, 0).p95Milliseconds).toBe(2)
    expect(samples).toEqual([2, 0.5, 1])
  })

  it.each([[[]], [[-1]], [[Number.NaN]], [[Number.POSITIVE_INFINITY]]])(
    'rejects invalid sample set %j',
    (samples) => {
      expect(() => summarizeCollisionBenchmark(samples, 0)).toThrow(RangeError)
    },
  )
})
