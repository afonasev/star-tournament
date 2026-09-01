import { deepFreeze } from '../core/deepFreeze'

import { type ArenaDefinition } from './arenaDefinition'

export function createArenaCollisionProjection(arena: ArenaDefinition) {
  return deepFreeze({ arenaContentHash: arena.contentHash, surfaces: arena.surfaces })
}

export function createArenaNavigationProjection(arena: ArenaDefinition) {
  return deepFreeze({ arenaContentHash: arena.contentHash, regions: arena.regions, links: arena.links })
}

export function createArenaSpawnProjection(arena: ArenaDefinition) {
  return deepFreeze({ arenaContentHash: arena.contentHash, regions: arena.spawnRegions, anchors: arena.spawnAnchors })
}

export function createArenaRenderProjection(arena: ArenaDefinition) {
  return deepFreeze({ arenaContentHash: arena.contentHash, surfaces: arena.surfaces })
}
