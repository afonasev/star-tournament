import { deepFreeze } from '../core/deepFreeze'
import {
  PERFORMANCE_REFERENCE_ID,
  PERFORMANCE_REFERENCE_SCHEMA_VERSION,
  PERFORMANCE_REFERENCE_V1,
  type HardPerformanceBudget,
  type PerformancePhaseId,
  type PerformanceReference,
  type ProvisionalPerformanceMetricId,
} from './reference'

export type UnavailablePerformanceMetricReason =
  | 'unsupported'
  | 'not-observed'
  | 'insufficient-samples'

export interface AvailablePerformanceMetric<Value> {
  readonly availability: 'available'
  readonly value: Value
}

export interface UnavailablePerformanceMetric {
  readonly availability: 'unavailable'
  readonly reason: UnavailablePerformanceMetricReason
}

export type OptionalPerformanceMetric<Value> =
  | AvailablePerformanceMetric<Value>
  | UnavailablePerformanceMetric

export interface DurationSampleSummary {
  readonly sampleCount: number
  readonly minimumMilliseconds: number
  readonly medianMilliseconds: number
  readonly p95Milliseconds: number
  readonly p99Milliseconds: number
  readonly maximumMilliseconds: number
  readonly meanMilliseconds: number
}

export interface RendererInfoSample {
  readonly drawCalls: number
  readonly triangles: number
  readonly geometries: number
  readonly textures: number
  readonly shaderPrograms: number
}

export interface RendererInfoSummary {
  readonly sampleCount: number
  readonly maximumDrawCalls: number
  readonly maximumTriangles: number
  readonly maximumGeometries: number
  readonly maximumTextures: number
  readonly maximumShaderPrograms: number
}

export interface LongTaskSummary {
  readonly count: number
  readonly totalDurationMilliseconds: number
  readonly maximumDurationMilliseconds: number
}

export interface HeapSummary {
  readonly sampleCount: number
  readonly firstBytes: number
  readonly lastBytes: number
  readonly minimumBytes: number
  readonly maximumBytes: number
  readonly deltaBytes: number
}

export interface ThermalSummary {
  readonly sampleCount: number
  readonly states: readonly string[]
  readonly lastState: string
}

export interface PerformanceCounters {
  readonly animationCallbacks: number
  readonly simulationSteps: number
  readonly webGlSubmissions: number
  readonly hudPublications: number
}

export interface PerformanceRates {
  readonly animationCallbacksPerSecond: number
  readonly simulationStepsPerSecond: number
  readonly webGlSubmissionsPerSecond: number
  readonly hudPublicationsPerSecond: number
}

export interface ProvisionalPerformanceEvidence {
  readonly animationFrameDurationMilliseconds: OptionalPerformanceMetric<DurationSampleSummary>
  readonly simulationStepDurationMilliseconds: OptionalPerformanceMetric<DurationSampleSummary>
  readonly rendererInfo: OptionalPerformanceMetric<RendererInfoSummary>
  readonly longTasks: OptionalPerformanceMetric<LongTaskSummary>
  readonly heap: OptionalPerformanceMetric<HeapSummary>
  readonly thermal: OptionalPerformanceMetric<ThermalSummary>
}

export interface PerformancePhaseMeasurement {
  readonly schemaVersion: typeof PERFORMANCE_REFERENCE_SCHEMA_VERSION
  readonly referenceId: typeof PERFORMANCE_REFERENCE_ID
  readonly phase: PerformancePhaseId
  readonly durationMilliseconds: number
  readonly counters: PerformanceCounters
  readonly rates: PerformanceRates
  readonly provisional: ProvisionalPerformanceEvidence
}

export interface HardPerformanceCheck {
  readonly metric:
    | 'animation-callbacks'
    | 'simulation-steps'
    | 'webgl-submissions'
    | 'hud-publications'
    | 'simulation-steps-per-second'
    | 'webgl-submissions-per-second'
    | 'hud-publications-per-second'
  readonly actual: number
  readonly minimum?: number
  readonly maximum: number
  readonly passed: boolean
}

export interface HardPerformanceGateResult {
  readonly referenceId: typeof PERFORMANCE_REFERENCE_ID
  readonly phase: PerformancePhaseId
  readonly passed: boolean
  readonly checks: readonly HardPerformanceCheck[]
}

