import { describe, expect, it } from 'vitest'

import { contentHash } from '../core/canonical'
import {
  COLLISION_BACKEND_PACKAGE,
  COLLISION_BACKEND_VERSION,
  COLLISION_CHECKPOINT_SCHEMA_VERSION,
  COLLISION_CONTRACT_VERSION,
  COLLISION_ERROR_CODES,
  CollisionContractError,
  RUNTIME_COLLISION_COMPATIBILITY_IDENTITY,
  collisionCheckpointHash,
  normalizeCollisionDistance,
  parseCollisionCapsuleMovementResult,
  parseCollisionCheckpointPayload,
  parseCollisionCompatibilityIdentity,
  parseCollisionHits,
  serializeCollisionCheckpointPayload,
  type CollisionCheckpointPayload,
} from './contracts'

function checkpointInput(): CollisionCheckpointPayload {
  return {
    schemaVersion: COLLISION_CHECKPOINT_SCHEMA_VERSION,
    tick: 120,
    compatibility: RUNTIME_COLLISION_COMPATIBILITY_IDENTITY,
    arena: {
      id: 'collision-fixture-v1',
      seed: 42,
      generatorVersion: 'collision-fixture-generator-v1',
      contentHash: contentHash({ arena: 'collision-fixture-v1' }),
    },
    fixture: { id: 'collision-workload', version: 1 },
    capsules: [
      {
        semanticId: 'participant-b',
        position: { x: 2, y: 1.4, z: 0 },
        radius: 0.4,
        halfHeight: 1,
      },
      {
        semanticId: 'participant-a',
        position: { x: -2, y: 1.4, z: 0 },
        radius: 0.4,
        halfHeight: 1,
      },
    ],
  }
}

describe('collision compatibility identity', () => {
  it('accepts and freezes the exact versioned runtime identity', () => {
    const parsed = parseCollisionCompatibilityIdentity({
      backendVersion: COLLISION_BACKEND_VERSION,
      contractVersion: COLLISION_CONTRACT_VERSION,
      backendPackage: COLLISION_BACKEND_PACKAGE,
    })

    expect(parsed).toBe(RUNTIME_COLLISION_COMPATIBILITY_IDENTITY)
    expect(Object.isFrozen(parsed)).toBe(true)
  })

  it.each([
    ['previous contract version', { ...RUNTIME_COLLISION_COMPATIBILITY_IDENTITY, contractVersion: 'collision-contract-v1' }],
    ['backend package', { ...RUNTIME_COLLISION_COMPATIBILITY_IDENTITY, backendPackage: 'other' }],
    ['backend version', { ...RUNTIME_COLLISION_COMPATIBILITY_IDENTITY, backendVersion: '0.21.0' }],
  ])('rejects an unsupported %s with the stable error code', (_name, identity) => {
    expect.assertions(2)
    try {
      parseCollisionCompatibilityIdentity(identity)
    } catch (error) {
      expect(error).toBeInstanceOf(CollisionContractError)
      expect((error as CollisionContractError).code).toBe(
        COLLISION_ERROR_CODES.unsupportedIdentity,
      )
    }
  })
})

describe('backend-neutral collision results', () => {
  it('normalizes, orders, and freezes hits independently of backend callback order', () => {
    const hits = parseCollisionHits([
      {
        semanticId: 'surface-z',
        distance: 1.00000049,
        point: { x: 1, y: 0, z: 0 },
        normal: { x: -1, y: 0, z: 0 },
      },
      {
        semanticId: 'surface-a',
        distance: 1.0000004,
        point: { x: 1, y: 0, z: 0 },
        normal: { x: -1, y: 0, z: 0 },
      },
      {
        semanticId: 'surface-near',
        distance: 0.25,
        point: { x: 0.25, y: 0, z: 0 },
        normal: { x: -1, y: 0, z: 0 },
      },
    ])

    expect(hits.map((hit) => hit.semanticId)).toEqual([
      'surface-near',
      'surface-a',
      'surface-z',
    ])
    expect(hits[1]?.distance).toBe(1)
    expect(normalizeCollisionDistance(0.12345678)).toBe(0.123457)
    expect(Object.isFrozen(hits)).toBe(true)
    expect(Object.isFrozen(hits[0]?.point)).toBe(true)
  })

  it('returns a deeply frozen capsule movement result with ordered contacts', () => {
    const result = parseCollisionCapsuleMovementResult({
      position: { x: 1, y: 2, z: 3 },
      grounded: true,
      contacts: [
        {
          semanticId: 'wall',
          distance: 0.2,
          point: { x: 1, y: 2, z: 3 },
          normal: { x: -1, y: 0, z: 0 },
        },
        {
          semanticId: 'floor',
          distance: 0,
          point: { x: 1, y: 0, z: 3 },
          normal: { x: 0, y: 1, z: 0 },
        },
      ],
    })

    expect(result.contacts.map((contact) => contact.semanticId)).toEqual(['floor', 'wall'])
    expect(Object.isFrozen(result)).toBe(true)
    expect(Object.isFrozen(result.contacts)).toBe(true)
    expect(Object.isFrozen(result.contacts[0]?.normal)).toBe(true)
  })
})

describe('CollisionCheckpointPayload', () => {
  it('round-trips canonically, sorts capsules, freezes all data, and preserves the hash', () => {
    const source = checkpointInput()
    const parsed = parseCollisionCheckpointPayload(source)
    const serialized = serializeCollisionCheckpointPayload(parsed)
    const restored = parseCollisionCheckpointPayload(serialized)

    expect(restored).toEqual(parsed)
    expect(restored.capsules.map((capsule) => capsule.semanticId)).toEqual([
      'participant-a',
      'participant-b',
    ])
    expect(collisionCheckpointHash(restored)).toBe(collisionCheckpointHash(parsed))
    expect(Object.isFrozen(restored)).toBe(true)
    expect(Object.isFrozen(restored.arena)).toBe(true)
    expect(Object.isFrozen(restored.capsules)).toBe(true)
    expect(Object.isFrozen(restored.capsules[0]?.position)).toBe(true)
  })

  it('hashes equivalent capsule sets identically regardless of input order', () => {
    const first = checkpointInput()
    const second = { ...first, capsules: [...first.capsules].reverse() }

    expect(collisionCheckpointHash(first)).toBe(collisionCheckpointHash(second))
  })

  it.each([
    ['checkpoint schema', { ...checkpointInput(), schemaVersion: 2 }],
    [
      'compatibility identity',
      {
        ...checkpointInput(),
        compatibility: {
          ...RUNTIME_COLLISION_COMPATIBILITY_IDENTITY,
          backendVersion: '0.21.0',
        },
      },
    ],
  ])('rejects an incompatible %s before reconstruction', (_name, value) => {
    expect.assertions(1)
    try {
      parseCollisionCheckpointPayload(value)
    } catch (error) {
      expect((error as CollisionContractError).code).toBe(
        COLLISION_ERROR_CODES.unsupportedIdentity,
      )
    }
  })

  it('rejects malformed, duplicate, or non-canonical data with the stable invalid-data code', () => {
    const duplicate = checkpointInput()
    const malformedValues: unknown[] = [
      '{invalid-json',
      { ...duplicate, capsules: [duplicate.capsules[0], duplicate.capsules[0]] },
      { ...duplicate, tick: -1 },
    ]

    for (const value of malformedValues) {
      try {
        parseCollisionCheckpointPayload(value)
        throw new Error('expected parser to reject malformed collision data')
      } catch (error) {
        expect((error as CollisionContractError).code).toBe(COLLISION_ERROR_CODES.invalidData)
      }
    }
  })
})
