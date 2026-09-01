import * as THREE from 'three'

import type { ArenaDefinition, ArenaBoxSurface } from '../arena/arenaDefinition'
import type { SimulationSnapshot } from '../simulation/snapshot'

export const MAX_DEVICE_PIXEL_RATIO = 2

export class WebGlUnavailableError extends Error {
  constructor(cause?: unknown) {
    super('Браузер не предоставил совместимый WebGL-контекст', { cause })
    this.name = 'WebGlUnavailableError'
  }
}

export interface RendererPort {
  readonly domElement: HTMLCanvasElement
  setPixelRatio(pixelRatio: number): void
  setSize(width: number, height: number, updateStyle?: boolean): void
  setViewport(x: number, y: number, width: number, height: number): void
  setScissor(x: number, y: number, width: number, height: number): void
  setScissorTest(enabled: boolean): void
  render(scene: THREE.Scene, camera: THREE.Camera): void
  dispose(): void
  forceContextLoss?(): void
}

export interface DiagnosticRenderer {
  readonly arenaId: string
  readonly arenaHash: string
  readonly render: (snapshot?: SimulationSnapshot) => void
  readonly resize: () => void
  readonly dispose: () => void
}

export interface DiagnosticRendererOptions {
  readonly container: HTMLElement
  readonly arena: ArenaDefinition
  readonly rendererFactory?: () => RendererPort
  readonly devicePixelRatio?: () => number
  readonly resizeObserverFactory?: (
    onResize: () => void,
  ) => ElementResizeObserver | undefined
}

export interface ElementResizeObserver {
  observe(target: Element): void
  disconnect(): void
}

const materialColors: Record<ArenaBoxSurface['material'], number> = {
  floor: 0x243845,
  wall: 0x33495a,
  accent: 0x2fa4ba,
}

function defaultRendererFactory(): RendererPort {
  try {
    return new THREE.WebGLRenderer({
      antialias: true,
      powerPreference: 'high-performance',
    })
  } catch (error) {
    throw new WebGlUnavailableError(error)
  }
}

function defaultResizeObserverFactory(onResize: () => void): ElementResizeObserver | undefined {
  if (typeof ResizeObserver === 'undefined') return undefined
  return new ResizeObserver(() => onResize())
}

