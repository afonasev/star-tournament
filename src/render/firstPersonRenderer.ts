import * as THREE from 'three'

import type { ArenaBoxSurface, ArenaDefinition } from '../arena/arenaDefinition'
import type { RendererInfoSample } from '../performance'
import type { ValidatedGameDesignProfile } from '../profiles/gameDesignProfile'
import type {
  CombatSliceScenario,
  RobotTargetDefinition,
  ScenarioVector3,
  TargetHitVolume,
} from '../scenario'
import {
  WebGlUnavailableError,
  type ElementResizeObserver,
  type RendererPort,
} from './diagnosticRenderer'

export const DEFAULT_PRESENTATION_TICKS_PER_SECOND = 60 as const
export const DEFAULT_WEBGL_POWER_PREFERENCE: WebGLPowerPreference = 'default'

export interface PixelBudgetDevicePixelRatioOptions {
  readonly cssWidth: number
  readonly cssHeight: number
  readonly devicePixelRatio: number
  readonly pixelBudget: number
}

export function resolvePixelBudgetDevicePixelRatio({
  cssWidth,
  cssHeight,
  devicePixelRatio,
  pixelBudget,
}: PixelBudgetDevicePixelRatioOptions): number {
  for (const [name, value] of Object.entries({
    cssWidth,
    cssHeight,
    devicePixelRatio,
    pixelBudget,
  })) {
    if (!Number.isFinite(value) || value <= 0) {
      throw new RangeError(`Pixel budget ${name} must be greater than zero`)
    }
  }
  return Math.min(devicePixelRatio, Math.sqrt(pixelBudget / (cssWidth * cssHeight)))
}

export interface FirstPersonRenderPlayer {
  readonly position: ScenarioVector3
  readonly yaw: number
  readonly pitch: number
}

export interface FirstPersonRenderTarget {
  readonly semanticId: string
  readonly kind: 'stationary-fixture'
  readonly color: string
  readonly health: number
  readonly alive: boolean
}

export interface FirstPersonRenderWeapon {
  readonly semanticId: string
  readonly ammo: number
}

export interface FirstPersonRenderCorpse {
  readonly corpseId: string
  readonly position: ScenarioVector3
  readonly yaw: number
}

interface FirstPersonRenderEventBase {
  readonly eventId: string
  readonly tick: number
}

export interface FirstPersonShotRenderEvent extends FirstPersonRenderEventBase {
  readonly kind: 'shotgun-shot'
}

export interface FirstPersonImpactRenderEvent extends FirstPersonRenderEventBase {
  readonly kind: 'shotgun-impact'
  readonly position: ScenarioVector3
}

export interface FirstPersonTargetDestroyedRenderEvent extends FirstPersonRenderEventBase {
  readonly kind: 'target-destroyed'
  readonly targetId: string
}

export interface FirstPersonIgnoredRenderEvent extends FirstPersonRenderEventBase {
  readonly kind: 'target-damaged' | 'participant-respawned' | 'dry-fire'
}

export type FirstPersonRenderEvent =
  | FirstPersonShotRenderEvent
  | FirstPersonImpactRenderEvent
  | FirstPersonTargetDestroyedRenderEvent
  | FirstPersonIgnoredRenderEvent

export interface FirstPersonRenderSnapshot {
  readonly tick: number
  readonly participants: readonly ((FirstPersonRenderPlayer & {
    readonly semanticId: string
    readonly kind: 'local-seat'
    readonly weapon: FirstPersonRenderWeapon
  }) | FirstPersonRenderTarget)[]
  readonly corpses: readonly FirstPersonRenderCorpse[]
  readonly events: readonly FirstPersonRenderEvent[]
}

export interface FirstPersonRenderer {
  readonly arenaId: string
  readonly arenaHash: string
  readonly scenarioId: string
  readonly scenarioHash: string
  readonly profileId: string
  readonly profileHash: string
  readonly render: (snapshot: FirstPersonRenderSnapshot) => void
  readonly resize: () => void
  readonly dispose: () => void
}

