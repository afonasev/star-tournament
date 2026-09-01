import { describe, expect, it } from 'vitest'

import {
  PERFORMANCE_PHASE_IDS,
  PERFORMANCE_REFERENCE_V1,
} from './reference'

describe('performance-reference-v1', () => {
  it('defines immutable portable budgets for every reference phase', () => {
    expect(PERFORMANCE_REFERENCE_V1).toMatchObject({
      schemaVersion: 1,
      id: 'performance-reference-v1',
      revision: 1,
    })
    expect(Object.keys(PERFORMANCE_REFERENCE_V1.hardBudgets)).toEqual(PERFORMANCE_PHASE_IDS)
    expect(PERFORMANCE_REFERENCE_V1.hardBudgets.paused).toEqual({
      kind: 'zero-work',
      maximumAnimationCallbacks: 0,
      maximumSimulationSteps: 0,
      maximumWebGlSubmissions: 0,
      maximumHudPublications: 0,
    })
    for (const phase of ['running-idle', 'movement-jump', 'combat-burst'] as const) {
      expect(PERFORMANCE_REFERENCE_V1.hardBudgets[phase]).toEqual({
        kind: 'running-cadence',
        minimumSimulationStepsPerSecond: 59,
        maximumSimulationStepsPerSecond: 61,
        maximumWebGlSubmissionsPerSecond: 61,
        maximumHudPublicationsPerSecond: 11,
      })
    }
    expect(PERFORMANCE_REFERENCE_V1.provisionalMetrics).toEqual([
      'animation-frame-duration-milliseconds',
      'simulation-step-duration-milliseconds',
      'renderer-draw-calls',
      'long-tasks',
      'heap',
      'thermal',
    ])
    expect(Object.isFrozen(PERFORMANCE_REFERENCE_V1)).toBe(true)
    expect(Object.isFrozen(PERFORMANCE_REFERENCE_V1.hardBudgets)).toBe(true)
  })
})
