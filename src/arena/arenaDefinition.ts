import { canonicalSerialize, contentHash, isContentHash, type ContentHash } from '../core/canonical'
import { deepFreeze } from '../core/deepFreeze'

export const ARENA_DEFINITION_SCHEMA_VERSION = 5 as const
export const ARENA_QUATERNION_NORMALIZATION_EPSILON = 1e-6
export const ARENA_SIZE_IDS = ['small', 'medium', 'large'] as const

export type ArenaSize = (typeof ARENA_SIZE_IDS)[number]

export interface ArenaVector3 { x: number; y: number; z: number }
export interface ArenaQuaternion { x: number; y: number; z: number; w: number }

export interface ArenaGeneratorProfileIdentity {
  id: string
  revision: number
  contentHash: ContentHash
}

export type ArenaSurfaceMaterial = 'floor' | 'wall' | 'accent'
export type ArenaRegionRole = 'combat' | 'route' | 'spawn'
export type ArenaLinkExposure = 'open' | 'covered'

export type ArenaSurfaceClass = 'wall' | 'window' | 'movement-barrier' | 'floor' | 'ramp' | 'slab'
export interface ArenaSurfaceSemantics {
  classification: ArenaSurfaceClass
  movement: boolean
  projectile: boolean
  sector: string
}
export interface ArenaAttachment {
  id: string
  surfaceId: string
  kind: 'relief-niche' | 'lamp' | 'arch'
}
export interface ArenaBoxSurface {
  semantics?: ArenaSurfaceSemantics
  id: string
  kind: 'box'
  center: ArenaVector3
  size: ArenaVector3
  orientation: ArenaQuaternion
  material: ArenaSurfaceMaterial
}

export type ArenaPrimitiveSurface = ArenaBoxSurface

export interface ArenaRegion {
  id: string
  role: ArenaRegionRole
  center: ArenaVector3
  size: ArenaVector3
  supportSurfaceId?: string
}

export interface ArenaLink {
  id: string
  fromRegionId: string
  toRegionId: string
  exposure: ArenaLinkExposure
  transitionSurfaceId?: string
}

export interface ArenaSpawnRegion {
  id: string
  regionId: string
  anchorIds: string[]
}

export interface ArenaSpawnAnchor {
  id: string
  spawnRegionId: string
  position: ArenaVector3
  yawRadians: number
  supportSurfaceId?: string
}

export interface ArenaDefinitionPayload {
  schemaVersion: 3 | 4 | typeof ARENA_DEFINITION_SCHEMA_VERSION
  id: string
  seed: number
  size: ArenaSize
  generatorVersion: string
  generatorProfile: ArenaGeneratorProfileIdentity
  recipeId?: string
  attachments?: ArenaAttachment[]
  regions: ArenaRegion[]
  links: ArenaLink[]
  surfaces: ArenaPrimitiveSurface[]
  spawnRegions: ArenaSpawnRegion[]
  spawnAnchors: ArenaSpawnAnchor[]
}

export interface ArenaDefinition extends ArenaDefinitionPayload { contentHash: ContentHash }

export type ArenaValidationErrorCode =
  | 'INVALID_TYPE'
  | 'MISSING_FIELD'
  | 'UNSUPPORTED_SCHEMA_VERSION'
  | 'INVALID_NUMBER'
  | 'INVALID_SURFACE_KIND'
  | 'INVALID_SURFACE_MASK'
  | 'INVALID_SURFACE_MATERIAL'
  | 'INVALID_SURFACE_SIZE'
  | 'INVALID_ORIENTATION'
  | 'INVALID_REGION_ROLE'
  | 'INVALID_LINK_EXPOSURE'
  | 'DUPLICATE_ID'
  | 'EMPTY_SURFACES'
  | 'EMPTY_SPAWN_ANCHORS'
  | 'INVALID_REFERENCE'
  | 'NON_CANONICAL_ORDER'
  | 'CONTENT_HASH_MISMATCH'
  | 'INVALID_JSON'
  | 'UNRECOGNIZED_FIELD'

