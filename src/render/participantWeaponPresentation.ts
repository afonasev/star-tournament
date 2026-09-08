import * as THREE from 'three'
import type { ArenaGlbTemplateCache } from './arenaGlbAssets'
import { createRobotAnimationController, type RobotAnimationController } from './robotAnimation'

const SHELL_COLOR = 0xe8edf0
const JOINT_COLOR = 0x10263a
const FALLBACK_COLOR = '#b8dce5'

export const DOUBLE_BARREL_SHOTGUN_PRESENTATION_ID = 'double-barrel-shotgun' as const

export interface PresentationResources {
  readonly geometries: THREE.BufferGeometry[]
  readonly materials: THREE.Material[]
}

export interface RobotPresentation {
  readonly group: THREE.Group
  /** Renderer-owned mount; its local Z offset is never simulation state. */
  readonly weaponPresentation: THREE.Object3D
  readonly identityMaterials: readonly THREE.MeshStandardMaterial[]
  readonly weaponIndicatorMaterials: readonly THREE.MeshStandardMaterial[]
  readonly animation?: RobotAnimationController
}

export interface FirstPersonWeaponPresentation {
  readonly group: THREE.Group
  readonly muzzleFlashes: readonly THREE.Group[]
}

function addKineticMuzzleBursts(group: THREE.Group, resources: PresentationResources, positions: readonly THREE.Vector3Tuple[]): readonly THREE.Group[] {
  const coreGeometry = new THREE.SphereGeometry(0.072, 8, 6)
  const flareGeometry = new THREE.ConeGeometry(0.092, 0.3, 6)
  const coreMaterial = new THREE.MeshBasicMaterial({ color: 0xfff0cf, transparent: true, opacity: 0.96 })
  const flareMaterial = new THREE.MeshBasicMaterial({ color: 0xff9d43, transparent: true, opacity: 0.72 })
  resources.geometries.push(coreGeometry, flareGeometry)
  resources.materials.push(coreMaterial, flareMaterial)
  return positions.map((position, index) => {
    const burst = new THREE.Group()
    burst.name = index === 0 ? 'presentation:muzzle-flash' : 'presentation:muzzle-flash:right'
    burst.position.set(...position)
    const core = new THREE.Mesh(coreGeometry, coreMaterial)
    core.name = 'presentation:muzzle-flash:core'
    const flare = new THREE.Mesh(flareGeometry, flareMaterial)
    flare.name = 'presentation:muzzle-flash:flare'
    flare.rotation.x = -Math.PI / 2
    flare.position.z = -0.14
    burst.add(core, flare)
    burst.visible = false
    group.add(burst)
    return burst
  })
}

function standardMaterial(resources: PresentationResources, color: THREE.ColorRepresentation, options: Partial<THREE.MeshStandardMaterialParameters> = {}): THREE.MeshStandardMaterial {
  const material = new THREE.MeshStandardMaterial({ color, metalness: 0.58, roughness: 0.33, ...options })
  resources.materials.push(material)
  return material
}

function addMesh(group: THREE.Group, geometry: THREE.BufferGeometry, material: THREE.Material, resources: PresentationResources, name: string, position: THREE.Vector3Tuple, rotation: THREE.EulerTuple = [0, 0, 0]): THREE.Mesh {
  resources.geometries.push(geometry)
  const mesh = new THREE.Mesh(geometry, material)
  mesh.name = name
  mesh.position.set(...position)
  mesh.rotation.set(...rotation)
  group.add(mesh)
  return mesh
}

function identityMaterial(resources: PresentationResources, color: THREE.ColorRepresentation): THREE.MeshStandardMaterial {
  return standardMaterial(resources, color, {
    emissive: color,
    emissiveIntensity: 0.54,
    metalness: 0.38,
    roughness: 0.26,
  })
}

