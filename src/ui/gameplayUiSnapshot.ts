import { deepFreeze } from '../core/deepFreeze'
import type { ValidatedGameDesignProfile } from '../profiles/gameDesignProfile'

export type GameplayUiStatus = 'start' | 'running' | 'paused' | 'error'
export type GameplayCrosshairFeedback = 'idle' | 'hit' | 'dry-fire'

export interface GameplayDebugSnapshot {
  readonly tick: number
  readonly stateHash: string
  readonly profileIdentity: string
  readonly arenaIdentity: string
  readonly collisionIdentity: string
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
}

export interface GameplayUiSnapshot {
  readonly status: GameplayUiStatus
  readonly health: number
  readonly maximumHealth: number
  readonly weaponName: string
  readonly ammo: number
  readonly crosshairFeedback: GameplayCrosshairFeedback
  readonly errorMessage?: string
  readonly debug?: GameplayDebugSnapshot
}

export interface GameplayUiSourceSnapshot {
  readonly tick: number
  readonly player: {
    readonly health: number
  }
  readonly weapon: {
    readonly ammo: number
  }
  readonly events: readonly {
    readonly eventId: string
    readonly tick: number
    readonly kind:
      | 'shotgun-shot'
      | 'shotgun-impact'
      | 'target-damaged'
      | 'target-destroyed'
      | 'dry-fire'
  }[]
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
    for (const event of source.events) {
      if (event.kind === 'target-damaged' || event.kind === 'target-destroyed') {
        this.hitFeedbackThroughTick = Math.max(
          this.hitFeedbackThroughTick,
          event.tick + feedbackTicks,
        )
      } else if (event.kind === 'dry-fire') {
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

    return createGameplayUiSnapshot({
      status,
      health: source.player.health,
      maximumHealth: this.profile.rules.player.startingHealth,
      weaponName: this.weaponName,
      ammo: source.weapon.ammo,
      crosshairFeedback,
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
