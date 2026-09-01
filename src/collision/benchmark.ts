import { deepFreeze } from '../core/deepFreeze'

export const COLLISION_BENCHMARK_SCHEMA_VERSION = 1 as const
export const COLLISION_BENCHMARK_WORKLOAD_ID = 'collision-workload-v1' as const
export const COLLISION_BENCHMARK_SAMPLE_COUNT = 10_000 as const

export interface CollisionBenchmarkSummary {
  readonly schemaVersion: typeof COLLISION_BENCHMARK_SCHEMA_VERSION
  readonly workloadId: typeof COLLISION_BENCHMARK_WORKLOAD_ID
  readonly warmupSamples: number
  readonly sampleCount: number
  readonly p95Milliseconds: number
  readonly p99Milliseconds: number
  readonly maxMilliseconds: number
}

function percentile(sortedSamples: readonly number[], percentileValue: number): number {
  const rank = Math.ceil(percentileValue * sortedSamples.length) - 1
  return sortedSamples[Math.max(0, rank)] ?? 0
}

/**
 * Summarizes externally measured wall-clock samples. Timing never enters the
 * simulation snapshot, collision checkpoint, or their hashes.
 */
export function summarizeCollisionBenchmark(
  samplesMilliseconds: readonly number[],
  warmupSamples: number,
): CollisionBenchmarkSummary {
  if (!Number.isSafeInteger(warmupSamples) || warmupSamples < 0) {
    throw new RangeError('warmupSamples must be a non-negative safe integer')
  }
  if (samplesMilliseconds.length === 0) {
    throw new RangeError('samplesMilliseconds must not be empty')
  }
  if (samplesMilliseconds.some((sample) => !Number.isFinite(sample) || sample < 0)) {
    throw new RangeError('benchmark samples must be finite non-negative milliseconds')
  }

  const sorted = [...samplesMilliseconds].sort((left, right) => left - right)
  return deepFreeze({
    schemaVersion: COLLISION_BENCHMARK_SCHEMA_VERSION,
    workloadId: COLLISION_BENCHMARK_WORKLOAD_ID,
    warmupSamples,
    sampleCount: sorted.length,
    p95Milliseconds: percentile(sorted, 0.95),
    p99Milliseconds: percentile(sorted, 0.99),
    maxMilliseconds: sorted[sorted.length - 1] ?? 0,
  })
}

