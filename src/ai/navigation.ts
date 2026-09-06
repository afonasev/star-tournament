import { arenaFloorHeight, supportedPoint, supportedPointOn } from '../arena/elevation'
import { simulationMath } from '../core/simulationMath'
import type { ArenaDefinition } from '../arena'
import type { CollisionVector3 as Vec } from '../collision/contracts'
import type { ValidatedGameDesignProfile } from '../profiles'

export const distance = (a: Vec, b: Vec): number => simulationMath.hypot(a.x - b.x, a.z - b.z)
export interface NavigationCapabilities {
  readonly destinations: readonly Vec[]
  clear(a: Vec, b: Vec): boolean
  route(a: Vec, b: Vec): Vec[]
}
interface Box { x: number; z: number; hx: number; hz: number }
function intersects(a: Vec, b: Vec, box: Box): boolean {
  let lo = 0; let hi = 1
  for (const axis of ['x', 'z'] as const) {
    const half = axis === 'x' ? box.hx : box.hz
    const delta = b[axis] - a[axis]
    if (delta === 0) { if (Math.abs(a[axis] - box[axis]) > half) return false; continue }
    const u = (box[axis] - half - a[axis]) / delta; const v = (box[axis] + half - a[axis]) / delta
    lo = Math.max(lo, Math.min(u, v)); hi = Math.min(hi, Math.max(u, v))
    if (lo > hi) return false
  }
  return true
}
const cache = new WeakMap<ArenaDefinition, Map<string, NavigationCapabilities>>()
export function arenaNavigation(arena: ArenaDefinition, profile: ValidatedGameDesignProfile): NavigationCapabilities {
  let variants = cache.get(arena)
  if (!variants) { variants = new Map(); cache.set(arena, variants) }
  const cached = variants.get(profile.contentHash)
  if (cached) return cached
  // Skin is the controller's soft contact offset, not solid geometry. Expanding
  // obstacles by it strands a capsule after float32 contact correction at a wall.
  const radius = profile.rules.player.capsule.radius
  const clearance = profile.rules.bots.navigation.clearance + (arena.recipeId ? profile.rules.bots.navigation.waypointRadius : 0)
  const projectedBoxes: Box[] = arena.surfaces.filter(s => {
    if(s.material === 'floor' || s.center.y+s.size.y/2 <= 0) return false
    if(!s.id.startsWith('portal-solid:')) return true
    const q=s.orientation
    const hy=(Math.abs(2*(q.x*q.y+q.z*q.w))*s.size.x+Math.abs(1-2*(q.x*q.x+q.z*q.z))*s.size.y+Math.abs(2*(q.y*q.z-q.x*q.w))*s.size.z)/2
    const floorY=arenaFloorHeight(arena,s.center.x,s.center.z)??0
    return s.center.y-hy <= floorY+2*(profile.rules.player.capsule.halfHeight+radius)+profile.rules.player.capsule.skinWidth
  }).map(s => {
    // Conservative world AABB for a box's authored quaternion. Current maps
    // are axis aligned; conservative expansion never invents a traversable gap.
    const q = s.orientation
    const hx = (Math.abs(1 - 2 * (q.y*q.y + q.z*q.z)) * s.size.x + Math.abs(2 * (q.x*q.y - q.z*q.w)) * s.size.y + Math.abs(2 * (q.x*q.z + q.y*q.w)) * s.size.z) / 2
    const hz = (Math.abs(2 * (q.x*q.z - q.y*q.w)) * s.size.x + Math.abs(2 * (q.y*q.z + q.x*q.w)) * s.size.y + Math.abs(1 - 2 * (q.x*q.x + q.y*q.y)) * s.size.z) / 2
    return { x: s.center.x, z: s.center.z, hx: hx + radius, hz: hz + radius }
  })
  // Overlapping portal pieces inside a wall add no planar constraint. Keep
  // only maximal bounds to avoid multiplying graph corners for the same solid.
  const boxes = !arena.recipeId ? projectedBoxes : projectedBoxes.filter((box,i,all) => !all.some((other,j) => j!==i
    && other.x-other.hx <= box.x-box.hx && other.x+other.hx >= box.x+box.hx
    && other.z-other.hz <= box.z-box.hz && other.z+other.hz >= box.z+box.hz
    && (other.hx>box.hx || other.hz>box.hz || j<i)))
  const floor = arena.surfaces.find(s => s.material === 'floor')!
  const inside = (p: Vec) => Math.abs(p.x - floor.center.x) < floor.size.x / 2 - radius && Math.abs(p.z - floor.center.z) < floor.size.z / 2 - radius
  const clear = (a: Vec, b: Vec) => inside(b) && !boxes.some(box => intersects(a, b, box))
  const declaredSupport = new Map<string, string>()
  const pointKey = (point: Pick<Vec, 'x' | 'z'>) => `${point.x}:${point.z}`
  for (const region of arena.regions) if (region.supportSurfaceId) declaredSupport.set(pointKey(region.center), region.supportSurfaceId)
  for (const anchor of arena.spawnAnchors) if (anchor.supportSurfaceId) declaredSupport.set(pointKey(anchor.position), anchor.supportSurfaceId)
  const project = (point: Vec) => {
    if (!arena.recipeId) return point
    const support = declaredSupport.get(pointKey(point))
    return support ? supportedPointOn(arena, support, point) ?? supportedPoint(arena, point) : supportedPoint(arena, point)
  }
  const breaks = arena.surfaces.filter(s => s.semantics?.classification === 'ramp').flatMap(s => {
    const q=s.orientation
    const topX=s.center.x-q.z*q.w*s.size.y
    const halfX=(1-2*q.z*q.z)*s.size.x/2
    return [topX-halfX,topX+halfX]
  })
  const segment = (a: Vec,b: Vec): Vec[] => {
    if (!arena.recipeId) return [b]
    const fractions = [...new Set(breaks.map(x => (x-a.x)/(b.x-a.x)).filter(t => t>0 && t<1))].sort((x,y)=>x-y)
    return [...fractions.map(t=>project({x:a.x+(b.x-a.x)*t,y:0,z:a.z+(b.z-a.z)*t})),project(b)]
  }
  const routeDistance = (a:Vec,b:Vec) => {
    if (!arena.recipeId) return distance(a,b)
    let previous=project(a), total=0
    for (const next of segment(a,b)) { total+=simulationMath.hypot(next.x-previous.x,next.y-previous.y,next.z-previous.z);previous=next }
    return total
  }
  const regionById = new Map(arena.regions.map(r => [r.id, r.center]))
  const candidates: Vec[] = [...arena.regions.map(r => r.center), ...arena.spawnAnchors.map(a => a.position)]
  for (const link of arena.links) { const a = regionById.get(link.fromRegionId)!; const b = regionById.get(link.toRegionId)!; candidates.push({ x: (a.x+b.x)/2, y: 0, z: (a.z+b.z)/2 }) }
  for (const box of boxes) for (const x of [-1, 1]) for (const z of [-1, 1]) candidates.push({ x: box.x + x*(box.hx+clearance), y: 0, z: box.z + z*(box.hz+clearance) })
  const nodes = candidates.map(project).filter(p => clear(p, p))
  const edges = nodes.map(() => [] as Array<[number, number]>)
  for (let i=0; i<nodes.length; i++) for (let j=i+1; j<nodes.length; j++) if (clear(nodes[i]!, nodes[j]!)) { const d=routeDistance(nodes[i]!,nodes[j]!); edges[i]!.push([j,d]); edges[j]!.push([i,d]) }
  const nav: NavigationCapabilities = {
    destinations: arena.spawnAnchors.map(a => a.position).filter(p => clear(p,p)), clear,
    route(a, b) {
      if (clear(a,b)) return segment(a,b)
      const costs = nodes.map(p => clear(a,p) ? routeDistance(a,p) : Infinity)
      const parents = nodes.map(() => -1); const visited = new Set<number>()
      let best = -1; let bestCost = Infinity
      for (let step=0;step<nodes.length;step++) {
        let i=-1; let cost=Infinity
        for(let j=0;j<nodes.length;j++) if(!visited.has(j) && costs[j]!<cost) { cost=costs[j]!;i=j }
        if(i<0 || cost>=bestCost) break
        visited.add(i)
        if(clear(nodes[i]!,b) && cost+routeDistance(nodes[i]!,b)<bestCost) { best=i;bestCost=cost+routeDistance(nodes[i]!,b) }
        for(const [j,d] of edges[i]!) if(cost+d<costs[j]!) { costs[j]=cost+d;parents[j]=i }
      }
      if(best<0) return []
      const route=[b]; for(let i=best;i>=0;i=parents[i]!) route.unshift(nodes[i]!)
      if (!arena.recipeId) return route
      return route.flatMap((point,index) => segment(index === 0 ? a : route[index-1]!, point))
    },
  }
  variants.set(profile.contentHash,nav);return nav
}
