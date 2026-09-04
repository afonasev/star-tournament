import { describe, expect, it } from 'vitest'

import { canonicalArenaJson, createArenaDefinition } from './arenaDefinition'
import { validateArenaGameplay } from './arenaGameplayValidation'
import { allocateArenaSpawns } from './spawnAllocation'
import { generateProceduralArena, PROCEDURAL_FALLBACK_ARENA, requireProceduralArena } from './proceduralArena'
import { PROTOTYPE_V1_PROFILE } from '../profiles'

const seeds = [0, 1, 0x53544152, 0xffffffff]
const sizes = ['small', 'medium', 'large'] as const

function intervalOverlap(firstCenter: number, firstSize: number, secondCenter: number, secondSize: number): number {
  return Math.min(firstCenter + firstSize / 2, secondCenter + secondSize / 2)
    - Math.max(firstCenter - firstSize / 2, secondCenter - secondSize / 2)
}

function visuallyDistinctVerticalCoplanarOverlaps(arena: ReturnType<typeof requireProceduralArena>): string[] {
  const overlaps: string[] = []
  arena.surfaces.forEach((first, index) => arena.surfaces.slice(index + 1).forEach((second) => {
    if (first.material === second.material) return
    for (const axis of ['x', 'z'] as const) {
      const otherAxis = axis === 'x' ? 'z' : 'x'
      for (const direction of [-1, 1] as const) {
        const firstFace = first.center[axis] + direction * first.size[axis] / 2
        const secondFace = second.center[axis] + direction * second.size[axis] / 2
        if (Math.abs(firstFace - secondFace) > 1e-9) continue
        const planarOverlap = intervalOverlap(first.center[otherAxis], first.size[otherAxis], second.center[otherAxis], second.size[otherAxis])
        const verticalOverlap = intervalOverlap(first.center.y, first.size.y, second.center.y, second.size.y)
        if (planarOverlap > 1e-6 && verticalOverlap > 1e-6) overlaps.push(`${first.id}|${second.id}|${axis}|${firstFace}`)
      }
    }
  }))
  return overlaps.sort()
}

function subCapsuleGaps(arena: ReturnType<typeof requireProceduralArena>, minimumClearance: number): string[] {
  const solids = arena.surfaces.filter(({ id }) => id !== 'floor')
  const gaps: string[] = []
  const coveredByAnotherSolid = (firstId: string, secondId: string, xMinimum: number, xMaximum: number, zMinimum: number, zMaximum: number, yMinimum: number, yMaximum: number): boolean => solids.some((candidate) => (
    candidate.id !== firstId
      && candidate.id !== secondId
      && candidate.center.x - candidate.size.x / 2 <= xMinimum + 1e-9
      && candidate.center.x + candidate.size.x / 2 >= xMaximum - 1e-9
      && candidate.center.z - candidate.size.z / 2 <= zMinimum + 1e-9
      && candidate.center.z + candidate.size.z / 2 >= zMaximum - 1e-9
      && candidate.center.y - candidate.size.y / 2 <= yMinimum + 1e-9
      && candidate.center.y + candidate.size.y / 2 >= yMaximum - 1e-9
  ))
  solids.forEach((first, index) => solids.slice(index + 1).forEach((second) => {
    const xGap = Math.abs(first.center.x - second.center.x) - (first.size.x + second.size.x) / 2
    const zGap = Math.abs(first.center.z - second.center.z) - (first.size.z + second.size.z) / 2
    const xOverlap = intervalOverlap(first.center.x, first.size.x, second.center.x, second.size.x)
    const zOverlap = intervalOverlap(first.center.z, first.size.z, second.center.z, second.size.z)
    const yMinimum = Math.max(first.center.y - first.size.y / 2, second.center.y - second.size.y / 2)
    const yMaximum = Math.min(first.center.y + first.size.y / 2, second.center.y + second.size.y / 2)
    if (xGap > 1e-6 && xGap + 1e-9 < minimumClearance && zOverlap > 1e-6) {
      const left = first.center.x <= second.center.x ? first : second
      const right = left === first ? second : first
      const xMinimum = left.center.x + left.size.x / 2
      const xMaximum = right.center.x - right.size.x / 2
      const zMinimum = Math.max(first.center.z - first.size.z / 2, second.center.z - second.size.z / 2)
      const zMaximum = Math.min(first.center.z + first.size.z / 2, second.center.z + second.size.z / 2)
      if (!coveredByAnotherSolid(first.id, second.id, xMinimum, xMaximum, zMinimum, zMaximum, yMinimum, yMaximum)) gaps.push(`${first.id}|${second.id}|x|${xGap}`)
    }
    if (zGap > 1e-6 && zGap + 1e-9 < minimumClearance && xOverlap > 1e-6) {
      const north = first.center.z <= second.center.z ? first : second
      const south = north === first ? second : first
      const xMinimum = Math.max(first.center.x - first.size.x / 2, second.center.x - second.size.x / 2)
      const xMaximum = Math.min(first.center.x + first.size.x / 2, second.center.x + second.size.x / 2)
      const zMinimum = north.center.z + north.size.z / 2
      const zMaximum = south.center.z - south.size.z / 2
      if (!coveredByAnotherSolid(first.id, second.id, xMinimum, xMaximum, zMinimum, zMaximum, yMinimum, yMaximum)) gaps.push(`${first.id}|${second.id}|z|${zGap}`)
    }
  }))
  return gaps.sort()
}

