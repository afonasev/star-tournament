import { createCombatSliceScenario, type CombatSliceScenarioPayload, type TargetHitVolume } from './combatSliceScenario'

function targetVolumes(): readonly TargetHitVolume[] {
  return [
    {
      semanticId: 'head',
      kind: 'sphere',
      zone: 'head',
      centerOffset: { x: 0, y: 1.78, z: 0 },
      radius: 0.24,
    },
    {
      semanticId: 'torso',
      kind: 'box',
      zone: 'torso',
      centerOffset: { x: 0, y: 1.18, z: 0 },
      halfExtents: { x: 0.34, y: 0.48, z: 0.22 },
    },
    {
      semanticId: 'arm-left',
      kind: 'capsule',
      zone: 'limb',
      startOffset: { x: -0.48, y: 1.5, z: 0 },
      endOffset: { x: -0.48, y: 0.82, z: 0 },
      radius: 0.12,
    },
    {
      semanticId: 'arm-right',
      kind: 'capsule',
      zone: 'limb',
      startOffset: { x: 0.48, y: 1.5, z: 0 },
      endOffset: { x: 0.48, y: 0.82, z: 0 },
      radius: 0.12,
    },
    {
      semanticId: 'leg-left',
      kind: 'capsule',
      zone: 'limb',
      startOffset: { x: -0.2, y: 0.72, z: 0 },
      endOffset: { x: -0.2, y: 0.14, z: 0 },
      radius: 0.14,
    },
    {
      semanticId: 'leg-right',
      kind: 'capsule',
      zone: 'limb',
      startOffset: { x: 0.2, y: 0.72, z: 0 },
      endOffset: { x: 0.2, y: 0.14, z: 0 },
      radius: 0.14,
    },
  ]
}

const payload: CombatSliceScenarioPayload = {
  schemaVersion: 2,
  id: 'match-slice-scenario-v2',
  playerSpawn: {
    semanticId: 'local-player-1',
    name: 'Pilot',
    participantKind: 'local-seat',
    position: { x: 0, y: 0, z: 6 },
    yawRadians: 0,
  },
  targets: [
    {
      semanticId: 'robot-center',
      name: 'Center',
      participantKind: 'stationary-fixture',
      position: { x: 0, y: 0, z: 4 },
      yawRadians: Math.PI,
      hitVolumes: targetVolumes(),
    },
    {
      semanticId: 'robot-left',
      name: 'Left',
      participantKind: 'stationary-fixture',
      position: { x: -5, y: 0, z: 1.5 },
      yawRadians: Math.PI * 0.75,
      hitVolumes: targetVolumes(),
    },
    {
      semanticId: 'robot-right',
      name: 'Right',
      participantKind: 'stationary-fixture',
      position: { x: 5, y: 0, z: -4 },
      yawRadians: Math.PI * 1.25,
      hitVolumes: targetVolumes(),
    },
  ],
}

export const COMBAT_SLICE_SCENARIO_V1 = createCombatSliceScenario(payload)
