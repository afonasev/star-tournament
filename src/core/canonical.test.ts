import { describe, expect, it } from 'vitest'

import {
  CanonicalSerializationError,
  canonicalSerialize,
  contentHash,
  hashText,
  parseCanonicalJson,
} from './canonical'

describe('canonicalSerialize', () => {
  it('sorts object keys recursively while preserving array order', () => {
    const first = {
      zebra: 2,
      alpha: { z: true, a: null },
      items: [{ second: 2, first: 1 }, 'last'],
    }
    const second = {
      items: [{ first: 1, second: 2 }, 'last'],
      alpha: { a: null, z: true },
      zebra: 2,
    }

    const expected =
      '{"alpha":{"a":null,"z":true},"items":[{"first":1,"second":2},"last"],"zebra":2}'
    expect(canonicalSerialize(first)).toBe(expected)
    expect(canonicalSerialize(second)).toBe(expected)
    expect(canonicalSerialize(['last', 'first'])).toBe('["last","first"]')
  })

  it('uses JSON string escaping and canonicalizes negative zero', () => {
    expect(canonicalSerialize({ text: 'line\n"quoted"', zero: -0 })).toBe(
      '{"text":"line\\n\\"quoted\\"","zero":0}',
    )
  })

  it.each([
    ['root undefined', undefined],
    ['nested undefined', { invalid: undefined }],
    ['NaN', Number.NaN],
    ['positive infinity', Number.POSITIVE_INFINITY],
    ['negative infinity', Number.NEGATIVE_INFINITY],
    ['bigint', 1n],
    ['function', () => undefined],
    ['symbol', Symbol('unsupported')],
    ['date', new Date(0)],
    ['map', new Map()],
  ])('rejects invalid value: %s', (_name, value) => {
    expect(() => canonicalSerialize(value)).toThrow(CanonicalSerializationError)
  })

  it('rejects sparse arrays, symbol properties, accessors, and cycles with a stable path', () => {
    const sparse = new Array<unknown>(2)
    sparse[1] = 'present'
    expect(() => canonicalSerialize({ nested: sparse })).toThrow(/\$\.nested\[0\].*sparse/)

    const withSymbol = { valid: true, [Symbol('hidden')]: 1 }
    expect(() => canonicalSerialize(withSymbol)).toThrow(/symbol properties/)

    const withGetter = Object.defineProperty({}, 'value', {
      enumerable: true,
      get: () => 1,
    })
    expect(() => canonicalSerialize(withGetter)).toThrow(/\$\.value.*accessor/)

    const cyclic: { self?: unknown } = {}
    cyclic.self = cyclic
    expect(() => canonicalSerialize(cyclic)).toThrow(/\$\.self.*cyclic/)
  })

  it('parses supported JSON and restores its canonical form', () => {
    const parsed = parseCanonicalJson('{"z":[2,1],"a":{"b":true}}')
    expect(canonicalSerialize(parsed)).toBe('{"a":{"b":true},"z":[2,1]}')
    expect(() => parseCanonicalJson('{bad json')).toThrow(/invalid JSON/)
  })
})

describe('versioned FNV-1a 64-bit hashing', () => {
  it('matches the standard UTF-8 hello vector and includes an algorithm version', () => {
    expect(hashText('hello')).toBe('fnv1a64-v1:a430d84680aabd0b')
    expect(hashText('Привет')).toMatch(/^fnv1a64-v1:[0-9a-f]{16}$/)
  })

  it('is insensitive to object key order and changes with effective data', () => {
    expect(contentHash({ b: 2, a: 1 })).toBe(contentHash({ a: 1, b: 2 }))
    expect(contentHash({ a: 1, b: 2 })).not.toBe(contentHash({ a: 1, b: 3 }))
    expect(contentHash([1, 2])).not.toBe(contentHash([2, 1]))
  })
})