interface MutablePhaseMeasurement {
  readonly phase: PerformancePhaseId
  readonly startedAtMilliseconds: number
  animationCallbacks: number
  simulationSteps: number
  webGlSubmissions: number
  hudPublications: number
  readonly animationDurations: number[]
  readonly simulationDurations: number[]
  readonly rendererInfoSamples: RendererInfoSample[]
  readonly longTaskDurations: number[]
  readonly heapSamples: number[]
  readonly thermalStates: string[]
  readonly declaredAvailable: Set<ProvisionalPerformanceMetricId>
  readonly unavailableReasons: Map<ProvisionalPerformanceMetricId, UnavailablePerformanceMetricReason>
}

function available<Value>(value: Value): AvailablePerformanceMetric<Value> {
  return deepFreeze({ availability: 'available', value })
}

function unavailable(
  reason: UnavailablePerformanceMetricReason,
): UnavailablePerformanceMetric {
  return deepFreeze({ availability: 'unavailable', reason })
}

function assertFiniteNonNegative(value: number, label: string): void {
  if (!Number.isFinite(value) || value < 0) {
    throw new RangeError(`${label} must be a finite non-negative number`)
  }
}

function percentile(sorted: readonly number[], percentileValue: number): number {
  const rank = Math.ceil(sorted.length * percentileValue) - 1
  return sorted[Math.max(0, rank)] ?? 0
}

function summarizeDurations(
  phase: MutablePhaseMeasurement,
  metric: ProvisionalPerformanceMetricId,
  samples: readonly number[],
): OptionalPerformanceMetric<DurationSampleSummary> {
  if (samples.length === 0) {
    const reason = phase.unavailableReasons.get(metric) ??
      (phase.declaredAvailable.has(metric) ? 'insufficient-samples' : 'not-observed')
    return unavailable(reason)
  }
  const sorted = [...samples].sort((left, right) => left - right)
  const total = sorted.reduce((sum, sample) => sum + sample, 0)
  return available({
    sampleCount: sorted.length,
    minimumMilliseconds: sorted[0] ?? 0,
    medianMilliseconds: percentile(sorted, 0.5),
    p95Milliseconds: percentile(sorted, 0.95),
    p99Milliseconds: percentile(sorted, 0.99),
    maximumMilliseconds: sorted[sorted.length - 1] ?? 0,
    meanMilliseconds: total / sorted.length,
  })
}

function maximum(
  samples: readonly RendererInfoSample[],
  select: (sample: RendererInfoSample) => number,
): number {
  return samples.reduce((result, sample) => Math.max(result, select(sample)), 0)
}

function metricReason(
  phase: MutablePhaseMeasurement,
  metric: ProvisionalPerformanceMetricId,
): UnavailablePerformanceMetricReason {
  return phase.unavailableReasons.get(metric) ?? 'not-observed'
}

function summarizeRendererInfo(
  phase: MutablePhaseMeasurement,
): OptionalPerformanceMetric<RendererInfoSummary> {
  const samples = phase.rendererInfoSamples
  if (samples.length === 0 && !phase.declaredAvailable.has('renderer-draw-calls')) {
    return unavailable(metricReason(phase, 'renderer-draw-calls'))
  }
  return available({
    sampleCount: samples.length,
    maximumDrawCalls: maximum(samples, (sample) => sample.drawCalls),
    maximumTriangles: maximum(samples, (sample) => sample.triangles),
    maximumGeometries: maximum(samples, (sample) => sample.geometries),
    maximumTextures: maximum(samples, (sample) => sample.textures),
    maximumShaderPrograms: maximum(samples, (sample) => sample.shaderPrograms),
  })
}

function summarizeLongTasks(
  phase: MutablePhaseMeasurement,
): OptionalPerformanceMetric<LongTaskSummary> {
  const samples = phase.longTaskDurations
  if (samples.length === 0 && !phase.declaredAvailable.has('long-tasks')) {
    return unavailable(metricReason(phase, 'long-tasks'))
  }
  return available({
    count: samples.length,
    totalDurationMilliseconds: samples.reduce((sum, duration) => sum + duration, 0),
    maximumDurationMilliseconds: Math.max(0, ...samples),
  })
}

function summarizeHeap(phase: MutablePhaseMeasurement): OptionalPerformanceMetric<HeapSummary> {
  const samples = phase.heapSamples
  if (samples.length < 2) {
    const reason = phase.unavailableReasons.get('heap') ??
      (phase.declaredAvailable.has('heap') ? 'insufficient-samples' : 'not-observed')
    return unavailable(reason)
  }
  const first = samples[0] ?? 0
  const last = samples[samples.length - 1] ?? first
  return available({
    sampleCount: samples.length,
    firstBytes: first,
    lastBytes: last,
    minimumBytes: Math.min(...samples),
    maximumBytes: Math.max(...samples),
    deltaBytes: last - first,
  })
}

