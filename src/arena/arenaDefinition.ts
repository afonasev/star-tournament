import { canonicalSerialize, contentHash, isContentHash, type ContentHash } from '../core/canonical'
import { deepFreeze } from '../core/deepFreeze'

export const ARENA_DEFINITION_SCHEMA_VERSION = 2 as const

// A quaternion in serialized arena data must already be normalized. The
// tolerance only absorbs ordinary floating-point authoring noise; accepting a
// wider range would make renderer and collision backends interpret the same
// canonical payload differently.
export const ARENA_QUATERNION_NORMALIZATION_EPSILON = 1e-6

export interface ArenaVector3 {
  x: number
  y: number
  z: number
}

export interface ArenaQuaternion {
  x: number
  y: number
  z: number
  w: number
}

export type ArenaSurfaceMaterial = 'floor' | 'wall' | 'accent'

export interface ArenaBoxSurface {
  id: string
  kind: 'box'
  center: ArenaVector3
  size: ArenaVector3
  orientation: ArenaQuaternion
  material: ArenaSurfaceMaterial
}

export type ArenaPrimitiveSurface = ArenaBoxSurface

export interface ArenaSpawnAnchor {
  id: string
  position: ArenaVector3
  yawRadians: number
}

export interface ArenaDefinitionPayload {
  schemaVersion: typeof ARENA_DEFINITION_SCHEMA_VERSION
  id: string
  seed: number
  generatorVersion: string
  surfaces: ArenaPrimitiveSurface[]
  spawnAnchors: ArenaSpawnAnchor[]
}

export interface ArenaDefinition extends ArenaDefinitionPayload {
  contentHash: string
}

export type ArenaValidationErrorCode =
  | 'INVALID_TYPE'
  | 'MISSING_FIELD'
  | 'UNSUPPORTED_SCHEMA_VERSION'
  | 'INVALID_NUMBER'
  | 'INVALID_SURFACE_KIND'
  | 'INVALID_SURFACE_MATERIAL'
  | 'INVALID_SURFACE_SIZE'
  | 'INVALID_ORIENTATION'
  | 'DUPLICATE_ID'
  | 'EMPTY_SURFACES'
  | 'EMPTY_SPAWN_ANCHORS'
  | 'CONTENT_HASH_MISMATCH'
  | 'INVALID_JSON'
  | 'UNRECOGNIZED_FIELD'

export interface ArenaValidationError {
  code: ArenaValidationErrorCode
  paths: readonly string[]
  message: string
}

export type ArenaValidationResult =
  | { ok: true; arena: ArenaDefinition }
  | { ok: false; errors: readonly ArenaValidationError[] }

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function validateRequiredString(
  value: Record<string, unknown>,
  key: string,
  path: string,
  errors: ArenaValidationError[],
): void {
  const field = value[key]
  if (field === undefined) {
    errors.push({ code: 'MISSING_FIELD', paths: [path], message: `Отсутствует обязательное поле ${path}.` })
  } else if (typeof field !== 'string' || field.length === 0) {
    errors.push({ code: 'INVALID_TYPE', paths: [path], message: `${path} должно быть непустой строкой.` })
  }
}

function validateVector(
  value: unknown,
  path: string,
  errors: ArenaValidationError[],
  requirePositive: boolean,
): value is ArenaVector3 {
  if (!isRecord(value)) {
    errors.push({ code: 'INVALID_TYPE', paths: [path], message: `${path} должен быть объектом Vector3.` })
    return false
  }

  validateAllowedKeys(value, ['x', 'y', 'z'], path, errors)

  let valid = true
  for (const axis of ['x', 'y', 'z'] as const) {
    const axisPath = `${path}.${axis}`
    const component = value[axis]
    if (component === undefined) {
      errors.push({ code: 'MISSING_FIELD', paths: [axisPath], message: `Отсутствует ${axisPath}.` })
      valid = false
    } else if (typeof component !== 'number' || !Number.isFinite(component)) {
      errors.push({ code: 'INVALID_NUMBER', paths: [axisPath], message: `${axisPath} должен быть конечным числом.` })
      valid = false
    } else if (requirePositive && component <= 0) {
      errors.push({ code: 'INVALID_SURFACE_SIZE', paths: [axisPath], message: `${axisPath} должен быть больше нуля.` })
      valid = false
    }
  }
  return valid
}

