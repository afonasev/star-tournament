export interface DeathSlideOffset {
  readonly progress: number
  readonly x: number
  readonly z: number
}

export const DEATH_HIT_SECONDS = 0.1
export const DEATH_FALL_SECONDS = 0.35
export const DEATH_REST_SECONDS = DEATH_HIT_SECONDS + DEATH_FALL_SECONDS

export function resolveDeathFallProgress(elapsedSeconds: number): number {
  if (!Number.isFinite(elapsedSeconds)) return 1
  const linear = Math.min(1, Math.max(0, (elapsedSeconds - DEATH_HIT_SECONDS) / DEATH_FALL_SECONDS))
  return linear * linear * (3 - 2 * linear)
}

export function resolveDeathSlideOffset(
  elapsedSeconds: number,
  fallYaw: number,
  distanceMeters: number,
): DeathSlideOffset {
  const progress = resolveDeathFallProgress(elapsedSeconds)
  const distance = Number.isFinite(distanceMeters) ? distanceMeters * progress : 0
  return { progress, x: Math.sin(fallYaw) * distance, z: Math.cos(fallYaw) * distance }
}