function addShotgun(group: THREE.Group, resources: PresentationResources, color: string, name: string, scale = 1): readonly THREE.MeshStandardMaterial[] {
  const shell = standardMaterial(resources, SHELL_COLOR)
  const joint = standardMaterial(resources, JOINT_COLOR, { metalness: 0.72, roughness: 0.22 })
  const indicator = identityMaterial(resources, color)
  const barrelGeometry = new THREE.CylinderGeometry(0.09 * scale, 0.105 * scale, 0.94 * scale, 10)
  const chamberGeometry = new THREE.BoxGeometry(0.42 * scale, 0.24 * scale, 0.48 * scale, 2, 1, 2)
  const gripGeometry = new THREE.BoxGeometry(0.17 * scale, 0.38 * scale, 0.18 * scale)
  const indicatorGeometry = new THREE.BoxGeometry(0.18 * scale, 0.075 * scale, 0.32 * scale)
  for (const x of [-0.14 * scale, 0.14 * scale]) {
    addMesh(group, barrelGeometry.clone(), joint, resources, `${name}:barrel:${x < 0 ? 'left' : 'right'}`, [x, 0.03 * scale, -0.62 * scale], [Math.PI / 2, 0, 0])
  }
  addMesh(group, chamberGeometry, shell, resources, `${name}:chamber`, [0, 0, -0.12 * scale])
  addMesh(group, gripGeometry, joint, resources, `${name}:grip`, [0, -0.27 * scale, 0.11 * scale], [0.2, 0, 0])
  addMesh(group, indicatorGeometry, indicator, resources, `${name}:owner-indicator`, [0, 0.1 * scale, -0.15 * scale])
  return [indicator]
}

export function createRobotPresentation(resources: PresentationResources, color: string): RobotPresentation {
  const group = new THREE.Group()
  group.name = 'robot:model:light-sport'
  const shell = standardMaterial(resources, SHELL_COLOR)
  const joints = standardMaterial(resources, JOINT_COLOR, { metalness: 0.74, roughness: 0.24 })
  const identity = identityMaterial(resources, color)
  const torso = new THREE.Group()
  torso.name = 'robot:torso'
  group.add(torso)
  addMesh(torso, new THREE.CapsuleGeometry(0.34, 0.48, 5, 10), shell, resources, 'robot:torso:shell', [0, 1.18, 0])
  addMesh(torso, new THREE.SphereGeometry(0.19, 12, 8), identity, resources, 'robot:identity:core', [0, 1.26, -0.31])
  addMesh(group, new THREE.SphereGeometry(0.22, 12, 8), shell, resources, 'robot:head', [0, 1.88, 0])
  addMesh(group, new THREE.BoxGeometry(0.29, 0.07, 0.055), identity, resources, 'robot:identity:visor', [0, 1.9, -0.21])
  addMesh(group, new THREE.BoxGeometry(0.58, 0.28, 0.32), identity, resources, 'robot:identity:shoulders', [0, 1.5, 0])
  addMesh(group, new THREE.BoxGeometry(0.09, 0.34, 0.09), identity, resources, 'robot:identity:rear-beacon', [0, 1.56, 0.3])
  for (const x of [-0.38, 0.38]) {
    addMesh(group, new THREE.CapsuleGeometry(0.095, 0.42, 4, 8), joints, resources, `robot:arm:${x < 0 ? 'left' : 'right'}`, [x, 1.1, 0], [0, 0, x < 0 ? 0.2 : -0.2])
    addMesh(group, new THREE.SphereGeometry(0.12, 8, 6), shell, resources, `robot:hand:${x < 0 ? 'left' : 'right'}`, [x, 0.72, -0.08])
    addMesh(group, new THREE.CapsuleGeometry(0.13, 0.54, 4, 8), joints, resources, `robot:leg:${x < 0 ? 'left' : 'right'}`, [x * 0.48, 0.35, 0])
    addMesh(group, new THREE.BoxGeometry(0.23, 0.15, 0.38), shell, resources, `robot:foot:${x < 0 ? 'left' : 'right'}`, [x * 0.48, 0.02, -0.08])
  }
  const weaponMount = new THREE.Group()
  weaponMount.name = 'robot:weapon-mount'
  weaponMount.position.set(0.06, 0.92, -0.42)
  weaponMount.rotation.set(-0.04, 0, 0)
  group.add(weaponMount)
  const weaponIndicatorMaterials = addShotgun(weaponMount, resources, color, 'robot:weapon', 0.74)
  return { group, weaponPresentation: weaponMount, identityMaterials: [identity], weaponIndicatorMaterials }
}