function summarizeThermal(
  phase: MutablePhaseMeasurement,
): OptionalPerformanceMetric<ThermalSummary> {
  const samples = phase.thermalStates
  if (samples.length === 0) {
    const reason = phase.unavailableReasons.get('thermal') ??
      (phase.declaredAvailable.has('thermal') ? 'insufficient-samples' : 'not-observed')
    return unavailable(reason)
  }
  return available({
    sampleCount: samples.length,
    states: [...samples],
    lastState: samples[samples.length - 1] ?? '',
  })
}

function createMutablePhase(
  phase: PerformancePhaseId,
  startedAtMilliseconds: number,
): MutablePhaseMeasurement {
  return {
    phase,
    startedAtMilliseconds,
    animationCallbacks: 0,
    simulationSteps: 0,
    webGlSubmissions: 0,
    hudPublications: 0,
    animationDurations: [],
    simulationDurations: [],
    rendererInfoSamples: [],
    longTaskDurations: [],
    heapSamples: [],
    thermalStates: [],
    declaredAvailable: new Set(),
    unavailableReasons: new Map(),
  }
}

export class PresentationPerformanceProbe {
  private currentPhase: MutablePhaseMeasurement | undefined

  constructor(private readonly now: () => number = () => performance.now()) {}

  startPhase(phase: PerformancePhaseId): void {
    if (this.currentPhase !== undefined) {
      throw new Error(`Performance phase ${this.currentPhase.phase} is already active`)
    }
    const startedAtMilliseconds = this.now()
    assertFiniteNonNegative(startedAtMilliseconds, 'Performance phase start time')
    this.currentPhase = createMutablePhase(phase, startedAtMilliseconds)
  }

  recordAnimationCallback(durationMilliseconds: number): void {
    const phase = this.activePhase()
    assertFiniteNonNegative(durationMilliseconds, 'Animation callback duration')
    phase.animationCallbacks += 1
    phase.declaredAvailable.add('animation-frame-duration-milliseconds')
    phase.animationDurations.push(durationMilliseconds)
  }

  recordSimulationStep(durationMilliseconds: number): void {
    const phase = this.activePhase()
    assertFiniteNonNegative(durationMilliseconds, 'Simulation step duration')
    phase.simulationSteps += 1
    phase.declaredAvailable.add('simulation-step-duration-milliseconds')
    phase.simulationDurations.push(durationMilliseconds)
  }

  recordWebGlSubmission(rendererInfo?: RendererInfoSample): void {
    const phase = this.activePhase()
    phase.webGlSubmissions += 1
    if (rendererInfo !== undefined) {
      for (const [label, value] of Object.entries(rendererInfo)) {
        assertFiniteNonNegative(value, `Renderer info ${label}`)
      }
      phase.declaredAvailable.add('renderer-draw-calls')
      phase.rendererInfoSamples.push({ ...rendererInfo })
    }
  }

  recordHudPublication(): void {
    this.activePhase().hudPublications += 1
  }

  recordLongTask(durationMilliseconds: number): void {
    const phase = this.activePhase()
    assertFiniteNonNegative(durationMilliseconds, 'Long task duration')
    phase.declaredAvailable.add('long-tasks')
    phase.longTaskDurations.push(durationMilliseconds)
  }

  recordHeapSample(bytes: number): void {
    const phase = this.activePhase()
    assertFiniteNonNegative(bytes, 'Heap sample')
    phase.declaredAvailable.add('heap')
    phase.heapSamples.push(bytes)
  }

  recordThermalState(state: string): void {
    if (state.length === 0) throw new TypeError('Thermal state must be non-empty')
    const phase = this.activePhase()
    phase.declaredAvailable.add('thermal')
    phase.thermalStates.push(state)
  }

  declareMetricAvailable(metric: ProvisionalPerformanceMetricId): void {
    const phase = this.activePhase()
    phase.unavailableReasons.delete(metric)
    phase.declaredAvailable.add(metric)
  }

  markMetricUnavailable(
    metric: ProvisionalPerformanceMetricId,
    reason: UnavailablePerformanceMetricReason = 'unsupported',
  ): void {
    const phase = this.activePhase()
    if (phase.declaredAvailable.has(metric)) {
      throw new Error(`Performance metric ${metric} was already observed as available`)
    }
    phase.unavailableReasons.set(metric, reason)
  }

