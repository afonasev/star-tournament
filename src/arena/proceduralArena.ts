import { portalCollisionSurfaces } from './portalGeometry'
import { arenaSupportHeight, rampBox, supportedPoint } from './elevation'
import { resolveArenaRecipe, type ArenaRecipeGraph } from './recipeGraph'
import { type ContentHash } from '../core/canonical'
import { PROTOTYPE_V1_PROFILE, type ArenaGeneratorPresetRules, type GameDesignProfile, type ValidatedGameDesignProfile } from '../profiles'

import {
  createArenaDefinition,
  type ArenaBoxSurface,
  type ArenaDefinition,
  type ArenaDefinitionPayload,
  type ArenaLink,
  type ArenaQuaternion,
  type ArenaRegion,
  type ArenaSize,
  type ArenaSpawnAnchor,
  type ArenaSpawnRegion,
} from './arenaDefinition'
import { validateArenaGameplay, type ArenaGameplayValidationReport } from './arenaGameplayValidation'

export const PROCEDURAL_ARENA_GENERATOR_VERSION = 'arena-layers-v1' as const
export const PROCEDURAL_ARENA_GENERATION_ATTEMPTS = 4

export interface ProceduralArenaGenerationOptions {
  readonly size: ArenaSize
  readonly seed: number
  readonly profile?: GameDesignProfile | ValidatedGameDesignProfile
}

export type ProceduralArenaGenerationResult =
  | { readonly ok: true; readonly arena: ArenaDefinition; readonly report: ArenaGameplayValidationReport; readonly attempt: number }
  | { readonly ok: false; readonly seed: number; readonly size: ArenaSize; readonly attempts: number; readonly reports: readonly ArenaGameplayValidationReport[] }

const IDENTITY: ArenaQuaternion = Object.freeze({ x: 0, y: 0, z: 0, w: 1 })

function box(id: string, x: number, y: number, z: number, sx: number, sy: number, sz: number, material: ArenaBoxSurface['material']): ArenaBoxSurface {
  return { id, kind: 'box', center: { x, y, z }, size: { x: sx, y: sy, z: sz }, orientation: IDENTITY, material }
}

interface PlanarPoint { readonly x: number; readonly z: number }

function appendAxisAlignedWall(
  surfaces: ArenaBoxSurface[],
  id: string,
  from: PlanarPoint,
  to: PlanarPoint,
  wallHeight: number,
  wallThickness: number,
  material: ArenaBoxSurface['material'] = 'wall',
): void {
  if (from.x !== to.x && from.z !== to.z) throw new TypeError(`Wall ${id} must be axis-aligned`)
  const horizontal = from.z === to.z
  surfaces.push(box(
    id,
    (from.x + to.x) / 2,
    wallHeight / 2,
    (from.z + to.z) / 2,
    horizontal ? Math.abs(to.x - from.x) : wallThickness,
    wallHeight,
    horizontal ? wallThickness : Math.abs(to.z - from.z),
    material,
  ))
}

function appendHorizontalRunWithDoors(
  surfaces: ArenaBoxSurface[],
  idPrefix: string,
  xMinimum: number,
  xMaximum: number,
  z: number,
  doorCenters: readonly number[],
  doorwayWidth: number,
  wallHeight: number,
  wallThickness: number,
  material: ArenaBoxSurface['material'] = 'wall',
): void {
  let cursor = xMinimum
  const intervals = [...doorCenters].sort((a, b) => a - b).map((center) => ({ minimum: center - doorwayWidth / 2, maximum: center + doorwayWidth / 2 }))
  let segment = 1
  for (const interval of intervals) {
    if (interval.minimum > cursor) appendAxisAlignedWall(surfaces, `${idPrefix}-segment-${String(segment++).padStart(2, '0')}`, { x: cursor, z }, { x: interval.minimum, z }, wallHeight, wallThickness, material)
    cursor = Math.max(cursor, interval.maximum)
  }
  if (cursor < xMaximum) appendAxisAlignedWall(surfaces, `${idPrefix}-segment-${String(segment).padStart(2, '0')}`, { x: cursor, z }, { x: xMaximum, z }, wallHeight, wallThickness, material)
}

function appendVerticalRunWithDoors(
  surfaces: ArenaBoxSurface[],
  idPrefix: string,
  zMinimum: number,
  zMaximum: number,
  x: number,
  doorCenters: readonly number[],
  doorwayWidth: number,
  wallHeight: number,
  wallThickness: number,
  material: ArenaBoxSurface['material'] = 'wall',
): void {
  let cursor = zMinimum
  const intervals = [...doorCenters].sort((a, b) => a - b).map((center) => ({ minimum: center - doorwayWidth / 2, maximum: center + doorwayWidth / 2 }))
  let segment = 1
  for (const interval of intervals) {
    if (interval.minimum > cursor) appendAxisAlignedWall(surfaces, `${idPrefix}-segment-${String(segment++).padStart(2, '0')}`, { x, z: cursor }, { x, z: interval.minimum }, wallHeight, wallThickness, material)
    cursor = Math.max(cursor, interval.maximum)
  }
  if (cursor < zMaximum) appendAxisAlignedWall(surfaces, `${idPrefix}-segment-${String(segment).padStart(2, '0')}`, { x, z: cursor }, { x, z: zMaximum }, wallHeight, wallThickness, material)
}