function validateQuaternion(
  value: unknown,
  path: string,
  errors: ArenaValidationError[],
): value is ArenaQuaternion {
  if (!isRecord(value)) {
    errors.push({ code: 'INVALID_TYPE', paths: [path], message: `${path} должен быть объектом Quaternion.` })
    return false
  }

  validateAllowedKeys(value, ['x', 'y', 'z', 'w'], path, errors)

  let valid = true
  for (const componentName of ['x', 'y', 'z', 'w'] as const) {
    const componentPath = `${path}.${componentName}`
    const component = value[componentName]
    if (component === undefined) {
      errors.push({ code: 'MISSING_FIELD', paths: [componentPath], message: `Отсутствует ${componentPath}.` })
      valid = false
    } else if (typeof component !== 'number' || !Number.isFinite(component)) {
      errors.push({
        code: 'INVALID_NUMBER',
        paths: [componentPath],
        message: `${componentPath} должен быть конечным числом.`,
      })
      valid = false
    }
  }

  if (!valid) return false

  const quaternion = value as unknown as ArenaQuaternion
  const squaredLength =
    quaternion.x * quaternion.x +
    quaternion.y * quaternion.y +
    quaternion.z * quaternion.z +
    quaternion.w * quaternion.w
  if (Math.abs(squaredLength - 1) > ARENA_QUATERNION_NORMALIZATION_EPSILON) {
    errors.push({
      code: 'INVALID_ORIENTATION',
      paths: [path],
      message: `${path} должен быть нормализованным Quaternion.`,
    })
    return false
  }

  return true
}

function validateAllowedKeys(
  value: Record<string, unknown>,
  allowedKeys: readonly string[],
  path: string,
  errors: ArenaValidationError[],
): void {
  const allowed = new Set(allowedKeys)
  for (const key of Object.keys(value).sort()) {
    if (!allowed.has(key)) {
      const fieldPath = path === '$' ? key : `${path}.${key}`
      errors.push({
        code: 'UNRECOGNIZED_FIELD',
        paths: [fieldPath],
        message: `${fieldPath} не входит в schema ArenaDefinition.`,
      })
    }
  }
}

function validateUniqueIds(
  values: readonly unknown[],
  collectionPath: string,
  errors: ArenaValidationError[],
): void {
  const seen = new Map<string, number>()
  values.forEach((value, index) => {
    if (!isRecord(value) || typeof value.id !== 'string' || value.id.length === 0) return
    const previousIndex = seen.get(value.id)
    if (previousIndex !== undefined) {
      errors.push({
        code: 'DUPLICATE_ID',
        paths: [`${collectionPath}.${previousIndex}.id`, `${collectionPath}.${index}.id`],
        message: `Идентификатор ${value.id} повторяется в ${collectionPath}.`,
      })
    } else {
      seen.set(value.id, index)
    }
  })
}

export function arenaContentPayload(arena: ArenaDefinition): ArenaDefinitionPayload {
  const { contentHash: _contentHash, ...payload } = arena
  return payload
}

export function computeArenaContentHash(arena: ArenaDefinition): ContentHash {
  return contentHash(arenaContentPayload(arena))
}

export function canonicalArenaJson(arena: ArenaDefinition): string {
  return canonicalSerialize(arena)
}