export function createDiagnosticRenderer({
  container,
  arena,
  rendererFactory = defaultRendererFactory,
  devicePixelRatio = () => window.devicePixelRatio || 1,
  resizeObserverFactory = defaultResizeObserverFactory,
}: DiagnosticRendererOptions): DiagnosticRenderer {
  let renderer: RendererPort
  try {
    renderer = rendererFactory()
  } catch (error) {
    if (error instanceof WebGlUnavailableError) throw error
    throw new WebGlUnavailableError(error)
  }

  const scene = new THREE.Scene()
  scene.background = new THREE.Color(0x06101a)
  scene.fog = new THREE.FogExp2(0x07131e, 0.021)

  const camera = new THREE.PerspectiveCamera(58, 1, 0.1, 250)
  camera.position.set(18, 14, 20)
  camera.lookAt(0, 1.7, 0)

  const geometries: THREE.BufferGeometry[] = []
  const materials: THREE.Material[] = []

  const hemisphere = new THREE.HemisphereLight(0xaadfff, 0x09101a, 2.1)
  scene.add(hemisphere)
  const keyLight = new THREE.DirectionalLight(0xd8f6ff, 3.2)
  keyLight.position.set(-8, 15, 7)
  scene.add(keyLight)
  const accentLight = new THREE.PointLight(0x39ddff, 32, 36, 1.6)
  accentLight.position.set(0, 6, 0)
  scene.add(accentLight)

  for (const surface of arena.surfaces) {
    const geometry = new THREE.BoxGeometry(surface.size.x, surface.size.y, surface.size.z)
    const material = new THREE.MeshStandardMaterial({
      color: materialColors[surface.material],
      metalness: surface.material === 'accent' ? 0.38 : 0.12,
      roughness: surface.material === 'floor' ? 0.76 : 0.58,
      emissive: surface.material === 'accent' ? 0x073d49 : 0x000000,
      emissiveIntensity: surface.material === 'accent' ? 0.7 : 0,
    })
    const mesh = new THREE.Mesh(geometry, material)
    mesh.name = surface.id
    mesh.position.set(surface.center.x, surface.center.y, surface.center.z)
    mesh.quaternion.set(
      surface.orientation.x,
      surface.orientation.y,
      surface.orientation.z,
      surface.orientation.w,
    )
    scene.add(mesh)
    geometries.push(geometry)
    materials.push(material)
  }

  const markerGeometry = new THREE.RingGeometry(0.45, 0.68, 24)
  const markerMaterial = new THREE.MeshBasicMaterial({
    color: 0x71efff,
    side: THREE.DoubleSide,
    transparent: true,
    opacity: 0.78,
  })
  geometries.push(markerGeometry)
  materials.push(markerMaterial)
  for (const anchor of arena.spawnAnchors) {
    const marker = new THREE.Mesh(markerGeometry, markerMaterial)
    marker.name = `spawn-${anchor.id}`
    marker.position.set(anchor.position.x, anchor.position.y + 0.025, anchor.position.z)
    marker.rotation.x = -Math.PI / 2
    marker.rotation.z = anchor.yawRadians
    scene.add(marker)
  }

  const probeGeometry = new THREE.IcosahedronGeometry(0.48, 2)
  const probeMaterial = new THREE.MeshStandardMaterial({
    color: 0xe6fbff,
    emissive: 0x16849c,
    emissiveIntensity: 1.25,
    metalness: 0.52,
    roughness: 0.24,
  })
  const probe = new THREE.Mesh(probeGeometry, probeMaterial)
  probe.name = 'diagnostic-probe'
  scene.add(probe)
  geometries.push(probeGeometry)
  materials.push(probeMaterial)

  const canvas = renderer.domElement
  canvas.dataset.arenaId = arena.id
  canvas.dataset.arenaHash = arena.contentHash
  canvas.setAttribute('aria-label', `Диагностическая 3D-арена ${arena.id}`)
  container.replaceChildren(canvas)

  let disposed = false
  const resize = () => {
    if (disposed) return
    const bounds = container.getBoundingClientRect()
    const width = Math.max(1, Math.round(bounds.width || container.clientWidth || 1))
    const height = Math.max(1, Math.round(bounds.height || container.clientHeight || 1))
    renderer.setPixelRatio(Math.min(Math.max(devicePixelRatio(), 1), MAX_DEVICE_PIXEL_RATIO))
    renderer.setSize(width, height, false)
    renderer.setViewport(0, 0, width, height)
    renderer.setScissor(0, 0, width, height)
    renderer.setScissorTest(true)
    camera.aspect = width / height
    camera.updateProjectionMatrix()
  }

  const render = (snapshot?: SimulationSnapshot) => {
    if (disposed) return
    const entity = snapshot?.entities[0]
    if (entity) {
      probe.position.set(entity.position.x, entity.position.y, entity.position.z)
      probe.rotation.y = snapshot.tick * 0.025
    }
    renderer.render(scene, camera)
  }

  let resizeObserver: ElementResizeObserver | undefined
  let windowResizeListenerActive = false
  const dispose = () => {
    if (disposed) return
    disposed = true
    if (windowResizeListenerActive) {
      window.removeEventListener('resize', resize)
      windowResizeListenerActive = false
    }
    resizeObserver?.disconnect()
    geometries.forEach((geometry) => geometry.dispose())
    materials.forEach((material) => material.dispose())
    renderer.dispose()
    renderer.forceContextLoss?.()
    canvas.remove()
    scene.clear()
  }

  try {
    window.addEventListener('resize', resize)
    windowResizeListenerActive = true
    resizeObserver = resizeObserverFactory(resize)
    resizeObserver?.observe(container)
    resize()
  } catch (error) {
    dispose()
    if (error instanceof WebGlUnavailableError) throw error
    throw new WebGlUnavailableError(error)
  }

  return {
    arenaId: arena.id,
    arenaHash: arena.contentHash,
    render,
    resize,
    dispose,
  }
}