export interface FirstPersonRendererOptions {
  readonly container: HTMLElement
  readonly arena: ArenaDefinition
  readonly scenario: CombatSliceScenario
  readonly profile: ValidatedGameDesignProfile
  readonly pixelBudget: number
  readonly onWebGlSubmission?: (rendererInfo?: RendererInfoSample) => void
  readonly rendererFactory?: () => RendererPort
  readonly devicePixelRatio?: () => number
  readonly resizeObserverFactory?: (
    onResize: () => void,
  ) => ElementResizeObserver | undefined
  readonly ticksPerSecond?: number
}

interface TargetPresentation {
  readonly group: THREE.Group
  readonly material: THREE.MeshStandardMaterial
  readonly baseY: number
  destroyed: boolean
  color: string
}

interface TransientMesh {
  readonly mesh: THREE.Mesh
  readonly expiresAfterTick: number
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
      powerPreference: DEFAULT_WEBGL_POWER_PREFERENCE,
    })
  } catch (error) {
    throw new WebGlUnavailableError(error)
  }
}

function defaultResizeObserverFactory(onResize: () => void): ElementResizeObserver | undefined {
  if (typeof ResizeObserver === 'undefined') return undefined
  return new ResizeObserver(() => onResize())
}

function addArena(
  scene: THREE.Scene,
  arena: ArenaDefinition,
  geometries: THREE.BufferGeometry[],
  materials: THREE.Material[],
): void {
  for (const surface of arena.surfaces) {
    const geometry = new THREE.BoxGeometry(surface.size.x, surface.size.y, surface.size.z)
    const material = new THREE.MeshStandardMaterial({
      color: materialColors[surface.material],
      metalness: surface.material === 'accent' ? 0.38 : 0.1,
      roughness: surface.material === 'floor' ? 0.8 : 0.58,
      emissive: surface.material === 'accent' ? 0x073d49 : 0x000000,
      emissiveIntensity: surface.material === 'accent' ? 0.65 : 0,
    })
    const mesh = new THREE.Mesh(geometry, material)
    mesh.name = `arena:${surface.id}`
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
}

function volumeMesh(
  volume: TargetHitVolume,
  material: THREE.Material,
  geometries: THREE.BufferGeometry[],
): THREE.Mesh {
  if (volume.kind === 'sphere') {
    const geometry = new THREE.SphereGeometry(volume.radius, 16, 10)
    geometries.push(geometry)
    const mesh = new THREE.Mesh(geometry, material)
    mesh.position.set(volume.centerOffset.x, volume.centerOffset.y, volume.centerOffset.z)
    return mesh
  }
  if (volume.kind === 'box') {
    const geometry = new THREE.BoxGeometry(
      volume.halfExtents.x * 2,
      volume.halfExtents.y * 2,
      volume.halfExtents.z * 2,
    )
    geometries.push(geometry)
    const mesh = new THREE.Mesh(geometry, material)
    mesh.position.set(volume.centerOffset.x, volume.centerOffset.y, volume.centerOffset.z)
    return mesh
  }

  const start = new THREE.Vector3(volume.startOffset.x, volume.startOffset.y, volume.startOffset.z)
  const end = new THREE.Vector3(volume.endOffset.x, volume.endOffset.y, volume.endOffset.z)
  const direction = end.clone().sub(start)
  const geometry = new THREE.CapsuleGeometry(volume.radius, direction.length(), 6, 10)
  geometries.push(geometry)
  const mesh = new THREE.Mesh(geometry, material)
  mesh.position.copy(start.add(end).multiplyScalar(0.5))
  mesh.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), direction.normalize())
  return mesh
}

