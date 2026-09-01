import { describe, expect, it } from 'vitest'

import {
  COMBAT_SLICE_SCENARIO_ERROR_CODES,
  CombatSliceScenarioError,
  createCombatSliceScenario,
  parseCombatSliceScenario,
  serializeCombatSliceScenario,
} from './combatSliceScenario'
import { COMBAT_SLICE_SCENARIO_V1 } from './combatSliceScenarioV1'

describe('combat-slice-scenario-v1', () => {
  it('has a versioned immutable schema with semantic-sorted targets and hit volumes', () => {
    const scenario = COMBAT_SLICE_SCENARIO_V1

    expect(scenario.schemaVersion).toBe(1)
    expect(scenario.id).toBe('combat-slice-scenario-v1')
    expect(scenario.targets.map((target) => target.semanticId)).toEqual([
      'robot-center',
      'robot-left',
      'robot-right',
    ])
    expect(scenario.targets[0]?.hitVolumes.map((volume) => volume.semanticId)).toEqual([
      'arm-left',
      'arm-right',
      'head',
      'leg-left',
      'leg-right',
      'torso',
    ])
    expect(scenario.contentHash).toMatch(/^fnv1a64-v1:[0-9a-f]{16}$/)
    expect(Object.isFrozen(scenario)).toBe(true)
    expect(Object.isFrozen(scenario.targets)).toBe(true)
    expect(Object.isFrozen(scenario.playerSpawn.position)).toBe(true)
    expect(Object.isFrozen(scenario.targets[0]?.position)).toBe(true)
    expect(Object.isFrozen(scenario.targets[0]?.hitVolumes[0])).toBe(true)
  })

  it('normalizes reordered targets and volumes to the same content hash', () => {
    const reordered = createCombatSliceScenario({
      schemaVersion: 1,
      id: 'combat-slice-scenario-v1',
      playerSpawn: COMBAT_SLICE_SCENARIO_V1.playerSpawn,
      targets: [...COMBAT_SLICE_SCENARIO_V1.targets]
        .reverse()
        .map((target) => ({ ...target, hitVolumes: [...target.hitVolumes].reverse() })),
    })

    expect(reordered).toEqual(COMBAT_SLICE_SCENARIO_V1)
    expect(reordered.contentHash).toBe(COMBAT_SLICE_SCENARIO_V1.contentHash)
  })

  it('round-trips canonical JSON and detaches mutable input', () => {
    const mutable = JSON.parse(serializeCombatSliceScenario(COMBAT_SLICE_SCENARIO_V1)) as {
      targets: { position: { x: number } }[]
    }
    const parsed = parseCombatSliceScenario(mutable)
    mutable.targets[0]!.position.x = 999

    expect(parsed).toEqual(COMBAT_SLICE_SCENARIO_V1)
    expect(parsed.targets[0]?.position.x).not.toBe(999)
    expect(parseCombatSliceScenario(serializeCombatSliceScenario(parsed))).toEqual(parsed)
  })

  it.each([
    [
      'zero sphere radius',
      (scenario: Record<string, unknown>) => {
        const targets = scenario.targets as { hitVolumes: Record<string, unknown>[] }[]
        const head = targets[0]!.hitVolumes.find((volume) => volume.semanticId === 'head')!
        head.radius = 0
      },
    ],
    [
      'collapsed capsule',
      (scenario: Record<string, unknown>) => {
        const targets = scenario.targets as { hitVolumes: Record<string, unknown>[] }[]
        const arm = targets[0]!.hitVolumes.find((volume) => volume.semanticId === 'arm-left')!
        arm.endOffset = arm.startOffset
      },
    ],
    [
      'missing limb pair',
      (scenario: Record<string, unknown>) => {
        const targets = scenario.targets as { hitVolumes: Record<string, unknown>[] }[]
        targets[0]!.hitVolumes = targets[0]!.hitVolumes.filter(
          (volume) => volume.semanticId !== 'leg-right',
        )
      },
    ],
  ])('rejects invalid robot geometry: %s', (_name, mutate) => {
    const malformed = JSON.parse(serializeCombatSliceScenario(COMBAT_SLICE_SCENARIO_V1)) as Record<string, unknown>
    mutate(malformed)

    expect(() => parseCombatSliceScenario(malformed)).toThrow(CombatSliceScenarioError)
    try {
      parseCombatSliceScenario(malformed)
    } catch (error) {
      expect((error as CombatSliceScenarioError).code).toBe(
        COMBAT_SLICE_SCENARIO_ERROR_CODES.invalidData,
      )
    }
  })

  it('rejects unsupported identity and stale content hashes with stable codes', () => {
    const serialized = JSON.parse(serializeCombatSliceScenario(COMBAT_SLICE_SCENARIO_V1)) as Record<string, unknown>

    expect(() => parseCombatSliceScenario({ ...serialized, schemaVersion: 2 })).toThrow(
      expect.objectContaining({ code: COMBAT_SLICE_SCENARIO_ERROR_CODES.unsupportedIdentity }),
    )
    expect(() => parseCombatSliceScenario({ ...serialized, contentHash: 'fnv1a64-v1:0000000000000000' })).toThrow(
      expect.objectContaining({ code: COMBAT_SLICE_SCENARIO_ERROR_CODES.contentHashMismatch }),
    )
  })
})