function appendNorthSouthRoom(
  surfaces: ArenaBoxSurface[],
  idPrefix: string,
  side: 'north' | 'south',
  centerX: number,
  boundaryZ: number,
  roomDepth: number,
  roomHalfWidth: number,
  doorwayWidth: number,
  wallHeight: number,
  wallThickness: number,
): void {
  const direction = side === 'north' ? -1 : 1
  const jambHalfWidth = doorwayWidth / 2 + wallThickness / 2
  // Join the jamb to the corridor's outer face. The touching faces point in
  // opposite directions, so only one can be visible from either side.
  const jambStartZ = boundaryZ + direction * wallThickness / 2
  const shoulderZ = boundaryZ + direction * roomDepth * 0.32
  const backZ = boundaryZ + direction * roomDepth
  appendAxisAlignedWall(surfaces, `${idPrefix}-jamb-west`, { x: centerX - jambHalfWidth, z: jambStartZ }, { x: centerX - jambHalfWidth, z: shoulderZ }, wallHeight, wallThickness)
  appendAxisAlignedWall(surfaces, `${idPrefix}-jamb-east`, { x: centerX + jambHalfWidth, z: jambStartZ }, { x: centerX + jambHalfWidth, z: shoulderZ }, wallHeight, wallThickness)
  appendAxisAlignedWall(surfaces, `${idPrefix}-shoulder-west`, { x: centerX - roomHalfWidth, z: shoulderZ }, { x: centerX - jambHalfWidth, z: shoulderZ }, wallHeight, wallThickness)
  appendAxisAlignedWall(surfaces, `${idPrefix}-shoulder-east`, { x: centerX + jambHalfWidth, z: shoulderZ }, { x: centerX + roomHalfWidth, z: shoulderZ }, wallHeight, wallThickness)
  appendAxisAlignedWall(surfaces, `${idPrefix}-side-west`, { x: centerX - roomHalfWidth, z: shoulderZ }, { x: centerX - roomHalfWidth, z: backZ }, wallHeight, wallThickness)
  appendAxisAlignedWall(surfaces, `${idPrefix}-side-east`, { x: centerX + roomHalfWidth, z: shoulderZ }, { x: centerX + roomHalfWidth, z: backZ }, wallHeight, wallThickness)
  appendHorizontalRunWithDoors(surfaces, `${idPrefix}-back`, centerX - roomHalfWidth, centerX + roomHalfWidth, backZ, [centerX], doorwayWidth, wallHeight, wallThickness, 'accent')
}

function appendEastWestRoom(
  surfaces: ArenaBoxSurface[],
  idPrefix: string,
  side: 'east' | 'west',
  centerZ: number,
  boundaryX: number,
  roomDepth: number,
  roomHalfWidth: number,
  doorwayWidth: number,
  wallHeight: number,
  wallThickness: number,
): void {
  const direction = side === 'west' ? -1 : 1
  const jambHalfWidth = doorwayWidth / 2 + wallThickness / 2
  // Join the jamb to the corridor's outer face. The touching faces point in
  // opposite directions, so only one can be visible from either side.
  const jambStartX = boundaryX + direction * wallThickness / 2
  const shoulderX = boundaryX + direction * roomDepth * 0.32
  const backX = boundaryX + direction * roomDepth
  appendAxisAlignedWall(surfaces, `${idPrefix}-jamb-north`, { x: jambStartX, z: centerZ - jambHalfWidth }, { x: shoulderX, z: centerZ - jambHalfWidth }, wallHeight, wallThickness)
  appendAxisAlignedWall(surfaces, `${idPrefix}-jamb-south`, { x: jambStartX, z: centerZ + jambHalfWidth }, { x: shoulderX, z: centerZ + jambHalfWidth }, wallHeight, wallThickness)
  appendAxisAlignedWall(surfaces, `${idPrefix}-shoulder-north`, { x: shoulderX, z: centerZ - roomHalfWidth }, { x: shoulderX, z: centerZ - jambHalfWidth }, wallHeight, wallThickness)
  appendAxisAlignedWall(surfaces, `${idPrefix}-shoulder-south`, { x: shoulderX, z: centerZ + jambHalfWidth }, { x: shoulderX, z: centerZ + roomHalfWidth }, wallHeight, wallThickness)
  appendAxisAlignedWall(surfaces, `${idPrefix}-side-north`, { x: shoulderX, z: centerZ - roomHalfWidth }, { x: backX, z: centerZ - roomHalfWidth }, wallHeight, wallThickness)
  appendAxisAlignedWall(surfaces, `${idPrefix}-side-south`, { x: shoulderX, z: centerZ + roomHalfWidth }, { x: backX, z: centerZ + roomHalfWidth }, wallHeight, wallThickness)
  appendVerticalRunWithDoors(surfaces, `${idPrefix}-back`, centerZ - roomHalfWidth, centerZ + roomHalfWidth, backX, [centerZ], doorwayWidth, wallHeight, wallThickness, 'accent')
}