describe('broken-ring-v2 procedural arenas', () => {
  it.each(sizes)('reproduces the versioned seed corpus for %s', (size) => {
    for (const seed of seeds) {
      const first = generateProceduralArena({ size, seed })
      const second = generateProceduralArena({ size, seed })
      expect(first.ok).toBe(true)
      expect(second.ok).toBe(true)
      if (!first.ok || !second.ok) continue
      expect(first.report.accepted).toBe(true)
      expect(first.attempt).toBe(0)
      expect(first.report.supportedModes).toEqual(['ffa', 'teams'])
      expect(first.arena.spawnAnchors.length).toBeGreaterThanOrEqual(12)
      expect(canonicalArenaJson(first.arena)).toBe(canonicalArenaJson(second.arena))
      expect(first.arena.contentHash).toBe(second.arena.contentHash)
      expect(visuallyDistinctVerticalCoplanarOverlaps(first.arena)).toEqual([])
      const capsuleClearance = (PROTOTYPE_V1_PROFILE.rules.player.capsule.radius + PROTOTYPE_V1_PROFILE.rules.player.capsule.skinWidth) * 2
      expect(subCapsuleGaps(first.arena, capsuleClearance)).toEqual([])
    }
  })

  it('uses distinct topology budgets rather than uniform coordinate scaling', () => {
    const arenas = sizes.map((size) => requireProceduralArena({ size, seed: 123 }))
    expect(arenas.map(({ regions }) => regions.length)).toEqual([9, 11, 13])
    expect(arenas.map(({ surfaces }) => surfaces.filter(({ id }) => id.startsWith('wall-architecture-room-')).length)).toEqual([16, 24, 32])
    expect(new Set(arenas.map(({ contentHash }) => contentHash)).size).toBe(3)
  })

  it.each(sizes)('builds connected architecture, doorways, corridors and an irregular perimeter for %s', (size) => {
    const arena = requireProceduralArena({ size, seed: 0x53544152 })
    const report = validateArenaGameplay(arena, PROTOTYPE_V1_PROFILE)
    const perimeter = arena.surfaces.filter(({ id }) => id.startsWith('wall-perimeter-'))
    const architecture = arena.surfaces.filter(({ id }) => id.startsWith('wall-architecture-'))
    expect(perimeter).toHaveLength(12)
    expect(architecture.length).toBeGreaterThan(15)
    expect(report.metrics).toMatchObject({ perimeterSegmentCount: 12, doorwayCount: size === 'small' ? 2 : size === 'medium' ? 3 : 4 })
    expect(report.metrics.corridorClearanceMeters).toBeGreaterThanOrEqual(PROTOTYPE_V1_PROFILE.rules.arenaGenerator.presets[size].corridorWidthMeters)
    expect(report.errors).not.toEqual(expect.arrayContaining([expect.objectContaining({ code: 'ARCHITECTURE_DISCONNECTED' })]))
    expect(visuallyDistinctVerticalCoplanarOverlaps(arena)).toEqual([])
  })

  it('keeps the shipped representative identities stable', () => {
    expect(sizes.map((size) => requireProceduralArena({ size, seed: 0x53544152 }).contentHash)).toEqual([
      'fnv1a64-v1:b324cd00c41a9b65',
      'fnv1a64-v1:2d7bc49c91552e89',
      'fnv1a64-v1:1e58e009056ae2e1',
    ])
    expect(requireProceduralArena({ size: 'small', seed: 0x53544152, profile: PROTOTYPE_V1_PROFILE }).contentHash).toBe('fnv1a64-v1:b324cd00c41a9b65')
    expect(PROCEDURAL_FALLBACK_ARENA).toMatchObject({ id: 'shipped-fallback-arena-v2', generatorVersion: 'shipped-fallback-v2', size: 'medium' })
    expect(PROCEDURAL_FALLBACK_ARENA.contentHash).not.toBe(requireProceduralArena({ size: 'medium', seed: 0x53544152 }).contentHash)
    expect(validateArenaGameplay(PROCEDURAL_FALLBACK_ARENA, PROTOTYPE_V1_PROFILE).accepted).toBe(true)
  })

  it.each(['ffa', 'teams'] as const)('allocates eight distinct slots in %s', (mode) => {
    const arena = requireProceduralArena({ size: 'small', seed: 77 })
    const minimumOpponentSeparationMeters = PROTOTYPE_V1_PROFILE.rules.arenaGenerator.presets.small.minimumSpawnSeparationMeters
    const participants = Array.from({ length: 8 }, (_, index) => ({
      semanticId: `player-${index + 1}`,
      team: mode === 'teams' ? (index % 2 === 0 ? 'team-a' as const : 'team-b' as const) : null,
    }))
    const allocations = allocateArenaSpawns({
      arena,
      mode,
      participants,
      capsuleDiameterMeters: (PROTOTYPE_V1_PROFILE.rules.player.capsule.radius + PROTOTYPE_V1_PROFILE.rules.player.capsule.skinWidth) * 2,
      minimumOpponentSeparationMeters,
    })
    expect(allocations).toHaveLength(8)
    expect(new Set(allocations.map(({ anchorId }) => anchorId)).size).toBe(8)
    expect(new Set(allocations.map(({ position }) => `${position.x}:${position.z}`)).size).toBe(8)
    if (mode === 'teams') {
      const regionByPlayer = new Map(allocations.map((allocation) => [allocation.participantId, allocation.spawnRegionId]))
      expect(new Set(participants.filter(({ team }) => team === 'team-a').map(({ semanticId }) => regionByPlayer.get(semanticId))).size).toBe(1)
      expect(new Set(participants.filter(({ team }) => team === 'team-b').map(({ semanticId }) => regionByPlayer.get(semanticId))).size).toBe(1)
    }
    const teamById = new Map(participants.map(({ semanticId, team }) => [semanticId, team]))
    allocations.forEach((first, index) => allocations.slice(index + 1).forEach((second) => {
      if (mode === 'ffa' || teamById.get(first.participantId) !== teamById.get(second.participantId)) {
        expect(Math.hypot(first.position.x - second.position.x, first.position.z - second.position.z)).toBeGreaterThanOrEqual(minimumOpponentSeparationMeters)
      }
    }))
  })

  it('skips occupied anchors and rejects blocked or fragile definitions', () => {
    const arena = requireProceduralArena({ size: 'small', seed: 99 })
    const firstAnchor = arena.spawnAnchors[0]!
    const [allocation] = allocateArenaSpawns({ arena, mode: 'ffa', participants: [{ semanticId: 'player', team: null }], occupied: [{ semanticId: 'occupied', position: firstAnchor.position }], capsuleDiameterMeters: 2 })
    expect(allocation?.anchorId).not.toBe(firstAnchor.id)

    const { contentHash: _hash, ...payload } = arena
    const fragile = createArenaDefinition({ ...payload, links: payload.links.filter(({ id }) => id !== 'link-spawn-east-north' && id !== 'link-spawn-east-south') })
    expect(validateArenaGameplay(fragile, PROTOTYPE_V1_PROFILE)).toMatchObject({ accepted: false, errors: expect.arrayContaining([expect.objectContaining({ code: 'SINGLE_LINK_FAILURE' })]) })
    const blocked = createArenaDefinition({ ...payload, surfaces: [...payload.surfaces, { id: 'spawn-blocker', kind: 'box', center: { ...firstAnchor.position, y: 1 }, size: { x: 2, y: 2, z: 2 }, orientation: { x: 0, y: 0, z: 0, w: 1 }, material: 'wall' }] })
    expect(validateArenaGameplay(blocked, PROTOTYPE_V1_PROFILE)).toMatchObject({ accepted: false, errors: expect.arrayContaining([expect.objectContaining({ code: 'SPAWN_INTERSECTS_STATIC_GEOMETRY' })]) })
    const disconnected = createArenaDefinition({ ...payload, surfaces: [...payload.surfaces, { id: 'wall-architecture-orphan', kind: 'box', center: { x: 0, y: 1, z: 0 }, size: { x: 1, y: 2, z: 1 }, orientation: { x: 0, y: 0, z: 0, w: 1 }, material: 'wall' }] })
    expect(validateArenaGameplay(disconnected, PROTOTYPE_V1_PROFILE)).toMatchObject({ accepted: false, errors: expect.arrayContaining([expect.objectContaining({ code: 'ARCHITECTURE_DISCONNECTED' })]) })
    const unsealed = createArenaDefinition({ ...payload, surfaces: payload.surfaces.filter(({ id }) => !id.startsWith('wall-architecture-gap-seal-')) })
    expect(validateArenaGameplay(unsealed, PROTOTYPE_V1_PROFILE)).toMatchObject({ accepted: false, errors: expect.arrayContaining([expect.objectContaining({ code: 'SUB_CAPSULE_GAP' })]) })
  })
})
