import { arenaNavigation } from '../ai/navigation'
import { arenaFloorHeight } from './elevation'
import { deepFreeze } from '../core/deepFreeze'
import { type GameDesignProfile, type ValidatedGameDesignProfile } from '../profiles'

import { blocksProjectiles, supportsNavigation, type ArenaDefinition, type ArenaSpawnAnchor } from './arenaDefinition'

export const ARENA_GAMEPLAY_VALIDATION_VERSION = 'arena-gameplay-v3' as const

export type ArenaGameplayValidationErrorCode =
  | 'PROFILE_IDENTITY_MISMATCH'
  | 'INSUFFICIENT_SPAWN_SLOTS'
  | 'SPAWN_INTERSECTS_STATIC_GEOMETRY'
  | 'INSUFFICIENT_DISTINCT_PLACEMENT'
  | 'DISCONNECTED_REGION_GRAPH'
  | 'SINGLE_LINK_FAILURE'
  | 'ROUTE_CHOICE_MISSING'
  | 'ARCHITECTURE_DISCONNECTED'
  | 'RECTANGULAR_PERIMETER'
  | 'CORRIDOR_CLEARANCE_FAILED'
  | 'DOORWAY_CLEARANCE_FAILED'
  | 'SUB_CAPSULE_GAP'
  | 'MODE_FAIRNESS_FAILED'
  | 'INVALID_ELEVATION_ROUTE'

export interface ArenaGameplayValidationError { readonly code: ArenaGameplayValidationErrorCode; readonly semanticIds: readonly string[]; readonly message: string }
export interface ArenaGameplayValidationMetrics {
  readonly recipeId: string
  readonly rampCount: number
  readonly maximumRouteDistanceMeters: number
  readonly regionCount: number
  readonly linkCount: number
  readonly freeSpawnSlotCount: number
  readonly minimumSpawnDistanceMeters: number
  readonly routeRedundancyRatio: number
  readonly maximumTeamDistanceRatio: number
  readonly initialEnemyLosRatio: number
  readonly perimeterSegmentCount: number
  readonly architecturalSurfaceCount: number
  readonly doorwayCount: number
  readonly corridorClearanceMeters: number
}
export interface ArenaGameplayValidationReport {
  readonly version: typeof ARENA_GAMEPLAY_VALIDATION_VERSION
  readonly arenaContentHash: string
  readonly profileContentHash: string
  readonly supportedModes: readonly ['ffa', 'teams']
  readonly accepted: boolean
  readonly metrics: ArenaGameplayValidationMetrics
  readonly errors: readonly ArenaGameplayValidationError[]
}

function overlapsSolid(anchor: ArenaSpawnAnchor, arena: ArenaDefinition, radius: number): boolean {
  return arena.surfaces.some((surface) => {
    if (supportsNavigation(surface)) return false
    const dx = Math.abs(anchor.position.x - surface.center.x)
    const dz = Math.abs(anchor.position.z - surface.center.z)
    return dx < surface.size.x / 2 + radius && dz < surface.size.z / 2 + radius && surface.center.y + surface.size.y / 2 > 0
  })
}

function connectedSpawnRegions(arena: ArenaDefinition, omittedLinkId?: string): boolean {
  const graph = new Map<string, string[]>()
  for (const region of arena.regions) graph.set(region.id, [])
  for (const link of arena.links) {
    if (link.id === omittedLinkId) continue
    graph.get(link.fromRegionId)?.push(link.toRegionId)
    graph.get(link.toRegionId)?.push(link.fromRegionId)
  }
  const start = arena.spawnRegions[0]?.regionId
  if (start === undefined) return false
  const visited = new Set([start])
  const queue = [start]
  while (queue.length > 0) {
    const id = queue.shift()!
    for (const next of graph.get(id) ?? []) if (!visited.has(next)) { visited.add(next); queue.push(next) }
  }
  return arena.spawnRegions.every(({ regionId }) => visited.has(regionId))
}

function minimumDistance(anchors: readonly ArenaSpawnAnchor[]): number {
  let minimum = Number.POSITIVE_INFINITY
  anchors.forEach((first, index) => anchors.slice(index + 1).forEach((second) => {
    minimum = Math.min(minimum, Math.hypot(first.position.x - second.position.x, first.position.z - second.position.z))
  }))
  return Number.isFinite(minimum) ? minimum : 0
}

