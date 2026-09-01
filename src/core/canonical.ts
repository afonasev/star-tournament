const FNV1A_64_OFFSET_BASIS = 0xcbf29ce484222325n
const FNV1A_64_PRIME = 0x100000001b3n
const UINT64_MASK = 0xffffffffffffffffn

export const CONTENT_HASH_VERSION = 'fnv1a64-v1' as const

export type ContentHash = `${typeof CONTENT_HASH_VERSION}:${string}`

export type CanonicalValue =
  | null
  | boolean
  | number
  | string
  | readonly CanonicalValue[]
  | { readonly [key: string]: CanonicalValue }

export class CanonicalSerializationError extends TypeError {
  readonly path: string

  constructor(path: string, reason: string) {
    super(`Cannot canonically serialize ${path}: ${reason}`)
    this.name = 'CanonicalSerializationError'
    this.path = path
  }
}

function childPath(path: string, key: string): string {
  return /^[A-Za-z_$][A-Za-z0-9_$]*$/.test(key)
    ? `${path}.${key}`
    : `${path}[${JSON.stringify(key)}]`
}

function serializeValue(value: unknown, path: string, ancestors: Set<object>): string {
  if (value === null) {
    return 'null'
  }

  switch (typeof value) {
    case 'boolean':
    case 'string':
      return JSON.stringify(value)
    case 'number':
      if (!Number.isFinite(value)) {
        throw new CanonicalSerializationError(path, 'numbers must be finite')
      }
      return JSON.stringify(value)
    case 'undefined':
      throw new CanonicalSerializationError(path, 'undefined is not supported')
    case 'bigint':
    case 'function':
    case 'symbol':
      throw new CanonicalSerializationError(path, `${typeof value} values are not supported`)
    case 'object':
      break
  }

  const objectValue = value
  if (ancestors.has(objectValue)) {
    throw new CanonicalSerializationError(path, 'cyclic references are not supported')
  }

  ancestors.add(objectValue)
  try {
    if (Array.isArray(objectValue)) {
      const symbolKeys = Object.getOwnPropertySymbols(objectValue)
      if (symbolKeys.length > 0) {
        throw new CanonicalSerializationError(path, 'symbol properties are not supported')
      }

      const values: string[] = []
      for (let index = 0; index < objectValue.length; index += 1) {
        if (!Object.prototype.hasOwnProperty.call(objectValue, index)) {
          throw new CanonicalSerializationError(`${path}[${index}]`, 'sparse arrays are not supported')
        }
        values.push(serializeValue(objectValue[index], `${path}[${index}]`, ancestors))
      }

      const extraKeys = Object.keys(objectValue).filter((key) => !/^(0|[1-9]\d*)$/.test(key))
      if (extraKeys.length > 0) {
        throw new CanonicalSerializationError(
          childPath(path, extraKeys.sort()[0] ?? ''),
          'extra array properties are not supported',
        )
      }

      return `[${values.join(',')}]`
    }

    const prototype = Object.getPrototypeOf(objectValue) as object | null
    if (prototype !== Object.prototype && prototype !== null) {
      throw new CanonicalSerializationError(path, 'only plain objects are supported')
    }

    const symbolKeys = Object.getOwnPropertySymbols(objectValue)
    if (symbolKeys.length > 0) {
      throw new CanonicalSerializationError(path, 'symbol properties are not supported')
    }

    const keys = Object.getOwnPropertyNames(objectValue).sort()
    const properties: string[] = []
    for (const key of keys) {
      const propertyPath = childPath(path, key)
      const descriptor = Object.getOwnPropertyDescriptor(objectValue, key)
      if (descriptor === undefined || !descriptor.enumerable) {
        throw new CanonicalSerializationError(propertyPath, 'non-enumerable properties are not supported')
      }
      if (!('value' in descriptor)) {
        throw new CanonicalSerializationError(propertyPath, 'accessor properties are not supported')
      }
      properties.push(`${JSON.stringify(key)}:${serializeValue(descriptor.value, propertyPath, ancestors)}`)
    }

    return `{${properties.join(',')}}`
  } finally {
    ancestors.delete(objectValue)
  }
}

/**
 * Produces a deterministic JSON representation for the supported data-only tree.
 * Object keys are sorted recursively while array order is preserved.
 */
export function canonicalSerialize(value: unknown): string {
  return serializeValue(value, '$', new Set<object>())
}

/** Parses JSON and verifies that it belongs to the canonical data-only domain. */
export function parseCanonicalJson(serialized: string): CanonicalValue {
  let parsed: unknown
  try {
    parsed = JSON.parse(serialized) as unknown
  } catch (error) {
    const detail = error instanceof Error ? error.message : 'unknown JSON parse error'
    throw new CanonicalSerializationError('$', `invalid JSON (${detail})`)
  }

  canonicalSerialize(parsed)
  return parsed as CanonicalValue
}

function utf8Bytes(value: string): readonly number[] {
  const bytes: number[] = []
  for (let index = 0; index < value.length; index += 1) {
    let codePoint = value.codePointAt(index)
    if (codePoint === undefined) {
      continue
    }
    if (codePoint > 0xffff) {
      index += 1
    }

    if (codePoint <= 0x7f) {
      bytes.push(codePoint)
    } else if (codePoint <= 0x7ff) {
      bytes.push(0xc0 | (codePoint >> 6), 0x80 | (codePoint & 0x3f))
    } else if (codePoint <= 0xffff) {
      bytes.push(
        0xe0 | (codePoint >> 12),
        0x80 | ((codePoint >> 6) & 0x3f),
        0x80 | (codePoint & 0x3f),
      )
    } else {
      bytes.push(
        0xf0 | (codePoint >> 18),
        0x80 | ((codePoint >> 12) & 0x3f),
        0x80 | ((codePoint >> 6) & 0x3f),
        0x80 | (codePoint & 0x3f),
      )
    }
  }
  return bytes
}

/** Hashes text as UTF-8 with the versioned FNV-1a 64-bit diagnostic contract. */
export function hashText(value: string): ContentHash {
  let hash = FNV1A_64_OFFSET_BASIS
  for (const byte of utf8Bytes(value)) {
    hash ^= BigInt(byte)
    hash = (hash * FNV1A_64_PRIME) & UINT64_MASK
  }
  return `${CONTENT_HASH_VERSION}:${hash.toString(16).padStart(16, '0')}`
}

/** Hashes the canonical representation of a supported data-only value. */
export function contentHash(value: unknown): ContentHash {
  return hashText(canonicalSerialize(value))
}

export function isContentHash(value: unknown): value is ContentHash {
  return typeof value === 'string' && /^fnv1a64-v1:[0-9a-f]{16}$/.test(value)
}
