export interface ReleaseInfo {
  readonly version: string
  readonly environment: 'production' | 'development'
  readonly branch: string
  readonly deployedAt: string | null
}

declare const __STAR_TOURNAMENT_RELEASE_INFO__: ReleaseInfo

export const RELEASE_INFO: Readonly<ReleaseInfo> = Object.freeze(__STAR_TOURNAMENT_RELEASE_INFO__)

export function releaseCaption(info: Readonly<ReleaseInfo> = RELEASE_INFO): string {
  if (info.environment === 'development') return `DEV · ${info.branch}`
  return info.deployedAt === null ? `v${info.version}` : `v${info.version} · ${info.deployedAt}`
}