function canPlaceEight(anchors: readonly ArenaSpawnAnchor[], separation: number): boolean {
  const selected: ArenaSpawnAnchor[] = []
  for (const anchor of anchors) {
    if (selected.every((other) => Math.hypot(anchor.position.x - other.position.x, anchor.position.z - other.position.z) >= separation)) selected.push(anchor)
    if (selected.length === 8) return true
  }
  return false
}

function planarDistance(first: { x: number; z: number }, second: { x: number; z: number }): number {
  return Math.hypot(first.x - second.x, first.z - second.z)
}

function segmentIntersectsBox(first: { x: number; z: number }, second: { x: number; z: number }, center: { x: number; z: number }, halfX: number, halfZ: number): boolean {
  let low = 0
  let high = 1
  for (const axis of ['x', 'z'] as const) {
    const delta = second[axis] - first[axis]
    const minimum = center[axis] - (axis === 'x' ? halfX : halfZ)
    const maximum = center[axis] + (axis === 'x' ? halfX : halfZ)
    if (Math.abs(delta) < 1e-9) { if (first[axis] < minimum || first[axis] > maximum) return false; continue }
    const a = (minimum - first[axis]) / delta
    const b = (maximum - first[axis]) / delta
    low = Math.max(low, Math.min(a, b)); high = Math.min(high, Math.max(a, b))
    if (low > high) return false
  }
  return high > 0.02 && low < 0.98
}

function hasLineOfSight(first: ArenaSpawnAnchor, second: ArenaSpawnAnchor, arena: ArenaDefinition): boolean {
  return !arena.surfaces.some((surface) => !supportsNavigation(surface) && blocksProjectiles(surface) && !surface.id.startsWith('wall-perimeter-') && surface.size.y >= 1.5 && segmentIntersectsBox(first.position, second.position, surface.center, surface.size.x / 2, surface.size.z / 2))
}

function surfacesTouch(first: ArenaDefinition['surfaces'][number], second: ArenaDefinition['surfaces'][number], tolerance = 0.02): boolean {
  return Math.abs(first.center.x - second.center.x) <= (first.size.x + second.size.x) / 2 + tolerance
    && Math.abs(first.center.z - second.center.z) <= (first.size.z + second.size.z) / 2 + tolerance
}

function subCapsuleGapSurfaceIds(arena: ArenaDefinition, minimumClearance: number): string[] {
  // Rotated portal parts are verified by physical capsule tests; this planar gap
  // audit applies to the axis-aligned architectural wall shell.
  const solids = arena.surfaces.filter(surface => !supportsNavigation(surface) && !surface.id.startsWith('portal-solid:'))
  const affected = new Set<string>()
  const coveredByAnotherSolid = (firstId: string, secondId: string, xMinimum: number, xMaximum: number, zMinimum: number, zMaximum: number, yMinimum: number, yMaximum: number): boolean => solids.some((candidate) => (
    candidate.id !== firstId
      && candidate.id !== secondId
      && candidate.center.x - candidate.size.x / 2 <= xMinimum + 1e-9
      && candidate.center.x + candidate.size.x / 2 >= xMaximum - 1e-9
      && candidate.center.z - candidate.size.z / 2 <= zMinimum + 1e-9
      && candidate.center.z + candidate.size.z / 2 >= zMaximum - 1e-9
      && candidate.center.y - candidate.size.y / 2 <= yMinimum + 1e-9
      && candidate.center.y + candidate.size.y / 2 >= yMaximum - 1e-9
  ))
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

    const xGapMinimum = Math.min(firstXMaximum, secondXMaximum)
    const xGapMaximum = Math.max(firstXMinimum, secondXMinimum)
    const xGap = xGapMaximum - xGapMinimum
    if (xGap > 1e-6 && xGap + 1e-9 < minimumClearance && zOverlapMaximum - zOverlapMinimum > 1e-6 && !coveredByAnotherSolid(first.id, second.id, xGapMinimum, xGapMaximum, zOverlapMinimum, zOverlapMaximum, yMinimum, yMaximum)) {
      affected.add(first.id); affected.add(second.id)
    }

    const zGapMinimum = Math.min(firstZMaximum, secondZMaximum)
    const zGapMaximum = Math.max(firstZMinimum, secondZMinimum)
    const zGap = zGapMaximum - zGapMinimum
    if (zGap > 1e-6 && zGap + 1e-9 < minimumClearance && xOverlapMaximum - xOverlapMinimum > 1e-6 && !coveredByAnotherSolid(first.id, second.id, xOverlapMinimum, xOverlapMaximum, zGapMinimum, zGapMaximum, yMinimum, yMaximum)) {
      affected.add(first.id); affected.add(second.id)
    }
  }))
  return [...affected].sort()
}