export interface ArenaValidationError { code: ArenaValidationErrorCode; paths: readonly string[]; message: string }
export type ArenaValidationResult = { ok: true; arena: ArenaDefinition } | { ok: false; errors: readonly ArenaValidationError[] }

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function allowed(value: Record<string, unknown>, keys: readonly string[], path: string, errors: ArenaValidationError[]): void {
  const set = new Set(keys)
  for (const key of Object.keys(value).sort()) {
    if (!set.has(key)) errors.push({ code: 'UNRECOGNIZED_FIELD', paths: [path === '$' ? key : `${path}.${key}`], message: `${key} не входит в schema ArenaDefinition.` })
  }
}

function requiredString(value: Record<string, unknown>, key: string, path: string, errors: ArenaValidationError[]): string | undefined {
  const field = value[key]
  if (field === undefined) errors.push({ code: 'MISSING_FIELD', paths: [path], message: `Отсутствует ${path}.` })
  else if (typeof field !== 'string' || field.length === 0) errors.push({ code: 'INVALID_TYPE', paths: [path], message: `${path} должно быть непустой строкой.` })
  else return field
  return undefined
}

function finite(value: unknown, path: string, errors: ArenaValidationError[], positive = false): number | undefined {
  if (typeof value !== 'number' || !Number.isFinite(value)) {
    errors.push({ code: 'INVALID_NUMBER', paths: [path], message: `${path} должен быть конечным числом.` })
    return undefined
  }
  if (positive && value <= 0) {
    errors.push({ code: 'INVALID_SURFACE_SIZE', paths: [path], message: `${path} должен быть больше нуля.` })
    return undefined
  }
  return value
}

function vector(value: unknown, path: string, errors: ArenaValidationError[], positive = false): ArenaVector3 | undefined {
  if (value === undefined) {
    errors.push({ code: 'MISSING_FIELD', paths: [path], message: `Отсутствует ${path}.` })
    return undefined
  }
  if (!isRecord(value)) {
    errors.push({ code: 'INVALID_TYPE', paths: [path], message: `${path} должен быть Vector3.` })
    return undefined
  }
  allowed(value, ['x', 'y', 'z'], path, errors)
  const x = finite(value.x, `${path}.x`, errors, positive)
  const y = finite(value.y, `${path}.y`, errors, positive)
  const z = finite(value.z, `${path}.z`, errors, positive)
  return x === undefined || y === undefined || z === undefined ? undefined : { x, y, z }
}

function quaternion(value: unknown, path: string, errors: ArenaValidationError[]): ArenaQuaternion | undefined {
  if (value === undefined) {
    errors.push({ code: 'MISSING_FIELD', paths: [path], message: `Отсутствует ${path}.` })
    return undefined
  }
  if (!isRecord(value)) {
    errors.push({ code: 'INVALID_TYPE', paths: [path], message: `${path} должен быть Quaternion.` })
    return undefined
  }
  allowed(value, ['x', 'y', 'z', 'w'], path, errors)
  const x = finite(value.x, `${path}.x`, errors)
  const y = finite(value.y, `${path}.y`, errors)
  const z = finite(value.z, `${path}.z`, errors)
  const w = finite(value.w, `${path}.w`, errors)
  if (x === undefined || y === undefined || z === undefined || w === undefined) return undefined
  if (Math.abs(x * x + y * y + z * z + w * w - 1) > ARENA_QUATERNION_NORMALIZATION_EPSILON) {
    errors.push({ code: 'INVALID_ORIENTATION', paths: [path], message: `${path} должен быть нормализованным Quaternion.` })
    return undefined
  }
  return { x, y, z, w }
}

function uniqueIds(values: readonly unknown[], path: string, errors: ArenaValidationError[]): void {
  const seen = new Map<string, number>()
  values.forEach((value, index) => {
    if (!isRecord(value) || typeof value.id !== 'string') return
    const previous = seen.get(value.id)
    if (previous === undefined) seen.set(value.id, index)
    else errors.push({ code: 'DUPLICATE_ID', paths: [`${path}.${previous}.id`, `${path}.${index}.id`], message: `Идентификатор ${value.id} повторяется.` })
  })
}

function canonicalIds(values: readonly unknown[], path: string, errors: ArenaValidationError[]): void {
  const ids = values.map((value) => isRecord(value) && typeof value.id === 'string' ? value.id : '')
  const sorted = [...ids].sort((a, b) => a.localeCompare(b, 'en'))
  if (ids.some((id, index) => id !== sorted[index])) errors.push({ code: 'NON_CANONICAL_ORDER', paths: [path], message: `${path} должен быть отсортирован по semantic id.` })
}