function buildRegions(size: ArenaSize, width: number, depth: number): { regions: ArenaRegion[]; links: ArenaLink[] } {
  const regions: ArenaRegion[] = [
    { id: 'combat-center', role: 'combat', center: { x: 0, y: 0, z: 0 }, size: { x: width * 0.28, y: 1, z: depth * 0.3 } },
    { id: 'route-east', role: 'route', center: { x: width * 0.34, y: 0, z: 0 }, size: { x: width * 0.24, y: 1, z: depth * 0.72 } },
    { id: 'route-north', role: 'route', center: { x: 0, y: 0, z: -depth * 0.34 }, size: { x: width * 0.72, y: 1, z: depth * 0.24 } },
    { id: 'route-south', role: 'route', center: { x: 0, y: 0, z: depth * 0.34 }, size: { x: width * 0.72, y: 1, z: depth * 0.24 } },
    { id: 'route-west', role: 'route', center: { x: -width * 0.34, y: 0, z: 0 }, size: { x: width * 0.24, y: 1, z: depth * 0.72 } },
    { id: 'spawn-east', role: 'spawn', center: { x: width * 0.38, y: 0, z: 0 }, size: { x: width * 0.18, y: 1, z: depth * 0.62 } },
    { id: 'spawn-north', role: 'spawn', center: { x: 0, y: 0, z: -depth * 0.38 }, size: { x: width * 0.62, y: 1, z: depth * 0.18 } },
    { id: 'spawn-south', role: 'spawn', center: { x: 0, y: 0, z: depth * 0.38 }, size: { x: width * 0.62, y: 1, z: depth * 0.18 } },
    { id: 'spawn-west', role: 'spawn', center: { x: -width * 0.38, y: 0, z: 0 }, size: { x: width * 0.18, y: 1, z: depth * 0.62 } },
  ]
  if (size !== 'small') {
    regions.push(
      { id: 'combat-east', role: 'combat', center: { x: width * 0.22, y: 0, z: 0 }, size: { x: width * 0.16, y: 1, z: depth * 0.24 } },
      { id: 'combat-west', role: 'combat', center: { x: -width * 0.22, y: 0, z: 0 }, size: { x: width * 0.16, y: 1, z: depth * 0.24 } },
    )
  }
  if (size === 'large') {
    regions.push(
      { id: 'combat-north-hub', role: 'combat', center: { x: 0, y: 0, z: -depth * 0.2 }, size: { x: width * 0.22, y: 1, z: depth * 0.16 } },
      { id: 'combat-south-hub', role: 'combat', center: { x: 0, y: 0, z: depth * 0.2 }, size: { x: width * 0.22, y: 1, z: depth * 0.16 } },
    )
  }

  const links: ArenaLink[] = []
  const add = (id: string, fromRegionId: string, toRegionId: string, exposure: ArenaLink['exposure']) => links.push({ id, fromRegionId, toRegionId, exposure })
  for (const side of ['east', 'north', 'south', 'west'] as const) {
    add(`link-center-${side}`, 'combat-center', `route-${side}`, side === 'east' || side === 'west' ? 'open' : 'covered')
    add(`link-route-spawn-${side}`, `route-${side}`, `spawn-${side}`, 'covered')
  }
  add('link-ring-east-north', 'route-east', 'route-north', 'covered')
  add('link-ring-east-south', 'route-east', 'route-south', 'open')
  add('link-ring-north-west', 'route-north', 'route-west', 'open')
  add('link-ring-south-west', 'route-south', 'route-west', 'covered')
  add('link-spawn-east-north', 'spawn-east', 'route-north', 'covered')
  add('link-spawn-east-south', 'spawn-east', 'route-south', 'open')
  add('link-spawn-north-east', 'spawn-north', 'route-east', 'covered')
  add('link-spawn-north-west', 'spawn-north', 'route-west', 'open')
  add('link-spawn-south-east', 'spawn-south', 'route-east', 'open')
  add('link-spawn-south-west', 'spawn-south', 'route-west', 'covered')
  add('link-spawn-west-north', 'spawn-west', 'route-north', 'open')
  add('link-spawn-west-south', 'spawn-west', 'route-south', 'covered')
  if (size !== 'small') {
    add('link-combat-east-center', 'combat-east', 'combat-center', 'open')
    add('link-combat-east-ring', 'combat-east', 'route-east', 'covered')
    add('link-combat-west-center', 'combat-west', 'combat-center', 'open')
    add('link-combat-west-ring', 'combat-west', 'route-west', 'covered')
  }
  if (size === 'large') {
    add('link-hub-north-center', 'combat-north-hub', 'combat-center', 'open')
    add('link-hub-north-ring', 'combat-north-hub', 'route-north', 'covered')
    add('link-hub-south-center', 'combat-south-hub', 'combat-center', 'open')
    add('link-hub-south-ring', 'combat-south-hub', 'route-south', 'covered')
  }
  return { regions, links }
}

function buildSpawns(size: ArenaSize, rules: ArenaGeneratorPresetRules): { spawnRegions: ArenaSpawnRegion[]; spawnAnchors: ArenaSpawnAnchor[] } {
  const sides = ['east', 'north', 'south', 'west'] as const
  const width = rules.widthMeters
  const depth = rules.depthMeters
  const perSide = Math.ceil(rules.spawnSlotCount / sides.length)
  const anchors: ArenaSpawnAnchor[] = []
  for (const side of sides) {
    for (let index = 0; index < perSide && anchors.length < rules.spawnSlotCount; index += 1) {
      const t = perSide === 1 ? 0 : index / (perSide - 1) - 0.5
      const sideBand = index < Math.ceil(perSide / 2)
        ? -0.4 + index * 0.12
        : 0.22 + (index - Math.ceil(perSide / 2)) * 0.14
      const id = `spawn-${side}-${String(index + 1).padStart(2, '0')}`
      const position = side === 'east'
        ? { x: width * 0.38, y: 0, z: (size === 'small' ? t * 0.58 : sideBand) * depth }
        : side === 'west'
          ? { x: -width * 0.38, y: 0, z: (size === 'small' ? -t * 0.58 : -sideBand) * depth }
          : side === 'north'
            ? { x: -t * width * 0.58, y: 0, z: -depth * 0.4 }
            : { x: t * width * 0.58, y: 0, z: depth * 0.4 }
      // Cardinal yaw values avoid engine-dependent transcendental rounding in
      // canonical arena bytes while keeping every spawn aimed toward the hub.
      const yawRadians = side === 'east' ? -Math.PI / 2 : side === 'west' ? Math.PI / 2 : side === 'north' ? Math.PI : 0
      anchors.push({ id, spawnRegionId: `spawn-region-${side}`, position, yawRadians })
    }
  }
  const spawnRegions = sides.map((side): ArenaSpawnRegion => ({
    id: `spawn-region-${side}`,
    regionId: `spawn-${side}`,
    anchorIds: anchors.filter((anchor) => anchor.spawnRegionId === `spawn-region-${side}`).map(({ id }) => id),
  }))
  return { spawnRegions, spawnAnchors: anchors }
}

