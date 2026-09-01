import { deepFreeze } from '../core/deepFreeze'
import type { CollisionBenchmarkSummary } from '../collision/benchmark'

export type RuntimeStatus = 'starting' | 'running' | 'stopped' | 'error'
export type CollisionRuntimeStatus = 'loading' | 'ready' | 'error'

export interface CollisionUiState {
  readonly status: CollisionRuntimeStatus
  readonly identity: string
  readonly fixtureId: string
  readonly fixtureStatus: 'pending' | 'passed' | 'failed'
  readonly checkpointHash?: string
  readonly initLatencyMilliseconds?: Readonly<{ cold: number; warm: number }>
  readonly benchmark?: CollisionBenchmarkSummary
  readonly errorCode?: string
}

export interface UiSnapshot {
  readonly status: RuntimeStatus
  readonly tick: number
  readonly stateHash: string
  readonly profileId: string
  readonly profileHash: string
  readonly arenaId: string
  readonly arenaHash: string
  readonly collision: CollisionUiState
  readonly errorMessage?: string
}

export function freezeUiSnapshot(snapshot: UiSnapshot): Readonly<UiSnapshot> {
  return deepFreeze({ ...snapshot })
}
