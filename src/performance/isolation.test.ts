import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

import { COLLISION_FIXTURE_ARENA } from '../collision/fixture'
import type { ContentHash } from '../core/canonical'
import { createActionFrame } from '../input/actionFrame'
import { PROTOTYPE_V1_PROFILE } from '../profiles'
import {
  createInitialPlayableSimulationSnapshot,
  createPlayableSimulationReplay,
  playableSimulationSnapshotHash,
  serializePlayableSimulationReplay,
} from '../simulation'
import { PresentationPerformanceProbe } from './probe'

describe('performance telemetry isolation', () => {
  it('has no production dependency on simulation, replay or physical input', () => {
    const directory = dirname(fileURLToPath(import.meta.url))
    for (const file of ['index.ts', 'probe.ts', 'reference.ts']) {
      const source = readFileSync(join(directory, file), 'utf8')
      expect(source).not.toMatch(/from\s+['"][^'"]*(simulation|input|render|runtime|ui)[^'"]*['"]/)
    }
  })

  it('does not change actual snapshot, replay or hash while collecting evidence', () => {
    const snapshot = createInitialPlayableSimulationSnapshot({
      rngSeed: COLLISION_FIXTURE_ARENA.seed,
      arena: {
        id: COLLISION_FIXTURE_ARENA.id,
        seed: COLLISION_FIXTURE_ARENA.seed,
        generatorVersion: COLLISION_FIXTURE_ARENA.generatorVersion,
        contentHash: COLLISION_FIXTURE_ARENA.contentHash as ContentHash,
      },
      profile: PROTOTYPE_V1_PROFILE,
    })
    const replay = createPlayableSimulationReplay(snapshot, [
      createActionFrame(1, []),
    ])
    const hashBefore = playableSimulationSnapshotHash(snapshot)
    const replayBefore = serializePlayableSimulationReplay(replay)
    let now = 0
    const probe = new PresentationPerformanceProbe(() => now)

    probe.startPhase('running-idle')
    probe.recordAnimationCallback(1)
    probe.recordSimulationStep(0.5)
    probe.recordWebGlSubmission()
    probe.recordHudPublication()
    now = 1_000
    probe.finishPhase()

    expect(playableSimulationSnapshotHash(snapshot)).toBe(hashBefore)
    expect(serializePlayableSimulationReplay(replay)).toBe(replayBefore)
  })
})