function architectureMetrics(arena: ArenaDefinition, rules: GameDesignProfile['rules']['arenaGenerator']['presets']['small']): {
  perimeter: ArenaDefinition['surfaces']
  architecture: ArenaDefinition['surfaces']
  disconnected: string[]
  irregularPerimeter: boolean
  doorwayCount: number
  invalidDoorways: string[]
  corridorClearanceMeters: number
} {
  const perimeter = arena.surfaces.filter(({ id }) => id.startsWith('wall-perimeter-'))
  const architecture = arena.surfaces.filter(({ id }) => id.startsWith('wall-architecture-'))
  const disconnected = architecture.filter((surface) => !architecture.some((candidate) => candidate.id !== surface.id && surfacesTouch(surface, candidate))).map(({ id }) => id)
  const perimeterClosed = perimeter.length >= 8 && perimeter.every((surface) => perimeter.filter((candidate) => candidate.id !== surface.id && surfacesTouch(surface, candidate)).length >= 2)
  const hasInsetHorizontal = perimeter.some((surface) => surface.size.x > surface.size.z && Math.abs(surface.center.z) < rules.depthMeters / 2 - rules.wallThicknessMeters / 4)
  const hasInsetVertical = perimeter.some((surface) => surface.size.z > surface.size.x && Math.abs(surface.center.x) < rules.widthMeters / 2 - rules.wallThicknessMeters / 4)
  const roomPrefixes = [...new Set(architecture.filter(({ id }) => id.includes('-jamb-')).map(({ id }) => id.slice(0, id.indexOf('-jamb-'))))]
  const invalidDoorways: string[] = []
  for (const prefix of roomPrefixes) {
    const west = architecture.find(({ id }) => id === `${prefix}-jamb-west`)
    const east = architecture.find(({ id }) => id === `${prefix}-jamb-east`)
    const north = architecture.find(({ id }) => id === `${prefix}-jamb-north`)
    const south = architecture.find(({ id }) => id === `${prefix}-jamb-south`)
    const clearance = west !== undefined && east !== undefined
      ? east.center.x - east.size.x / 2 - (west.center.x + west.size.x / 2)
      : north !== undefined && south !== undefined
        ? south.center.z - south.size.z / 2 - (north.center.z + north.size.z / 2)
        : 0
    if (clearance + 1e-9 < rules.doorwayWidthMeters) invalidDoorways.push(prefix)
  }
  const north = architecture.find(({ id }) => id.startsWith('wall-architecture-corridor-north-segment-'))
  const south = architecture.find(({ id }) => id.startsWith('wall-architecture-corridor-south-segment-'))
  const corridorClearanceMeters = north === undefined || south === undefined ? 0 : south.center.z - south.size.z / 2 - (north.center.z + north.size.z / 2)
  return { perimeter, architecture, disconnected, irregularPerimeter: perimeterClosed && hasInsetHorizontal && hasInsetVertical, doorwayCount: roomPrefixes.length, invalidDoorways, corridorClearanceMeters }
}

