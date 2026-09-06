import type { ArenaGeneratorPresetRules } from '../profiles'
import type { ArenaSize } from './arenaDefinition'

export const ARENA_RECIPE_IDS = ['grand-hall', 'split-atrium', 'service-loop', 'flanked-courtyard'] as const
export type ArenaRecipeId = typeof ARENA_RECIPE_IDS[number]
export interface ArenaRecipeGraph {
  id: ArenaRecipeId
  size: ArenaSize
  corridorWidth: number
  roomDepth: number
  northDoors: number[]
  southDoors: number[]
  raised: boolean
}

/** Recipe composition ratios are topology invariants, not free per-surface noise. */
export function resolveArenaRecipe(size: ArenaSize, seed: number, rules: ArenaGeneratorPresetRules): ArenaRecipeGraph {
  // Schema v6 release snapshots predate recipeCount.  They remain immutable
  // and use the original single topology, while v7 profiles opt into the
  // versioned recipe matrix explicitly.
  const recipeCount = Number.isSafeInteger(rules.recipeCount) && rules.recipeCount > 0
    ? Math.min(rules.recipeCount, ARENA_RECIPE_IDS.length)
    : 1
  const id = ARENA_RECIPE_IDS[seed % recipeCount]!
  const halfLength = rules.widthMeters / 2 - rules.perimeterInsetMeters - rules.routeInsetMeters
  const mirror = seed & 4 ? -1 : 1
  const courtyard = id === 'flanked-courtyard'
  return {
    id, size,
    corridorWidth: id === 'grand-hall' ? rules.corridorWidthMeters * 1.5 : rules.corridorWidthMeters,
    roomDepth: rules.routeInsetMeters,
    northDoors: courtyard ? [-halfLength / 2, halfLength / 2] : [id === 'service-loop' ? -halfLength / 2 : -mirror * halfLength * 0.24],
    southDoors: courtyard ? [-halfLength / 2, halfLength / 2] : [id === 'service-loop' ? -halfLength / 2 : mirror * halfLength * 0.24],
    raised: id === 'split-atrium',
  }
}
