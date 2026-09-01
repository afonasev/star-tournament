import { describe, expect, it } from 'vitest'

import { DIAGNOSTIC_ARENA } from '../arena'
import type {
  CollisionCapsuleProjection,
  CollisionCapsuleMovementResult,
  CollisionHit,
  CollisionVector3,
} from '../collision/contracts'
import type { ContentHash } from '../core/canonical'
import { createActionFrame, type ActionFrame } from '../input/actionFrame'
import { PROTOTYPE_V1_PROFILE } from '../profiles'
import {
  createInitialSimulationSnapshot,
  parseSimulationSnapshot,
  serializeSimulationSnapshot,
  simulationSnapshotHash,
  type SimulationEvent,
  type SimulationSnapshot,
} from './playableSnapshot'
import {
  createPlayableSimulationReplay,
  parsePlayableSimulationReplay,
  runPlayableSimulationReplay,
  serializePlayableSimulationReplay,
  type PlayableHeadlessRunResult,
  type PlayableSimulationReplay,
  type PlayableSimulationStep,
} from './playableReplay'
import { stepPlayableSimulation, type PlayableCollisionPort } from './playableStep'

const PARTICIPANT_ID = 'local-player-1'
const MOVEMENT_TICK_COUNT = 10_000
const RESTORE_TICK = 5_000
const COMBAT_RESTORE_TICK = 44

/**
 * Backend-neutral collision fixture for determinism verification. Projection is
 * authoritative every tick, so a restored replay does not inherit hidden port
 * state. The only geometry is an infinite y=0 floor and a deterministic
 * backstop used to turn every missed pellet into an ordered static impact.
 */
class DeterministicCollisionPort implements PlayableCollisionPort {
  private readonly capsules = new Map<string, CollisionCapsuleProjection>()

  projectCapsules(capsules: readonly CollisionCapsuleProjection[]): void {
    this.capsules.clear()
    for (const capsule of capsules) {
      this.capsules.set(capsule.semanticId, {
        ...capsule,
        position: { ...capsule.position },
      })
    }
  }

  moveCapsule(
    semanticId: string,
    desiredTranslation: CollisionVector3,
  ): CollisionCapsuleMovementResult {
    const capsule = this.capsules.get(semanticId)
    if (capsule === undefined) {
      throw new Error(`Capsule ${semanticId} must be projected before movement`)
    }
    const floorCenterY = capsule.radius + capsule.halfHeight
    const desiredY = capsule.position.y + desiredTranslation.y
    const grounded = desiredY <= floorCenterY
    const position = {
      x: capsule.position.x + desiredTranslation.x,
      y: grounded ? floorCenterY : desiredY,
      z: capsule.position.z + desiredTranslation.z,
    }
    this.capsules.set(semanticId, { ...capsule, position })
    return {
      position,
      grounded,
      contacts: grounded
        ? [{
            semanticId: 'deterministic-floor',
            distance: 0,
            point: { x: position.x, y: 0, z: position.z },
            normal: { x: 0, y: 1, z: 0 },
          }]
        : [],
    }
  }

  raycast(
    origin: CollisionVector3,
    direction: CollisionVector3,
    maximumDistance: number,
    excludeSemanticIds: readonly string[] = [],
  ): readonly CollisionHit[] {
    if (excludeSemanticIds.includes('deterministic-backstop')) return []
    return [{
      semanticId: 'deterministic-backstop',
      distance: maximumDistance,
      point: {
        x: origin.x + direction.x * maximumDistance,
        y: origin.y + direction.y * maximumDistance,
        z: origin.z + direction.z * maximumDistance,
      },
      normal: { x: -direction.x, y: -direction.y, z: -direction.z },
    }]
  }
}

function initialSnapshot(): SimulationSnapshot {
  return createInitialSimulationSnapshot({
    rngSeed: 0x51a7e,
    arena: {
      id: DIAGNOSTIC_ARENA.id,
      seed: DIAGNOSTIC_ARENA.seed,
      generatorVersion: DIAGNOSTIC_ARENA.generatorVersion,
      contentHash: DIAGNOSTIC_ARENA.contentHash as ContentHash,
    },
  })
}

function createStepper(): PlayableSimulationStep {
  const collision = new DeterministicCollisionPort()
  return (snapshot, frame) =>
    stepPlayableSimulation(snapshot, frame, collision, PROTOTYPE_V1_PROFILE)
}

