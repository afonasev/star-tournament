import { type ContentHash } from '../core/canonical'
import { PROTOTYPE_V1_PROFILE, type ArenaGeneratorPresetRules, type GameDesignProfile, type ValidatedGameDesignProfile } from '../profiles'
import { createSeededRngState, nextRandomUint32, type SeededRngState } from '../simulation/rng'

import {
  ARENA_DEFINITION_SCHEMA_VERSION,
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

export const PROCEDURAL_ARENA_GENERATOR_VERSION = 'broken-ring-v2' as const
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

function random01(state: SeededRngState): { state: SeededRngState; value: number } {
  const sample = nextRandomUint32(state)
  return { state: sample.state, value: sample.value / 0x1_0000_0000 }
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

function buildSurfaces(size: ArenaSize, rules: ArenaGeneratorPresetRules, seed: number, attempt: number, minimumClearance: number): ArenaBoxSurface[] {
  const {
    widthMeters: width,
    depthMeters: depth,
    wallHeightMeters: wallHeight,
    wallThicknessMeters: wallThickness,
    corridorWidthMeters: corridorWidth,
    doorwayWidthMeters: doorwayWidth,
    perimeterInsetMeters: perimeterInset,
    routeInsetMeters: routeInset,
    coverSizeMeters: coverSize,
  } = rules
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

  let rng = createSeededRngState((seed ^ (attempt * 0x9e3779b9)) >>> 0)
  const mirrorSample = random01(rng); rng = mirrorSample.state
  const mirror = mirrorSample.value < 0.5 ? -1 : 1
  // Primary room/corridor walls form the arena's ceiling-aligned shell.
  // Only separately identified buttresses below are intentional low cover.
  const architectureHeight = wallHeight
  const corridorBoundaryOffset = corridorWidth / 2 + wallThickness / 2
  const corridorHalfLength = halfWidth - perimeterInset - routeInset
  const roomDepth = routeInset
  const roomHalfWidth = doorwayWidth / 2 + corridorWidth * 0.55
  const northDoorX = mirror * -corridorHalfLength * 0.24
  const southDoorX = mirror * corridorHalfLength * 0.24

  appendHorizontalRunWithDoors(surfaces, 'wall-architecture-corridor-north', -corridorHalfLength, corridorHalfLength, -corridorBoundaryOffset, [northDoorX], doorwayWidth, architectureHeight, wallThickness)
  appendHorizontalRunWithDoors(surfaces, 'wall-architecture-corridor-south', -corridorHalfLength, corridorHalfLength, corridorBoundaryOffset, [southDoorX], doorwayWidth, architectureHeight, wallThickness)
  appendNorthSouthRoom(surfaces, 'wall-architecture-room-north', 'north', northDoorX, -corridorBoundaryOffset, roomDepth, roomHalfWidth, doorwayWidth, architectureHeight, wallThickness)
  appendNorthSouthRoom(surfaces, 'wall-architecture-room-south', 'south', southDoorX, corridorBoundaryOffset, roomDepth, roomHalfWidth, doorwayWidth, architectureHeight, wallThickness)

  if (size === 'small') {
    appendAxisAlignedWall(surfaces, 'wall-architecture-corridor-east-cap', { x: corridorHalfLength, z: -corridorBoundaryOffset }, { x: corridorHalfLength, z: corridorBoundaryOffset }, architectureHeight, wallThickness, 'accent')
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
      architectureHeight * 0.34,
      source.center.z + (horizontal ? inwardZ * offset : 0),
      horizontal ? Math.min(coverSize, source.size.x) : coverSize,
      architectureHeight * 0.68,
      horizontal ? coverSize : Math.min(coverSize, source.size.z),
      index % 2 === 0 ? 'accent' : 'wall',
    ))
  }
  appendSubCapsuleGapSeals(surfaces, minimumClearance)
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
  const semantic = buildRegions(options.size, rules.widthMeters, rules.depthMeters)
  const spawns = buildSpawns(options.size, rules)
  const payload: ArenaDefinitionPayload = {
    schemaVersion: ARENA_DEFINITION_SCHEMA_VERSION,
    id: `procedural-${options.size}-${options.seed.toString(16).padStart(8, '0')}`,
    seed: options.seed,
    size: options.size,
    generatorVersion: PROCEDURAL_ARENA_GENERATOR_VERSION,
    generatorProfile: {
      id: options.profile.id,
      revision: options.profile.revision,
      contentHash: options.profile.contentHash as ContentHash,
    },
    regions: semantic.regions,
    links: semantic.links,
    surfaces: buildSurfaces(options.size, rules, options.seed, attempt, minimumClearance),
    spawnRegions: spawns.spawnRegions,
    spawnAnchors: spawns.spawnAnchors,
  }
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
  id: 'shipped-fallback-arena-v2',
  generatorVersion: 'shipped-fallback-v2',
})
