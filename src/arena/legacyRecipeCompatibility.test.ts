import { expect, it } from 'vitest'
import { PROTOTYPE_V1_PROFILE as legacy } from '../profiles/legacyGameDesignProfileV6'
import { validateGameDesignProfile } from '../profiles'
import { DIAGNOSTIC_ARENA } from './diagnosticArena'
import { createInitialSimulationSnapshot, parseSimulationSnapshot } from '../simulation/playableSnapshot'
import { createPlayableSimulationReplay, parsePlayableSimulationReplay, serializePlayableSimulationReplay } from '../simulation/playableReplay'
import { createActionFrame } from '../input/actionFrame'

it('retains the historical full profile and arena in replay without identity migration', () => {
  expect(legacy.contentHash).toBe('fnv1a64-v1:b7ca910122e09e91')
  expect(validateGameDesignProfile(legacy)).toMatchObject({ok:true,profile:legacy})
  const validation=validateGameDesignProfile(legacy)
  if (!validation.ok) throw new Error('Historical profile rejected')
  const snapshot=createInitialSimulationSnapshot({rngSeed:5,arena:DIAGNOSTIC_ARENA,profile:validation.profile})
  expect(parseSimulationSnapshot(snapshot).profile).toEqual(snapshot.profile)
  const replay=createPlayableSimulationReplay(snapshot,[createActionFrame(1,[])])
  const restored=parsePlayableSimulationReplay(serializePlayableSimulationReplay(replay))
  expect(restored).toEqual(replay)
  expect(restored.initialSnapshot.profile.revision).toBe(6)
  expect(restored.arenaDefinition.schemaVersion).toBe(3)
})
