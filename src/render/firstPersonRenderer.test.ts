// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest'
import * as THREE from 'three'

import { DIAGNOSTIC_ARENA, requireProceduralArena } from '../arena'
import { PRESENTATION_BALANCED_V1_PROFILE } from '../presentation'
import { PROTOTYPE_V1_PROFILE } from '../profiles'
import { COMBAT_SLICE_SCENARIO_V1 } from '../scenario'
import type { PlayableSimulationSnapshot } from '../simulation'
import type { GameplayUiSourceSnapshot } from '../ui/gameplayUiSnapshot'
import {
  DEFAULT_WEBGL_POWER_PREFERENCE,
  createFirstPersonRenderer,
  resolvePixelBudgetDevicePixelRatio,
  type FirstPersonRenderSnapshot,
} from './firstPersonRenderer'
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

  it('renders one identity-wired arena, primitive robots and profile camera', () => {
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
    }
    expect((scene as THREE.Scene).getObjectByName('presentation:detail:floor')).toBeDefined()
    expect((scene as THREE.Scene).getObjectByName('presentation:floor-guide:floor:x:1')).toBeDefined()
    expect((scene as THREE.Scene).getObjectByName('presentation:wall-module:wall-north:1')).toBeDefined()
    expect((scene as THREE.Scene).getObjectByName('presentation:wall-buttress:wall-north:1:-0.32')).toBeDefined()
    expect((scene as THREE.Scene).getObjectByName('presentation:frame:wall-north:1:top')).toBeDefined()
    expect((scene as THREE.Scene).getObjectByName('presentation:accent-trim:cover-center')).toBeDefined()
    expect((scene as THREE.Scene).getObjectByName('presentation:landmark:combat-center')).toBeDefined()
    expect(adapter.arenaHash).toBe(DIAGNOSTIC_ARENA.contentHash)
    const portalNames: string[] = []
    ;(scene as THREE.Scene).traverse((object) => { if (object.name.startsWith('presentation:portal:')) portalNames.push(object.name) })
    expect(portalNames).toEqual([])
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
    const portals: THREE.Object3D[] = []
    scene.traverse((object) => { if (object.name.startsWith('presentation:portal:')) portals.push(object) })
    expect(portals.length).toBeGreaterThan(0)
    for (const portal of portals) {
      expect(portal.children.map(({ name }) => name)).toEqual(expect.arrayContaining([
        expect.stringMatching(/^presentation:portal-shell:/),
        expect.stringMatching(/^presentation:portal-inner:/),
        expect.stringMatching(/^presentation:portal-pillar:.*:left$/),
        expect.stringMatching(/^presentation:portal-pillar:.*:right$/),
        expect.stringMatching(/^presentation:portal-cap:.*:left$/),
        expect.stringMatching(/^presentation:portal-cap:.*:right$/),
      ]))
    }
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
      const material = (group.children[0] as THREE.Mesh).material as THREE.MeshStandardMaterial
      expect(`#${material.color.getHexString()}`).toBe(participant.color)
    }

    const targetId = COMBAT_SLICE_SCENARIO_V1.targets[0]!.semanticId
    adapter.render(snapshot({ tick: 2, participants: initial.participants.map((participant) => participant.semanticId === targetId ? { ...participant, health: 0, alive: false } : participant) }))
    adapter.render(snapshot({ tick: 3, participants: initial.participants }))
    const restored = scene.getObjectByName(`target:${targetId}`) as THREE.Group
    expect(`#${((restored.children[0] as THREE.Mesh).material as THREE.MeshStandardMaterial).color.getHexString()}`).toBe('#f59e0b')
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
        { eventId: 'shot-1', tick: 5, kind: 'shotgun-shot' },
        {
          eventId: 'impact-1',
          tick: 5,
          kind: 'shotgun-impact',
          position: { x: 0, y: 1, z: 0 },
        },
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
    expect(scene.getObjectByName('presentation:impact:impact-1')).toBeDefined()
    expect(scene.getObjectByName('presentation:destroyed:destroyed-1')).toBeDefined()
    expect(scene.getObjectByName(`target:${destroyedTargetId}`)?.userData.presentationState)
      .toBe('destroyed')

    adapter.render(snapshot({ tick: 20 }))
    expect(scene.getObjectByName('presentation:muzzle-flash')).toHaveProperty('visible', false)
    expect(scene.getObjectByName('presentation:impact:impact-1')).toBeUndefined()
    expect(scene.getObjectByName('presentation:destroyed:destroyed-1')).toBeUndefined()
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
        position: { x: 0, y: 1, z: 0 },
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
