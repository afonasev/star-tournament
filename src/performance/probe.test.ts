import { describe, expect, it } from 'vitest'

import {
  PresentationPerformanceProbe,
  evaluateHardPerformanceGate,
} from './probe'

function manualClock(initial = 0) {
  let milliseconds = initial
  return {
    now: () => milliseconds,
    advance: (durationMilliseconds: number) => {
      milliseconds += durationMilliseconds
    },
  }
}

describe('PresentationPerformanceProbe', () => {
  it('records portable counters, duration summaries and renderer evidence', () => {
    const clock = manualClock(100)
    const probe = new PresentationPerformanceProbe(clock.now)
    probe.startPhase('running-idle')
    for (let index = 0; index < 60; index += 1) {
      probe.recordAnimationCallback(index % 2 === 0 ? 4 : 6)
      probe.recordSimulationStep(index % 2 === 0 ? 1 : 2)
      probe.recordWebGlSubmission({
        drawCalls: 30 + index % 2,
        triangles: 4_000,
        geometries: 33,
        textures: 0,
        shaderPrograms: 3,
      })
    }
    for (let index = 0; index < 10; index += 1) probe.recordHudPublication()
    probe.declareMetricAvailable('long-tasks')
    probe.recordHeapSample(1_000)
    probe.recordHeapSample(1_120)
    probe.recordThermalState('nominal')
    clock.advance(1_000)

    const measurement = probe.finishPhase()

    expect(measurement.counters).toEqual({
      animationCallbacks: 60,
      simulationSteps: 60,
      webGlSubmissions: 60,
      hudPublications: 10,
    })
    expect(measurement.rates).toEqual({
      animationCallbacksPerSecond: 60,
      simulationStepsPerSecond: 60,
      webGlSubmissionsPerSecond: 60,
      hudPublicationsPerSecond: 10,
    })
    expect(measurement.provisional.animationFrameDurationMilliseconds).toMatchObject({
      availability: 'available',
      value: { sampleCount: 60, medianMilliseconds: 4, p95Milliseconds: 6 },
    })
    expect(measurement.provisional.simulationStepDurationMilliseconds).toMatchObject({
      availability: 'available',
      value: { sampleCount: 60, maximumMilliseconds: 2 },
    })
    expect(measurement.provisional.rendererInfo).toEqual({
      availability: 'available',
      value: {
        sampleCount: 60,
        maximumDrawCalls: 31,
        maximumTriangles: 4_000,
        maximumGeometries: 33,
        maximumTextures: 0,
        maximumShaderPrograms: 3,
      },
    })
    expect(measurement.provisional.longTasks).toEqual({
      availability: 'available',
      value: { count: 0, totalDurationMilliseconds: 0, maximumDurationMilliseconds: 0 },
    })
    expect(measurement.provisional.heap).toMatchObject({
      availability: 'available',
      value: { deltaBytes: 120 },
    })
    expect(measurement.provisional.thermal).toEqual({
      availability: 'available',
      value: { sampleCount: 1, states: ['nominal'], lastState: 'nominal' },
    })
    expect(Object.isFrozen(measurement)).toBe(true)
    expect(evaluateHardPerformanceGate(measurement).passed).toBe(true)
  })

  it('enforces zero paused work independently from optional evidence', () => {
    const clock = manualClock()
    const probe = new PresentationPerformanceProbe(clock.now)
    probe.startPhase('paused')
    probe.markMetricUnavailable('renderer-draw-calls')
    probe.markMetricUnavailable('long-tasks')
    probe.markMetricUnavailable('heap')
    probe.markMetricUnavailable('thermal')
    clock.advance(30_000)

    const measurement = probe.finishPhase()
    const gate = evaluateHardPerformanceGate(measurement)

    expect(measurement.counters).toEqual({
      animationCallbacks: 0,
      simulationSteps: 0,
      webGlSubmissions: 0,
      hudPublications: 0,
    })
    expect(measurement.provisional.rendererInfo).toEqual({
      availability: 'unavailable',
      reason: 'unsupported',
    })
    expect(gate.passed).toBe(true)
    expect(gate.checks).toHaveLength(4)
  })

  it('fails paused animation work even when simulation and presentation stay idle', () => {
    const clock = manualClock()
    const probe = new PresentationPerformanceProbe(clock.now)
    probe.startPhase('paused')
    probe.recordAnimationCallback(0.1)
    clock.advance(1_000)

    const gate = evaluateHardPerformanceGate(probe.finishPhase())

    expect(gate.passed).toBe(false)
    expect(gate.checks[0]).toMatchObject({
      metric: 'animation-callbacks',
      actual: 1,
      maximum: 0,
      passed: false,
    })
  })

  it('fails cadence regressions and keeps optional metrics explicitly unavailable', () => {
    const clock = manualClock()
    const probe = new PresentationPerformanceProbe(clock.now)
    probe.startPhase('combat-burst')
    for (let index = 0; index < 58; index += 1) probe.recordSimulationStep(2)
    for (let index = 0; index < 62; index += 1) probe.recordWebGlSubmission()
    for (let index = 0; index < 12; index += 1) probe.recordHudPublication()
    probe.markMetricUnavailable('renderer-draw-calls', 'unsupported')
    probe.declareMetricAvailable('heap')
    probe.recordHeapSample(1_000)
    clock.advance(1_000)

    const measurement = probe.finishPhase()
    const gate = evaluateHardPerformanceGate(measurement)

    expect(gate.passed).toBe(false)
    expect(gate.checks.map((check) => check.passed)).toEqual([false, false, false])
    expect(measurement.provisional.rendererInfo).toEqual({
      availability: 'unavailable',
      reason: 'unsupported',
    })
    expect(measurement.provisional.heap).toEqual({
      availability: 'unavailable',
      reason: 'insufficient-samples',
    })
    expect(measurement.provisional.thermal).toEqual({
      availability: 'unavailable',
      reason: 'not-observed',
    })
  })

  it('rejects invalid lifecycle and samples', () => {
    const clock = manualClock()
    const probe = new PresentationPerformanceProbe(clock.now)
    expect(() => probe.recordHudPublication()).toThrow(/No performance phase/)
    probe.startPhase('movement-jump')
    expect(() => probe.startPhase('paused')).toThrow(/already active/)
    expect(() => probe.recordSimulationStep(-1)).toThrow(/non-negative/)
    expect(() => probe.recordThermalState('')).toThrow(/non-empty/)
    expect(() => probe.finishPhase()).toThrow(/greater than zero/)
  })
})
