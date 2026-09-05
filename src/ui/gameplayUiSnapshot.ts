import { deepFreeze } from '../core/deepFreeze'
import { projectStandings, type StandingsProjection } from '../match'
import type { ValidatedGameDesignProfile } from '../profiles/gameDesignProfile'
import type { MatchFinalResult, MatchParticipantState, MatchPhase, SimulationEvent, SimulationMatchState } from '../simulation/playableSnapshot'

export type GameplayUiStatus = 'start' | 'running' | 'paused' | 'results' | 'error'
export type GameplayCrosshairFeedback = 'idle' | 'hit' | 'dry-fire'

export interface GameplayDebugSnapshot {
  readonly tick: number
  readonly stateHash: string
  readonly profileIdentity: string
  readonly arenaIdentity: string
  readonly collisionIdentity: string
  readonly scenarioIdentity: string
  readonly configurationIdentity: string
  readonly matchPhase: MatchPhase
  readonly remainingTicks: number
  readonly playerPosition: {
    readonly x: number
    readonly y: number
    readonly z: number
  }
  readonly playerYawDegrees: number
  readonly playerPitchDegrees: number
  readonly playerGrounded: boolean
  readonly ammo: number
  readonly targets: readonly {
    readonly semanticId: string
    readonly health: number
    readonly active: boolean
  }[]
  readonly participantStatistics: readonly { readonly semanticId: string; readonly kills: number; readonly assists: number; readonly deaths: number; readonly score: number }[]
}

export interface GameplayUiSnapshot {
  readonly status: GameplayUiStatus
  readonly health: number
  readonly maximumHealth: number
  readonly weaponName: string
  readonly ammo: number
  readonly crosshairFeedback: GameplayCrosshairFeedback
  readonly matchPhase: MatchPhase
  readonly remainingTicks: number
  readonly timerText: string
  readonly scoreboardVisible: boolean
  readonly standings: StandingsProjection
  readonly result: MatchFinalResult | null
  readonly deathMessage?: string
  readonly errorMessage?: string
  readonly debug?: GameplayDebugSnapshot
}

export interface GameplayUiSourceSnapshot {
  readonly tick: number
  readonly participants: readonly MatchParticipantState[]
  readonly match: SimulationMatchState
  readonly events: readonly SimulationEvent[]
}

export interface GameplayUiProjectorOptions {
  readonly profile: ValidatedGameDesignProfile
  readonly weaponName?: string
  readonly ticksPerSecond?: number
}

export class GameplayUiProjector {
  private readonly profile: ValidatedGameDesignProfile
  private readonly weaponName: string
  private readonly ticksPerSecond: number
  private hitFeedbackThroughTick = -1
  private dryFireFeedbackThroughTick = -1
  private lastTick = -1

  constructor({
    profile,
    weaponName = 'Двухстволка',
    ticksPerSecond = 60,
  }: GameplayUiProjectorOptions) {
    if (!Number.isFinite(ticksPerSecond) || ticksPerSecond <= 0) {
      throw new RangeError('GameplayUiProjector ticksPerSecond must be greater than zero')
    }
    if (weaponName.length === 0) {
      throw new TypeError('GameplayUiProjector weaponName must be non-empty')
    }
    this.profile = profile
    this.weaponName = weaponName
    this.ticksPerSecond = ticksPerSecond
  }

  project(
    source: GameplayUiSourceSnapshot,
    status: GameplayUiStatus,
    debug?: GameplayDebugSnapshot,
    errorMessage?: string,
    scoreboardVisible = false,
  ): Readonly<GameplayUiSnapshot> {
    if (source.tick < this.lastTick) {
      this.hitFeedbackThroughTick = -1
      this.dryFireFeedbackThroughTick = -1
    }
    this.lastTick = source.tick

    const feedbackTicks = Math.max(
      1,
      Math.ceil(
        this.profile.rules.weapons.doubleBarrelShotgun.hitmarkerSeconds *
          this.ticksPerSecond,
      ),
    )
    const localId = source.participants.find(p => p.kind === 'local-seat')?.semanticId
    for (const event of source.events) {
      if ((event.kind === 'target-damaged' && event.attackerId === localId) || (event.kind === 'target-destroyed' && event.killerId === localId)) {
        this.hitFeedbackThroughTick = Math.max(
          this.hitFeedbackThroughTick,
          event.tick + feedbackTicks,
        )
      } else if (event.kind === 'dry-fire' && event.shooterId === localId) {
        this.dryFireFeedbackThroughTick = Math.max(
          this.dryFireFeedbackThroughTick,
          event.tick + feedbackTicks,
        )
      }
    }

    const crosshairFeedback: GameplayCrosshairFeedback =
      source.tick <= this.hitFeedbackThroughTick
        ? 'hit'
        : source.tick <= this.dryFireFeedbackThroughTick
          ? 'dry-fire'
          : 'idle'

    const local = source.participants.find(({ kind }) => kind === 'local-seat')
    if (local === undefined) throw new TypeError('GameplayUiSourceSnapshot requires one local-seat participant')
    const matchPhase = source.match.phase
    const effectiveStatus = matchPhase === 'finished' ? 'results' : status
    const remainingSeconds = Math.ceil(source.match.remainingTicks / this.ticksPerSecond)
    const minutes = Math.floor(remainingSeconds / 60)
    const seconds = remainingSeconds % 60
    return createGameplayUiSnapshot({
      status: effectiveStatus,
      health: local.health,
      maximumHealth: this.profile.rules.player.startingHealth,
      weaponName: this.weaponName,
      ammo: local.weapon.ammo,
      crosshairFeedback,
      matchPhase,
      remainingTicks: source.match.remainingTicks,
      timerText: matchPhase === 'overtime' ? 'OVERTIME' : `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`,
      scoreboardVisible: scoreboardVisible && matchPhase !== 'finished',
      standings: projectStandings(source.match.mode, source.participants),
      result: source.match.result,
      ...(!local.alive ? { deathMessage: local.deathSource ? `Вас убил ${source.participants.find(p => p.semanticId === local.deathSource!.participantId)?.name ?? local.deathSource.participantId}` : 'Вы погибли' } : {}),
      ...(debug === undefined ? {} : { debug }),
      ...(errorMessage === undefined ? {} : { errorMessage }),
    })
  }
}