function normalizeById<T extends { id: string }>(values: readonly T[]): T[] {
  return [...values].sort((a, b) => a.id.localeCompare(b.id, 'en'))
}

export function normalizeArenaPayload(payload: ArenaDefinitionPayload): ArenaDefinitionPayload {
  return {
    ...payload,
    generatorProfile: { ...payload.generatorProfile },
    ...(payload.attachments ? { attachments: normalizeById(payload.attachments).map(entry => ({ ...entry })) } : {}),
    regions: normalizeById(payload.regions).map((entry) => ({ ...entry, center: { ...entry.center }, size: { ...entry.size } })),
    links: normalizeById(payload.links).map((entry) => ({ ...entry })),
    surfaces: normalizeById(payload.surfaces).map((entry) => ({ ...entry, center: { ...entry.center }, size: { ...entry.size }, orientation: { ...entry.orientation } })),
    spawnRegions: normalizeById(payload.spawnRegions).map((entry) => ({ ...entry, anchorIds: [...entry.anchorIds].sort((a, b) => a.localeCompare(b, 'en')) })),
    spawnAnchors: normalizeById(payload.spawnAnchors).map((entry) => ({ ...entry, position: { ...entry.position } })),
  }
}

export function arenaContentPayload(arena: ArenaDefinition): ArenaDefinitionPayload {
  const { contentHash: _contentHash, ...payload } = arena
  return normalizeArenaPayload(payload)
}

export function computeArenaContentHash(arena: ArenaDefinition): ContentHash {
  return contentHash(arenaContentPayload(arena))
}

export function canonicalArenaJson(arena: ArenaDefinition): string {
  return canonicalSerialize({ ...arenaContentPayload(arena), contentHash: computeArenaContentHash(arena) })
}

export function createArenaDefinition(payload: ArenaDefinitionPayload): ArenaDefinition {
  const normalized = normalizeArenaPayload(payload)
  const candidate = { ...normalized, contentHash: contentHash(normalized) } as ArenaDefinition
  const result = validateArenaDefinition(candidate)
  if (!result.ok) throw new TypeError(result.errors.map((error) => `${error.code}:${error.paths.join(',')}`).join(' '))
  return result.arena
}

