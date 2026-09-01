export type RuntimeStatus = 'starting' | 'running' | 'stopped' | 'error'

export interface UiSnapshot {
  readonly status: RuntimeStatus
  readonly tick: number
  readonly stateHash: string
  readonly profileId: string
  readonly profileHash: string
  readonly arenaId: string
  readonly arenaHash: string
  readonly errorMessage?: string
}

export function freezeUiSnapshot(snapshot: UiSnapshot): Readonly<UiSnapshot> {
  return Object.freeze({ ...snapshot })
}
