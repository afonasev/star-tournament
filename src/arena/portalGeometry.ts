import kit from './portalKit.json'
import type { ArenaBoxSurface } from './arenaDefinition'
import { arenaDoorwayGaps } from './doorwayGaps'

// Authored asset dimensions/tessellation are a shared geometry contract, not balance.
export const portalScale = (width: number): number => width / kit.clearWidth

export function portalCollisionSurfaces(surfaces: readonly ArenaBoxSurface[]): ArenaBoxSurface[] {
  return arenaDoorwayGaps(surfaces).flatMap(gap => {
    const scale = portalScale(gap.width)
    return kit.parts.map((part, index) => {
      const position = part[1] as number[], size = part[2] as number[]
      const rotation = (part[3] as number[] | undefined) ?? [0, 0, 0, 1]
      const [x, y, z] = position.map(v => v * scale) as [number, number, number]
      // Exact Y quarter-turn composed with the authored Z rotation.
      const h = Math.SQRT1_2
      return {
        id: `portal-solid:${gap.id}:${index}`, kind: 'box' as const, material: 'wall' as const,
        center: gap.horizontalAlongX
          ? {x: gap.spanCenter + x, y: gap.baseY + y, z: gap.planeCoordinate + z}
          : {x: gap.planeCoordinate + z, y: gap.baseY + y, z: gap.spanCenter - x},
        size: {x: size[0]! * scale, y: size[1]! * scale, z: size[2]! * scale},
        orientation: gap.horizontalAlongX
          ? {x: 0, y: 0, z: rotation[2]!, w: rotation[3]!}
          : {x: h * rotation[2]!, y: h * rotation[3]!, z: h * rotation[2]!, w: h * rotation[3]!},
        semantics: {classification: 'wall' as const, movement: true, projectile: true, sector: 'portal'},
      }
    })
  })
}
