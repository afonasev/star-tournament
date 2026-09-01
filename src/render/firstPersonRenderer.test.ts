// @vitest-environment jsdom
import { afterEach, describe, expect, expectTypeOf, it, vi } from 'vitest'
import * as THREE from 'three'

import { DIAGNOSTIC_ARENA } from '../arena'
import { PROTOTYPE_V1_PROFILE } from '../profiles'
import { COMBAT_SLICE_SCENARIO_V1 } from '../scenario'
import type { PlayableSimulationSnapshot } from '../simulation'
import type { GameplayUiSourceSnapshot } from '../ui/gameplayUiSnapshot'
import {
  createFirstPersonRenderer,
  type FirstPersonRenderSnapshot,
} from './firstPersonRenderer'
import type { RendererPort } from './diagnosticRenderer'

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
    player: {
      position: { x: 1, y: 2, z: 3 },
      yaw: Math.PI / 2,
      pitch: Math.PI / 6,
    },
    weapon: { semanticId: 'double-barrel-shotgun', ammo: 20 },
    targets: COMBAT_SLICE_SCENARIO_V1.targets.map((target) => ({
      semanticId: target.semanticId,
      health: 100,
      active: true,
    })),
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
  it('accepts the playable snapshot through presentation-only structural boundaries', () => {
    expectTypeOf<PlayableSimulationSnapshot>().toMatchTypeOf<FirstPersonRenderSnapshot>()
    expectTypeOf<PlayableSimulationSnapshot>().toMatchTypeOf<GameplayUiSourceSnapshot>()
  })

  it('renders one identity-wired arena, primitive robots and profile camera', () => {
    const container = createContainer()
    const rendererPort = makeRenderer()
    const adapter = createFirstPersonRenderer({
      container,
      arena: DIAGNOSTIC_ARENA,
      scenario: COMBAT_SLICE_SCENARIO_V1,
      profile: PROTOTYPE_V1_PROFILE,
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
      rendererFactory: () => rendererPort,
      resizeObserverFactory: (onResize) => {
        notifyResize = onResize
        return observer
      },
    })

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
    const camera = vi.mocked(rendererPort.render).mock.calls[0]![1] as THREE.PerspectiveCamera
    expect(camera.aspect).toBe(2)
    expect(camera.fov).toBe(PROTOTYPE_V1_PROFILE.rules.camera.fieldOfViewDegrees)
    adapter.dispose()
    expect(observer.disconnect).toHaveBeenCalledTimes(1)
  })

  it('presents shot, impact and destroyed events until their profile-defined expiry', () => {
    const container = createContainer()
    const rendererPort = makeRenderer()
    const adapter = createFirstPersonRenderer({
      container,
      arena: DIAGNOSTIC_ARENA,
      scenario: COMBAT_SLICE_SCENARIO_V1,
      profile: PROTOTYPE_V1_PROFILE,
      rendererFactory: () => rendererPort,
    })
    const destroyedTargetId = COMBAT_SLICE_SCENARIO_V1.targets[0]!.semanticId
    adapter.render(snapshot({
      tick: 5,
      targets: snapshot().targets.map((target) =>
        target.semanticId === destroyedTargetId
          ? { ...target, health: 0, active: false }
          : target,
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
    expect(rendererPort.render).not.toHaveBeenCalled()
  })
})
