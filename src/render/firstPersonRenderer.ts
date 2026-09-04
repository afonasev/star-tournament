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
import { CLEAN_FUTURE_SPORT_V1, arenaMaterialToken } from './cleanFutureSport'
import type { ArenaGlbTemplateCache } from './arenaGlbAssets'

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
  readonly position: ScenarioVector3
  readonly yaw: number
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
  /** Prepared presentation-only templates. Runtime owns their lifetime. */
  readonly arenaGlbAssets?: ArenaGlbTemplateCache
}

interface TargetPresentation {
  readonly group: THREE.Group
  readonly material: THREE.MeshStandardMaterial
  baseY: number
  destroyed: boolean
  color: string
}

interface TransientMesh {
  readonly mesh: THREE.Mesh
  readonly expiresAfterTick: number
}

interface ArenaDoorwayGap {
  readonly id: string
  readonly horizontalAlongX: boolean
  readonly planeCoordinate: number
  readonly spanCenter: number
  readonly baseY: number
  readonly height: number
  readonly width: number
}

function arenaDoorwayGaps(surfaces: readonly ArenaBoxSurface[]): ArenaDoorwayGap[] {
  const walls = surfaces.filter((surface) => surface.material === 'wall')
  const gaps: ArenaDoorwayGap[] = []
  const epsilon = 0.001
  for (let firstIndex = 0; firstIndex < walls.length; firstIndex += 1) {
    const first = walls[firstIndex]!
    if (Math.abs(first.orientation.x) > epsilon || Math.abs(first.orientation.y) > epsilon || Math.abs(first.orientation.z) > epsilon || Math.abs(first.orientation.w - 1) > epsilon) continue
    const horizontalAlongX = first.size.x >= first.size.z
    const firstSpan = horizontalAlongX ? first.size.x : first.size.z
    const firstStart = (horizontalAlongX ? first.center.x : first.center.z) - firstSpan / 2
    const firstEnd = firstStart + firstSpan
    for (const second of walls.slice(firstIndex + 1)) {
      if (Math.abs(second.orientation.x) > epsilon || Math.abs(second.orientation.y) > epsilon || Math.abs(second.orientation.z) > epsilon || Math.abs(second.orientation.w - 1) > epsilon) continue
      if ((second.size.x >= second.size.z) !== horizontalAlongX) continue
      const secondPlane = horizontalAlongX ? second.center.z : second.center.x
      const firstPlane = horizontalAlongX ? first.center.z : first.center.x
      if (Math.abs(firstPlane - secondPlane) > epsilon || Math.abs(first.center.y - second.center.y) > epsilon || Math.abs(first.size.y - second.size.y) > epsilon) continue
      const secondSpan = horizontalAlongX ? second.size.x : second.size.z
      const secondStart = (horizontalAlongX ? second.center.x : second.center.z) - secondSpan / 2
      const secondEnd = secondStart + secondSpan
      const left = firstStart <= secondStart ? { surface: first, end: firstEnd } : { surface: second, end: secondEnd }
      const right = firstStart <= secondStart ? { surface: second, start: secondStart } : { surface: first, start: firstStart }
      const width = right.start - left.end
      if (width < 1 || width > 4.5) continue
      gaps.push({
        id: `${left.surface.id}:${right.surface.id}`,
        horizontalAlongX,
        planeCoordinate: firstPlane,
        spanCenter: left.end + width / 2,
        baseY: first.center.y - first.size.y / 2,
        height: first.size.y,
        width,
      })
    }
  }
  return gaps.sort((left, right) => left.id.localeCompare(right.id, 'en'))
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
  textures: THREE.Texture[],
  onTextureReady: () => void,
  arenaGlbAssets?: ArenaGlbTemplateCache,
): void {
  const textureLoader = new THREE.TextureLoader()
  const tiledTexture = (url: string, repeat: number) => {
    const texture = textureLoader.load(url, onTextureReady)
    texture.colorSpace = THREE.SRGBColorSpace
    texture.wrapS = texture.wrapT = THREE.RepeatWrapping
    texture.repeat.set(repeat, repeat)
    return texture
  }
  const wallTexture = tiledTexture('/assets/textures/arena-wall-panel-v1.png', 2)
  const floorTexture = tiledTexture('/assets/textures/arena-floor-panel-v1.png', 3)
  textures.push(wallTexture, floorTexture)
  const textured = (parameters: THREE.MeshStandardMaterialParameters, map: THREE.Texture) => new THREE.MeshStandardMaterial({ ...parameters, map })
  const panelMaterial = textured(CLEAN_FUTURE_SPORT_V1.details.panel, wallTexture)
  const trimMaterial = textured(CLEAN_FUTURE_SPORT_V1.details.trim, wallTexture)
  const wallModuleMaterial = textured(CLEAN_FUTURE_SPORT_V1.details.wallModule, wallTexture)
  const routeGuideMaterial = textured(CLEAN_FUTURE_SPORT_V1.details.routeGuide, floorTexture)
  const portalShellMaterial = textured(CLEAN_FUTURE_SPORT_V1.details.portalShell, wallTexture)
  const portalInnerMaterial = textured(CLEAN_FUTURE_SPORT_V1.details.portalInner, wallTexture)
  const landmarkMaterial = textured(CLEAN_FUTURE_SPORT_V1.details.landmark, wallTexture)
  materials.push(panelMaterial, trimMaterial, wallModuleMaterial, routeGuideMaterial, portalShellMaterial, portalInnerMaterial, landmarkMaterial)

  // GLB templates share authored materials. Clone before attaching a map so every
  // renderer instance remains presentation-owned and the template cache stays immutable.
  const textureGlbInstance = (object: THREE.Object3D, map: THREE.Texture) => object.traverse((child) => {
    if (!(child instanceof THREE.Mesh)) return
    // Authored kit geometry can omit UVs because it previously used flat colour.
    // Give this presentation clone stable planar UVs rather than touching the cached GLB template.
    if (child.geometry.getAttribute('uv') === undefined) {
      const geometry = child.geometry.clone()
      geometry.computeBoundingBox()
      const bounds = geometry.boundingBox!
      const position = geometry.getAttribute('position')
      const width = Math.max(0.001, bounds.max.x - bounds.min.x)
      const height = Math.max(0.001, bounds.max.y - bounds.min.y)
      const uv = new Float32Array(position.count * 2)
      for (let index = 0; index < position.count; index += 1) {
        uv[index * 2] = (position.getX(index) - bounds.min.x) / width
        uv[index * 2 + 1] = (position.getY(index) - bounds.min.y) / height
      }
      geometry.setAttribute('uv', new THREE.BufferAttribute(uv, 2))
      child.geometry = geometry
      geometries.push(geometry)
    }
    const sourceMaterials = Array.isArray(child.material) ? child.material : [child.material]
    const mapped = sourceMaterials.map((source) => {
      const material = source.clone()
      if (material instanceof THREE.MeshStandardMaterial) {
        material.map = map
        material.color.setHex(0xffffff)
        // Some kit faces deliberately point away from the studio key light.
        // Reuse the detail map as a restrained emissive layer so panels remain
        // legible at gameplay distance instead of collapsing into flat black.
        material.emissiveMap = map
        material.emissive.setHex(0x8ca7b0)
        material.emissiveIntensity = 0.32
        material.needsUpdate = true
      }
      materials.push(material)
      return material
    })
    child.material = Array.isArray(child.material) ? mapped : mapped[0]!
  })

  const addDetailBox = (
    group: THREE.Group,
    name: string,
    size: THREE.Vector3,
    position: THREE.Vector3,
    material: THREE.Material,
  ) => {
    const geometry = new THREE.BoxGeometry(size.x, size.y, size.z)
    const mesh = new THREE.Mesh(geometry, material)
    mesh.name = name
    mesh.position.copy(position)
    group.add(mesh)
    geometries.push(geometry)
  }

  for (const surface of arena.surfaces) {
    const geometry = new THREE.BoxGeometry(surface.size.x, surface.size.y, surface.size.z)
    const token = arenaMaterialToken(surface.material)
    const material = new THREE.MeshStandardMaterial({ ...token, map: surface.material === 'floor' ? floorTexture : surface.material === 'wall' ? wallTexture : null })
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

    const detail = new THREE.Group()
    detail.name = `presentation:detail:${surface.id}`
    detail.position.copy(mesh.position)
    detail.quaternion.copy(mesh.quaternion)
    const { x, y, z } = surface.size
    if (surface.material === 'wall') {
      const horizontalAlongX = x >= z
      const span = horizontalAlongX ? x : z
      const thickness = horizontalAlongX ? z : x
      const depth = thickness / 2 + 0.025
      for (const side of [-1, 1]) {
        const face = side * depth
        const frameSpan = Math.max(0.2, span - 0.3)
        const frameHeight = Math.max(0.2, y - 0.42)
        if (horizontalAlongX) {
          if (arenaGlbAssets === undefined) {
            addDetailBox(detail, `presentation:wall-module:${surface.id}:${side}`, new THREE.Vector3(Math.max(0.7, span * 0.42), Math.max(0.5, y * 0.34), 0.045), new THREE.Vector3(0, 0, face + side * 0.015), wallModuleMaterial)
            for (const bayOffset of [-0.32, 0.32]) addDetailBox(detail, `presentation:wall-buttress:${surface.id}:${side}:${bayOffset}`, new THREE.Vector3(0.42, Math.max(0.8, y * 0.76), 0.22), new THREE.Vector3(span * bayOffset, -y * 0.04, face + side * 0.08), portalShellMaterial)
          } else for (const bayOffset of [-0.32, 0.32]) { const bay = arenaGlbAssets.createInstance('wall-bay'); textureGlbInstance(bay, wallTexture); bay.name = `presentation:glb:wall-bay:${surface.id}:${side}:${bayOffset}`; bay.position.set(span * bayOffset, -y / 2, face + side * 0.08); detail.add(bay) }
          addDetailBox(detail, `presentation:frame:${surface.id}:${side}:top`, new THREE.Vector3(frameSpan, 0.1, 0.035), new THREE.Vector3(0, y / 2 - 0.2, face), panelMaterial)
          addDetailBox(detail, `presentation:frame:${surface.id}:${side}:bottom`, new THREE.Vector3(frameSpan, 0.08, 0.035), new THREE.Vector3(0, -y / 2 + 0.2, face), panelMaterial)
          addDetailBox(detail, `presentation:frame:${surface.id}:${side}:left`, new THREE.Vector3(0.09, frameHeight, 0.035), new THREE.Vector3(-span / 2 + 0.22, 0, face), panelMaterial)
          addDetailBox(detail, `presentation:frame:${surface.id}:${side}:right`, new THREE.Vector3(0.09, frameHeight, 0.035), new THREE.Vector3(span / 2 - 0.22, 0, face), panelMaterial)
          addDetailBox(detail, `presentation:seam:${surface.id}:${side}`, new THREE.Vector3(0.035, Math.max(0.2, y - 0.65), 0.025), new THREE.Vector3(0, 0, face + side * 0.01), trimMaterial)
        } else {
          if (arenaGlbAssets === undefined) {
            addDetailBox(detail, `presentation:wall-module:${surface.id}:${side}`, new THREE.Vector3(0.045, Math.max(0.5, y * 0.34), Math.max(0.7, span * 0.42)), new THREE.Vector3(face + side * 0.015, 0, 0), wallModuleMaterial)
            for (const bayOffset of [-0.32, 0.32]) addDetailBox(detail, `presentation:wall-buttress:${surface.id}:${side}:${bayOffset}`, new THREE.Vector3(0.22, Math.max(0.8, y * 0.76), 0.42), new THREE.Vector3(face + side * 0.08, -y * 0.04, span * bayOffset), portalShellMaterial)
          } else for (const bayOffset of [-0.32, 0.32]) { const bay = arenaGlbAssets.createInstance('wall-bay'); textureGlbInstance(bay, wallTexture); bay.name = `presentation:glb:wall-bay:${surface.id}:${side}:${bayOffset}`; bay.position.set(face + side * 0.08, -y / 2, span * bayOffset); bay.rotation.y = Math.PI / 2; detail.add(bay) }
          addDetailBox(detail, `presentation:frame:${surface.id}:${side}:top`, new THREE.Vector3(0.035, 0.1, frameSpan), new THREE.Vector3(face, y / 2 - 0.2, 0), panelMaterial)
          addDetailBox(detail, `presentation:frame:${surface.id}:${side}:bottom`, new THREE.Vector3(0.035, 0.08, frameSpan), new THREE.Vector3(face, -y / 2 + 0.2, 0), panelMaterial)
          addDetailBox(detail, `presentation:frame:${surface.id}:${side}:left`, new THREE.Vector3(0.035, frameHeight, 0.09), new THREE.Vector3(face, 0, -span / 2 + 0.22), panelMaterial)
          addDetailBox(detail, `presentation:frame:${surface.id}:${side}:right`, new THREE.Vector3(0.035, frameHeight, 0.09), new THREE.Vector3(face, 0, span / 2 - 0.22), panelMaterial)
          addDetailBox(detail, `presentation:seam:${surface.id}:${side}`, new THREE.Vector3(0.025, Math.max(0.2, y - 0.65), 0.035), new THREE.Vector3(face + side * 0.01, 0, 0), trimMaterial)
        }
      }
    } else if (surface.material === 'floor') {
      const top = y / 2 + 0.025
      for (const [index, offset] of [-0.24, 0, 0.24].entries()) {
        if (arenaGlbAssets === undefined) { addDetailBox(detail, `presentation:floor-guide:${surface.id}:x:${index}`, new THREE.Vector3(Math.max(0.5, x * 0.17), 0.028, 0.18), new THREE.Vector3(x * offset, top, 0), routeGuideMaterial); addDetailBox(detail, `presentation:floor-guide:${surface.id}:z:${index}`, new THREE.Vector3(0.18, 0.028, Math.max(0.5, z * 0.17)), new THREE.Vector3(0, top, z * offset), routeGuideMaterial) }
        else { const guideX = arenaGlbAssets.createInstance('floor-route-guide'); textureGlbInstance(guideX, floorTexture); guideX.name = `presentation:glb:floor-route-guide:${surface.id}:x:${index}`; guideX.position.set(x * offset, top, 0); detail.add(guideX); const guideZ = arenaGlbAssets.createInstance('floor-route-guide'); textureGlbInstance(guideZ, floorTexture); guideZ.name = `presentation:glb:floor-route-guide:${surface.id}:z:${index}`; guideZ.position.set(0, top, z * offset); guideZ.rotation.y = Math.PI / 2; detail.add(guideZ) }
      }
    } else {
      addDetailBox(detail, `presentation:accent-trim:${surface.id}`, new THREE.Vector3(Math.max(0.2, x + 0.04), 0.045, Math.max(0.2, z + 0.04)), new THREE.Vector3(0, y / 2 + 0.025, 0), trimMaterial)
    }
    scene.add(detail)
  }

  for (const gap of arenaDoorwayGaps(arena.surfaces)) {
    if (arenaGlbAssets !== undefined) {
      const portal = arenaGlbAssets.createInstance('portal-facade')
      textureGlbInstance(portal, wallTexture)
      portal.name = `presentation:glb:portal-facade:${gap.id}`
      if (gap.horizontalAlongX) portal.position.set(gap.spanCenter, gap.baseY, gap.planeCoordinate)
      else { portal.position.set(gap.planeCoordinate, gap.baseY, gap.spanCenter); portal.rotation.y = Math.PI / 2 }
      scene.add(portal)
      continue
    }
    const portal = new THREE.Group()
    portal.name = `presentation:portal:${gap.id}`
    const radius = gap.width / 2
    const innerRadius = Math.max(0.38, radius - 0.28)
    const pillarHeight = Math.max(1.1, Math.min(gap.height * 0.46, gap.height - radius - 0.24))
    const portalCurve = new THREE.CatmullRomCurve3(
      Array.from({ length: 17 }, (_, index) => {
        const progress = index / 16
        return new THREE.Vector3(Math.cos(Math.PI * progress) * radius, Math.sin(Math.PI * progress) * radius, 0)
      }),
    )
    const arc = new THREE.TubeGeometry(portalCurve, 20, 0.36, 10, false)
    const arch = new THREE.Mesh(arc, portalShellMaterial)
    arch.name = `presentation:portal-shell:${gap.id}`
    const innerCurve = new THREE.CatmullRomCurve3(
      Array.from({ length: 17 }, (_, index) => {
        const progress = index / 16
        return new THREE.Vector3(Math.cos(Math.PI * progress) * innerRadius, Math.sin(Math.PI * progress) * innerRadius, 0)
      }),
    )
    const innerArc = new THREE.Mesh(new THREE.TubeGeometry(innerCurve, 20, 0.14, 8, false), portalInnerMaterial)
    innerArc.name = `presentation:portal-inner:${gap.id}`
    const pillarGeometry = new THREE.BoxGeometry(0.72, pillarHeight, 0.58)
    const innerPillarGeometry = new THREE.BoxGeometry(0.23, pillarHeight, 0.22)
    const capGeometry = new THREE.SphereGeometry(0.38, 10, 8)
    const leftPillar = new THREE.Mesh(pillarGeometry, portalShellMaterial)
    const rightPillar = new THREE.Mesh(pillarGeometry, portalShellMaterial)
    const innerLeftPillar = new THREE.Mesh(innerPillarGeometry, portalInnerMaterial)
    const innerRightPillar = new THREE.Mesh(innerPillarGeometry, portalInnerMaterial)
    const leftCap = new THREE.Mesh(capGeometry, portalShellMaterial)
    const rightCap = new THREE.Mesh(capGeometry, portalShellMaterial)
    leftPillar.name = `presentation:portal-pillar:${gap.id}:left`
    rightPillar.name = `presentation:portal-pillar:${gap.id}:right`
    innerLeftPillar.name = `presentation:portal-inner-pillar:${gap.id}:left`
    innerRightPillar.name = `presentation:portal-inner-pillar:${gap.id}:right`
    leftCap.name = `presentation:portal-cap:${gap.id}:left`
    rightCap.name = `presentation:portal-cap:${gap.id}:right`
    if (gap.horizontalAlongX) {
      arch.position.set(gap.spanCenter, gap.baseY + pillarHeight, gap.planeCoordinate)
      innerArc.position.set(gap.spanCenter, gap.baseY + pillarHeight, gap.planeCoordinate - 0.04)
      leftPillar.position.set(gap.spanCenter - radius, gap.baseY + pillarHeight / 2, gap.planeCoordinate)
      rightPillar.position.set(gap.spanCenter + radius, gap.baseY + pillarHeight / 2, gap.planeCoordinate)
      innerLeftPillar.position.set(gap.spanCenter - innerRadius, gap.baseY + pillarHeight / 2, gap.planeCoordinate - 0.04)
      innerRightPillar.position.set(gap.spanCenter + innerRadius, gap.baseY + pillarHeight / 2, gap.planeCoordinate - 0.04)
      leftCap.position.set(gap.spanCenter - radius, gap.baseY + pillarHeight, gap.planeCoordinate)
      rightCap.position.set(gap.spanCenter + radius, gap.baseY + pillarHeight, gap.planeCoordinate)
    } else {
      arch.position.set(gap.planeCoordinate, gap.baseY + pillarHeight, gap.spanCenter)
      arch.rotation.y = Math.PI / 2
      innerArc.position.set(gap.planeCoordinate - 0.04, gap.baseY + pillarHeight, gap.spanCenter)
      innerArc.rotation.y = Math.PI / 2
      leftPillar.position.set(gap.planeCoordinate, gap.baseY + pillarHeight / 2, gap.spanCenter - radius)
      rightPillar.position.set(gap.planeCoordinate, gap.baseY + pillarHeight / 2, gap.spanCenter + radius)
      innerLeftPillar.position.set(gap.planeCoordinate - 0.04, gap.baseY + pillarHeight / 2, gap.spanCenter - innerRadius)
      innerRightPillar.position.set(gap.planeCoordinate - 0.04, gap.baseY + pillarHeight / 2, gap.spanCenter + innerRadius)
      leftCap.position.set(gap.planeCoordinate, gap.baseY + pillarHeight, gap.spanCenter - radius)
      rightCap.position.set(gap.planeCoordinate, gap.baseY + pillarHeight, gap.spanCenter + radius)
    }
    portal.add(arch, innerArc, leftPillar, rightPillar, innerLeftPillar, innerRightPillar, leftCap, rightCap)
    scene.add(portal)
    geometries.push(arc, innerArc.geometry, pillarGeometry, innerPillarGeometry, capGeometry)
  }

  const combatCenter = arena.regions.find((region) => region.id === 'combat-center')
    ?? arena.regions.find((region) => region.role === 'combat')
  if (combatCenter !== undefined) {
    if (arenaGlbAssets !== undefined) {
      const landmark = arenaGlbAssets.createInstance('central-landmark')
      textureGlbInstance(landmark, wallTexture)
      landmark.name = 'presentation:glb:central-landmark:combat-center'
      const tallestSurface = Math.max(...arena.surfaces.map((surface) => surface.center.y + surface.size.y / 2))
      landmark.position.set(combatCenter.center.x, tallestSurface + 0.7, combatCenter.center.z)
      scene.add(landmark)
      return
    }
    const geometry = new THREE.TorusGeometry(1.15, 0.065, 6, 24)
    const landmark = new THREE.Mesh(geometry, landmarkMaterial)
    landmark.name = 'presentation:landmark:combat-center'
    const tallestSurface = Math.max(...arena.surfaces.map((surface) => surface.center.y + surface.size.y / 2))
    landmark.position.set(combatCenter.center.x, tallestSurface + 0.7, combatCenter.center.z)
    landmark.rotation.x = Math.PI / 2
    scene.add(landmark)
    geometries.push(geometry)
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
  arenaGlbAssets,
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
  scene.background = new THREE.Color(CLEAN_FUTURE_SPORT_V1.background)
  scene.fog = new THREE.FogExp2(CLEAN_FUTURE_SPORT_V1.fog.color, CLEAN_FUTURE_SPORT_V1.fog.density)
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
  const textures: THREE.Texture[] = []
  addArena(scene, arena, geometries, materials, textures, () => renderer.render(scene, camera), arenaGlbAssets)

  const hemisphere = new THREE.HemisphereLight(0xffffff, 0x9fb8d0, 2.4)
  scene.add(hemisphere)
  const keyLight = new THREE.DirectionalLight(0xffffff, 2.4)
  keyLight.position.set(-8, 15, 7)
  scene.add(keyLight)
  const rimLight = new THREE.DirectionalLight(CLEAN_FUTURE_SPORT_V1.participantRim.color, CLEAN_FUTURE_SPORT_V1.participantRim.intensity)
  rimLight.position.set(9, 8, -10)
  scene.add(rimLight)

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
      target.baseY = targetState.position.y
      target.group.position.set(targetState.position.x, targetState.position.y + (destroyed ? -0.2 : 0), targetState.position.z)
      target.group.rotation.y = targetState.yaw
      const presentationColor = destroyed ? '#354148' : targetState.color.toLowerCase()
      if (destroyed !== target.destroyed) {
        target.destroyed = destroyed
        target.group.userData.presentationState = destroyed ? 'destroyed' : 'active'
        target.group.rotation.z = destroyed ? 0.18 : 0
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
    textures.forEach((texture) => texture.dispose())
    renderer.dispose()
    renderer.forceContextLoss?.()
    arenaGlbAssets?.dispose()
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
