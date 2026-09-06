// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest'
import * as THREE from 'three'

import {
  DIAGNOSTIC_ARENA,
  createArenaCollisionProjection,
  createArenaNavigationProjection,
  createArenaSpawnProjection,
  requireProceduralArena,
} from '../arena'
import { GRAPHICS_QUALITY_V1, PRESENTATION_BALANCED_V1_PROFILE } from '../presentation'
import { PROTOTYPE_V1_PROFILE } from '../profiles'
import { COMBAT_SLICE_SCENARIO_V1 } from '../scenario'
import {
  createInitialSimulationSnapshot,
  createPlayableSimulationReplay,
  serializePlayableSimulationReplay,
  simulationSnapshotHash,
  type PlayableSimulationSnapshot,
} from '../simulation'
import type { GameplayUiSourceSnapshot } from '../ui/gameplayUiSnapshot'
import {
  DEFAULT_WEBGL_POWER_PREFERENCE,
  createFirstPersonRenderer,
  resolveLocalLightBudget,
  resolvePixelBudgetDevicePixelRatio,
  resolveLocalLightShadowBudget,
  type FirstPersonRenderSnapshot,
} from './firstPersonRenderer'
import type { ArenaGlbTemplateCache } from './arenaGlbAssets'
import type { RendererPort } from './diagnosticRenderer'

const PIXEL_BUDGET = PRESENTATION_BALANCED_V1_PROFILE.render.backingBufferPixelBudget

function makeRenderer(): RendererPort {
  return {
    domElement: document.createElement('canvas'),
    setPixelRatio: vi.fn(),
    setSize: vi.fn(),
    setViewport: vi.fn(),
    setScissor: vi.fn(),
    setScissorTest: vi.fn(),
    render: vi.fn(),
    dispose: vi.fn(),
    forceContextLoss: vi.fn(),
  }
}

function makeArenaGlbAssets(): ArenaGlbTemplateCache {
  return {
    createInstance: vi.fn((key) => {
      const group = new THREE.Group()
      group.name = `template:${key}`
      group.add(new THREE.Mesh(new THREE.BoxGeometry(1, 1, 1), new THREE.MeshStandardMaterial()))
      return group
    }),
    dispose: vi.fn(),
  }
}

function snapshot(
  overrides: Partial<FirstPersonRenderSnapshot> = {},
): FirstPersonRenderSnapshot {
  return {
    tick: 1,
    participants: [{
      semanticId: 'local-player-1',
      kind: 'local-seat',
      position: { x: 1, y: 2, z: 3 },
      yaw: Math.PI / 2,
      pitch: Math.PI / 6,
      weapon: { semanticId: 'double-barrel-shotgun', ammo: 20 },
    }, ...COMBAT_SLICE_SCENARIO_V1.targets.map((target, index) => ({
      semanticId: target.semanticId,
      kind: 'stationary-fixture' as const,
      color: ['#f59e0b', '#22c55e', '#a855f7'][index]!,
      health: 100,
      alive: true,
      position: target.position,
      yaw: target.yawRadians,
    }))],
    corpses: [],
    events: [],
    ...overrides,
  }
}

function createContainer(width = 960, height = 540): HTMLDivElement {
  const container = document.createElement('div')
  vi.spyOn(container, 'getBoundingClientRect').mockReturnValue({
    width,
    height,
    top: 0,
    right: width,
    bottom: height,
    left: 0,
    x: 0,
    y: 0,
    toJSON: () => ({}),
  })
  return container
}

afterEach(() => {
  document.body.replaceChildren()
  vi.restoreAllMocks()
})

