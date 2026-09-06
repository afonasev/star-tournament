import type { ArenaGeneratorPresetRules } from '../profiles'
import type { ArenaSize } from './arenaDefinition'

export const ARENA_RECIPE_IDS = ['wide-bypass-hall', 'bypass-gallery', 'fire-balconies', 'dual-combat-decks', 'basement-service-loop'] as const
export type ArenaRecipeId = typeof ARENA_RECIPE_IDS[number]
export interface ArenaRecipeGraph {
  id: ArenaRecipeId
  size: ArenaSize
  corridorWidth: number
  roomDepth: number
  northDoors: number[]
  southDoors: number[]
  verticalRole: 'wide' | 'gallery' | 'balcony' | 'dual-deck' | 'basement'
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
  const courtyard = id === 'fire-balconies'
  return {
    id, size,
    corridorWidth: id === 'wide-bypass-hall' ? rules.corridorWidthMeters * 1.5 : rules.corridorWidthMeters,
    roomDepth: rules.routeInsetMeters,
    northDoors: courtyard ? [-halfLength / 2, halfLength / 2] : [id === 'basement-service-loop' ? -halfLength / 2 : -mirror * halfLength * 0.24],
    southDoors: courtyard ? [-halfLength / 2, halfLength / 2] : [id === 'basement-service-loop' ? -halfLength / 2 : mirror * halfLength * 0.24],
    verticalRole: id === 'wide-bypass-hall' ? 'wide' : id === 'bypass-gallery' ? 'gallery' : id === 'fire-balconies' ? 'balcony' : id === 'dual-combat-decks' ? 'dual-deck' : 'basement',
  }
}