function fairnessMetrics(arena: ArenaDefinition, profile: GameDesignProfile | ValidatedGameDesignProfile): { maximumTeamDistanceRatio: number; initialEnemyLosRatio: number } {
  const combat = arena.regions.filter(({ role }) => role === 'combat')
  const nav = arena.recipeId ? arenaNavigation(arena,profile as ValidatedGameDesignProfile) : undefined
  const routeLength = (a:typeof combat[number]['center'],b:typeof a) => {
    if (!nav) return planarDistance(a,b)
    const route=nav.route(a,b)
    if (!route.length) return Infinity
    let previous=a,total=0
    for(const point of route) { total+=Math.hypot(point.x-previous.x,point.y-previous.y,point.z-previous.z);previous=point }
    return total
  }
  const distances = new Map(arena.spawnRegions.map((spawn) => {
    const region = arena.regions.find(({ id }) => id === spawn.regionId)!
    return [spawn.id, Math.min(...combat.map((target) => routeLength(region.center, target.center)))] as const
  }))
  const opposingPairs = [['spawn-region-east', 'spawn-region-west'], ['spawn-region-north', 'spawn-region-south']] as const
  const distanceRatios = opposingPairs.map(([first, second]) => {
    const a = distances.get(first) ?? 0
    const b = distances.get(second) ?? 0
    return Math.min(a, b) <= 0 ? Number.POSITIVE_INFINITY : Math.max(a, b) / Math.min(a, b)
  })
  const maximumTeamDistanceRatio = Math.max(...distanceRatios)
  let pairs = 0
  let visible = 0
  for (const [firstId, secondId] of opposingPairs) {
    const first = (arena.spawnRegions.find(({ id }) => id === firstId)?.anchorIds ?? []).map((id) => arena.spawnAnchors.find((anchor) => anchor.id === id)!).filter(Boolean)
    const second = (arena.spawnRegions.find(({ id }) => id === secondId)?.anchorIds ?? []).map((id) => arena.spawnAnchors.find((anchor) => anchor.id === id)!).filter(Boolean)
    for (const a of first) for (const b of second) { pairs += 1; if (hasLineOfSight(a, b, arena)) visible += 1 }
  }
  return { maximumTeamDistanceRatio, initialEnemyLosRatio: pairs === 0 ? 1 : visible / pairs }
}

