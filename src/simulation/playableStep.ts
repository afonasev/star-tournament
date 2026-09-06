import { advanceBots } from '../ai/planner'
import { createActionFrame } from '../input/actionFrame'
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
  parseSimulationSnapshot,
  type SimulationSnapshot,
  type SimulationEvent,
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
  const current = parseSimulationSnapshot(snapshot, configuration, profileInput)
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

  const localIds = new Set(current.participants.filter(p => p.kind === 'local-seat').map(p => p.semanticId))
  for (const action of normalizedFrame.actions) if (!localIds.has(action.participantId)) throw new TypeError('External actions must target a local seat')
  collision.projectCapsules(livingParticipantCollisionCapsules(current, profile))
  const hasBots = current.participants.some(p => p.kind === 'bot')
  if (hasBots && !arena) throw new TypeError('Bots require a full ArenaDefinition')
  const ai = hasBots ? advanceBots(current, collision, profile, arena!) : { states: new Map(), actions: [] }
  const actions = createActionFrame(expectedTick, [...normalizedFrame.actions, ...ai.actions])
  let participants = current.participants.map(p => ai.states.has(p.semanticId) ? { ...p, botState: ai.states.get(p.semanticId)! } : p)
  for (let i=0;i<participants.length;i++) {
    const local = participants[i]!
    if (!local.alive || local.kind === 'stationary-fixture') continue
    const participantId = local.semanticId
    const jumpPressed = actionValue(actions, participantId, 'jump') === 1
    collision.projectCapsules(livingParticipantCollisionCapsules({ ...current, participants }, profile))
    const intent = buildMovementIntent(local, {
      forward: actionValue(actions, participantId, 'move-forward'),
      right: actionValue(actions, participantId, 'move-right'),
      lookYaw: actionValue(actions, participantId, 'look-yaw'),
      lookPitch: actionValue(actions, participantId, 'look-pitch'),
      jumpPressed: jumpPressed && !local.previousJumpPressed,
    }, { ...profile.rules.player.movement, horizontalSensitivityDegrees: profile.rules.camera.horizontalSensitivity, verticalSensitivityDegrees: profile.rules.camera.verticalSensitivity, maximumPitchDegrees: profile.rules.camera.maximumPitchDegrees }, SIMULATION_FIXED_DELTA_SECONDS)
    // A grounded downward probe stabilizes Rapier autostep at controller skin.
    const desired = local.grounded && !(jumpPressed && !local.previousJumpPressed)
      ? { ...intent.desiredTranslation, y: Math.min(intent.desiredTranslation.y, -profile.rules.player.capsule.skinWidth) }
      : intent.desiredTranslation
    participants[i] = { ...local, ...applyCollisionMovement(local, intent, collision.moveCapsule(participantId, desired), SIMULATION_FIXED_DELTA_SECONDS), previousJumpPressed: jumpPressed }
  }
  collision.projectCapsules(livingParticipantCollisionCapsules({ ...current, participants }, profile))
  const combatParticipants = participants
  const events: SimulationEvent[] = []
  const offset = profile.rules.player.capsule.radius + profile.rules.player.capsule.halfHeight
  const template = COMBAT_SLICE_SCENARIO_V1.targets[0]!
  participants = participants.map(player => {
    if (!player.alive || player.kind === 'stationary-fixture') return player
    const others = combatParticipants.filter(p => p.semanticId !== player.semanticId)
    const result = resolveShotgunTrigger(expectedTick, SIMULATION_TICKS_PER_SECOND,
      actionValue(actions, player.semanticId, 'fire') === 1, player, player.weapon,
      others.map(p => ({ semanticId:p.semanticId, lifeId:p.lifeId, health:p.health, active:p.alive })),
      others.map(p => ({ ...template, semanticId:p.semanticId, name:p.name,
        position: { ...p.position, y:p.position.y-(p.kind === 'stationary-fixture' ? 0 : offset) }, yawRadians:p.yaw })),
      { eyeHeight:profile.rules.player.eyeHeight, ...profile.rules.weapons.doubleBarrelShotgun }, collision)
    events.push(...result.events)
    return { ...player, weapon: { ...result.weapon, semanticId: player.weapon.semanticId } }
  })
  const reduced = reduceMatchTick({ ...current, participants }, events, profile, configuration, COMBAT_SLICE_SCENARIO_V1, arena)
  return parseSimulationSnapshot({
    ...current, tick: expectedTick, rng: nextRandomUint32(current.rng).state,
    participants: reduced.participants, corpses: reduced.corpses, match: reduced.match, events: reduced.events,
  }, configuration, profile)
}