describe('createFirstPersonRenderer', () => {
  it('uses the standard WebGL power preference and resolves Retina/4K DPR from a pixel budget', () => {
    expect(DEFAULT_WEBGL_POWER_PREFERENCE).toBe('default')
    expect(resolvePixelBudgetDevicePixelRatio({
      cssWidth: 960,
      cssHeight: 540,
      devicePixelRatio: 3,
      pixelBudget: PIXEL_BUDGET,
    })).toBe(2)
    expect(resolvePixelBudgetDevicePixelRatio({
      cssWidth: 1440,
      cssHeight: 900,
      devicePixelRatio: 2,
      pixelBudget: PIXEL_BUDGET,
    })).toBeCloseTo(Math.sqrt(1.6))
    expect(resolvePixelBudgetDevicePixelRatio({
      cssWidth: 3840,
      cssHeight: 2160,
      devicePixelRatio: 2,
      pixelBudget: PIXEL_BUDGET,
    })).toBe(0.5)
    expect(() => resolvePixelBudgetDevicePixelRatio({
      cssWidth: 0,
      cssHeight: 1080,
      devicePixelRatio: 1,
      pixelBudget: PIXEL_BUDGET,
    })).toThrow(/cssWidth/)
  })

  it('accepts the playable snapshot through presentation-only structural boundaries', () => {
    const acceptsRenderSnapshot = (_snapshot: FirstPersonRenderSnapshot) => true
    const acceptsUiSnapshot = (_snapshot: GameplayUiSourceSnapshot) => true
    expect(acceptsRenderSnapshot({} as PlayableSimulationSnapshot)).toBe(true)
    expect(acceptsUiSnapshot({} as PlayableSimulationSnapshot)).toBe(true)
  })

  it('renders one identity-wired arena, sports robots, world weapons and profile camera', () => {
    const container = createContainer()
    const rendererPort = makeRenderer()
    const adapter = createFirstPersonRenderer({
      container,
      arena: DIAGNOSTIC_ARENA,
      scenario: COMBAT_SLICE_SCENARIO_V1,
      profile: PROTOTYPE_V1_PROFILE,
      pixelBudget: PIXEL_BUDGET,
      rendererFactory: () => rendererPort,
      devicePixelRatio: () => 3,
    })

    adapter.render(snapshot())

    expect(container.querySelector('canvas')).toHaveProperty(
      'dataset.scenarioId',
      COMBAT_SLICE_SCENARIO_V1.id,
    )
    expect(container.querySelector('canvas')).toHaveProperty(
      'dataset.profileHash',
      PROTOTYPE_V1_PROFILE.contentHash,
    )
    expect(rendererPort.setPixelRatio).toHaveBeenCalledWith(2)
    const [scene, cameraValue] = vi.mocked(rendererPort.render).mock.calls[0]!
    const camera = cameraValue as THREE.PerspectiveCamera
    expect(camera.fov).toBe(PROTOTYPE_V1_PROFILE.rules.camera.fieldOfViewDegrees)
    expect(camera.near).toBe(PROTOTYPE_V1_PROFILE.rules.camera.nearPlane)
    expect(camera.far).toBe(PROTOTYPE_V1_PROFILE.rules.camera.farPlane)
    expect(camera.position.toArray()).toEqual([
      1,
      2 + PROTOTYPE_V1_PROFILE.rules.player.eyeHeight,
      3,
    ])
    const forward = new THREE.Vector3(0, 0, -1).applyQuaternion(camera.quaternion)
    expect(forward.x).toBeCloseTo(Math.cos(Math.PI / 6))
    expect(forward.y).toBeCloseTo(Math.sin(Math.PI / 6))
    expect(forward.z).toBeCloseTo(0)
    for (const target of COMBAT_SLICE_SCENARIO_V1.targets) {
      expect((scene as THREE.Scene).getObjectByName(`target:${target.semanticId}`)).toBeDefined()
      expect((scene as THREE.Scene).getObjectByName('robot:identity:core')).toBeDefined()
      expect((scene as THREE.Scene).getObjectByName('robot:weapon:barrel:left')).toBeDefined()
    }
    expect((scene as THREE.Scene).getObjectByName('presentation:first-person-viewmodel')).toBeDefined()
    expect((scene as THREE.Scene).getObjectByName('viewmodel:forearm:left')).toBeDefined()
    expect((scene as THREE.Scene).getObjectByName('viewmodel:weapon:barrel:right')).toBeDefined()
    expect((scene as THREE.Scene).getObjectByName('presentation:detail:floor')).toBeDefined()
    expect((scene as THREE.Scene).getObjectByName('presentation:floor-guide:floor:x:1')).toBeDefined()
    expect((scene as THREE.Scene).getObjectByName('presentation:wall-module:wall-north:1')).toBeDefined()
    expect((scene as THREE.Scene).getObjectByName('presentation:wall-buttress:wall-north:1:-0.32')).toBeDefined()
    expect((scene as THREE.Scene).getObjectByName('presentation:frame:wall-north:1:top')).toBeDefined()
    expect((scene as THREE.Scene).getObjectByName('presentation:lamp-housing:lamp-rhythm:wall-north')).toBeDefined()
    expect((scene as THREE.Scene).getObjectByName('presentation:lamp-lens:lamp-rhythm:wall-north')).toBeDefined()
    expect((scene as THREE.Scene).getObjectByName('presentation:ceiling-lamp:lamp-rhythm:wall-north')).toBeDefined()
    expect((scene as THREE.Scene).getObjectByName('presentation:accent-trim:cover-center')).toBeDefined()
    const ceiling = (scene as THREE.Scene).getObjectByName('presentation:ceiling') as THREE.Mesh
    expect((ceiling.material as THREE.MeshStandardMaterial).map).toBeDefined()
    expect((ceiling.material as THREE.MeshStandardMaterial).side).toBe(THREE.DoubleSide)
    expect((scene as THREE.Scene).getObjectByName('presentation:landmark:combat-center')).toBeDefined()
    const wallShell = (scene as THREE.Scene).getObjectByName('arena:wall-north') as THREE.Mesh
    const accentShell = (scene as THREE.Scene).getObjectByName('arena:cover-center') as THREE.Mesh
    expect((wallShell.material as THREE.MeshStandardMaterial).map).toBeDefined()
    expect((accentShell.material as THREE.MeshStandardMaterial).map).toBeDefined()
    expect((wallShell.material as THREE.MeshStandardMaterial).polygonOffset).toBe(false)
    expect(adapter.arenaHash).toBe(DIAGNOSTIC_ARENA.contentHash)
    const portalNames: string[] = []
    ;(scene as THREE.Scene).traverse((object) => { if (object.name.startsWith('presentation:portal:')) portalNames.push(object.name) })
    expect(portalNames).toEqual([])
  })

  it('uses the quality-limited nearest lamp lights as soft architectural shadow casters', () => {
    expect(resolveLocalLightBudget(GRAPHICS_QUALITY_V1.low)).toBe(0)
    expect(resolveLocalLightBudget(GRAPHICS_QUALITY_V1.balanced)).toBe(8)
    expect(resolveLocalLightBudget(GRAPHICS_QUALITY_V1.high)).toBe(16)
    expect(resolveLocalLightBudget(GRAPHICS_QUALITY_V1.ultra)).toBe(16)
    expect(resolveLocalLightShadowBudget(GRAPHICS_QUALITY_V1.low)).toBe(0)
    expect(resolveLocalLightShadowBudget(GRAPHICS_QUALITY_V1.balanced)).toBe(1)
    expect(resolveLocalLightShadowBudget(GRAPHICS_QUALITY_V1.high)).toBe(2)
    expect(resolveLocalLightShadowBudget(GRAPHICS_QUALITY_V1.ultra)).toBe(4)

    const shadowMap = { enabled: false, type: THREE.BasicShadowMap }
    const rendererPort: RendererPort = { ...makeRenderer(), shadowMap }
    const adapter = createFirstPersonRenderer({
      container: createContainer(),
      arena: requireProceduralArena({ size: 'small', seed: 1234 }),
      scenario: COMBAT_SLICE_SCENARIO_V1,
      profile: PROTOTYPE_V1_PROFILE,
      pixelBudget: PIXEL_BUDGET,
      rendererFactory: () => rendererPort,
      graphicsQuality: GRAPHICS_QUALITY_V1.high,
      arenaGlbAssets: makeArenaGlbAssets(),
    })

    adapter.render(snapshot())
    expect(shadowMap).toEqual({ enabled: true, type: THREE.PCFSoftShadowMap })
    const scene = vi.mocked(rendererPort.render).mock.calls[0]![0] as THREE.Scene
    const lamps: THREE.PointLight[] = []
    scene.traverse((object) => { if (object.name.startsWith('presentation:lamp-light:')) lamps.push(object as THREE.PointLight) })
    expect(lamps.length).toBeGreaterThan(2)
    expect(lamps.filter((lamp) => lamp.visible)).toHaveLength(Math.min(16, lamps.length))
    expect(lamps.filter((lamp) => lamp.castShadow)).toHaveLength(2)
    expect(lamps.filter((lamp) => lamp.castShadow).every((lamp) => lamp.shadow.mapSize.x === 512 && lamp.shadow.radius === 0.5)).toBe(true)
    const architecturalMeshes: THREE.Mesh[] = []
    scene.traverse((object) => { if (object instanceof THREE.Mesh && object.name.startsWith('arena:')) architecturalMeshes.push(object) })
    expect(architecturalMeshes.filter((mesh) => !mesh.name.startsWith('arena:floor')).every((mesh) => !mesh.receiveShadow && !mesh.castShadow)).toBe(true)
    expect(architecturalMeshes.filter((mesh) => mesh.name.startsWith('arena:floor')).every((mesh) => mesh.receiveShadow && !mesh.castShadow)).toBe(true)
    const lenses: THREE.Mesh[] = []
    scene.traverse((object) => { if (object instanceof THREE.Mesh && object.name.startsWith('presentation:lamp-lens:')) lenses.push(object) })
    expect(lenses.length).toBeGreaterThan(2)
    expect(lenses.every((lens) => (lens.material as THREE.MeshStandardMaterial).emissiveIntensity === PRESENTATION_BALANCED_V1_PROFILE.render.localLightIntensity * 1.5)).toBe(true)
  })

  it('keeps GLB wall bays in front of the canonical shell with a positive clearance', () => {
    const rendererPort = makeRenderer()
    const adapter = createFirstPersonRenderer({
      container: createContainer(),
      arena: DIAGNOSTIC_ARENA,
      scenario: COMBAT_SLICE_SCENARIO_V1,
      profile: PROTOTYPE_V1_PROFILE,
      pixelBudget: PIXEL_BUDGET,
      rendererFactory: () => rendererPort,
      arenaGlbAssets: makeArenaGlbAssets(),
    })

    adapter.render(snapshot())
    const scene = vi.mocked(rendererPort.render).mock.calls[0]![0] as THREE.Scene
    const shell = scene.getObjectByName('arena:wall-north')!
    const shellBounds = new THREE.Box3().setFromObject(shell)
    const bays: THREE.Object3D[] = []
    scene.traverse((object) => { if (object.name.startsWith('presentation:glb:wall-bay:wall-north')) bays.push(object) })

    expect(bays).toHaveLength(4)
    for (const bay of bays) {
      const bounds = new THREE.Box3().setFromObject(bay)
      expect(bounds.max.z < shellBounds.min.z || bounds.min.z > shellBounds.max.z).toBe(true)
    }
  })

  it('keeps fallback wall detail inside the canonical shell with clearance', () => {
    const container = createContainer()
    const rendererPort = makeRenderer()
    const adapter = createFirstPersonRenderer({
      container,
      arena: DIAGNOSTIC_ARENA,
      scenario: COMBAT_SLICE_SCENARIO_V1,
      profile: PROTOTYPE_V1_PROFILE,
      pixelBudget: PIXEL_BUDGET,
      rendererFactory: () => rendererPort,
    })

    adapter.render(snapshot())
    const scene = vi.mocked(rendererPort.render).mock.calls[0]![0] as THREE.Scene
    const wall = DIAGNOSTIC_ARENA.surfaces.find(({ id }) => id === 'wall-north')!
    const bay = scene.getObjectByName('presentation:wall-buttress:wall-north:1:-0.32') as THREE.Mesh
    const seam = scene.getObjectByName('presentation:seam:wall-north:1') as THREE.Mesh
    bay.geometry.computeBoundingBox()
    seam.geometry.computeBoundingBox()
    const bayDepth = bay.geometry.boundingBox!.getSize(new THREE.Vector3()).z
    const seamDepth = seam.geometry.boundingBox!.getSize(new THREE.Vector3()).z

    expect(wall.size.z / 2 - (Math.abs(bay.position.z) + bayDepth / 2)).toBeGreaterThan(0.005)
    expect(Math.abs(seam.position.z) + seamDepth / 2).toBeLessThan(wall.size.z / 2)
  })

  it('adds rounded portals only around real procedural wall gaps', () => {
    const container = createContainer()
    const rendererPort = makeRenderer()
    const arena = requireProceduralArena({ size: 'small', seed: 1234 })
    const adapter = createFirstPersonRenderer({
      container,
      arena,
      scenario: COMBAT_SLICE_SCENARIO_V1,
      profile: PROTOTYPE_V1_PROFILE,
      pixelBudget: PIXEL_BUDGET,
      rendererFactory: () => rendererPort,
    })

    adapter.render(snapshot())
    const scene = vi.mocked(rendererPort.render).mock.calls[0]![0] as THREE.Scene
    const solids=arena.surfaces.filter(s=>s.id.startsWith('portal-solid:'))
    const gapSeals=arena.surfaces.filter(s=>s.id.startsWith('wall-architecture-gap-seal-'))
    expect(solids.length).toBeGreaterThan(0)
    expect(gapSeals.length).toBeGreaterThan(0)
    expect(gapSeals.every((seal) => scene.getObjectByName(`arena:${seal.id}`) === undefined)).toBe(true)
    for(const solid of solids) {
      const mesh=scene.getObjectByName(`arena:${solid.id}`) as THREE.Mesh
      expect(mesh).toBeDefined()
      expect(mesh.position.toArray()).toEqual([solid.center.x,solid.center.y,solid.center.z])
      expect(mesh.quaternion.toArray()).toEqual([solid.orientation.x,solid.orientation.y,solid.orientation.z,solid.orientation.w])
    }
    adapter.dispose()

  })

  it('uses every required manifest module while keeping portals restricted to real doorway gaps', () => {
    const container = createContainer()
    const rendererPort = makeRenderer()
    const createInstance = vi.fn<ArenaGlbTemplateCache['createInstance']>((key) => {
      const group = new THREE.Group()
      group.name = `template:${key}`
      group.add(new THREE.Mesh(new THREE.BoxGeometry(1, 1, 1), new THREE.MeshStandardMaterial()))
      return group
    })
    const arenaGlbAssets: ArenaGlbTemplateCache = { createInstance, dispose: vi.fn() }
    const adapter = createFirstPersonRenderer({
      container,
      arena: requireProceduralArena({ size: 'small', seed: 1234 }),
      scenario: COMBAT_SLICE_SCENARIO_V1,
      profile: PROTOTYPE_V1_PROFILE,
      pixelBudget: PIXEL_BUDGET,
      rendererFactory: () => rendererPort,
      arenaGlbAssets,
    })

    adapter.render(snapshot())

    expect(createInstance.mock.calls.map(([key]) => key)).toEqual(expect.arrayContaining([
      'portal-facade', 'wall-bay', 'floor-route-guide', 'central-landmark',
    ]))
    const scene = vi.mocked(rendererPort.render).mock.calls[0]![0] as THREE.Scene
    const portals: THREE.Object3D[] = []
    scene.traverse((object) => {
      if (object.name.startsWith('presentation:glb:portal-facade:')) portals.push(object)
    })
    expect(portals.length).toBeGreaterThan(0)
    expect(portals.length).toBeLessThan(requireProceduralArena({ size: 'small', seed: 1234 }).surfaces.length)
  })

  it('keeps arena, collision, navigation, spawn, snapshot and replay identities unchanged by GLB presentation', () => {
    const initial = createInitialSimulationSnapshot({
      rngSeed: 0x53544152,
      arena: DIAGNOSTIC_ARENA,
    })
    const before = {
      arena: DIAGNOSTIC_ARENA.contentHash,
      collision: JSON.stringify(createArenaCollisionProjection(DIAGNOSTIC_ARENA)),
      navigation: JSON.stringify(createArenaNavigationProjection(DIAGNOSTIC_ARENA)),
      spawns: JSON.stringify(createArenaSpawnProjection(DIAGNOSTIC_ARENA)),
      snapshot: simulationSnapshotHash(initial),
      replay: serializePlayableSimulationReplay(createPlayableSimulationReplay(initial, [])),
    }
    const rendererPort = makeRenderer()
    const createInstance = vi.fn<ArenaGlbTemplateCache['createInstance']>((key) => {
      const instance = new THREE.Group()
      instance.add(new THREE.Mesh(new THREE.BoxGeometry(1, 1, 1), new THREE.MeshStandardMaterial()))
      instance.name = `template:${key}`
      return instance
    })
    const adapter = createFirstPersonRenderer({
      container: createContainer(), arena: DIAGNOSTIC_ARENA, scenario: COMBAT_SLICE_SCENARIO_V1,
      profile: PROTOTYPE_V1_PROFILE, pixelBudget: PIXEL_BUDGET, rendererFactory: () => rendererPort,
      arenaGlbAssets: { createInstance, dispose: vi.fn() },
    })
    adapter.render(snapshot())
    const after = {
      arena: DIAGNOSTIC_ARENA.contentHash,
      collision: JSON.stringify(createArenaCollisionProjection(DIAGNOSTIC_ARENA)),
      navigation: JSON.stringify(createArenaNavigationProjection(DIAGNOSTIC_ARENA)),
      spawns: JSON.stringify(createArenaSpawnProjection(DIAGNOSTIC_ARENA)),
      snapshot: simulationSnapshotHash(initial),
      replay: serializePlayableSimulationReplay(createPlayableSimulationReplay(initial, [])),
    }
    expect(after).toEqual(before)
    expect(createInstance).toHaveBeenCalled()
  })

  it('updates resize projection without changing profile camera parameters', () => {
    const container = createContainer(640, 360)
    const rendererPort = makeRenderer()
    let notifyResize: () => void = () => undefined
    const observer = { observe: vi.fn(), disconnect: vi.fn() }
    const adapter = createFirstPersonRenderer({
      container,
      arena: DIAGNOSTIC_ARENA,
      scenario: COMBAT_SLICE_SCENARIO_V1,
      profile: PROTOTYPE_V1_PROFILE,
      pixelBudget: PIXEL_BUDGET,
      rendererFactory: () => rendererPort,
      resizeObserverFactory: (onResize) => {
        notifyResize = onResize
        return observer
      },
    })

    adapter.render(snapshot())
    vi.mocked(container.getBoundingClientRect).mockReturnValue({
      width: 800,
      height: 400,
      top: 0,
      right: 800,
      bottom: 400,
      left: 0,
      x: 0,
      y: 0,
      toJSON: () => ({}),
    })
    notifyResize()
    adapter.render(snapshot())

    expect(rendererPort.setSize).toHaveBeenLastCalledWith(800, 400, false)
    expect(rendererPort.render).toHaveBeenCalledTimes(2)
    const camera = vi.mocked(rendererPort.render).mock.calls[1]![1] as THREE.PerspectiveCamera
    expect(camera.aspect).toBe(2)
    expect(camera.fov).toBe(PROTOTYPE_V1_PROFILE.rules.camera.fieldOfViewDegrees)
    adapter.dispose()
    expect(observer.disconnect).toHaveBeenCalledTimes(1)
  })

  it('renders each living mannequin with its participant color and restores it after respawn', () => {
    const container = createContainer()
    const rendererPort = makeRenderer()
    const adapter = createFirstPersonRenderer({ container, arena: DIAGNOSTIC_ARENA, scenario: COMBAT_SLICE_SCENARIO_V1, profile: PROTOTYPE_V1_PROFILE, pixelBudget: PIXEL_BUDGET, rendererFactory: () => rendererPort })
    const initial = snapshot()
    adapter.render(initial)
    const scene = vi.mocked(rendererPort.render).mock.calls[0]![0] as THREE.Scene
    for (const participant of initial.participants.filter((entry) => entry.kind === 'stationary-fixture')) {
      const group = scene.getObjectByName(`target:${participant.semanticId}`) as THREE.Group
      const material = (group.getObjectByName('robot:identity:core') as THREE.Mesh).material as THREE.MeshStandardMaterial
      expect(`#${material.color.getHexString()}`).toBe((participant as import('./firstPersonRenderer').FirstPersonRenderTarget).color)
    }

    const targetId = COMBAT_SLICE_SCENARIO_V1.targets[0]!.semanticId
    adapter.render(snapshot({ tick: 2, participants: initial.participants.map((participant) => participant.semanticId === targetId ? { ...participant, health: 0, alive: false } : participant) }))
    adapter.render(snapshot({ tick: 3, participants: initial.participants }))
    const restored = scene.getObjectByName(`target:${targetId}`) as THREE.Group
    expect(`#${((restored.getObjectByName('robot:identity:core') as THREE.Mesh).material as THREE.MeshStandardMaterial).color.getHexString()}`).toBe('#f59e0b')
  })

  it('presents shot, impact and destroyed events until their profile-defined expiry', () => {
    const container = createContainer()
    const rendererPort = makeRenderer()
    const adapter = createFirstPersonRenderer({
      container,
      arena: DIAGNOSTIC_ARENA,
      scenario: COMBAT_SLICE_SCENARIO_V1,
      profile: PROTOTYPE_V1_PROFILE,
      pixelBudget: PIXEL_BUDGET,
      rendererFactory: () => rendererPort,
    })
    const destroyedTargetId = COMBAT_SLICE_SCENARIO_V1.targets[0]!.semanticId
    adapter.render(snapshot({
      tick: 5,
      participants: snapshot().participants.map((participant) =>
        participant.kind === 'stationary-fixture' && participant.semanticId === destroyedTargetId
          ? { ...participant, health: 0, alive: false }
          : participant,
      ),
      events: [
        {
          eventId: 'tick-5:impact:local-player-1:0',
          tick: 5,
          kind: 'shotgun-impact',
          pelletIndex: 0,
          position: { x: 0, y: 1, z: 0 },
          normal: { x: 0, y: 1, z: 0 },
          targetId: null,
          surfaceId: 'wall',
          zone: null,
        },
        { eventId: 'tick-5:shot:local-player-1:1', tick: 5, kind: 'shotgun-shot', shooterId: 'local-player-1', origin: { x: 0, y: 1, z: 0 }, pelletDirections: [{ x: 0, y: 0, z: -1 }] },
        {
          eventId: 'destroyed-1',
          tick: 5,
          kind: 'target-destroyed',
          targetId: destroyedTargetId,
        },
      ],
    }))

    const scene = vi.mocked(rendererPort.render).mock.calls[0]![0] as THREE.Scene
    expect(scene.getObjectByName('presentation:muzzle-flash')).toHaveProperty('visible', true)
    expect(scene.getObjectByName('presentation:muzzle-flash:right')).toHaveProperty('visible', true)
    expect(scene.getObjectByName('presentation:first-person-viewmodel')?.position.z).toBeCloseTo(-0.57)
    expect(scene.getObjectByName('presentation:pellet-trails:tick-5:shot:local-player-1:1')).toBeDefined()
    const trail = scene.getObjectByName('presentation:pellet-trails:tick-5:shot:local-player-1:1') as THREE.InstancedMesh
    expect(trail).toBeInstanceOf(THREE.InstancedMesh)
    expect(trail.count).toBe(1)
    expect(trail.frustumCulled).toBe(false)
    const matrix = new THREE.Matrix4()
    trail.getMatrixAt(0, matrix)
    expect(new THREE.Vector3().setFromMatrixPosition(matrix)).not.toEqual(new THREE.Vector3(0, 1, 0))
    expect(scene.getObjectByName('presentation:impact:tick-5:impact:local-player-1:0')?.getObjectByName('presentation:impact:surface-chip')).toBeDefined()
    expect(scene.getObjectByName('presentation:destroyed:destroyed-1')).toBeDefined()
    expect(scene.getObjectByName(`target:${destroyedTargetId}`)?.userData.presentationState)
      .toBe('destroyed')

    adapter.render(snapshot({ tick: 20 }))
    expect(scene.getObjectByName('presentation:muzzle-flash')).toHaveProperty('visible', false)
    expect(scene.getObjectByName('presentation:muzzle-flash:right')).toHaveProperty('visible', false)
    expect(scene.getObjectByName('presentation:first-person-viewmodel')?.position.z).toBeCloseTo(-0.68)
    expect(scene.getObjectByName('presentation:pellet-trails:tick-5:shot:local-player-1:1')).toBeUndefined()
    expect(scene.getObjectByName('presentation:impact:tick-5:impact:local-player-1:0')).toBeUndefined()
    expect(scene.getObjectByName('presentation:destroyed:destroyed-1')).toBeUndefined()
  })

  it('does not present a successful-shot recoil or flash without a shotgun event', () => {
    const container = createContainer()
    const rendererPort = makeRenderer()
    const adapter = createFirstPersonRenderer({ container, arena: DIAGNOSTIC_ARENA, scenario: COMBAT_SLICE_SCENARIO_V1, profile: PROTOTYPE_V1_PROFILE, pixelBudget: PIXEL_BUDGET, rendererFactory: () => rendererPort })

    adapter.render(snapshot({ tick: 4, events: [{ eventId: 'dry-1', tick: 4, kind: 'dry-fire' }] }))

    const scene = vi.mocked(rendererPort.render).mock.calls[0]![0] as THREE.Scene
    expect(scene.getObjectByName('presentation:muzzle-flash')).toHaveProperty('visible', false)
    expect(scene.getObjectByName('presentation:muzzle-flash:right')).toHaveProperty('visible', false)
    expect(scene.getObjectByName('presentation:first-person-viewmodel')?.position.z).toBeCloseTo(-0.68)
  })

  it('projects authoritative corpse lifetime independently from a respawned fixture', () => {
    const container = createContainer()
    const rendererPort = makeRenderer()
    const adapter = createFirstPersonRenderer({ container, arena: DIAGNOSTIC_ARENA, scenario: COMBAT_SLICE_SCENARIO_V1, profile: PROTOTYPE_V1_PROFILE, pixelBudget: PIXEL_BUDGET, rendererFactory: () => rendererPort })
    adapter.render(snapshot({ tick: 180, corpses: [{ corpseId: 'robot-center:life:1:corpse', position: { x: 0, y: 0, z: -2 }, yaw: 0 }] }))
    const scene = vi.mocked(rendererPort.render).mock.calls[0]![0] as THREE.Scene
    expect(scene.getObjectByName('corpse:robot-center:life:1:corpse')).toBeDefined()
    adapter.render(snapshot({ tick: 601, corpses: [] }))
    expect(scene.getObjectByName('corpse:robot-center:life:1:corpse')).toBeUndefined()
  })

  it('fully disposes listeners, canvas, geometries, materials and WebGL resources', () => {
    const removeListener = vi.spyOn(window, 'removeEventListener')
    const disposeGeometry = vi.spyOn(THREE.BufferGeometry.prototype, 'dispose')
    const disposeMaterial = vi.spyOn(THREE.Material.prototype, 'dispose')
    const container = createContainer()
    const rendererPort = makeRenderer()
    const observer = { observe: vi.fn(), disconnect: vi.fn() }
    const adapter = createFirstPersonRenderer({
      container,
      arena: DIAGNOSTIC_ARENA,
      scenario: COMBAT_SLICE_SCENARIO_V1,
      profile: PROTOTYPE_V1_PROFILE,
      pixelBudget: PIXEL_BUDGET,
      rendererFactory: () => rendererPort,
      resizeObserverFactory: () => observer,
    })

    adapter.dispose()
    adapter.dispose()

    expect(removeListener).toHaveBeenCalledWith('resize', adapter.resize)
    expect(observer.disconnect).toHaveBeenCalledTimes(1)
    expect(container.querySelector('canvas')).toBeNull()
    expect(disposeGeometry).toHaveBeenCalled()
    expect(disposeMaterial).toHaveBeenCalled()
    expect(rendererPort.dispose).toHaveBeenCalledTimes(1)
    expect(rendererPort.forceContextLoss).toHaveBeenCalledTimes(1)
    adapter.render(snapshot())
    adapter.resize()
    expect(rendererPort.render).not.toHaveBeenCalled()
  })

  it('submits only new ticks or one explicit resize invalidation, including while paused', () => {
    const container = createContainer()
    const rendererPort = makeRenderer()
    const onWebGlSubmission = vi.fn()
    let notifyResize: () => void = () => undefined
    const adapter = createFirstPersonRenderer({
      container,
      arena: DIAGNOSTIC_ARENA,
      scenario: COMBAT_SLICE_SCENARIO_V1,
      profile: PROTOTYPE_V1_PROFILE,
      pixelBudget: PIXEL_BUDGET,
      onWebGlSubmission,
      rendererFactory: () => rendererPort,
      resizeObserverFactory: (onResize) => {
        notifyResize = onResize
        return { observe: vi.fn(), disconnect: vi.fn() }
      },
    })

    expect(rendererPort.render).not.toHaveBeenCalled()
    adapter.render(snapshot({ tick: 8 }))
    adapter.render(snapshot({ tick: 8 }))
    adapter.render(snapshot({ tick: 8 }))
    expect(rendererPort.render).toHaveBeenCalledTimes(1)

    notifyResize()
    expect(rendererPort.render).toHaveBeenCalledTimes(2)
    adapter.render(snapshot({ tick: 8 }))
    expect(rendererPort.render).toHaveBeenCalledTimes(2)

    adapter.render(snapshot({ tick: 9 }))
    adapter.render(snapshot({ tick: 9 }))
    expect(rendererPort.render).toHaveBeenCalledTimes(3)
    expect(onWebGlSubmission).toHaveBeenCalledTimes(3)
  })

  it('preserves rewind cleanup while suppressing duplicate rewind submissions', () => {
    const container = createContainer()
    const rendererPort = makeRenderer()
    const adapter = createFirstPersonRenderer({
      container,
      arena: DIAGNOSTIC_ARENA,
      scenario: COMBAT_SLICE_SCENARIO_V1,
      profile: PROTOTYPE_V1_PROFILE,
      pixelBudget: PIXEL_BUDGET,
      rendererFactory: () => rendererPort,
    })
    adapter.render(snapshot({
      tick: 8,
      events: [{
        eventId: 'impact-before-rewind',
        tick: 8,
        kind: 'shotgun-impact',
        pelletIndex: 0,
        position: { x: 0, y: 1, z: 0 },
        normal: { x: 0, y: 1, z: 0 },
        targetId: null,
        surfaceId: 'wall',
        zone: null,
      }],
    }))
    const scene = vi.mocked(rendererPort.render).mock.calls[0]![0] as THREE.Scene
    expect(scene.getObjectByName('presentation:impact:impact-before-rewind')).toBeDefined()

    adapter.render(snapshot({ tick: 3 }))
    adapter.render(snapshot({ tick: 3 }))
    expect(scene.getObjectByName('presentation:impact:impact-before-rewind')).toBeUndefined()
    expect(rendererPort.render).toHaveBeenCalledTimes(2)
  })
})

it('renders semantic panes and barriers without changing canonical collision data', () => {
  const arena=requireProceduralArena({size:'medium',seed:1})
  const before=JSON.stringify(arena)
  const port=makeRenderer()
  const adapter=createFirstPersonRenderer({container:createContainer(),arena,scenario:COMBAT_SLICE_SCENARIO_V1,profile:PROTOTYPE_V1_PROFILE,pixelBudget:PIXEL_BUDGET,rendererFactory:()=>port})
  adapter.render(snapshot())
  const scene=vi.mocked(port.render).mock.calls[0]![0] as THREE.Scene
  for(const surface of arena.surfaces.filter(s=>['window','movement-barrier'].includes(s.semantics?.classification??''))) {
    const mesh=scene.getObjectByName(`arena:${surface.id}`) as THREE.Mesh
    expect((mesh.material as THREE.MeshStandardMaterial).transparent).toBe(true)
    expect((mesh.material as THREE.MeshStandardMaterial).depthWrite).toBe(false)
  }
  expect(JSON.stringify(arena)).toBe(before)
  adapter.dispose()
})
