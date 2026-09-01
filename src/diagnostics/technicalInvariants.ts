import { MAX_DEVICE_PIXEL_RATIO } from '../render/diagnosticRenderer'
import {
  MAX_CATCH_UP_TICKS,
  MAX_WALL_CLOCK_DELTA_SECONDS,
} from '../runtime/browserRuntime'
import { SIMULATION_TICKS_PER_SECOND } from '../simulation'

export interface TechnicalInvariant {
  readonly id: string
  readonly value: number
  readonly rationale: string
}

export const TECHNICAL_INVARIANTS: readonly TechnicalInvariant[] = Object.freeze([
  Object.freeze({
    id: 'simulation.ticksPerSecond',
    value: SIMULATION_TICKS_PER_SECOND,
    rationale: 'Defines snapshot, replay and future network compatibility.',
  }),
  Object.freeze({
    id: 'runtime.maximumWallClockDeltaSeconds',
    value: MAX_WALL_CLOCK_DELTA_SECONDS,
    rationale: 'Bounds scheduler recovery after a background-tab wall-clock gap.',
  }),
  Object.freeze({
    id: 'runtime.maximumCatchUpTicks',
    value: MAX_CATCH_UP_TICKS,
    rationale: 'Prevents the browser event loop from freezing during scheduler recovery.',
  }),
  Object.freeze({
    id: 'render.maximumDevicePixelRatio',
    value: MAX_DEVICE_PIXEL_RATIO,
    rationale: 'Caps diagnostic GPU pixel cost independently of gameplay rules.',
  }),
])
