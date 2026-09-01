// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest'
import * as THREE from 'three'

import type { ArenaDefinition } from '../arena/arenaDefinition'
import {
  createDiagnosticRenderer,
  type RendererPort,
  WebGlUnavailableError,
} from './diagnosticRenderer'

const arena = {
  schemaVersion: 3,
  id: 'diagnostic-arena-v1',
  seed: 7,
  size: 'small',
  generatorVersion: 'diagnostic-v1',
  generatorProfile: { id: 'prototype-v1', revision: 5, contentHash: 'fnv1a64-v1:0000000000000000' },
  regions: [
    { id: 'region-a', role: 'spawn', center: { x: 0, y: 0, z: 0 }, size: { x: 12, y: 1, z: 12 } },
  ],
  links: [],
  surfaces: [
    {
      id: 'floor',
      kind: 'box',
      center: { x: 0, y: -0.5, z: 0 },
      size: { x: 12, y: 1, z: 12 },
      orientation: { x: 0, y: 0, z: 0, w: 1 },
      material: 'floor',
    },
  ],
  spawnRegions: [
    { id: 'spawn-region-a', regionId: 'region-a', anchorIds: ['spawn-a'] },
  ],
  spawnAnchors: [
    { id: 'spawn-a', spawnRegionId: 'spawn-region-a', position: { x: 0, y: 0.02, z: 0 }, yawRadians: 0 },
  ],
  contentHash: 'fnv1a64-v1:0000000000000000',
} as const satisfies ArenaDefinition

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

afterEach(() => {
  document.body.replaceChildren()
  vi.restoreAllMocks()
})

