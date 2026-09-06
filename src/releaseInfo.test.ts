import { describe, expect, it } from 'vitest'

import { releaseCaption, type ReleaseInfo } from './releaseInfo'

describe('releaseCaption', () => {
  it('shows a development marker and source branch', () => {
    const info: ReleaseInfo = { version: '1.2.3', environment: 'development', branch: 'codex/example', deployedAt: null }
    expect(releaseCaption(info)).toBe('DEV · codex/example')
  })

  it('shows the semantic version and deployment date in production', () => {
    const info: ReleaseInfo = { version: '1.2.3', environment: 'production', branch: 'main', deployedAt: '2026-09-06 12:30 UTC' }
    expect(releaseCaption(info)).toBe('v1.2.3 · 2026-09-06 12:30 UTC')
  })
})
