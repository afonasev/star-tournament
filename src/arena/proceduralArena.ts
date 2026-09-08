import { compileRoomArena, ROOM_GENERATOR_VERSION } from './roomArena'
import { generateProceduralArena as generateLegacyArena } from './legacyProceduralArenaV1'
import { generateProceduralArena as generateV2Arena } from './legacyProceduralArenaV2'
import { PROTOTYPE_V1_PROFILE, type GameDesignProfile, type ValidatedGameDesignProfile } from '../profiles'
import { createArenaDefinition, type ArenaDefinition, type ArenaSize } from './arenaDefinition'
import { validateArenaGameplay, type ArenaGameplayValidationReport } from './arenaGameplayValidation'

export const PROCEDURAL_ARENA_GENERATOR_VERSION = ROOM_GENERATOR_VERSION
// This constructive compiler has no random retry step: identical input would
// repeat identical geometry and validation. A rejected seed fails explicitly.
export const PROCEDURAL_ARENA_GENERATION_ATTEMPTS = 1

export interface ProceduralArenaGenerationOptions {
  readonly size: ArenaSize
  readonly seed: number
  readonly profile?: GameDesignProfile | ValidatedGameDesignProfile
}

export type ProceduralArenaGenerationResult =
  | { readonly ok: true; readonly arena: ArenaDefinition; readonly report: ArenaGameplayValidationReport; readonly attempt: number }
  | { readonly ok: false; readonly seed: number; readonly size: ArenaSize; readonly attempts: number; readonly reports: readonly ArenaGameplayValidationReport[] }

export function generateProceduralArena(input: ProceduralArenaGenerationOptions): ProceduralArenaGenerationResult {
  if(input.profile&&Number(input.profile.schemaVersion)<8)return generateLegacyArena(input)
  if(input.profile&&Number(input.profile.schemaVersion)===8)return generateV2Arena(input)
  if (!Number.isSafeInteger(input.seed) || input.seed < 0 || input.seed > 0xffffffff) throw new RangeError('Procedural arena seed must be an unsigned 32-bit integer')
  const options = { ...input, seed: input.seed >>> 0, profile: input.profile ?? PROTOTYPE_V1_PROFILE }
  const reports: ArenaGameplayValidationReport[] = []
  for (let attempt = 0; attempt < PROCEDURAL_ARENA_GENERATION_ATTEMPTS; attempt += 1) {
    const arena = compileRoomArena(options.size,options.seed,options.profile)
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
  const errors = report?.errors.map(({ code, semanticIds }) => `${code}[${semanticIds.join(',')}]`).join(', ') ?? 'unknown validation error'
  throw new Error(`Не удалось построить арену ${input.size} для seed ${input.seed}: ${errors}; metrics ${JSON.stringify(report?.metrics)}`)
}

const fallbackSource = requireProceduralArena({ size: 'medium', seed: 0x53544152 })
const { contentHash: _fallbackSourceHash, ...fallbackPayload } = fallbackSource
export const PROCEDURAL_FALLBACK_ARENA = createArenaDefinition({
  ...fallbackPayload,
  id: 'shipped-fallback-arena-v4',
  generatorVersion: ROOM_GENERATOR_VERSION,
})