describe('createDiagnosticRenderer', () => {
  it('creates one identity-marked canvas and renders the arena', () => {
    const container = document.createElement('div')
    vi.spyOn(container, 'getBoundingClientRect').mockReturnValue({
      width: 960,
      height: 540,
      top: 0,
      right: 960,
      bottom: 540,
      left: 0,
      x: 0,
      y: 0,
      toJSON: () => ({}),
    })
    const rendererPort = makeRenderer()
    const adapter = createDiagnosticRenderer({
      container,
      arena,
      rendererFactory: () => rendererPort,
      devicePixelRatio: () => 3,
    })

    expect(container.querySelectorAll('canvas')).toHaveLength(1)
    expect(container.querySelector('canvas')).toHaveProperty('dataset.arenaId', arena.id)
    expect(container.querySelector('canvas')).toHaveProperty('dataset.arenaHash', arena.contentHash)
    expect(rendererPort.setPixelRatio).toHaveBeenCalledWith(2)
    expect(rendererPort.setSize).toHaveBeenCalledWith(960, 540, false)
    adapter.render()
    expect(vi.mocked(rendererPort.render)).toHaveBeenCalledTimes(1)
    const renderedScene = vi.mocked(rendererPort.render).mock.calls[0]?.[0] as THREE.Scene
    const floorMesh = renderedScene.getObjectByName('floor') as THREE.Mesh
    expect(floorMesh.quaternion.toArray()).toEqual([0, 0, 0, 1])
  })

  it('projects serialized surface orientation without breaking arena identity wiring', () => {
    const container = document.createElement('div')
    const rendererPort = makeRenderer()
    const rotatedArena: ArenaDefinition = {
      ...arena,
      surfaces: [
        {
          ...arena.surfaces[0],
          orientation: { x: 0, y: Math.SQRT1_2, z: 0, w: Math.SQRT1_2 },
        },
      ],
    }
    const adapter = createDiagnosticRenderer({
      container,
      arena: rotatedArena,
      rendererFactory: () => rendererPort,
    })

    adapter.render()

    const renderedScene = vi.mocked(rendererPort.render).mock.calls[0]?.[0] as THREE.Scene
    const floorMesh = renderedScene.getObjectByName('floor') as THREE.Mesh
    expect(floorMesh.quaternion.toArray()).toEqual([
      0,
      Math.SQRT1_2,
      0,
      Math.SQRT1_2,
    ])
    expect(container.querySelector('canvas')).toHaveProperty('dataset.arenaId', rotatedArena.id)
    expect(container.querySelector('canvas')).toHaveProperty(
      'dataset.arenaHash',
      rotatedArena.contentHash,
    )
    adapter.dispose()
  })

  it('removes resize behavior, canvas and renderer resources on dispose', () => {
    const removeListener = vi.spyOn(window, 'removeEventListener')
    const container = document.createElement('div')
    const rendererPort = makeRenderer()
    const adapter = createDiagnosticRenderer({
      container,
      arena,
      rendererFactory: () => rendererPort,
    })

    adapter.dispose()
    adapter.dispose()

    expect(removeListener).toHaveBeenCalledWith('resize', adapter.resize)
    expect(container.querySelector('canvas')).toBeNull()
    expect(vi.mocked(rendererPort.dispose)).toHaveBeenCalledTimes(1)
    expect(vi.mocked(rendererPort.forceContextLoss!)).toHaveBeenCalledTimes(1)
    adapter.render()
    expect(vi.mocked(rendererPort.render)).not.toHaveBeenCalled()
  })

  it('reacts to playfield size changes through ResizeObserver', () => {
    const container = document.createElement('div')
    let width = 640
    vi.spyOn(container, 'getBoundingClientRect').mockImplementation(() => ({
      width,
      height: 360,
      top: 0,
      right: width,
      bottom: 360,
      left: 0,
      x: 0,
      y: 0,
      toJSON: () => ({}),
    }))
    const rendererPort = makeRenderer()
    let notifyResize: () => void = () => undefined
    const observer = { observe: vi.fn(), disconnect: vi.fn() }
    const adapter = createDiagnosticRenderer({
      container,
      arena,
      rendererFactory: () => rendererPort,
      resizeObserverFactory: (onResize) => {
        notifyResize = onResize
        return observer
      },
    })

    width = 800
    notifyResize()

    expect(observer.observe).toHaveBeenCalledWith(container)
    expect(rendererPort.setSize).toHaveBeenLastCalledWith(800, 360, false)
    adapter.dispose()
    expect(observer.disconnect).toHaveBeenCalledTimes(1)
  })

  it('rolls back partial initialization when initial resize fails', () => {
    const removeListener = vi.spyOn(window, 'removeEventListener')
    const disposeGeometry = vi.spyOn(THREE.BufferGeometry.prototype, 'dispose')
    const disposeMaterial = vi.spyOn(THREE.Material.prototype, 'dispose')
    const container = document.createElement('div')
    const rendererPort = makeRenderer()
    vi.mocked(rendererPort.setSize).mockImplementation(() => {
      throw new Error('resize failed')
    })
    const observer = { observe: vi.fn(), disconnect: vi.fn() }

    expect(() =>
      createDiagnosticRenderer({
        container,
        arena,
        rendererFactory: () => rendererPort,
        resizeObserverFactory: () => observer,
      }),
    ).toThrow(WebGlUnavailableError)

    expect(removeListener).toHaveBeenCalledWith('resize', expect.any(Function))
    expect(observer.disconnect).toHaveBeenCalledTimes(1)
    expect(rendererPort.dispose).toHaveBeenCalledTimes(1)
    expect(rendererPort.forceContextLoss).toHaveBeenCalledTimes(1)
    expect(disposeGeometry).toHaveBeenCalled()
    expect(disposeMaterial).toHaveBeenCalled()
    expect(container.querySelector('canvas')).toBeNull()
  })

  it('normalizes context construction failures', () => {
    const container = document.createElement('div')
    expect(() =>
      createDiagnosticRenderer({
        container,
        arena,
        rendererFactory: () => {
          throw new Error('context failed')
        },
      }),
    ).toThrow(WebGlUnavailableError)
  })
})