function assertFiniteNonNegative(value: number, path: string): void {
  if (!Number.isFinite(value) || value < 0) {
    throw new RangeError(`${path} must be a finite non-negative number`)
  }
}

export function createGameplayUiSnapshot(
  snapshot: GameplayUiSnapshot,
): Readonly<GameplayUiSnapshot> {
  assertFiniteNonNegative(snapshot.health, 'GameplayUiSnapshot.health')
  assertFiniteNonNegative(snapshot.maximumHealth, 'GameplayUiSnapshot.maximumHealth')
  assertFiniteNonNegative(snapshot.ammo, 'GameplayUiSnapshot.ammo')
  if (!Number.isSafeInteger(snapshot.remainingTicks) || snapshot.remainingTicks < 0) throw new RangeError('GameplayUiSnapshot.remainingTicks must be a non-negative safe integer')
  if (!/^\d{2,}:\d{2}$/.test(snapshot.timerText) && snapshot.timerText !== 'OVERTIME') throw new TypeError('GameplayUiSnapshot.timerText must be MM:SS or OVERTIME')
  if (snapshot.matchPhase === 'overtime' && snapshot.timerText !== 'OVERTIME') throw new TypeError('GameplayUiSnapshot overtime must display OVERTIME')
  if ((snapshot.matchPhase === 'finished') !== (snapshot.result !== null)) throw new TypeError('GameplayUiSnapshot finished phase must match result')
  if (snapshot.status === 'results' && snapshot.matchPhase !== 'finished') throw new TypeError('GameplayUiSnapshot results status requires finished phase')
  if (snapshot.health > snapshot.maximumHealth) {
    throw new RangeError('GameplayUiSnapshot.health must not exceed maximumHealth')
  }
  if (snapshot.weaponName.length === 0) {
    throw new TypeError('GameplayUiSnapshot.weaponName must be non-empty')
  }
  if (snapshot.status === 'error' && !snapshot.errorMessage) {
    throw new TypeError('GameplayUiSnapshot.errorMessage is required for error status')
  }
  if (snapshot.debug !== undefined) {
    if (!Number.isSafeInteger(snapshot.debug.tick) || snapshot.debug.tick < 0) {
      throw new RangeError('GameplayUiSnapshot.debug.tick must be a non-negative safe integer')
    }
    for (const [path, value] of [
      ['stateHash', snapshot.debug.stateHash],
      ['profileIdentity', snapshot.debug.profileIdentity],
      ['arenaIdentity', snapshot.debug.arenaIdentity],
      ['collisionIdentity', snapshot.debug.collisionIdentity],
      ['scenarioIdentity', snapshot.debug.scenarioIdentity],
      ['configurationIdentity', snapshot.debug.configurationIdentity],
    ] as const) {
      if (value.length === 0) {
        throw new TypeError(`GameplayUiSnapshot.debug.${path} must be non-empty`)
      }
    }
    for (const [path, value] of [
      ['playerPosition.x', snapshot.debug.playerPosition.x],
      ['playerPosition.y', snapshot.debug.playerPosition.y],
      ['playerPosition.z', snapshot.debug.playerPosition.z],
      ['playerYawDegrees', snapshot.debug.playerYawDegrees],
      ['playerPitchDegrees', snapshot.debug.playerPitchDegrees],
      ['ammo', snapshot.debug.ammo],
    ] as const) {
      if (!Number.isFinite(value)) {
        throw new RangeError(`GameplayUiSnapshot.debug.${path} must be finite`)
      }
    }
    assertFiniteNonNegative(snapshot.debug.ammo, 'GameplayUiSnapshot.debug.ammo')
    if (!Number.isSafeInteger(snapshot.debug.remainingTicks) || snapshot.debug.remainingTicks < 0) throw new RangeError('GameplayUiSnapshot.debug.remainingTicks must be a non-negative safe integer')
    for (const [index, target] of snapshot.debug.targets.entries()) {
      if (target.semanticId.length === 0) {
        throw new TypeError(
          `GameplayUiSnapshot.debug.targets[${index}].semanticId must be non-empty`,
        )
      }
      assertFiniteNonNegative(
        target.health,
        `GameplayUiSnapshot.debug.targets[${index}].health`,
      )
    }
  }

  return deepFreeze({ ...snapshot })
}