function appendSubCapsuleGapSeals(surfaces: ArenaBoxSurface[], minimumClearance: number): void {
  const solids = surfaces.filter(({ id }) => id !== 'floor')
  const seen = new Set<string>()
  let sealIndex = 1
  const appendSeal = (xMinimum: number, xMaximum: number, zMinimum: number, zMaximum: number, yMinimum: number, yMaximum: number): void => {
    if (xMaximum - xMinimum <= 1e-6 || zMaximum - zMinimum <= 1e-6 || yMaximum - yMinimum <= 1e-6) return
    const key = [xMinimum, xMaximum, zMinimum, zMaximum, yMinimum, yMaximum].map((value) => value.toFixed(9)).join(':')
    if (seen.has(key)) return
    seen.add(key)
    surfaces.push(box(
      `wall-architecture-gap-seal-${String(sealIndex++).padStart(2, '0')}`,
      (xMinimum + xMaximum) / 2,
      (yMinimum + yMaximum) / 2,
      (zMinimum + zMaximum) / 2,
      xMaximum - xMinimum,
      yMaximum - yMinimum,
      zMaximum - zMinimum,
      'wall',
    ))
  }

  solids.forEach((first, index) => solids.slice(index + 1).forEach((second) => {
    const firstXMinimum = first.center.x - first.size.x / 2
    const firstXMaximum = first.center.x + first.size.x / 2
    const firstZMinimum = first.center.z - first.size.z / 2
    const firstZMaximum = first.center.z + first.size.z / 2
    const secondXMinimum = second.center.x - second.size.x / 2
    const secondXMaximum = second.center.x + second.size.x / 2
    const secondZMinimum = second.center.z - second.size.z / 2
    const secondZMaximum = second.center.z + second.size.z / 2
    const xOverlapMinimum = Math.max(firstXMinimum, secondXMinimum)
    const xOverlapMaximum = Math.min(firstXMaximum, secondXMaximum)
    const zOverlapMinimum = Math.max(firstZMinimum, secondZMinimum)
    const zOverlapMaximum = Math.min(firstZMaximum, secondZMaximum)
    const yMinimum = Math.max(first.center.y - first.size.y / 2, second.center.y - second.size.y / 2)
    const yMaximum = Math.min(first.center.y + first.size.y / 2, second.center.y + second.size.y / 2)

    const left = first.center.x <= second.center.x ? first : second
    const right = left === first ? second : first
    const xGapMinimum = left.center.x + left.size.x / 2
    const xGapMaximum = right.center.x - right.size.x / 2
    const xGap = xGapMaximum - xGapMinimum
    if (xGap > 1e-6 && xGap + 1e-9 < minimumClearance && zOverlapMaximum - zOverlapMinimum > 1e-6) {
      appendSeal(xGapMinimum, xGapMaximum, zOverlapMinimum, zOverlapMaximum, yMinimum, yMaximum)
    }

    const north = first.center.z <= second.center.z ? first : second
    const south = north === first ? second : first
    const zGapMinimum = north.center.z + north.size.z / 2
    const zGapMaximum = south.center.z - south.size.z / 2
    const zGap = zGapMaximum - zGapMinimum
    if (zGap > 1e-6 && zGap + 1e-9 < minimumClearance && xOverlapMaximum - xOverlapMinimum > 1e-6) {
      appendSeal(xOverlapMinimum, xOverlapMaximum, zGapMinimum, zGapMaximum, yMinimum, yMaximum)
    }
  }))
}

