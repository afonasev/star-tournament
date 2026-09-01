export {
  ARENA_DEFINITION_SCHEMA_VERSION,
  ARENA_QUATERNION_NORMALIZATION_EPSILON,
  ARENA_SIZE_IDS,
  arenaContentPayload,
  canonicalArenaJson,
  computeArenaContentHash,
  createArenaDefinition,
  normalizeArenaPayload,
  parseArenaDefinition,
  validateArenaDefinition,
} from './arenaDefinition'

export type {
  ArenaBoxSurface,
  ArenaDefinition,
  ArenaDefinitionPayload,
  ArenaGeneratorProfileIdentity,
  ArenaLink,
  ArenaLinkExposure,
  ArenaPrimitiveSurface,
  ArenaQuaternion,
  ArenaRegion,
  ArenaRegionRole,
  ArenaSize,
  ArenaSpawnAnchor,
  ArenaSpawnRegion,
  ArenaSurfaceMaterial,
  ArenaValidationError,
  ArenaValidationErrorCode,
  ArenaValidationResult,
  ArenaVector3,
} from './arenaDefinition'

export { DIAGNOSTIC_ARENA } from './diagnosticArena'
export {
  ARENA_GAMEPLAY_VALIDATION_VERSION,
  validateArenaGameplay,
  type ArenaGameplayValidationError,
  type ArenaGameplayValidationMetrics,
  type ArenaGameplayValidationReport,
} from './arenaGameplayValidation'
export {
  PROCEDURAL_ARENA_GENERATION_ATTEMPTS,
  PROCEDURAL_ARENA_GENERATOR_VERSION,
  PROCEDURAL_FALLBACK_ARENA,
  generateProceduralArena,
  requireProceduralArena,
  type ProceduralArenaGenerationOptions,
  type ProceduralArenaGenerationResult,
} from './proceduralArena'
export {
  createArenaCollisionProjection,
  createArenaNavigationProjection,
  createArenaRenderProjection,
  createArenaSpawnProjection,
} from './projections'
export {
  allocateArenaSpawns,
  type OccupiedSpawnPosition,
  type SpawnAllocation,
  type SpawnAllocationOptions,
  type SpawnAllocationParticipant,
} from './spawnAllocation'