function movementFrames(): readonly ActionFrame[] {
  return Array.from({ length: MOVEMENT_TICK_COUNT }, (_, index) => {
    const tick = index + 1
    const movementPhase = Math.floor(index / 300) % 4
    const lookPhase = Math.floor(index / 120) % 4
    const forward = movementPhase === 0 ? 1 : movementPhase === 2 ? -1 : 0
    const right = movementPhase === 1 ? 0.75 : movementPhase === 3 ? -0.75 : 0
    const lookYaw = lookPhase < 2 ? 0.2 : -0.2
    const lookPitch = lookPhase === 0 || lookPhase === 3 ? 0.1 : -0.1
    const jump = index % 240 === 0 ? 1 : 0
    return createActionFrame(tick, [
      { participantId: PARTICIPANT_ID, actionId: 'move-forward', value: forward },
      { participantId: PARTICIPANT_ID, actionId: 'move-right', value: right },
      { participantId: PARTICIPANT_ID, actionId: 'look-yaw', value: lookYaw },
      { participantId: PARTICIPANT_ID, actionId: 'look-pitch', value: lookPitch },
      { participantId: PARTICIPANT_ID, actionId: 'jump', value: jump },
    ])
  })
}

interface ObservedRun {
  readonly result: PlayableHeadlessRunResult
  readonly observedHashes: readonly ContentHash[]
  readonly serializedCheckpoint: string | null
}

function runWithObserverCadence(
  replay: PlayableSimulationReplay,
  cadence: 0 | 1 | 2 | 4,
  checkpointTick: number | null = null,
): ObservedRun {
  const observedHashes: ContentHash[] = []
  let serializedCheckpoint: string | null = null
  const simulationStep = createStepper()
  const observingStep: PlayableSimulationStep = (snapshot, frame) => {
    const next = simulationStep(snapshot, frame)
    if (checkpointTick === next.tick) {
      serializedCheckpoint = serializeSimulationSnapshot(next)
    }
    if (cadence !== 0 && next.tick % cadence === 0) {
      observedHashes.push(simulationSnapshotHash(next))
    }
    return next
  }
  return {
    result: runPlayableSimulationReplay(replay, observingStep),
    observedHashes,
    serializedCheckpoint,
  }
}

function combatFrames(): readonly ActionFrame[] {
  const pressedTicks = new Set([1, 2, 4, 43, 85])
  return Array.from({ length: 90 }, (_, index) => {
    const tick = index + 1
    return createActionFrame(tick, [{
      participantId: PARTICIPANT_ID,
      actionId: 'fire',
      value: pressedTicks.has(tick) ? 1 : 0,
    }])
  })
}

interface CombatTickRecord {
  readonly tick: number
  readonly ammo: number
  readonly shotSequence: number
  readonly targets: readonly {
    readonly semanticId: string
    readonly health: number
    readonly active: boolean
  }[]
  readonly events: readonly SimulationEvent[]
}

interface CombatRun {
  readonly result: PlayableHeadlessRunResult
  readonly records: readonly CombatTickRecord[]
}

function recordCombatTick(snapshot: SimulationSnapshot): CombatTickRecord {
  return {
    tick: snapshot.tick,
    ammo: snapshot.weapon.ammo,
    shotSequence: snapshot.weapon.shotSequence,
    targets: snapshot.targets.map((target) => ({ ...target })),
    events: snapshot.events.map((event) => ({ ...event })),
  }
}

function runCombatReplay(replay: PlayableSimulationReplay): CombatRun {
  const records: CombatTickRecord[] = []
  const simulationStep = createStepper()
  const recordingStep: PlayableSimulationStep = (snapshot, frame) => {
    const next = simulationStep(snapshot, frame)
    records.push(recordCombatTick(next))
    return next
  }
  return {
    result: runPlayableSimulationReplay(replay, recordingStep),
    records,
  }
}

function shotDirections(records: readonly CombatTickRecord[]) {
  return records.flatMap((record) => record.events
    .filter((event) => event.kind === 'shotgun-shot')
    .map((event) => ({
      tick: event.tick,
      shotSequence: event.shotSequence,
      pelletDirections: event.pelletDirections,
    })))
}

function orderedHits(records: readonly CombatTickRecord[]) {
  return records.flatMap((record) => record.events
    .filter((event) => event.kind === 'shotgun-impact')
    .map((event) => ({
      eventId: event.eventId,
      tick: event.tick,
      pelletIndex: event.pelletIndex,
      targetId: event.targetId,
      surfaceId: event.surfaceId,
      zone: event.zone,
      position: event.position,
      normal: event.normal,
    })))
}

