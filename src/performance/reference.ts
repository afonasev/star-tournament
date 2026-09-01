import { deepFreeze } from '../core/deepFreeze'

export const PERFORMANCE_REFERENCE_SCHEMA_VERSION = 1 as const
export const PERFORMANCE_REFERENCE_ID = 'performance-reference-v1' as const

export const PERFORMANCE_PHASE_IDS = [
  'paused',
  'running-idle',
  'movement-jump',
  'combat-burst',
] as const

export type PerformancePhaseId = (typeof PERFORMANCE_PHASE_IDS)[number]
export type RunningPerformancePhaseId = Exclude<PerformancePhaseId, 'paused'>

export interface PausedHardPerformanceBudget {
  readonly kind: 'zero-work'
  readonly maximumAnimationCallbacks: 0
  readonly maximumSimulationSteps: 0
  readonly maximumWebGlSubmissions: 0
  readonly maximumHudPublications: 0
}

export interface RunningHardPerformanceBudget {
  readonly kind: 'running-cadence'
  readonly minimumSimulationStepsPerSecond: 59
  readonly maximumSimulationStepsPerSecond: 61
  readonly maximumWebGlSubmissionsPerSecond: 61
  readonly maximumHudPublicationsPerSecond: 11
}

export type HardPerformanceBudget =
  | PausedHardPerformanceBudget
  | RunningHardPerformanceBudget

export type ProvisionalPerformanceMetricId =
  | 'animation-frame-duration-milliseconds'
  | 'simulation-step-duration-milliseconds'
  | 'renderer-draw-calls'
  | 'long-tasks'
  | 'heap'
  | 'thermal'

export interface PerformanceReference {
  readonly schemaVersion: typeof PERFORMANCE_REFERENCE_SCHEMA_VERSION
  readonly id: typeof PERFORMANCE_REFERENCE_ID
  readonly revision: 1
  readonly hardBudgets: Readonly<Record<PerformancePhaseId, HardPerformanceBudget>>
  readonly provisionalMetrics: readonly ProvisionalPerformanceMetricId[]
}

const pausedBudget: PausedHardPerformanceBudget = {
  kind: 'zero-work',
  maximumAnimationCallbacks: 0,
  maximumSimulationSteps: 0,
  maximumWebGlSubmissions: 0,
  maximumHudPublications: 0,
}

const runningBudget: RunningHardPerformanceBudget = {
  kind: 'running-cadence',
  minimumSimulationStepsPerSecond: 59,
  maximumSimulationStepsPerSecond: 61,
  maximumWebGlSubmissionsPerSecond: 61,
  maximumHudPublicationsPerSecond: 11,
}

export const PERFORMANCE_REFERENCE_V1: PerformanceReference = deepFreeze({
  schemaVersion: PERFORMANCE_REFERENCE_SCHEMA_VERSION,
  id: PERFORMANCE_REFERENCE_ID,
  revision: 1,
  hardBudgets: {
    paused: pausedBudget,
    'running-idle': runningBudget,
    'movement-jump': runningBudget,
    'combat-burst': runningBudget,
  },
  provisionalMetrics: [
    'animation-frame-duration-milliseconds',
    'simulation-step-duration-milliseconds',
    'renderer-draw-calls',
    'long-tasks',
    'heap',
    'thermal',
  ],
})