function buildSurfaces(size: ArenaSize, rules: ArenaGeneratorPresetRules, _seed: number, _attempt: number, minimumClearance: number, recipe: ArenaRecipeGraph): ArenaBoxSurface[] {
  const {
    widthMeters: width,
    depthMeters: depth,
    wallHeightMeters: wallHeight,
    wallThicknessMeters: wallThickness,
    corridorWidthMeters: _corridorWidth,
    doorwayWidthMeters: doorwayWidth,
    perimeterInsetMeters: perimeterInset,
    routeInsetMeters: routeInset,
    coverSizeMeters: coverSize,
  } = rules
  const corridorWidth = recipe.corridorWidth
  const surfaces: ArenaBoxSurface[] = [box('floor', 0, -0.5, 0, width, 1, depth, 'floor')]

  // A closed stepped outline keeps the arena bounded without reducing the
  // exterior to four sides of one rectangular box.
  const halfWidth = width / 2
  const halfDepth = depth / 2
  const perimeter: PlanarPoint[] = [
    { x: -halfWidth + perimeterInset, z: -halfDepth },
    { x: halfWidth - perimeterInset, z: -halfDepth },
    { x: halfWidth - perimeterInset, z: -halfDepth + perimeterInset },
    { x: halfWidth, z: -halfDepth + perimeterInset },
    { x: halfWidth, z: halfDepth - perimeterInset },
    { x: halfWidth - perimeterInset, z: halfDepth - perimeterInset },
    { x: halfWidth - perimeterInset, z: halfDepth },
    { x: -halfWidth + perimeterInset, z: halfDepth },
    { x: -halfWidth + perimeterInset, z: halfDepth - perimeterInset },
    { x: -halfWidth, z: halfDepth - perimeterInset },
    { x: -halfWidth, z: -halfDepth + perimeterInset },
    { x: -halfWidth + perimeterInset, z: -halfDepth + perimeterInset },
  ]
  perimeter.forEach((point, index) => appendAxisAlignedWall(
    surfaces,
    `wall-perimeter-${String(index + 1).padStart(2, '0')}`,
    point,
    perimeter[(index + 1) % perimeter.length]!,
    wallHeight,
    wallThickness,
  ))

  // Primary room/corridor walls form the arena's ceiling-aligned shell.
  // Only separately identified buttresses below are intentional low cover.
  const architectureHeight = wallHeight
  const corridorBoundaryOffset = corridorWidth / 2 + wallThickness / 2
  const corridorHalfLength = halfWidth - perimeterInset - routeInset
  const roomDepth = recipe.roomDepth
  const roomHalfWidth = doorwayWidth / 2 + rules.corridorWidthMeters * 0.55


  appendHorizontalRunWithDoors(surfaces, 'wall-architecture-corridor-north', -corridorHalfLength, corridorHalfLength, -corridorBoundaryOffset, recipe.northDoors, doorwayWidth, architectureHeight, wallThickness)
  appendHorizontalRunWithDoors(surfaces, 'wall-architecture-corridor-south', -corridorHalfLength, corridorHalfLength, corridorBoundaryOffset, recipe.southDoors, doorwayWidth, architectureHeight, wallThickness)
  for (const [index, northDoorX] of recipe.northDoors.entries()) appendNorthSouthRoom(surfaces, `wall-architecture-room-north-${index}`, 'north', northDoorX, -corridorBoundaryOffset, roomDepth, roomHalfWidth, doorwayWidth, architectureHeight, wallThickness)
  for (const [index, southDoorX] of recipe.southDoors.entries()) appendNorthSouthRoom(surfaces, `wall-architecture-room-south-${index}`, 'south', southDoorX, corridorBoundaryOffset, roomDepth, roomHalfWidth, doorwayWidth, architectureHeight, wallThickness)

  if (size === 'small') {
    appendVerticalRunWithDoors(surfaces, 'wall-architecture-corridor-east-cap', -corridorBoundaryOffset, corridorBoundaryOffset, corridorHalfLength, [0], doorwayWidth, architectureHeight, wallThickness)
  } else {
    appendVerticalRunWithDoors(surfaces, 'wall-architecture-corridor-east-cap', -corridorBoundaryOffset, corridorBoundaryOffset, corridorHalfLength, [0], doorwayWidth, architectureHeight, wallThickness)
    appendEastWestRoom(surfaces, 'wall-architecture-room-east', 'east', 0, corridorHalfLength, roomDepth, roomHalfWidth, doorwayWidth, architectureHeight, wallThickness)
  }
  if (size === 'large') {
    appendVerticalRunWithDoors(surfaces, 'wall-architecture-corridor-west-cap', -corridorBoundaryOffset, corridorBoundaryOffset, -corridorHalfLength, [0], doorwayWidth, architectureHeight, wallThickness)
    appendEastWestRoom(surfaces, 'wall-architecture-room-west', 'west', 0, -corridorHalfLength, roomDepth, roomHalfWidth, doorwayWidth, architectureHeight, wallThickness)
  }

  // Buttresses are attached to room back walls. They create cover without
  // reverting to unrelated cubes scattered through the playable footprint.
  const backs = surfaces.filter((surface) => surface.id.startsWith('wall-architecture-room-') && surface.id.includes('-back-'))
  // v6 release snapshots had one fixed buttress height.  Keep that legacy
  // geometry when the later v7 tuning fields are absent; never mutate the
  // stored revision just to make it runnable in a newer client.
  const legacyButtressHeight = wallHeight * 0.68
  const barrierLow = Number.isFinite(rules.barrierLowMeters) ? rules.barrierLowMeters : legacyButtressHeight
  const barrierHigh = Number.isFinite(rules.barrierHighMeters) ? rules.barrierHighMeters : legacyButtressHeight
  for (let index = 0; index < Math.min(rules.coverCount, backs.length); index += 1) {
    const source = backs[index]!
    const horizontal = source.size.x > source.size.z
    const inwardX = source.center.x === 0 ? 0 : -Math.sign(source.center.x)
    const inwardZ = source.center.z === 0 ? 0 : -Math.sign(source.center.z)
    // Embed the buttress slightly into the back wall without aligning its
    // outer face with the end face of the adjacent room side wall.
    const offset = coverSize / 2 + wallThickness / 4
    surfaces.push(box(
      `wall-architecture-buttress-${String(index + 1).padStart(2, '0')}`,
      source.center.x + (horizontal ? 0 : inwardX * offset),
      (index % 2 === 0 ? barrierLow : barrierHigh) / 2,
      source.center.z + (horizontal ? inwardZ * offset : 0),
      horizontal ? Math.min(coverSize, source.size.x) : coverSize,
      index % 2 === 0 ? barrierLow : barrierHigh,
      horizontal ? coverSize : Math.min(coverSize, source.size.z),
      index % 2 === 0 ? 'accent' : 'wall',
    ))
  }
  appendSubCapsuleGapSeals(surfaces, minimumClearance)
  for (const surface of surfaces.filter(s=>s.id.includes('-gap-seal-'))) { surface.size.y=wallHeight;surface.center.y=wallHeight/2 }
  return surfaces
}

