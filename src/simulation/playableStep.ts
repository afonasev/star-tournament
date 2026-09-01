import type {
  CollisionCapsuleMovementResult,
  CollisionCapsuleProjection,
  CollisionHit,
  CollisionVector3,
} from '../collision/contracts'
import type { ArenaDefinition } from '../arena'
import { resolveShotgunTrigger } from '../combat/shotgun'
import { parseActionFrame, type ActionFrame, type CanonicalActionId } from '../input/actionFrame'
import { DEFAULT_MATCH_CONFIGURATION, parseMatchConfiguration, type MatchConfiguration } from '../match'
import {
  validateGameDesignProfile,
  type GameDesignProfile,
  type ValidatedGameDesignProfile,
} from '../profiles'
import { COMBAT_SLICE_SCENARIO_V1 } from '../scenario'
import { buildMovementIntent, applyCollisionMovement } from './movement'
import { nextRandomUint32 } from './rng'
import {
  localParticipant,
  parseSimulationSnapshot,
  stationaryParticipants,
  type MatchParticipantState,
  type SimulationSnapshot,
} from './playableSnapshot'
import { reduceMatchTick } from './matchReducer'
import { SIMULATION_FIXED_DELTA_SECONDS, SIMULATION_TICKS_PER_SECOND } from './step'

export interface PlayableCollisionPort {
  projectCapsules(capsules: readonly CollisionCapsuleProjection[]): void
  moveCapsule(
    semanticId: string,
    desiredTranslation: CollisionVector3,
  ): CollisionCapsuleMovementResult
  raycast(
    origin: CollisionVector3,
    direction: CollisionVector3,
    maximumDistance: number,
    excludeSemanticIds?: readonly string[],
  ): readonly CollisionHit[]
}

export function livingParticipantCollisionCapsules(
  snapshot: SimulationSnapshot,
  profile: ValidatedGameDesignProfile,
): readonly CollisionCapsuleProjection[] {
  const verticalCenterOffset = profile.rules.player.capsule.halfHeight +
    profile.rules.player.capsule.radius
  return snapshot.participants
    .filter(({ alive }) => alive)
    .map((participant): CollisionCapsuleProjection => ({
      semanticId: participant.semanticId,
      position: participant.kind === 'stationary-fixture'
        ? { ...participant.position, y: participant.position.y + verticalCenterOffset }
        : participant.position,
      radius: profile.rules.player.capsule.radius,
      halfHeight: profile.rules.player.capsule.halfHeight,
    }))
    .sort((left, right) => left.semanticId.localeCompare(right.semanticId, 'en'))
}

function actionValue(
  frame: ActionFrame,
  participantId: string,
  actionId: CanonicalActionId,
): number {
  return frame.actions.find((action) =>
    action.participantId === participantId && action.actionId === actionId
  )?.value ?? 0
}