export function validateArenaDefinition(value: unknown): ArenaValidationResult {
  const errors: ArenaValidationError[] = []
  if (!isRecord(value)) {
    return {
      ok: false,
      errors: [{ code: 'INVALID_TYPE', paths: ['$'], message: 'ArenaDefinition должен быть объектом.' }],
    }
  }

  validateRequiredString(value, 'id', 'id', errors)
  validateRequiredString(value, 'generatorVersion', 'generatorVersion', errors)
  validateRequiredString(value, 'contentHash', 'contentHash', errors)
  validateAllowedKeys(
    value,
    ['schemaVersion', 'id', 'seed', 'generatorVersion', 'surfaces', 'spawnAnchors', 'contentHash'],
    '$',
    errors,
  )
  if (typeof value.contentHash === 'string' && value.contentHash.length > 0 && !isContentHash(value.contentHash)) {
    errors.push({
      code: 'INVALID_TYPE',
      paths: ['contentHash'],
      message: 'contentHash должен соответствовать формату fnv1a64-v1.',
    })
  }

  if (value.schemaVersion === undefined) {
    errors.push({ code: 'MISSING_FIELD', paths: ['schemaVersion'], message: 'Отсутствует schemaVersion.' })
  } else if (value.schemaVersion !== ARENA_DEFINITION_SCHEMA_VERSION) {
    errors.push({
      code: 'UNSUPPORTED_SCHEMA_VERSION',
      paths: ['schemaVersion'],
      message: `Поддерживается schemaVersion ${ARENA_DEFINITION_SCHEMA_VERSION}.`,
    })
  }

  if (value.seed === undefined) {
    errors.push({ code: 'MISSING_FIELD', paths: ['seed'], message: 'Отсутствует seed.' })
  } else if (typeof value.seed !== 'number' || !Number.isSafeInteger(value.seed) || value.seed < 0) {
    errors.push({ code: 'INVALID_NUMBER', paths: ['seed'], message: 'seed должен быть неотрицательным safe integer.' })
  }

  if (!Array.isArray(value.surfaces)) {
    errors.push({ code: 'INVALID_TYPE', paths: ['surfaces'], message: 'surfaces должен быть массивом.' })
  } else {
    if (value.surfaces.length === 0) {
      errors.push({ code: 'EMPTY_SURFACES', paths: ['surfaces'], message: 'Арена должна содержать хотя бы одну поверхность.' })
    }
    value.surfaces.forEach((surface, index) => {
      const path = `surfaces.${index}`
      if (!isRecord(surface)) {
        errors.push({ code: 'INVALID_TYPE', paths: [path], message: `${path} должен быть объектом.` })
        return
      }
      validateAllowedKeys(surface, ['id', 'kind', 'center', 'size', 'orientation', 'material'], path, errors)
      validateRequiredString(surface, 'id', `${path}.id`, errors)
      if (surface.kind !== 'box') {
        errors.push({ code: 'INVALID_SURFACE_KIND', paths: [`${path}.kind`], message: 'Поддерживается только primitive surface box.' })
      }
      if (surface.material !== 'floor' && surface.material !== 'wall' && surface.material !== 'accent') {
        errors.push({
          code: 'INVALID_SURFACE_MATERIAL',
          paths: [`${path}.material`],
          message: `${path}.material должен быть floor, wall или accent.`,
        })
      }
      validateVector(surface.center, `${path}.center`, errors, false)
      validateVector(surface.size, `${path}.size`, errors, true)
      if (surface.orientation === undefined) {
        errors.push({
          code: 'MISSING_FIELD',
          paths: [`${path}.orientation`],
          message: `Отсутствует ${path}.orientation.`,
        })
      } else {
        validateQuaternion(surface.orientation, `${path}.orientation`, errors)
      }
    })
    validateUniqueIds(value.surfaces, 'surfaces', errors)
  }

  if (!Array.isArray(value.spawnAnchors)) {
    errors.push({ code: 'INVALID_TYPE', paths: ['spawnAnchors'], message: 'spawnAnchors должен быть массивом.' })
  } else {
    if (value.spawnAnchors.length === 0) {
      errors.push({
        code: 'EMPTY_SPAWN_ANCHORS',
        paths: ['spawnAnchors'],
        message: 'Арена должна содержать хотя бы один spawn anchor.',
      })
    }
    value.spawnAnchors.forEach((spawn, index) => {
      const path = `spawnAnchors.${index}`
      if (!isRecord(spawn)) {
        errors.push({ code: 'INVALID_TYPE', paths: [path], message: `${path} должен быть объектом.` })
        return
      }
      validateAllowedKeys(spawn, ['id', 'position', 'yawRadians'], path, errors)
      validateRequiredString(spawn, 'id', `${path}.id`, errors)
      validateVector(spawn.position, `${path}.position`, errors, false)
      if (typeof spawn.yawRadians !== 'number' || !Number.isFinite(spawn.yawRadians)) {
        errors.push({ code: 'INVALID_NUMBER', paths: [`${path}.yawRadians`], message: `${path}.yawRadians должен быть конечным числом.` })
      }
    })
    validateUniqueIds(value.spawnAnchors, 'spawnAnchors', errors)
  }

  if (errors.length === 0) {
    const arena = value as unknown as ArenaDefinition
    if (computeArenaContentHash(arena) !== arena.contentHash) {
      errors.push({
        code: 'CONTENT_HASH_MISMATCH',
        paths: ['contentHash'],
        message: 'contentHash не соответствует содержимому ArenaDefinition.',
      })
    }
  }

  if (errors.length !== 0) {
    return { ok: false, errors: Object.freeze(errors) }
  }

  const detached = JSON.parse(canonicalSerialize(value)) as ArenaDefinition
  return { ok: true, arena: deepFreeze(detached) }
}

export function parseArenaDefinition(serialized: string): ArenaValidationResult {
  let parsed: unknown
  try {
    parsed = JSON.parse(serialized) as unknown
  } catch {
    return {
      ok: false,
      errors: [{ code: 'INVALID_JSON', paths: ['$'], message: 'ArenaDefinition содержит некорректный JSON.' }],
    }
  }
  return validateArenaDefinition(parsed)
}