interface ResolvedGenerationOptions {
  readonly size: ArenaSize
  readonly seed: number
  readonly profile: GameDesignProfile | ValidatedGameDesignProfile
}

function buildDefinition(options: ResolvedGenerationOptions, attempt: number): ArenaDefinition {
  const rules = options.profile.rules.arenaGenerator.presets[options.size]
  const minimumClearance = (options.profile.rules.player.capsule.radius + options.profile.rules.player.capsule.skinWidth) * 2
  const recipe = resolveArenaRecipe(options.size, options.seed, rules)
  const semantic = buildRegions(options.size, rules.widthMeters, rules.depthMeters)
  for (const [side,doors] of [['north',recipe.northDoors],['south',recipe.southDoors]] as const) {
    for(const [index,x] of doors.entries()) {
      const id=`recipe-room-${side}-${index}`
      semantic.regions.push({id,role:'route',center:{x,y:0,z:(side === 'north' ? -1 : 1)*(recipe.corridorWidth/2+rules.wallThicknessMeters/2+recipe.roomDepth*0.66)},size:{x:rules.doorwayWidthMeters+rules.corridorWidthMeters*1.1,y:1,z:recipe.roomDepth*0.68}})
      semantic.links.push({id:`link-${id}-inner`,fromRegionId:'combat-center',toRegionId:id,exposure:'open'},{id:`link-${id}-outer`,fromRegionId:id,toRegionId:`route-${side}`,exposure:'covered'})
    }
  }
  if (recipe.verticalRole === 'basement') {
    // The lower bowl is a full combat arena, not a dead-end shortcut: four
    // perimeter-facing combat pockets give every spawn side an equivalent
    // first engagement choice before players drop into the central basin.
    const pockets: Array<readonly [string, number, number]> = [
      ['east', rules.widthMeters * 0.38 - rules.corridorWidthMeters / 2, 0],
      ['west', -rules.widthMeters * 0.38 + rules.corridorWidthMeters / 2, 0],
      ['north', 0, -rules.depthMeters * 0.38 + rules.corridorWidthMeters / 2],
      ['south', 0, rules.depthMeters * 0.38 - rules.corridorWidthMeters / 2],
    ]
    for (const [side, x, z] of pockets) {
      const id = `combat-basement-${side}`
      semantic.regions.push({ id, role: 'combat', center: { x, y: 0, z }, size: { x: rules.corridorWidthMeters, y: 1, z: rules.corridorWidthMeters } })
      semantic.links.push({ id: `link-${id}-center`, fromRegionId: id, toRegionId: 'combat-center', exposure: 'covered' })
    }
  }
  const spawns = buildSpawns(options.size, rules)
  const payload: ArenaDefinitionPayload = {
    schemaVersion: 5,
    id: `procedural-${options.size}-${options.seed.toString(16).padStart(8, '0')}`,
    seed: options.seed,
    size: options.size,
    generatorVersion: PROCEDURAL_ARENA_GENERATOR_VERSION,
    generatorProfile: {
      id: options.profile.id,
      revision: options.profile.revision,
      contentHash: options.profile.contentHash as ContentHash,
    },
    recipeId: recipe.id,
    regions: semantic.regions.map(region => ({ ...region, supportSurfaceId: 'floor' })),
    links: semantic.links,
    surfaces: buildSurfaces(options.size, rules, options.seed, attempt, minimumClearance, recipe).map(surface => ({ ...surface, semantics: { classification: surface.material === 'floor' ? 'floor' : surface.id.includes('-buttress-') ? 'movement-barrier' : 'wall', movement: true, projectile: !surface.id.includes('-buttress-'), sector: surface.center.z < 0 ? 'north' : 'south' } })),
    spawnRegions: spawns.spawnRegions,
    spawnAnchors: spawns.spawnAnchors.map(anchor => ({ ...anchor, supportSurfaceId: 'floor' })),
  }
  for (const side of ['north', 'south']) {
    const window = payload.surfaces.find(s => s.id.startsWith(`wall-architecture-corridor-${side}-segment-`))
    if (window) window.semantics = { ...window.semantics!, classification:'window' }
  }
  payload.attachments = payload.surfaces.filter(s => s.id.includes('-back-')).map(s => ({ id:`relief-${s.id}`, surfaceId:s.id, kind:'relief-niche' }))
  payload.attachments.push(...payload.surfaces.filter(s => s.id.includes('-shoulder-')).map(s => ({ id:`lamp-${s.id}`, surfaceId:s.id, kind:'lamp' as const })))
  if (recipe.verticalRole === 'gallery' || recipe.verticalRole === 'balcony' || recipe.verticalRole === 'dual-deck') {
    const half = recipe.verticalRole === 'dual-deck' ? rules.rampWidthMeters : rules.rampWidthMeters / 2
    const run = rules.rampLengthMeters
    const height = recipe.verticalRole === 'dual-deck' ? rules.elevationDeltaMeters * rules.upperDeckHeightMultiplier : rules.elevationDeltaMeters
    const sector = recipe.verticalRole === 'gallery' ? 'gallery' : recipe.verticalRole === 'balcony' ? 'balcony' : 'upper-combat'
    // Adjacent ramp lanes tessellate a continuous cross-arena elevation belt.
    // Every doorway sees the same support height on both sides: no cliff access.
    // Adjacent ramp lanes tessellate a continuous cross-arena elevation belt.
    // Every doorway sees the same support height on both sides: no cliff access.
    const laneCount = Math.ceil(rules.depthMeters / rules.rampWidthMeters)
    const laneWidth = rules.depthMeters / laneCount
    for (let lane = 0; lane < laneCount; lane++) {
      const z = -rules.depthMeters / 2 + laneWidth * (lane + 0.5)
      const west = rampBox(`ramp-west-${lane}`, -half-run, -half, 0, height, z, laneWidth, rules.wallThicknessMeters)
      const east = rampBox(`ramp-east-${lane}`, half, half+run, height, 0, z, laneWidth, rules.wallThicknessMeters)
      west.semantics = { ...west.semantics!, sector }
      east.semantics = { ...east.semantics!, sector }
      payload.surfaces.push(west, east)
    }
    const supportSurfaceId = `floor-upper-${recipe.verticalRole}`
    payload.surfaces.push({ ...box(supportSurfaceId, 0, height / 2, 0, half * 2, height, rules.depthMeters, 'floor'), semantics: { classification:'floor',movement:true,projectile:true,sector } })
    if (recipe.verticalRole === 'balcony' || recipe.verticalRole === 'dual-deck') {
      const guardHeight = rules.barrierHighMeters
      const guardThickness = rules.wallThicknessMeters / 2
      const prefix = recipe.verticalRole === 'balcony' ? 'slab-balcony' : 'slab-dual-deck'
      payload.surfaces.push(
        { ...box(`${prefix}-north`, 0, height + guardHeight / 2, -rules.depthMeters / 2 + guardThickness / 2, half * 2, guardHeight, guardThickness, 'accent'), semantics: { classification: 'slab', movement: true, projectile: true, sector: 'gallery' } },
        { ...box(`${prefix}-south`, 0, height + guardHeight / 2, rules.depthMeters / 2 - guardThickness / 2, half * 2, guardHeight, guardThickness, 'accent'), semantics: { classification: 'slab', movement: true, projectile: true, sector: 'gallery' } },
      )
    }

    // A semantic layer is not inferred from the highest overlapping floor.
    // Regions in the deck footprint explicitly belong to it; all other
    // regions keep the main floor and links crossing the boundary name a ramp.
    const supportAt = (point: { x: number; z: number }): string => payload.surfaces
      .filter(surface => surface.semantics?.classification === 'floor' || surface.semantics?.classification === 'ramp')
      .map(surface => ({ id: surface.id, height: arenaSupportHeight(payload, surface.id, point.x, point.z) }))
      .filter((entry): entry is { id: string; height: number } => entry.height !== undefined)
      .sort((first, second) => second.height - first.height || first.id.localeCompare(second.id, 'en'))[0]?.id ?? 'floor'
    payload.regions = payload.regions.map(region => ({
      ...region,
      supportSurfaceId: supportAt(region.center),
    }))
    const regionById = new Map(payload.regions.map(region => [region.id, region]))
    const transitionSurfaceId = 'ramp-west-0'
    payload.links = payload.links.map(link => {
      const from = regionById.get(link.fromRegionId)
      const to = regionById.get(link.toRegionId)
      return from?.supportSurfaceId !== to?.supportSurfaceId ? { ...link, transitionSurfaceId } : link
    })
    payload.regions = payload.regions.map(region => ({
      ...region,
      // Keep legacy spawn/region placement on the visible walkable top while
      // retaining an explicit canonical support id for layer-aware consumers.
      center: supportedPoint(payload, region.center),
    }))
    payload.spawnAnchors = payload.spawnAnchors.map(anchor => ({
      ...anchor,
      supportSurfaceId: supportAt(anchor.position),
      position: supportedPoint(payload, anchor.position),
    }))
  } else if (recipe.verticalRole === 'basement') {
    const lowerHalf = rules.rampWidthMeters / 2
    const run = rules.rampLengthMeters
    const height = rules.elevationDeltaMeters * rules.basementDepthMultiplier
    const laneCount = Math.ceil(rules.depthMeters / rules.rampWidthMeters)
    const laneWidth = rules.depthMeters / laneCount
    const westDeckStart = -rules.widthMeters / 2 + rules.wallThicknessMeters
    const westDeckEnd = -lowerHalf - run
    const eastDeckStart = lowerHalf + run
    const eastDeckEnd = rules.widthMeters / 2 - rules.wallThicknessMeters
    const westSupportId = 'floor-main-west'
    const eastSupportId = 'floor-main-east'
    const northSupportId = 'floor-main-north'
    const southSupportId = 'floor-main-south'

    // The floor in the centre is deliberately retained at its original Y:
    // this is the accessible basement bowl.  The two side decks are a higher
    // main circulation layer, so descent never requires punching a hole into
    // a solid floor volume.
    const baseFloor = payload.surfaces.find(surface => surface.id === 'floor')!
    baseFloor.semantics = { ...baseFloor.semantics!, sector: 'basement' }
    payload.surfaces.push(
      { ...box(westSupportId, (westDeckStart + westDeckEnd) / 2, height / 2, 0, westDeckEnd - westDeckStart, height, rules.depthMeters - 2 * rules.wallThicknessMeters, 'floor'), semantics: { classification: 'floor', movement: true, projectile: true, sector: 'gallery' } },
      { ...box(eastSupportId, (eastDeckStart + eastDeckEnd) / 2, height / 2, 0, eastDeckEnd - eastDeckStart, height, rules.depthMeters - 2 * rules.wallThicknessMeters, 'floor'), semantics: { classification: 'floor', movement: true, projectile: true, sector: 'gallery' } },
      { ...box(northSupportId, 0, height / 2, (-rules.depthMeters / 2 + rules.wallThicknessMeters + -lowerHalf - run) / 2, eastDeckStart - westDeckEnd, height, rules.depthMeters / 2 - lowerHalf - run - rules.wallThicknessMeters, 'floor'), semantics: { classification: 'floor', movement: true, projectile: true, sector: 'gallery' } },
      { ...box(southSupportId, 0, height / 2, (rules.depthMeters / 2 - rules.wallThicknessMeters + lowerHalf + run) / 2, eastDeckStart - westDeckEnd, height, rules.depthMeters / 2 - lowerHalf - run - rules.wallThicknessMeters, 'floor'), semantics: { classification: 'floor', movement: true, projectile: true, sector: 'gallery' } },
    )
    for (let lane = 0; lane < laneCount; lane += 1) {
      const z = -rules.depthMeters / 2 + laneWidth * (lane + 0.5)
      const west = rampBox(`ramp-basement-west-${lane}`, westDeckEnd, -lowerHalf, height, 0, z, laneWidth, rules.wallThicknessMeters)
      const east = rampBox(`ramp-basement-east-${lane}`, lowerHalf, eastDeckStart, 0, height, z, laneWidth, rules.wallThicknessMeters)
      west.semantics = { ...west.semantics!, sector: 'basement' }
      east.semantics = { ...east.semantics!, sector: 'basement' }
      payload.surfaces.push(west, east)
    }
    const regionSupport = (x: number, z: number): string => payload.surfaces
      .filter(surface => surface.semantics?.classification === 'floor' || surface.semantics?.classification === 'ramp')
      .map(surface => ({ id: surface.id, height: arenaSupportHeight(payload, surface.id, x, z) }))
      .filter((entry): entry is { id: string; height: number } => entry.height !== undefined)
      .sort((first, second) => second.height - first.height || first.id.localeCompare(second.id, 'en'))[0]?.id ?? 'floor'
    payload.regions = payload.regions.map(region => ({
      ...region,
      // Spawn symmetry is preserved on the lower bowl; the raised side decks
      // are optional bypasses rather than a team-owned starting advantage.
      supportSurfaceId: regionSupport(region.center.x, region.center.z),
      center: supportedPoint(payload, region.center),
    }))
    payload.spawnAnchors = payload.spawnAnchors.map(anchor => ({ ...anchor, supportSurfaceId: regionSupport(anchor.position.x, anchor.position.z), position: supportedPoint(payload, anchor.position) }))
    const regionById = new Map(payload.regions.map(region => [region.id, region]))
    payload.links = payload.links.map(link => {
      const from = regionById.get(link.fromRegionId)
      const to = regionById.get(link.toRegionId)
      if (from?.supportSurfaceId === to?.supportSurfaceId) return link
      const transitionSurfaceId = from?.supportSurfaceId === westSupportId || to?.supportSurfaceId === westSupportId ? 'ramp-basement-west-0' : 'ramp-basement-east-0'
      return { ...link, transitionSurfaceId }
    })
  }
  payload.surfaces.push(...portalCollisionSurfaces(payload.surfaces))
  return createArenaDefinition(payload)
}

