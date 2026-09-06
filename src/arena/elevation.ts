import { simulationMath } from '../core/simulationMath'
import type { ArenaBoxSurface, ArenaDefinition, ArenaVector3 } from './arenaDefinition'
import { supportsNavigation } from './arenaDefinition'

/** Analytic upper plane query on canonical oriented boxes; no renderer state. */
export function arenaFloorHeight(arena: Pick<ArenaDefinition, 'surfaces'>, x: number, z: number): number | undefined {
  let height: number | undefined
  for (const s of arena.surfaces) {
    if (!supportsNavigation(s)) continue
    const q = s.orientation
    const nx = 2 * (q.x*q.y-q.z*q.w)
    const ny = 1-2*(q.x*q.x+q.z*q.z)
    const nz = 2*(q.y*q.z+q.x*q.w)
    if (ny <= 0) continue
    const y = s.center.y + (s.size.y/2-nx*(x-s.center.x)-nz*(z-s.center.z))/ny
    const dx=x-s.center.x, dy=y-s.center.y, dz=z-s.center.z
    const lx=(1-2*(q.y*q.y+q.z*q.z))*dx+2*(q.x*q.y+q.z*q.w)*dy+2*(q.x*q.z-q.y*q.w)*dz
    const lz=2*(q.x*q.z+q.y*q.w)*dx+2*(q.y*q.z-q.x*q.w)*dy+(1-2*(q.x*q.x+q.y*q.y))*dz
    // Geometry boundary tolerance absorbs only floating point round-off at shared faces.
    if (Math.abs(lx) <= s.size.x/2+1e-6 && Math.abs(lz) <= s.size.z/2+1e-6) height = Math.max(height ?? -Infinity,y)
  }
  return height
}

/** Resolves one declared support rather than collapsing overlapping floors to their highest Y. */
export function arenaSupportHeight(arena: Pick<ArenaDefinition, 'surfaces'>, supportSurfaceId: string, x: number, z: number): number | undefined {
  const surface = arena.surfaces.find(candidate => candidate.id === supportSurfaceId)
  if (surface === undefined || !supportsNavigation(surface)) return undefined
  const q = surface.orientation
  const nx = 2 * (q.x*q.y-q.z*q.w)
  const ny = 1-2*(q.x*q.x+q.z*q.z)
  const nz = 2*(q.y*q.z+q.x*q.w)
  if (ny <= 0) return undefined
  const y = surface.center.y + (surface.size.y/2-nx*(x-surface.center.x)-nz*(z-surface.center.z))/ny
  const dx=x-surface.center.x, dy=y-surface.center.y, dz=z-surface.center.z
  const lx=(1-2*(q.y*q.y+q.z*q.z))*dx+2*(q.x*q.y+q.z*q.w)*dy+2*(q.x*q.z-q.y*q.w)*dz
  const lz=2*(q.x*q.z-q.y*q.w)*dx+2*(q.y*q.z+q.x*q.w)*dy+(1-2*(q.x*q.x+q.y*q.y))*dz
  return Math.abs(lx) <= surface.size.x/2+1e-6 && Math.abs(lz) <= surface.size.z/2+1e-6 ? y : undefined
}

export function supportedPointOn(arena: Pick<ArenaDefinition, 'surfaces'>, supportSurfaceId: string, point: ArenaVector3): ArenaVector3 | undefined {
  const y = arenaSupportHeight(arena, supportSurfaceId, point.x, point.z)
  return y === undefined ? undefined : { ...point, y }
}

export function supportedPoint(arena: Pick<ArenaDefinition,'surfaces'>, point: ArenaVector3): ArenaVector3 {
  return { ...point, y: arenaFloorHeight(arena,point.x,point.z) ?? point.y }
}

/** Top endpoints define the slope; thickness is buried below the walking plane. */
export function rampBox(id: string, x0: number, x1: number, y0: number, y1: number, z: number, width: number, thickness: number): ArenaBoxSurface {
  const angle = simulationMath.atan2(y1-y0,x1-x0)
  return { id, kind:'box', material:'floor', center:{x:(x0+x1)/2+simulationMath.sin(angle)*thickness/2,y:(y0+y1)/2-simulationMath.cos(angle)*thickness/2,z},size:{x:simulationMath.hypot(x1-x0,y1-y0),y:thickness,z:width},orientation:{x:0,y:0,z:simulationMath.sin(angle/2),w:simulationMath.cos(angle/2)},semantics:{classification:'ramp',movement:true,projectile:true,sector:'atrium'} }
}