export function createFirstPersonWeaponPresentation(resources: PresentationResources, color: string): FirstPersonWeaponPresentation {
  const group = new THREE.Group()
  group.name = 'presentation:first-person-viewmodel'
  group.position.set(0.36, -0.34, -0.68)
  group.rotation.set(-0.08, -0.14, -0.02)
  const shell = standardMaterial(resources, SHELL_COLOR)
  const joints = standardMaterial(resources, JOINT_COLOR, { metalness: 0.74, roughness: 0.22 })
  for (const [side, x] of [['left', -0.26], ['right', 0.26]] as const) {
    addMesh(group, new THREE.CapsuleGeometry(0.095, 0.37, 4, 8), shell, resources, `viewmodel:forearm:${side}`, [x, -0.2, 0.06], [0.9, 0, side === 'left' ? 0.18 : -0.18])
    addMesh(group, new THREE.SphereGeometry(0.11, 8, 6), joints, resources, `viewmodel:hand:${side}`, [x * 0.7, -0.05, -0.13])
  }
  addShotgun(group, resources, color, 'viewmodel:weapon', 0.82)
  const flashes = addKineticMuzzleBursts(group, resources, [[-0.115, 0.025, -1.21], [0.115, 0.025, -1.21]])
  return { group, muzzleFlashes: flashes }
}

export function normalizePresentationColor(color: string | undefined): string {
  return color?.toLowerCase() || FALLBACK_COLOR
}

function textureGlbInstance(object: THREE.Object3D, resources: PresentationResources, texture: THREE.Texture, color: string): readonly THREE.MeshStandardMaterial[] {
  const identityMaterials: THREE.MeshStandardMaterial[] = []
  object.traverse((child) => {
    if (!(child instanceof THREE.Mesh)) return
    const sourceMaterials = Array.isArray(child.material) ? child.material : [child.material]
    const mapped = sourceMaterials.map((source) => {
      const material = source.clone()
      if (material instanceof THREE.MeshStandardMaterial) {
        // Embedded equipment maps belong to the template cache. Legacy untextured
        // assets can still use the supplied arena map; identity stays unpatterned.
        if (material.map === null && material.name !== 'identity' && material.name !== 'muzzle-dark') material.map = texture
        material.emissiveMap = null
        material.emissiveIntensity = material.name === 'identity' ? 0.62 : 0.18
        if (material.name === 'identity') {
          material.color.set(color)
          material.emissive.set(color)
          identityMaterials.push(material)
        }
        material.needsUpdate = true
      }
      resources.materials.push(material)
      return material
    })
    child.material = Array.isArray(child.material) ? mapped : mapped[0]!
  })
  return identityMaterials
}

export function createRobotGlbPresentation(resources: PresentationResources, assets: ArenaGlbTemplateCache | undefined, texture: THREE.Texture | undefined, color: string, preferredLod?: 'lod0' | 'lod1'): RobotPresentation {
  if (assets === undefined || texture === undefined) return createRobotPresentation(resources, color)
  const group = assets.createInstance('light-sport-robot', 0, preferredLod) as THREE.Group
  group.name = 'robot:model:light-sport-glb'
  const animation = createRobotAnimationController(group)
  const identityMaterials = textureGlbInstance(group, resources, texture, color)
  const weapon = assets.createInstance('double-barrel-shotgun')
  weapon.name = 'robot:weapon:double-barrel-glb'
  // GLTF scene roots retain authored weapon-space geometry; this offset aligns
  // the receiver with the torso-stable combat mount in the visible rest pose.
  weapon.position.set(0, 0.22, 0)
  weapon.rotation.x = -0.04
  ;(animation?.weaponMount ?? group).add(weapon)
  const weaponIndicatorMaterials = textureGlbInstance(weapon, resources, texture, color)
  return { group, weaponPresentation: weapon, identityMaterials, weaponIndicatorMaterials, animation }
}

export function createFirstPersonGlbPresentation(resources: PresentationResources, assets: ArenaGlbTemplateCache | undefined, texture: THREE.Texture | undefined, color: string): FirstPersonWeaponPresentation {
  if (assets === undefined || texture === undefined) return createFirstPersonWeaponPresentation(resources, color)
  const group = assets.createInstance('first-person-shotgun') as THREE.Group
  group.name = 'presentation:first-person-viewmodel'
  group.position.set(0.36, -0.34, -0.68)
  // Authored viewmodel assets keep the shared +Z-forward GLB contract; the
  // camera sees along -Z, so presentation turns that semantic front toward it.
  group.rotation.set(-0.08, Math.PI - 0.14, -0.02)
  textureGlbInstance(group, resources, texture, color)
  const flashes = addKineticMuzzleBursts(group, resources, [[-0.115 * 0.42, 0.025 * 0.42, 1.21 * 0.42], [0.115 * 0.42, 0.025 * 0.42, 1.21 * 0.42]])
  return { group, muzzleFlashes: flashes }
}