export function stepPlayableSimulation(
  snapshot: SimulationSnapshot,
  frame: ActionFrame,
  collision: PlayableCollisionPort,
  profileInput: GameDesignProfile | ValidatedGameDesignProfile,
  configurationInput: MatchConfiguration = DEFAULT_MATCH_CONFIGURATION,
  arena?: ArenaDefinition,
): SimulationSnapshot {
  const configuration = parseMatchConfiguration(configurationInput, profileInput)
  const current = parseSimulationSnapshot(snapshot, configuration)
  const normalizedFrame = parseActionFrame(frame)
  const expectedTick = current.tick + 1
  if (normalizedFrame.tick !== expectedTick) {
    throw new RangeError(
      `ActionFrame.tick ${normalizedFrame.tick} does not match next simulation tick ${expectedTick}`,
    )
  }
  const profileValidation = validateGameDesignProfile(profileInput)
  if (!profileValidation.ok) {
    throw new TypeError(
      `Playable simulation profile is invalid: ${profileValidation.errors.map((error) => error.code).join(', ')}`,
    )
  }
  const profile = profileValidation.profile
  if (
    profile.id !== current.profile.id ||
    profile.revision !== current.profile.revision ||
    profile.contentHash !== current.profile.contentHash
  ) {
    throw new TypeError('Playable simulation profile identity does not match snapshot')
  }
  if (current.match.phase === 'finished') return current

  const local = localParticipant(current)
  const participantId = local.semanticId
  const jumpPressed = actionValue(normalizedFrame, participantId, 'jump') === 1
  const firePressed = actionValue(normalizedFrame, participantId, 'fire') === 1
  collision.projectCapsules(livingParticipantCollisionCapsules(current, profile))
  const movementIntent = buildMovementIntent(
    local,
    {
      forward: actionValue(normalizedFrame, participantId, 'move-forward'),
      right: actionValue(normalizedFrame, participantId, 'move-right'),
      lookYaw: actionValue(normalizedFrame, participantId, 'look-yaw'),
      lookPitch: actionValue(normalizedFrame, participantId, 'look-pitch'),
      jumpPressed: jumpPressed && !local.previousJumpPressed,
    },
    {
      ...profile.rules.player.movement,
      horizontalSensitivityDegrees: profile.rules.camera.horizontalSensitivity,
      verticalSensitivityDegrees: profile.rules.camera.verticalSensitivity,
      maximumPitchDegrees: profile.rules.camera.maximumPitchDegrees,
    },
    SIMULATION_FIXED_DELTA_SECONDS,
  )
  // Rapier's autostep becomes numerically unstable when a grounded fixed-tick
  // gravity displacement sits extremely close to the controller skin. A
  // profile-derived downward contact probe keeps small arcade steps reliable;
  // it is applied for one grounded tick only and never overrides a jump.
  const desiredTranslation = local.grounded && !(jumpPressed && !local.previousJumpPressed)
    ? {
        ...movementIntent.desiredTranslation,
        y: Math.min(
          movementIntent.desiredTranslation.y,
          -profile.rules.player.capsule.skinWidth,
        ),
      }
    : movementIntent.desiredTranslation
  const moved = applyCollisionMovement(
    local,
    movementIntent,
    collision.moveCapsule(local.semanticId, desiredTranslation),
    SIMULATION_FIXED_DELTA_SECONDS,
  )
  const player: MatchParticipantState = {
    ...local,
    ...moved,
    previousJumpPressed: jumpPressed,
  }
  const shotgun = resolveShotgunTrigger(
    expectedTick,
    SIMULATION_TICKS_PER_SECOND,
    firePressed,
    player,
    local.weapon,
    stationaryParticipants(current).map(({ semanticId, lifeId, health, alive }) => ({
      semanticId,
      lifeId,
      health,
      active: alive,
    })),
    COMBAT_SLICE_SCENARIO_V1.targets.map((target) => {
      const participant = current.participants.find(({ semanticId }) => semanticId === target.semanticId)
      return participant === undefined ? target : { ...target, position: participant.position, yawRadians: participant.yaw }
    }),
    {
      eyeHeight: profile.rules.player.eyeHeight,
      ...profile.rules.weapons.doubleBarrelShotgun,
    },
    collision,
  )
  const preparedParticipants = current.participants.map((participant) => participant.semanticId === participantId
    ? { ...player, weapon: shotgun.weapon }
    : participant)
  const prepared = { ...current, participants: preparedParticipants } as SimulationSnapshot
  const reduced = reduceMatchTick(
    prepared,
    shotgun.events,
    profile,
    configuration,
    COMBAT_SLICE_SCENARIO_V1,
    arena,
  )
  return parseSimulationSnapshot({
    ...current,
    tick: expectedTick,
    rng: nextRandomUint32(current.rng).state,
    participants: reduced.participants,
    corpses: reduced.corpses,
    match: reduced.match,
    events: reduced.events,
  }, configuration)
}