function addRobotTarget(
  scene: THREE.Scene,
  target: RobotTargetDefinition,
  geometries: THREE.BufferGeometry[],
  materials: THREE.Material[],
): TargetPresentation {
  const group = new THREE.Group()
  group.name = `target:${target.semanticId}`
  group.position.set(target.position.x, target.position.y, target.position.z)
  group.rotation.y = target.yawRadians
  group.userData.presentationState = 'active'

  const material = new THREE.MeshStandardMaterial({
    color: 0xb8dce5,
    metalness: 0.68,
    roughness: 0.32,
    emissive: 0x0c5362,
    emissiveIntensity: 0.5,
  })
  materials.push(material)
  for (const volume of target.hitVolumes) {
    const mesh = volumeMesh(volume, material, geometries)
    mesh.name = `target:${target.semanticId}:${volume.semanticId}`
    group.add(mesh)
  }
  scene.add(group)
  return { group, material, baseY: target.position.y, destroyed: false, color: '#b8dce5' }
}

function eventDurationTicks(seconds: number, ticksPerSecond: number): number {
  return Math.max(1, Math.ceil(seconds * ticksPerSecond))
}

export function createFirstPersonRenderer({
  container,
  arena,
  scenario,
  profile,
  pixelBudget,
  onWebGlSubmission,
  rendererFactory = defaultRendererFactory,
  devicePixelRatio = () => window.devicePixelRatio || 1,
  resizeObserverFactory = defaultResizeObserverFactory,
  ticksPerSecond = DEFAULT_PRESENTATION_TICKS_PER_SECOND,
}: FirstPersonRendererOptions): FirstPersonRenderer {
  if (!Number.isFinite(ticksPerSecond) || ticksPerSecond <= 0) {
    throw new RangeError('FirstPersonRenderer ticksPerSecond must be greater than zero')
  }

  let renderer: RendererPort
  try {
    renderer = rendererFactory()
  } catch (error) {
    if (error instanceof WebGlUnavailableError) throw error
    throw new WebGlUnavailableError(error)
  }

  const scene = new THREE.Scene()
  scene.background = new THREE.Color(0x06101a)
  scene.fog = new THREE.FogExp2(0x07131e, 0.018)
  const camera = new THREE.PerspectiveCamera(
    profile.rules.camera.fieldOfViewDegrees,
    1,
    profile.rules.camera.nearPlane,
    profile.rules.camera.farPlane,
  )
  camera.name = 'first-person-camera'
  scene.add(camera)

  const geometries: THREE.BufferGeometry[] = []
  const materials: THREE.Material[] = []
  addArena(scene, arena, geometries, materials)

  const hemisphere = new THREE.HemisphereLight(0xbce8ff, 0x07101b, 1.9)
  scene.add(hemisphere)
  const keyLight = new THREE.DirectionalLight(0xe4f8ff, 2.8)
  keyLight.position.set(-8, 15, 7)
  scene.add(keyLight)

  const targetById = new Map<string, TargetPresentation>()
  for (const target of scenario.targets) {
    targetById.set(target.semanticId, addRobotTarget(scene, target, geometries, materials))
  }

  const muzzleGeometry = new THREE.SphereGeometry(0.08, 8, 6)
  const muzzleMaterial = new THREE.MeshBasicMaterial({ color: 0xffddb0 })
  const muzzleFlash = new THREE.Mesh(muzzleGeometry, muzzleMaterial)
  muzzleFlash.name = 'presentation:muzzle-flash'
  muzzleFlash.position.set(0.18, -0.16, -0.72)
  muzzleFlash.visible = false
  camera.add(muzzleFlash)
  geometries.push(muzzleGeometry)
  materials.push(muzzleMaterial)

  const impactGeometry = new THREE.SphereGeometry(0.075, 8, 6)
  const impactMaterial = new THREE.MeshBasicMaterial({ color: 0x9ff5ff })
  const destroyedGeometry = new THREE.SphereGeometry(1.05, 12, 8)
  const destroyedMaterial = new THREE.MeshBasicMaterial({
    color: 0xff895e,
    transparent: true,
    opacity: 0.48,
    wireframe: true,
  })
  geometries.push(impactGeometry, destroyedGeometry)
  materials.push(impactMaterial, destroyedMaterial)

  const processedEventIds = new Set<string>()
  const transientByEventId = new Map<string, TransientMesh>()
  const corpseById = new Map<string, THREE.Mesh>()
  let muzzleVisibleThroughTick = -1
  let lastSubmittedTick: number | undefined
  let lastSnapshot: FirstPersonRenderSnapshot | undefined
  let presentationInvalidated = true

  const canvas = renderer.domElement
  canvas.dataset.arenaId = arena.id
  canvas.dataset.arenaHash = arena.contentHash
  canvas.dataset.scenarioId = scenario.id
  canvas.dataset.scenarioHash = scenario.contentHash
  canvas.dataset.profileId = profile.id
  canvas.dataset.profileHash = profile.contentHash
  canvas.setAttribute('aria-label', `First-person арена ${arena.id}`)
  container.replaceChildren(canvas)

  let disposed = false
  const resize = () => {
    if (disposed) return
    const bounds = container.getBoundingClientRect()
    const width = Math.max(1, Math.round(bounds.width || container.clientWidth || 1))
    const height = Math.max(1, Math.round(bounds.height || container.clientHeight || 1))
    renderer.setPixelRatio(resolvePixelBudgetDevicePixelRatio({
      cssWidth: width,
      cssHeight: height,
      devicePixelRatio: devicePixelRatio(),
      pixelBudget,
    }))
    renderer.setSize(width, height, false)
    renderer.setViewport(0, 0, width, height)
    renderer.setScissor(0, 0, width, height)
    renderer.setScissorTest(true)
    camera.aspect = width / height
    camera.updateProjectionMatrix()
    presentationInvalidated = true
    if (lastSnapshot !== undefined) submit(lastSnapshot)
  }

  function removeExpiredTransientMeshes(tick: number): void {
    for (const [eventId, transient] of transientByEventId) {
      if (tick <= transient.expiresAfterTick) continue
      scene.remove(transient.mesh)
      transientByEventId.delete(eventId)
    }
  }

  function processEvents(snapshot: FirstPersonRenderSnapshot): void {
    for (const event of snapshot.events) {
      if (processedEventIds.has(event.eventId)) continue
      processedEventIds.add(event.eventId)
      if (event.kind === 'shotgun-shot') {
        muzzleVisibleThroughTick = Math.max(
          muzzleVisibleThroughTick,
          event.tick + eventDurationTicks(
            profile.rules.weapons.doubleBarrelShotgun.muzzleFeedbackSeconds,
            ticksPerSecond,
          ),
        )
      } else if (event.kind === 'shotgun-impact') {
        const mesh = new THREE.Mesh(impactGeometry, impactMaterial)
        mesh.name = `presentation:impact:${event.eventId}`
        mesh.position.set(event.position.x, event.position.y, event.position.z)
        scene.add(mesh)
        transientByEventId.set(event.eventId, {
          mesh,
          expiresAfterTick: event.tick + eventDurationTicks(
            profile.rules.weapons.doubleBarrelShotgun.impactFeedbackSeconds,
            ticksPerSecond,
          ),
        })
      } else if (event.kind === 'target-destroyed') {
        const target = targetById.get(event.targetId)
        if (target !== undefined) {
          const mesh = new THREE.Mesh(destroyedGeometry, destroyedMaterial)
          mesh.name = `presentation:destroyed:${event.eventId}`
          mesh.position.copy(target.group.position).add(new THREE.Vector3(0, 1, 0))
          scene.add(mesh)
          transientByEventId.set(event.eventId, {
            mesh,
            expiresAfterTick: event.tick + eventDurationTicks(
              profile.rules.weapons.doubleBarrelShotgun.impactFeedbackSeconds,
              ticksPerSecond,
            ),
          })
        }
      }
    }
  }

  function submit(snapshot: FirstPersonRenderSnapshot): void {
    if (snapshot.tick < (lastSubmittedTick ?? -1)) {
      for (const transient of transientByEventId.values()) scene.remove(transient.mesh)
      transientByEventId.clear()
      processedEventIds.clear()
      muzzleVisibleThroughTick = -1
    }
    const player = snapshot.participants.find((participant) => participant.kind === 'local-seat')
    if (player === undefined) throw new TypeError('FirstPersonRenderSnapshot requires one local-seat participant')
    camera.position.set(
      player.position.x,
      player.position.y + profile.rules.player.eyeHeight,
      player.position.z,
    )
    // Three.js positive Y rotation turns the -Z camera axis left, while the
    // simulation convention defines positive yaw as turning right.
    camera.rotation.set(player.pitch, -player.yaw, 0, 'YXZ')

    for (const targetState of snapshot.participants.filter((participant): participant is FirstPersonRenderTarget => participant.kind === 'stationary-fixture')) {
      const target = targetById.get(targetState.semanticId)
      if (target === undefined) continue
      const destroyed = !targetState.alive || targetState.health <= 0
      const presentationColor = destroyed ? '#354148' : targetState.color.toLowerCase()
      if (destroyed !== target.destroyed) {
        target.destroyed = destroyed
        target.group.userData.presentationState = destroyed ? 'destroyed' : 'active'
        target.group.rotation.z = destroyed ? 0.18 : 0
        target.group.position.y = target.baseY + (destroyed ? -0.2 : 0)
        target.material.emissiveIntensity = destroyed ? 0.12 : 0.5
      }
      if (presentationColor !== target.color) {
        target.color = presentationColor
        target.material.color.set(presentationColor)
        if (destroyed) target.material.emissive.setHex(0x210d0a)
        else target.material.emissive.set(presentationColor).multiplyScalar(0.28)
      }
    }

    const visibleCorpseIds = new Set(snapshot.corpses.map(({ corpseId }) => corpseId))
    for (const [corpseId, corpse] of corpseById) {
      if (!visibleCorpseIds.has(corpseId)) {
        scene.remove(corpse)
        corpseById.delete(corpseId)
      }
    }
    for (const corpseState of snapshot.corpses) {
      if (corpseById.has(corpseState.corpseId)) continue
      const corpse = new THREE.Mesh(destroyedGeometry, destroyedMaterial)
      corpse.name = `corpse:${corpseState.corpseId}`
      corpse.position.set(corpseState.position.x, corpseState.position.y + 0.25, corpseState.position.z)
      corpse.rotation.set(0, corpseState.yaw, 0.18)
      scene.add(corpse)
      corpseById.set(corpseState.corpseId, corpse)
    }

    processEvents(snapshot)
    removeExpiredTransientMeshes(snapshot.tick)
    muzzleFlash.visible = snapshot.tick <= muzzleVisibleThroughTick
    renderer.render(scene, camera)
    const info = renderer.info
    onWebGlSubmission?.(info === undefined ? undefined : {
      drawCalls: info.render.calls,
      triangles: info.render.triangles,
      geometries: info.memory.geometries,
      textures: info.memory.textures,
      shaderPrograms: info.programs?.length ?? 0,
    })
    lastSubmittedTick = snapshot.tick
    presentationInvalidated = false
  }

  const render = (snapshot: FirstPersonRenderSnapshot) => {
    if (disposed) return
    lastSnapshot = snapshot
    if (!presentationInvalidated && snapshot.tick === lastSubmittedTick) return
    submit(snapshot)
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
    transientByEventId.clear()
    corpseById.clear()
    processedEventIds.clear()
    lastSnapshot = undefined
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
    scenarioId: scenario.id,
    scenarioHash: scenario.contentHash,
    profileId: profile.id,
    profileHash: profile.contentHash,
    render,
    resize,
    dispose,
  }
}
