export {
  GAME_DESIGN_PROFILE_DESCRIPTORS,
  GAME_DESIGN_PROFILE_SCHEMA_VERSION,
  PROTOTYPE_V1_PROFILE,
  canonicalProfileJson,
  computeProfileContentHash,
  profileContentPayload,
  validateGameDesignProfile,
} from './gameDesignProfile'

export {
  GAME_DESIGN_LAB_STORAGE_KEY,
  allProfileRevisions,
  createLocalProfile,
  loadLocalProfileCatalog,
  loadReleaseManifest,
  persistLocalProfileCatalog,
  profileRevisionRef,
  RELEASE_MANIFEST,
  resolveSelectedProfile,
  sameProfileRevision,
  saveLocalRevision,
  selectLocalRevision,
  validateLocalProfileCatalog,
} from './localRevisions'

export type { LocalProfileCatalog, ProfileRevisionRef, ReleaseManifest } from './localRevisions'

export type {
  ArenaGeneratorPresetRules,
  ArenaGeneratorSize,
  GameDesignProfile,
  GameDesignProfilePayload,
  GameDesignProfileSource,
  GameDesignRules,
  NumericProfileDescriptor,
  ProfileDescriptorGroup,
  ProfileDescriptorUnit,
  ProfileValidationError,
  ProfileValidationErrorCode,
  ProfileValidationResult,
  ShippedGameDesignProfile,
  ValidatedGameDesignProfile,
} from './gameDesignProfile'
