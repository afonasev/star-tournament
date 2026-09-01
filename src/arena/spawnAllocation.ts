import { deepFreeze } from '../core/deepFreeze'
import { type MatchMode, type MatchTeam } from '../match'

import { type ArenaDefinition, type ArenaSpawnAnchor } from './arenaDefinition'

export interface SpawnAllocationParticipant {
  readonly semanticId: string
  readonly team: MatchTeam | null
}

export interface OccupiedSpawnPosition {
  readonly semanticId: string
  readonly position: { readonly x: number; readonly y: number; readonly z: number }
  readonly team?: MatchTeam | null
}

export interface SpawnAllocation {
  readonly participantId: string
  readonly anchorId: string
  readonly spawnRegionId: string
  readonly position: ArenaSpawnAnchor['position']
  readonly yawRadians: number
}

export interface SpawnAllocationOptions {
  readonly arena: ArenaDefinition
  readonly mode: MatchMode
  readonly participants: readonly SpawnAllocationParticipant[]
  readonly occupied?: readonly OccupiedSpawnPosition[]
  readonly capsuleDiameterMeters: number
  readonly minimumOpponentSeparationMeters?: number
}

function regionOrder(arena: ArenaDefinition, mode: MatchMode, participant: SpawnAllocationParticipant): string[] {
  const ids = arena.spawnRegions.map(({ id }) => id)
  if (mode === 'ffa' || participant.team === null || ids.length < 2) return ids
  const offset = participant.team === 'red' ? arena.seed % ids.length : (arena.seed % ids.length + Math.floor(ids.length / 2)) % ids.length
  return [...ids.slice(offset), ...ids.slice(0, offset)]
}

function overlaps(position: ArenaSpawnAnchor['position'], occupied: readonly OccupiedSpawnPosition[], minimum: number): boolean {
  return occupied.some((entry) => Math.hypot(position.x - entry.position.x, position.z - entry.position.z) < minimum)
}

function minimumDistance(position: ArenaSpawnAnchor['position'], occupied: readonly OccupiedSpawnPosition[]): number {
  if (occupied.length === 0) return Number.POSITIVE_INFINITY
  return Math.min(...occupied.map((entry) => Math.hypot(position.x - entry.position.x, position.z - entry.position.z)))
}

function conflictsWithOpponent(
  position: ArenaSpawnAnchor['position'],
  participant: SpawnAllocationParticipant,
  mode: MatchMode,
  occupied: readonly OccupiedSpawnPosition[],
  minimum: number,
): boolean {
  if (minimum <= 0) return false
  return occupied.some((entry) => {
    const opponent = mode === 'ffa' || participant.team === null || entry.team === undefined || entry.team === null || entry.team !== participant.team
    return opponent && Math.hypot(position.x - entry.position.x, position.z - entry.position.z) < minimum
  })
}

export function allocateArenaSpawns(options: SpawnAllocationOptions): readonly SpawnAllocation[] {
  const participants = [...options.participants].sort((a, b) => a.semanticId.localeCompare(b.semanticId, 'en'))
  const anchorsByRegion = new Map(options.arena.spawnRegions.map((region) => [region.id, region.anchorIds.map((id) => options.arena.spawnAnchors.find((anchor) => anchor.id === id)!).filter(Boolean)]))
  const claimed = new Set<string>()
  const occupied = [...(options.occupied ?? [])]
  const allocations: SpawnAllocation[] = []
  participants.forEach((participant, participantIndex) => {
    const orderedRegions = regionOrder(options.arena, options.mode, participant)
    const preferredIndex = options.mode === 'teams' ? 0 : participantIndex
    const rotatedRegions = [...orderedRegions.slice(preferredIndex % orderedRegions.length), ...orderedRegions.slice(0, preferredIndex % orderedRegions.length)]
    const candidates = rotatedRegions.flatMap((regionId, regionIndex) => (anchorsByRegion.get(regionId) ?? []).map((anchor, anchorIndex) => ({ anchor, order: regionIndex * options.arena.spawnAnchors.length + anchorIndex })))
      .filter(({ anchor }) => !claimed.has(anchor.id) && !overlaps(anchor.position, occupied, options.capsuleDiameterMeters))
      .filter(({ anchor }) => !conflictsWithOpponent(anchor.position, participant, options.mode, occupied, options.minimumOpponentSeparationMeters ?? 0))
    const selected = options.mode === 'ffa'
      ? candidates.sort((a, b) => minimumDistance(b.anchor.position, occupied) - minimumDistance(a.anchor.position, occupied) || a.order - b.order || a.anchor.id.localeCompare(b.anchor.id, 'en'))[0]?.anchor
      : candidates[0]?.anchor
    if (selected === undefined) throw new RangeError(`No distinct free arena spawn slot for ${participant.semanticId}`)
    claimed.add(selected.id)
    occupied.push({ semanticId: participant.semanticId, position: selected.position, team: participant.team })
    allocations.push({ participantId: participant.semanticId, anchorId: selected.id, spawnRegionId: selected.spawnRegionId, position: selected.position, yawRadians: selected.yawRadians })
  })
  return deepFreeze(allocations.sort((a, b) => a.participantId.localeCompare(b.participantId, 'en')))
}
