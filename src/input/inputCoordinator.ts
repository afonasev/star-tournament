import { createActionFrame, type ActionFrame } from './actionFrame'
import type { UiActionSnapshot } from './keyboardMouseAdapter'

export interface LocalInputAdapter {
  readonly participantId: string
  readonly drainActionFrame: (tick: number) => ActionFrame
  readonly drainUiActionSnapshot: () => Readonly<UiActionSnapshot>
  readonly clearInputState: () => void
  readonly dispose: () => void
  readonly isAvailable?: () => boolean
  readonly describeUnavailable?: () => string
}

export class InputCoordinator {
  constructor(private readonly adapters: readonly LocalInputAdapter[]) {}

  drainActionFrame(tick: number): ActionFrame {
    return createActionFrame(tick, this.adapters.flatMap((adapter) => adapter.drainActionFrame(tick).actions))
  }

  drainUiActionSnapshot(): Readonly<UiActionSnapshot> {
    const snapshots = this.adapters.map((adapter) => ({ adapter, snapshot: adapter.drainUiActionSnapshot() }))
    return Object.freeze({
      scoreboardHeld: snapshots.some(({ snapshot }) => snapshot.scoreboardHeld),
      scoreboardHeldSeatIds: snapshots.filter(({ snapshot }) => snapshot.scoreboardHeld).map(({ adapter }) => adapter.participantId),
      pauseRequested: snapshots.some(({ snapshot }) => snapshot.pauseRequested),
    })
  }

  unavailableParticipantId(): string | undefined {
    return this.adapters.find((adapter) => adapter.isAvailable?.() === false)?.describeUnavailable?.()
  }

  clearInputState(): void { this.adapters.forEach((adapter) => adapter.clearInputState()) }
  dispose(): void { this.adapters.forEach((adapter) => adapter.dispose()) }
}