  finishPhase(): PerformancePhaseMeasurement {
    const phase = this.activePhase()
    const finishedAtMilliseconds = this.now()
    assertFiniteNonNegative(finishedAtMilliseconds, 'Performance phase finish time')
    const durationMilliseconds = finishedAtMilliseconds - phase.startedAtMilliseconds
    if (!Number.isFinite(durationMilliseconds) || durationMilliseconds <= 0) {
      throw new RangeError('Performance phase duration must be greater than zero')
    }
    const seconds = durationMilliseconds / 1_000
    const counters: PerformanceCounters = {
      animationCallbacks: phase.animationCallbacks,
      simulationSteps: phase.simulationSteps,
      webGlSubmissions: phase.webGlSubmissions,
      hudPublications: phase.hudPublications,
    }
    const result: PerformancePhaseMeasurement = deepFreeze({
      schemaVersion: PERFORMANCE_REFERENCE_SCHEMA_VERSION,
      referenceId: PERFORMANCE_REFERENCE_ID,
      phase: phase.phase,
      durationMilliseconds,
      counters,
      rates: {
        animationCallbacksPerSecond: counters.animationCallbacks / seconds,
        simulationStepsPerSecond: counters.simulationSteps / seconds,
        webGlSubmissionsPerSecond: counters.webGlSubmissions / seconds,
        hudPublicationsPerSecond: counters.hudPublications / seconds,
      },
      provisional: {
        animationFrameDurationMilliseconds: summarizeDurations(
          phase,
          'animation-frame-duration-milliseconds',
          phase.animationDurations,
        ),
        simulationStepDurationMilliseconds: summarizeDurations(
          phase,
          'simulation-step-duration-milliseconds',
          phase.simulationDurations,
        ),
        rendererInfo: summarizeRendererInfo(phase),
        longTasks: summarizeLongTasks(phase),
        heap: summarizeHeap(phase),
        thermal: summarizeThermal(phase),
      },
    })
    this.currentPhase = undefined
    return result
  }

  private activePhase(): MutablePhaseMeasurement {
    if (this.currentPhase === undefined) {
      throw new Error('No performance phase is active')
    }
    return this.currentPhase
  }
}

function maximumCheck(
  metric: HardPerformanceCheck['metric'],
  actual: number,
  maximumValue: number,
): HardPerformanceCheck {
  return deepFreeze({
    metric,
    actual,
    maximum: maximumValue,
    passed: actual <= maximumValue,
  })
}

function rangeCheck(
  metric: HardPerformanceCheck['metric'],
  actual: number,
  minimumValue: number,
  maximumValue: number,
): HardPerformanceCheck {
  return deepFreeze({
    metric,
    actual,
    minimum: minimumValue,
    maximum: maximumValue,
    passed: actual >= minimumValue && actual <= maximumValue,
  })
}

function checksForBudget(
  measurement: PerformancePhaseMeasurement,
  budget: HardPerformanceBudget,
): readonly HardPerformanceCheck[] {
  if (budget.kind === 'zero-work') {
    return [
      maximumCheck(
        'animation-callbacks',
        measurement.counters.animationCallbacks,
        budget.maximumAnimationCallbacks,
      ),
      maximumCheck(
        'simulation-steps',
        measurement.counters.simulationSteps,
        budget.maximumSimulationSteps,
      ),
      maximumCheck(
        'webgl-submissions',
        measurement.counters.webGlSubmissions,
        budget.maximumWebGlSubmissions,
      ),
      maximumCheck(
        'hud-publications',
        measurement.counters.hudPublications,
        budget.maximumHudPublications,
      ),
    ]
  }
  return [
    rangeCheck(
      'simulation-steps-per-second',
      measurement.rates.simulationStepsPerSecond,
      budget.minimumSimulationStepsPerSecond,
      budget.maximumSimulationStepsPerSecond,
    ),
    maximumCheck(
      'webgl-submissions-per-second',
      measurement.rates.webGlSubmissionsPerSecond,
      budget.maximumWebGlSubmissionsPerSecond,
    ),
    maximumCheck(
      'hud-publications-per-second',
      measurement.rates.hudPublicationsPerSecond,
      budget.maximumHudPublicationsPerSecond,
    ),
  ]
}

export function evaluateHardPerformanceGate(
  measurement: PerformancePhaseMeasurement,
  reference: PerformanceReference = PERFORMANCE_REFERENCE_V1,
): HardPerformanceGateResult {
  if (
    measurement.schemaVersion !== reference.schemaVersion ||
    measurement.referenceId !== reference.id
  ) {
    throw new TypeError('Performance measurement identity does not match reference')
  }
  const checks = checksForBudget(measurement, reference.hardBudgets[measurement.phase])
  return deepFreeze({
    referenceId: reference.id,
    phase: measurement.phase,
    passed: checks.every((check) => check.passed),
    checks,
  })
}