export function validateArenaDefinition(value: unknown): ArenaValidationResult {
  const errors: ArenaValidationError[] = []
  if (!isRecord(value)) return { ok: false, errors: [{ code: 'INVALID_TYPE', paths: ['$'], message: 'ArenaDefinition должен быть объектом.' }] }
  allowed(value, ['schemaVersion', 'id', 'seed', 'size', 'generatorVersion', 'generatorProfile', 'regions', 'links', 'surfaces', 'spawnRegions', 'spawnAnchors', 'contentHash', 'recipeId', 'attachments'], '$', errors)
  requiredString(value, 'id', 'id', errors)
  requiredString(value, 'generatorVersion', 'generatorVersion', errors)
  requiredString(value, 'contentHash', 'contentHash', errors)
  if (value.schemaVersion !== 3 && value.schemaVersion !== 4 && value.schemaVersion !== ARENA_DEFINITION_SCHEMA_VERSION) errors.push({ code: value.schemaVersion === undefined ? 'MISSING_FIELD' : 'UNSUPPORTED_SCHEMA_VERSION', paths: ['schemaVersion'], message: `Поддерживается schemaVersion ${ARENA_DEFINITION_SCHEMA_VERSION}.` })
  if (!Number.isSafeInteger(value.seed) || (value.seed as number) < 0 || (value.seed as number) > 0xffffffff) errors.push({ code: 'INVALID_NUMBER', paths: ['seed'], message: 'seed должен быть unsigned 32-bit integer.' })
  if (!ARENA_SIZE_IDS.includes(value.size as ArenaSize)) errors.push({ code: 'INVALID_TYPE', paths: ['size'], message: 'size должен быть small, medium либо large.' })

  if (!isRecord(value.generatorProfile)) errors.push({ code: 'INVALID_TYPE', paths: ['generatorProfile'], message: 'generatorProfile должен быть identity object.' })
  else {
    allowed(value.generatorProfile, ['id', 'revision', 'contentHash'], 'generatorProfile', errors)
    requiredString(value.generatorProfile, 'id', 'generatorProfile.id', errors)
    if (!Number.isSafeInteger(value.generatorProfile.revision) || (value.generatorProfile.revision as number) < 1) errors.push({ code: 'INVALID_NUMBER', paths: ['generatorProfile.revision'], message: 'revision должна быть positive integer.' })
    if (!isContentHash(value.generatorProfile.contentHash)) errors.push({ code: 'INVALID_TYPE', paths: ['generatorProfile.contentHash'], message: 'generatorProfile.contentHash должен быть content hash.' })
  }

  const collectionNames = ['regions', 'links', 'surfaces', 'spawnRegions', 'spawnAnchors'] as const
  for (const name of collectionNames) {
    const collection = value[name]
    if (!Array.isArray(collection)) errors.push({ code: 'INVALID_TYPE', paths: [name], message: `${name} должен быть массивом.` })
    else { uniqueIds(collection, name, errors); canonicalIds(collection, name, errors) }
  }
  if (Array.isArray(value.surfaces) && value.surfaces.length === 0) errors.push({ code: 'EMPTY_SURFACES', paths: ['surfaces'], message: 'Арена должна содержать surfaces.' })
  if (Array.isArray(value.spawnAnchors) && value.spawnAnchors.length === 0) errors.push({ code: 'EMPTY_SPAWN_ANCHORS', paths: ['spawnAnchors'], message: 'Арена должна содержать spawn anchors.' })

  if (Array.isArray(value.regions)) value.regions.forEach((entry, index) => {
    const path = `regions.${index}`
    if (!isRecord(entry)) { errors.push({ code: 'INVALID_TYPE', paths: [path], message: `${path} должен быть object.` }); return }
    allowed(entry, ['id', 'role', 'center', 'size', 'supportSurfaceId'], path, errors); requiredString(entry, 'id', `${path}.id`, errors)
    if (entry.role !== 'combat' && entry.role !== 'route' && entry.role !== 'spawn') errors.push({ code: 'INVALID_REGION_ROLE', paths: [`${path}.role`], message: 'Недопустимая role.' })
    vector(entry.center, `${path}.center`, errors); vector(entry.size, `${path}.size`, errors, true)
    if (value.schemaVersion === ARENA_DEFINITION_SCHEMA_VERSION) requiredString(entry, 'supportSurfaceId', `${path}.supportSurfaceId`, errors)
  })
  if (Array.isArray(value.links)) value.links.forEach((entry, index) => {
    const path = `links.${index}`
    if (!isRecord(entry)) { errors.push({ code: 'INVALID_TYPE', paths: [path], message: `${path} должен быть object.` }); return }
    allowed(entry, ['id', 'fromRegionId', 'toRegionId', 'exposure', 'transitionSurfaceId'], path, errors); requiredString(entry, 'id', `${path}.id`, errors); requiredString(entry, 'fromRegionId', `${path}.fromRegionId`, errors); requiredString(entry, 'toRegionId', `${path}.toRegionId`, errors)
    if (entry.exposure !== 'open' && entry.exposure !== 'covered') errors.push({ code: 'INVALID_LINK_EXPOSURE', paths: [`${path}.exposure`], message: 'Недопустимая exposure.' })
  })
  if (Array.isArray(value.surfaces)) value.surfaces.forEach((entry, index) => {
    const path = `surfaces.${index}`
    if (!isRecord(entry)) { errors.push({ code: 'INVALID_TYPE', paths: [path], message: `${path} должен быть object.` }); return }
    allowed(entry, ['id', 'kind', 'center', 'size', 'orientation', 'material', 'semantics'], path, errors); requiredString(entry, 'id', `${path}.id`, errors)
    if (entry.kind !== 'box') errors.push({ code: 'INVALID_SURFACE_KIND', paths: [`${path}.kind`], message: 'Поддерживается только box.' })
    if (entry.material !== 'floor' && entry.material !== 'wall' && entry.material !== 'accent') errors.push({ code: 'INVALID_SURFACE_MATERIAL', paths: [`${path}.material`], message: 'Недопустимый material.' })
    vector(entry.center, `${path}.center`, errors); vector(entry.size, `${path}.size`, errors, true); quaternion(entry.orientation, `${path}.orientation`, errors)
    if (entry.semantics !== undefined) {
      const m = entry.semantics
      const classes = ['wall', 'window', 'movement-barrier', 'floor', 'ramp', 'slab']
      if (!isRecord(m) || !classes.includes(m.classification as string) || m.movement !== true || m.projectile !== (m.classification !== 'movement-barrier') || typeof m.sector !== 'string' || !m.sector) {
        errors.push({ code: 'INVALID_SURFACE_MASK', paths: [`${path}.semantics`], message: 'Недопустимая classification/query mask.' })
      } else allowed(m, ['classification', 'movement', 'projectile', 'sector'], `${path}.semantics`, errors)
    }
    if (value.recipeId !== undefined && entry.semantics === undefined) errors.push({ code: 'INVALID_SURFACE_MASK', paths: [path], message: 'Recipe surface требует explicit semantics.' })
  })
  if (Array.isArray(value.spawnRegions)) value.spawnRegions.forEach((entry, index) => {
    const path = `spawnRegions.${index}`
    if (!isRecord(entry)) { errors.push({ code: 'INVALID_TYPE', paths: [path], message: `${path} должен быть object.` }); return }
    allowed(entry, ['id', 'regionId', 'anchorIds'], path, errors); requiredString(entry, 'id', `${path}.id`, errors); requiredString(entry, 'regionId', `${path}.regionId`, errors)
    if (!Array.isArray(entry.anchorIds) || entry.anchorIds.some((id) => typeof id !== 'string' || id.length === 0)) errors.push({ code: 'INVALID_TYPE', paths: [`${path}.anchorIds`], message: 'anchorIds должен быть string array.' })
    else {
      const sorted = [...entry.anchorIds].sort((a, b) => a.localeCompare(b, 'en'))
      if (entry.anchorIds.some((id, anchorIndex) => id !== sorted[anchorIndex])) errors.push({ code: 'NON_CANONICAL_ORDER', paths: [`${path}.anchorIds`], message: 'anchorIds должны быть sorted.' })
      if (new Set(entry.anchorIds).size !== entry.anchorIds.length) errors.push({ code: 'DUPLICATE_ID', paths: [`${path}.anchorIds`], message: 'anchorIds должны быть unique.' })
    }
  })
  if (Array.isArray(value.spawnAnchors)) value.spawnAnchors.forEach((entry, index) => {
    const path = `spawnAnchors.${index}`
    if (!isRecord(entry)) { errors.push({ code: 'INVALID_TYPE', paths: [path], message: `${path} должен быть object.` }); return }
    allowed(entry, ['id', 'spawnRegionId', 'position', 'yawRadians', 'supportSurfaceId'], path, errors); requiredString(entry, 'id', `${path}.id`, errors); requiredString(entry, 'spawnRegionId', `${path}.spawnRegionId`, errors); vector(entry.position, `${path}.position`, errors); finite(entry.yawRadians, `${path}.yawRadians`, errors)
    if (value.schemaVersion === ARENA_DEFINITION_SCHEMA_VERSION) requiredString(entry, 'supportSurfaceId', `${path}.supportSurfaceId`, errors)
  })

  if (value.recipeId !== undefined && (typeof value.recipeId !== 'string' || !value.recipeId || (value.schemaVersion !== 4 && value.schemaVersion !== 5))) errors.push({ code: 'INVALID_TYPE', paths: ['recipeId'], message: 'Recipe требует schema v4 или v5.' })
  if (value.attachments !== undefined) {
    if (!Array.isArray(value.attachments)) errors.push({ code: 'INVALID_TYPE', paths: ['attachments'], message: 'Attachments должны быть массивом.' })
    else {
      uniqueIds(value.attachments, 'attachments', errors); canonicalIds(value.attachments, 'attachments', errors)
      value.attachments.forEach((entry, index) => {
        const path = `attachments.${index}`
        if (!isRecord(entry)) { errors.push({ code: 'INVALID_TYPE', paths: [path], message: 'Invalid attachment.' }); return }
        allowed(entry, ['id', 'surfaceId', 'kind'], path, errors)
        requiredString(entry, 'id', `${path}.id`, errors)
        const surface = Array.isArray(value.surfaces) ? value.surfaces.find(s => isRecord(s) && s.id === entry.surfaceId) : undefined
        if (!surface || !['relief-niche', 'lamp', 'arch'].includes(entry.kind as string) || (entry.kind === 'relief-niche' && surface.semantics?.classification !== 'wall')) errors.push({ code: 'INVALID_REFERENCE', paths: [path], message: 'Attachment требует подходящую canonical surface.' })
      })
    }
  }

  if (errors.length === 0) {
    const regions = new Set((value.regions as unknown as ArenaRegion[]).map(({ id }) => id))
    const supports = new Set((value.surfaces as unknown as ArenaBoxSurface[]).filter(supportsNavigation).map(({ id }) => id))
    const anchors = new Map((value.spawnAnchors as unknown as ArenaSpawnAnchor[]).map((entry) => [entry.id, entry]))
    const spawnRegions = new Map((value.spawnRegions as unknown as ArenaSpawnRegion[]).map((entry) => [entry.id, entry]))
    ;(value.links as unknown as ArenaLink[]).forEach((link, index) => {
      if (!regions.has(link.fromRegionId) || !regions.has(link.toRegionId) || link.fromRegionId === link.toRegionId) errors.push({ code: 'INVALID_REFERENCE', paths: [`links.${index}`], message: 'Link references invalid regions.' })
      const from = (value.regions as unknown as ArenaRegion[]).find(region => region.id === link.fromRegionId)
      const to = (value.regions as unknown as ArenaRegion[]).find(region => region.id === link.toRegionId)
      if (value.schemaVersion === ARENA_DEFINITION_SCHEMA_VERSION && from && to && from.supportSurfaceId !== to.supportSurfaceId && (!link.transitionSurfaceId || !supports.has(link.transitionSurfaceId))) errors.push({ code: 'INVALID_REFERENCE', paths: [`links.${index}.transitionSurfaceId`], message: 'Межслойная link требует navigation transition.' })
    })
    ;(value.spawnRegions as unknown as ArenaSpawnRegion[]).forEach((region, index) => {
      if (!regions.has(region.regionId) || region.anchorIds.some((id) => anchors.get(id)?.spawnRegionId !== region.id)) errors.push({ code: 'INVALID_REFERENCE', paths: [`spawnRegions.${index}`], message: 'Spawn region references invalid region/anchors.' })
    })
    ;(value.spawnAnchors as unknown as ArenaSpawnAnchor[]).forEach((anchor, index) => {
      if (!spawnRegions.has(anchor.spawnRegionId)) errors.push({ code: 'INVALID_REFERENCE', paths: [`spawnAnchors.${index}.spawnRegionId`], message: 'Unknown spawn region.' })
      if (value.schemaVersion === ARENA_DEFINITION_SCHEMA_VERSION && !supports.has(anchor.supportSurfaceId ?? '')) errors.push({ code: 'INVALID_REFERENCE', paths: [`spawnAnchors.${index}.supportSurfaceId`], message: 'Anchor требует navigation support.' })
    })
    if (value.schemaVersion === ARENA_DEFINITION_SCHEMA_VERSION) (value.regions as unknown as ArenaRegion[]).forEach((region, index) => {
      if (!supports.has(region.supportSurfaceId ?? '')) errors.push({ code: 'INVALID_REFERENCE', paths: [`regions.${index}.supportSurfaceId`], message: 'Region требует navigation support.' })
    })
  }

  if (errors.length === 0 && (!isContentHash(value.contentHash) || computeArenaContentHash(value as unknown as ArenaDefinition) !== value.contentHash)) errors.push({ code: 'CONTENT_HASH_MISMATCH', paths: ['contentHash'], message: 'contentHash не соответствует ArenaDefinition.' })
  if (errors.length > 0) return { ok: false, errors: deepFreeze(errors) }
  return { ok: true, arena: deepFreeze(JSON.parse(canonicalSerialize(value)) as ArenaDefinition) }
}

export function parseArenaDefinition(serialized: string): ArenaValidationResult {
  try { return validateArenaDefinition(JSON.parse(serialized) as unknown) }
  catch { return { ok: false, errors: [{ code: 'INVALID_JSON', paths: ['$'], message: 'ArenaDefinition содержит некорректный JSON.' }] } }
}

/** Historical v3 boxes retain their original all-query behavior without rewriting bytes. */
export function blocksProjectiles(surface: ArenaBoxSurface): boolean { return surface.semantics?.projectile ?? true }
export function blocksMovement(surface: ArenaBoxSurface): boolean { return surface.semantics?.movement ?? true }
export function supportsNavigation(surface: ArenaBoxSurface): boolean { return surface.semantics ? surface.semantics.classification === 'floor' || surface.semantics.classification === 'ramp' : surface.material === 'floor' }
