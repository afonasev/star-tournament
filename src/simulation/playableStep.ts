import type {
  CollisionCapsuleMovementResult,
  CollisionCapsuleProjection,
  CollisionHit,
  CollisionVector3,
} from '../collision/contracts'
import { resolveShotgunTrigger } from '../combat/shotgun'
import { parseActionFrame, type ActionFrame, type CanonicalActionId } from '../input/actionFrame'
import {
  validateGameDesignProfile,
  type GameDesignProfile,
  type ValidatedGameDesignProfile,
} from '../profiles'
import { COMBAT_SLICE_SCENARIO_V1 } from '../scenario'
import { buildMovementIntent, applyCollisionMovement } from './movement'
import { nextRandomUint32 } from './rng'
import {
  parseSimulationSnapshot,
  type PlayablePlayerState,
  type SimulationSnapshot,
} from './playableSnapshot'
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
): SimulationSnapshot {
  const current = parseSimulationSnapshot(snapshot)
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

  const participantId = current.player.semanticId
  const jumpPressed = actionValue(normalizedFrame, participantId, 'jump') === 1
  const firePressed = actionValue(normalizedFrame, participantId, 'fire') === 1
  collision.projectCapsules([{
    semanticId: current.player.semanticId,
    position: current.player.position,
    radius: profile.rules.player.capsule.radius,
    halfHeight: profile.rules.player.capsule.halfHeight,
  }])
  const movementIntent = buildMovementIntent(
    current.player,
    {
      forward: actionValue(normalizedFrame, participantId, 'move-forward'),
      right: actionValue(normalizedFrame, participantId, 'move-right'),
      lookYaw: actionValue(normalizedFrame, participantId, 'look-yaw'),
      lookPitch: actionValue(normalizedFrame, participantId, 'look-pitch'),
      jumpPressed: jumpPressed && !current.player.previousJumpPressed,
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
  const desiredTranslation = current.player.grounded && !(jumpPressed && !current.player.previousJumpPressed)
    ? {
        ...movementIntent.desiredTranslation,
        y: Math.min(
          movementIntent.desiredTranslation.y,
          -profile.rules.player.capsule.skinWidth,
        ),
      }
    : movementIntent.desiredTranslation
  const moved = applyCollisionMovement(
    current.player,
    movementIntent,
    collision.moveCapsule(current.player.semanticId, desiredTranslation),
    SIMULATION_FIXED_DELTA_SECONDS,
  )
  const player: PlayablePlayerState = {
    ...current.player,
    ...moved,
    previousJumpPressed: jumpPressed,
  }
  const shotgun = resolveShotgunTrigger(
    expectedTick,
    SIMULATION_TICKS_PER_SECOND,
    firePressed,
    player,
    current.weapon,
    current.targets,
    COMBAT_SLICE_SCENARIO_V1.targets,
    {
      eyeHeight: profile.rules.player.eyeHeight,
      ...profile.rules.weapons.doubleBarrelShotgun,
    },
    collision,
  )
  return parseSimulationSnapshot({
    ...current,
    tick: expectedTick,
    rng: nextRandomUint32(current.rng).state,
    player,
    weapon: shotgun.weapon,
    targets: shotgun.targets,
    events: shotgun.events,
  })
}
