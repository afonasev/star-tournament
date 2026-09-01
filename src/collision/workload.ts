import { contentHash, type ContentHash } from '../core/canonical'
import { deepFreeze } from '../core/deepFreeze'
import {
  COLLISION_BENCHMARK_SAMPLE_COUNT,
  COLLISION_BENCHMARK_WORKLOAD_ID,
  summarizeCollisionBenchmark,
  type CollisionBenchmarkSummary,
} from './benchmark'
import {
  collisionCheckpointHash,
  parseCollisionCheckpointPayload,
  serializeCollisionCheckpointPayload,
  type CollisionCapsuleProjection,
} from './contracts'
import { COLLISION_FIXTURE_ARENA } from './fixture'
import { createRapierCollisionWorld, type RapierCollisionWorld } from './rapierWorld'

export const COLLISION_WORKLOAD_ID = 'collision-workload-v1' as const
export const COLLISION_WORKLOAD_TICKS = 10_000 as const
export const COLLISION_WORKLOAD_PARTICIPANTS = 8 as const
export const COLLISION_WORKLOAD_CHECKPOINT_INTERVAL = 1_000 as const

const initialPositions = [
  { x: -6, y: 1, z: -5 },
  { x: -4, y: 1, z: -5 },
  { x: 2, y: 1, z: -5 },
  { x: 4, y: 1, z: -5 },
  { x: -6, y: 1, z: 3 },
  { x: -3, y: 1, z: 3 },
  { x: 2, y: 1, z: 3 },
  { x: 5, y: 1, z: 3 },
] as const

export const COLLISION_WORKLOAD_INITIAL_CAPSULES: readonly CollisionCapsuleProjection[] = deepFreeze(
  initialPositions.map((position, index) => ({
    semanticId: `participant-${String(index + 1).padStart(2, '0')}`,
    position,
    radius: 0.4,
    halfHeight: 0.5,
  })),
)

export interface CollisionWorkloadOptions {
  readonly tickCount?: number
  readonly observerPasses?: number
  readonly restoreAtTick?: number
  readonly now?: () => number
  readonly measureAfterTick?: number
}

export interface CollisionWorkloadResult {
  readonly workloadId: typeof COLLISION_WORKLOAD_ID
  readonly tickCount: number
  readonly tickHashes: readonly ContentHash[]
  readonly checkpointHashes: readonly ContentHash[]
  readonly finalGameplayHash: ContentHash
  readonly finalCollisionHash: ContentHash
  readonly benchmarkSamplesMilliseconds: readonly number[]
}

function movementFor(tick: number, participantIndex: number): { x: number; y: number; z: number } {
  const phase = (tick + participantIndex) % 4
  return {
    x: phase === 0 ? 0.025 : phase === 2 ? -0.025 : 0,
    y: -0.04,
    z: phase === 1 ? 0.025 : phase === 3 ? -0.025 : 0,
  }
}

function validateOptions(options: CollisionWorkloadOptions): {
  tickCount: number
  observerPasses: number
  restoreAtTick: number | undefined
  measureAfterTick: number
} {
  const tickCount = options.tickCount ?? COLLISION_WORKLOAD_TICKS
  const observerPasses = options.observerPasses ?? 0
  const restoreAtTick = options.restoreAtTick
  const measureAfterTick = options.measureAfterTick ?? tickCount
  for (const [label, value] of [['tickCount', tickCount], ['observerPasses', observerPasses], ['measureAfterTick', measureAfterTick]] as const) {
    if (!Number.isSafeInteger(value) || value < 0) throw new RangeError(`${label} must be a non-negative safe integer`)
  }
  if (restoreAtTick !== undefined && (!Number.isSafeInteger(restoreAtTick) || restoreAtTick <= 0 || restoreAtTick >= tickCount)) {
    throw new RangeError('restoreAtTick must be inside the workload range')
  }
  return { tickCount, observerPasses, restoreAtTick, measureAfterTick }
}

