import type { ArenaBoxSurface } from './arenaDefinition'

export interface ArenaDoorwayGap {
  readonly id: string
  readonly horizontalAlongX: boolean
  readonly planeCoordinate: number
  readonly spanCenter: number
  readonly baseY: number
  readonly height: number
  readonly width: number
}

export function arenaDoorwayGaps(surfaces: readonly ArenaBoxSurface[]): ArenaDoorwayGap[] {
  const walls = surfaces.filter((surface) => surface.material === 'wall' && !surface.id.startsWith('portal-solid:') && (!surface.semantics || surface.semantics.classification === 'wall' && /-segment-\d+$/.test(surface.id)))
  const gaps: ArenaDoorwayGap[] = []
  const epsilon = 0.001
  for (let firstIndex = 0; firstIndex < walls.length; firstIndex += 1) {
    const first = walls[firstIndex]!
    if (Math.abs(first.orientation.x) > epsilon || Math.abs(first.orientation.y) > epsilon || Math.abs(first.orientation.z) > epsilon || Math.abs(first.orientation.w - 1) > epsilon) continue
    const horizontalAlongX = first.size.x >= first.size.z
    const firstSpan = horizontalAlongX ? first.size.x : first.size.z
    const firstStart = (horizontalAlongX ? first.center.x : first.center.z) - firstSpan / 2
    const firstEnd = firstStart + firstSpan
    for (const second of walls.slice(firstIndex + 1)) {
      if (Math.abs(second.orientation.x) > epsilon || Math.abs(second.orientation.y) > epsilon || Math.abs(second.orientation.z) > epsilon || Math.abs(second.orientation.w - 1) > epsilon) continue
      if ((second.size.x >= second.size.z) !== horizontalAlongX) continue
      const secondPlane = horizontalAlongX ? second.center.z : second.center.x
      const firstPlane = horizontalAlongX ? first.center.z : first.center.x
      if (Math.abs(firstPlane - secondPlane) > epsilon || Math.abs(first.center.y - second.center.y) > epsilon || Math.abs(first.size.y - second.size.y) > epsilon) continue
      const secondSpan = horizontalAlongX ? second.size.x : second.size.z
      const secondStart = (horizontalAlongX ? second.center.x : second.center.z) - secondSpan / 2
      const secondEnd = secondStart + secondSpan
      const left = firstStart <= secondStart ? { surface: first, end: firstEnd } : { surface: second, end: secondEnd }
      const right = firstStart <= secondStart ? { surface: second, start: secondStart } : { surface: first, start: firstStart }
      if (first.semantics && first.id.replace(/-segment-\d+$/, '') !== second.id.replace(/-segment-\d+$/, '')) continue
      const width = right.start - left.end
      if (width < 1 || width > 4.5) continue
      gaps.push({
        id: `${left.surface.id}:${right.surface.id}`,
        horizontalAlongX,
        planeCoordinate: firstPlane,
        spanCenter: left.end + width / 2,
        baseY: first.center.y - first.size.y / 2,
        height: first.size.y,
        width,
      })
    }
  }
  return gaps.sort((left, right) => left.id.localeCompare(right.id, 'en'))
}