export function validateArenaGameplay(arena: ArenaDefinition, profile: GameDesignProfile | ValidatedGameDesignProfile): ArenaGameplayValidationReport {
  const errors: ArenaGameplayValidationError[] = []
  const rules = profile.rules.arenaGenerator.presets[arena.size]
  if (arena.generatorProfile.id !== profile.id || arena.generatorProfile.revision !== profile.revision || arena.generatorProfile.contentHash !== profile.contentHash) {
    errors.push({ code: 'PROFILE_IDENTITY_MISMATCH', semanticIds: [arena.id], message: 'Arena generator profile identity не совпадает с active profile.' })
  }
  if (arena.spawnAnchors.length < 12 || arena.spawnAnchors.length < rules.spawnSlotCount) {
    errors.push({ code: 'INSUFFICIENT_SPAWN_SLOTS', semanticIds: arena.spawnAnchors.map(({ id }) => id), message: 'Недостаточно spawn slots для roster из восьми участников.' })
  }
  const radius = profile.rules.player.capsule.radius + profile.rules.player.capsule.skinWidth
  const free = arena.spawnAnchors.filter((anchor) => !overlapsSolid(anchor, arena, radius))
  const blocked = arena.spawnAnchors.filter((anchor) => !free.includes(anchor))
  if (blocked.length > 0) errors.push({ code: 'SPAWN_INTERSECTS_STATIC_GEOMETRY', semanticIds: blocked.map(({ id }) => id), message: 'Spawn slots пересекают static geometry.' })
  if (!canPlaceEight(free, rules.minimumSpawnSeparationMeters)) errors.push({ code: 'INSUFFICIENT_DISTINCT_PLACEMENT', semanticIds: free.map(({ id }) => id), message: 'Нельзя разместить восемь capsule с требуемым separation.' })
  if (!connectedSpawnRegions(arena)) errors.push({ code: 'DISCONNECTED_REGION_GRAPH', semanticIds: arena.spawnRegions.map(({ regionId }) => regionId), message: 'Spawn regions не связаны navigation graph.' })
  const resilientLinks = arena.links.filter((link) => connectedSpawnRegions(arena, link.id)).length
  if (resilientLinks !== arena.links.length) errors.push({ code: 'SINGLE_LINK_FAILURE', semanticIds: arena.links.filter((link) => !connectedSpawnRegions(arena, link.id)).map(({ id }) => id), message: 'Удаление одной link разрывает spawn connectivity.' })
  const missingRouteChoice = arena.spawnRegions.filter((spawn) => {
    const incident = arena.links.filter((link) => link.fromRegionId === spawn.regionId || link.toRegionId === spawn.regionId)
    return !incident.some(({ exposure }) => exposure === 'open') || !incident.some(({ exposure }) => exposure === 'covered')
  })
  if (missingRouteChoice.length > 0) errors.push({ code: 'ROUTE_CHOICE_MISSING', semanticIds: missingRouteChoice.map(({ id }) => id), message: 'Spawn region не имеет открытого и защищённого вариантов выхода.' })
  const architecture = architectureMetrics(arena, rules)
  if (architecture.disconnected.length > 0 || architecture.architecture.length < 8) errors.push({ code: 'ARCHITECTURE_DISCONNECTED', semanticIds: architecture.disconnected.length > 0 ? architecture.disconnected : architecture.architecture.map(({ id }) => id), message: 'Interior surfaces не образуют связные room/corridor wall chains.' })
  if (!architecture.irregularPerimeter) errors.push({ code: 'RECTANGULAR_PERIMETER', semanticIds: architecture.perimeter.map(({ id }) => id), message: 'Внешний контур должен быть замкнутым и содержать размерно-зависимые уступы.' })
  if (architecture.corridorClearanceMeters + 1e-9 < rules.corridorWidthMeters) errors.push({ code: 'CORRIDOR_CLEARANCE_FAILED', semanticIds: architecture.architecture.filter(({ id }) => id.includes('corridor-')).map(({ id }) => id), message: 'Главный corridor не сохраняет profile-defined capsule clearance.' })
  if (architecture.doorwayCount < 2 || architecture.invalidDoorways.length > 0) errors.push({ code: 'DOORWAY_CLEARANCE_FAILED', semanticIds: architecture.invalidDoorways, message: 'Room doorway не сохраняет profile-defined capsule clearance.' })
  const narrowGapSurfaces = subCapsuleGapSurfaceIds(arena, radius * 2)
  if (narrowGapSurfaces.length > 0) errors.push({ code: 'SUB_CAPSULE_GAP', semanticIds: narrowGapSurfaces, message: 'Визуально открытый промежуток уже полного clearance participant capsule.' })
  let maximumRouteDistanceMeters = 0
  if (arena.recipeId) {
    const nav = arenaNavigation(arena, profile as ValidatedGameDesignProfile)
    const failed: string[] = []
    for (const spawn of arena.spawnAnchors) {
      const center = arena.regions.find(r => r.id === 'combat-center')!.center
      const route = nav.route(spawn.position,center)
      if (!route.length || arenaFloorHeight(arena,spawn.position.x,spawn.position.z) === undefined) failed.push(spawn.id)
      let previous = spawn.position, total = 0
      for(const point of route) { total += Math.hypot(point.x-previous.x,point.y-previous.y,point.z-previous.z); previous=point }
      maximumRouteDistanceMeters = Math.max(maximumRouteDistanceMeters,total)
    }
    if(failed.length) errors.push({code:'INVALID_ELEVATION_ROUTE',semanticIds:failed,message:'Spawn не имеет непрерывного поддерживаемого маршрута к combat center.'})
  }
  const fairness = fairnessMetrics(arena,profile)
  if (fairness.maximumTeamDistanceRatio > rules.maximumTeamDistanceRatio || fairness.initialEnemyLosRatio > rules.maximumInitialEnemyLosRatio) errors.push({ code: 'MODE_FAIRNESS_FAILED', semanticIds: arena.spawnRegions.map(({ id }) => id), message: 'Navigation-distance либо enemy-LOS metric вышла за предел profile.' })
  return deepFreeze({
    version: ARENA_GAMEPLAY_VALIDATION_VERSION,
    arenaContentHash: arena.contentHash,
    profileContentHash: profile.contentHash,
    supportedModes: ['ffa', 'teams'] as const,
    accepted: errors.length === 0,
    metrics: {
      recipeId: arena.recipeId ?? arena.generatorVersion,
      rampCount:arena.surfaces.filter(s=>s.semantics?.classification === 'ramp').length,
      maximumRouteDistanceMeters,
      regionCount: arena.regions.length,
      linkCount: arena.links.length,
      freeSpawnSlotCount: free.length,
      minimumSpawnDistanceMeters: minimumDistance(free),
      routeRedundancyRatio: arena.links.length === 0 ? 0 : resilientLinks / arena.links.length,
      maximumTeamDistanceRatio: fairness.maximumTeamDistanceRatio,
      initialEnemyLosRatio: fairness.initialEnemyLosRatio,
      perimeterSegmentCount: architecture.perimeter.length,
      architecturalSurfaceCount: architecture.architecture.length,
      doorwayCount: architecture.doorwayCount,
      corridorClearanceMeters: architecture.corridorClearanceMeters,
    },
    errors,
  })
}