describe('playable simulation long-run determinism', () => {
  it('keeps 10,000 movement/look/jump tick hashes identical across runs, restore, and observer cadences', () => {
    const frames = movementFrames()
    const canonicalReplay = parsePlayableSimulationReplay(serializePlayableSimulationReplay(
      createPlayableSimulationReplay(initialSnapshot(), frames),
    ))
    const baseline = runWithObserverCadence(canonicalReplay, 0, RESTORE_TICK)
    const independent = runWithObserverCadence(canonicalReplay, 1)
    const cadenceTwo = runWithObserverCadence(canonicalReplay, 2)
    const cadenceFour = runWithObserverCadence(canonicalReplay, 4)

    expect(baseline.result.tickHashes).toHaveLength(MOVEMENT_TICK_COUNT)
    expect(baseline.result.finalSnapshot.tick).toBe(MOVEMENT_TICK_COUNT)
    expect(independent.result).toEqual(baseline.result)
    expect(cadenceTwo.result).toEqual(baseline.result)
    expect(cadenceFour.result).toEqual(baseline.result)
    expect(independent.observedHashes).toEqual(baseline.result.tickHashes)
    expect(cadenceTwo.observedHashes).toEqual(
      baseline.result.tickHashes.filter((_, index) => (index + 1) % 2 === 0),
    )
    expect(cadenceFour.observedHashes).toEqual(
      baseline.result.tickHashes.filter((_, index) => (index + 1) % 4 === 0),
    )

    expect(baseline.serializedCheckpoint).not.toBeNull()
    const restoredSnapshot = parseSimulationSnapshot(baseline.serializedCheckpoint!)
    const restoredReplay = parsePlayableSimulationReplay(serializePlayableSimulationReplay(
      createPlayableSimulationReplay(restoredSnapshot, frames.slice(RESTORE_TICK)),
    ))
    const restored = runPlayableSimulationReplay(restoredReplay, createStepper())
    expect(restored.tickHashes).toEqual(baseline.result.tickHashes.slice(RESTORE_TICK))
    expect(restored.finalSnapshot).toEqual(baseline.result.finalSnapshot)
  }, 60_000)

  it('keeps combat edges, cooldown, pellet hits, ammo, health, events, and hashes identical after independent and restored runs', () => {
    const aimedInitial = parseSimulationSnapshot({
      ...initialSnapshot(),
      player: { ...initialSnapshot().player, pitch: -0.06 },
    })
    const frames = combatFrames()
    const replay = parsePlayableSimulationReplay(serializePlayableSimulationReplay(
      createPlayableSimulationReplay(aimedInitial, frames),
    ))
    const baseline = runCombatReplay(replay)
    const independent = runCombatReplay(replay)

    expect(independent).toEqual(baseline)
    expect(baseline.result.tickHashes).toHaveLength(90)
    expect(baseline.result.finalSnapshot.weapon).toMatchObject({
      ammo: 17,
      shotSequence: 3,
    })
    expect(shotDirections(baseline.records)).toHaveLength(3)
    expect(shotDirections(baseline.records).every((shot) =>
      shot.pelletDirections.length === PROTOTYPE_V1_PROFILE.rules.weapons.doubleBarrelShotgun.pelletCount
    )).toBe(true)
    expect(orderedHits(baseline.records)).toHaveLength(
      3 * PROTOTYPE_V1_PROFILE.rules.weapons.doubleBarrelShotgun.pelletCount,
    )
    expect(baseline.result.finalSnapshot.targets.some((target) => target.health < 100)).toBe(true)

    const checkpoint = baseline.records[COMBAT_RESTORE_TICK - 1]
    expect(checkpoint?.tick).toBe(COMBAT_RESTORE_TICK)
    const restoredSnapshot = parseSimulationSnapshot(serializeSimulationSnapshot(
      runCombatReplay(createPlayableSimulationReplay(aimedInitial, frames.slice(0, COMBAT_RESTORE_TICK)))
        .result.finalSnapshot,
    ))
    const restoredReplay = parsePlayableSimulationReplay(serializePlayableSimulationReplay(
      createPlayableSimulationReplay(restoredSnapshot, frames.slice(COMBAT_RESTORE_TICK)),
    ))
    const restoredTail = runCombatReplay(restoredReplay)
    const combinedRecords = [
      ...baseline.records.slice(0, COMBAT_RESTORE_TICK),
      ...restoredTail.records,
    ]

    expect(restoredTail.result.tickHashes).toEqual(
      baseline.result.tickHashes.slice(COMBAT_RESTORE_TICK),
    )
    expect(restoredTail.result.finalSnapshot).toEqual(baseline.result.finalSnapshot)
    expect(combinedRecords).toEqual(baseline.records)
    expect(shotDirections(combinedRecords)).toEqual(shotDirections(baseline.records))
    expect(orderedHits(combinedRecords)).toEqual(orderedHits(baseline.records))
  })
})