export async function runCollisionWorkload(
  options: CollisionWorkloadOptions = {},
): Promise<CollisionWorkloadResult> {
  const { tickCount, observerPasses, restoreAtTick, measureAfterTick } = validateOptions(options)
  let collision: RapierCollisionWorld = await createRapierCollisionWorld(
    COLLISION_FIXTURE_ARENA,
    COLLISION_WORKLOAD_INITIAL_CAPSULES,
  )
  const checkpointHashes: ContentHash[] = []
  const tickHashes: ContentHash[] = []
  const benchmarkSamplesMilliseconds: number[] = []
  let finalGameplayHash = contentHash({ workloadId: COLLISION_WORKLOAD_ID, tick: 0 })
  let finalCollisionHash = collisionCheckpointHash(collision.checkpoint(0))

  try {
    for (let tick = 1; tick <= tickCount; tick += 1) {
      const tickStart = options.now?.()
      const before = collision.checkpoint(tick - 1)
      collision.projectCapsules(before.capsules)

      for (let participantIndex = 0; participantIndex < before.capsules.length; participantIndex += 1) {
        const capsule = before.capsules[participantIndex]
        if (capsule !== undefined) {
          collision.moveCapsule(capsule.semanticId, movementFor(tick, participantIndex))
        }
      }

      const after = collision.checkpoint(tick)
      const origin = after.capsules[0]?.position ?? { x: 0, y: 1, z: 0 }
      const rayHits = collision.raycast(origin, { x: 0, y: 0, z: -1 }, 24, [after.capsules[0]?.semanticId ?? ''])
      const overlapHits = after.capsules[1] === undefined
        ? []
        : collision.overlapCapsule(after.capsules[1], [after.capsules[1].semanticId])
      finalCollisionHash = collisionCheckpointHash(after)
      finalGameplayHash = contentHash({
        workloadId: COLLISION_WORKLOAD_ID,
        tick,
        capsules: after.capsules,
        rayHits,
        overlapHits,
      })
      const tickHash = contentHash({ tick, finalGameplayHash, finalCollisionHash })
      tickHashes.push(tickHash)

      for (let pass = 0; pass < observerPasses; pass += 1) {
        // This deliberately has no externally visible effect. It models one to
        // four renderer/HUD observers reading the same immutable snapshot.
        contentHash(collision.checkpoint(tick))
      }

      if (tick % COLLISION_WORKLOAD_CHECKPOINT_INTERVAL === 0 || tick === tickCount) {
        checkpointHashes.push(tickHash)
      }

      if (tickStart !== undefined && tick > measureAfterTick) {
        const tickEnd = options.now?.()
        if (tickEnd === undefined) throw new Error('Collision benchmark clock became unavailable')
        benchmarkSamplesMilliseconds.push(tickEnd - tickStart)
      }

      if (tick === restoreAtTick) {
        const serialized = serializeCollisionCheckpointPayload(after)
        const restored = parseCollisionCheckpointPayload(serialized)
        collision.dispose()
        collision = await createRapierCollisionWorld(COLLISION_FIXTURE_ARENA, restored.capsules)
      }
    }
  } finally {
    collision.dispose()
  }

  return deepFreeze({
    workloadId: COLLISION_WORKLOAD_ID,
    tickCount,
    tickHashes,
    checkpointHashes,
    finalGameplayHash,
    finalCollisionHash,
    benchmarkSamplesMilliseconds,
  })
}

export async function runCollisionBrowserBenchmark(
  warmupTicks: number = 256,
  sampleCount: number = COLLISION_BENCHMARK_SAMPLE_COUNT,
  now: () => number = () => performance.now(),
): Promise<CollisionBenchmarkSummary> {
  const result = await runCollisionWorkload({
    tickCount: warmupTicks + sampleCount,
    measureAfterTick: warmupTicks,
    now,
  })
  if (result.benchmarkSamplesMilliseconds.length !== sampleCount) {
    throw new Error(`${COLLISION_BENCHMARK_WORKLOAD_ID} produced an invalid sample count`)
  }
  return summarizeCollisionBenchmark(result.benchmarkSamplesMilliseconds, warmupTicks)
}