export function generateProceduralArena(input: ProceduralArenaGenerationOptions): ProceduralArenaGenerationResult {
  if (!Number.isSafeInteger(input.seed) || input.seed < 0 || input.seed > 0xffffffff) throw new RangeError('Procedural arena seed must be an unsigned 32-bit integer')
  const options: ResolvedGenerationOptions = { ...input, seed: input.seed >>> 0, profile: input.profile ?? PROTOTYPE_V1_PROFILE }
  const reports: ArenaGameplayValidationReport[] = []
  for (let attempt = 0; attempt < PROCEDURAL_ARENA_GENERATION_ATTEMPTS; attempt += 1) {
    const arena = buildDefinition(options, attempt)
    const report = validateArenaGameplay(arena, options.profile)
    reports.push(report)
    if (report.accepted) return { ok: true, arena, report, attempt }
  }
  return { ok: false, seed: options.seed, size: options.size, attempts: PROCEDURAL_ARENA_GENERATION_ATTEMPTS, reports }
}

export function requireProceduralArena(input: ProceduralArenaGenerationOptions): ArenaDefinition {
  const result = generateProceduralArena(input)
  if (result.ok) return result.arena
  const report = result.reports.at(-1)
  const errors = report?.errors.map(({ code }) => code).join(', ') ?? 'unknown validation error'
  throw new Error(`Не удалось построить арену ${input.size} для seed ${input.seed}: ${errors}; metrics ${JSON.stringify(report?.metrics)}`)
}

const fallbackSource = requireProceduralArena({ size: 'medium', seed: 0x53544152 })
const { contentHash: _fallbackSourceHash, ...fallbackPayload } = fallbackSource
export const PROCEDURAL_FALLBACK_ARENA = createArenaDefinition({
  ...fallbackPayload,
  id: 'shipped-fallback-arena-v4',
  generatorVersion: 'shipped-fallback-v4',
})
