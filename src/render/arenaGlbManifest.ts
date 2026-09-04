import { deepFreeze } from '../core/deepFreeze'

export const ARENA_GLB_ASSET_KEYS = ['portal-facade', 'wall-bay', 'floor-route-guide', 'central-landmark'] as const
export type ArenaGlbAssetKey = (typeof ARENA_GLB_ASSET_KEYS)[number]

export interface ArenaGlbAssetEntry {
  readonly key: ArenaGlbAssetKey
  readonly lod: Readonly<{ readonly lod0Uri: string; readonly lod1Uri: string; readonly switchDistanceMeters: number; readonly hysteresisMeters: number }>
  readonly attachment: 'doorway-gap' | 'wall-surface' | 'floor-surface' | 'combat-region'
  readonly units: 'meters'
  readonly axes: Readonly<{ readonly up: '+Y'; readonly front: '+Z' }>
  readonly pivot: 'doorway-plane-center' | 'wall-base-center' | 'floor-contact-center' | 'combat-region-center'
  readonly attachmentBoundsMeters: Readonly<{ readonly x: number; readonly y: number; readonly z: number }>
  readonly collisionProxyAudit: Readonly<{ readonly id: string; readonly boundsMeters: readonly [number, number, number] }>
}
export type ArenaGlbManifest = Readonly<Record<ArenaGlbAssetKey, ArenaGlbAssetEntry>>
const assetUri = (name: string) => new URL(`../../public/assets/arena/${name}`, import.meta.url).href

export const CLEAN_FUTURE_SPORT_GLB_V1: ArenaGlbManifest = deepFreeze({
  'portal-facade': { key: 'portal-facade', lod: { lod0Uri: assetUri('portal-facade-lod0.glb'), lod1Uri: assetUri('portal-facade-lod1.glb'), switchDistanceMeters: 18, hysteresisMeters: 2 }, attachment: 'doorway-gap', units: 'meters', axes: { up: '+Y', front: '+Z' }, pivot: 'doorway-plane-center', attachmentBoundsMeters: { x: 4, y: 4, z: 0.8 }, collisionProxyAudit: { id: 'portal-facade-audit-proxy', boundsMeters: [4, 4, 0.8] } },
  'wall-bay': { key: 'wall-bay', lod: { lod0Uri: assetUri('wall-bay-lod0.glb'), lod1Uri: assetUri('wall-bay-lod1.glb'), switchDistanceMeters: 16, hysteresisMeters: 2 }, attachment: 'wall-surface', units: 'meters', axes: { up: '+Y', front: '+Z' }, pivot: 'wall-base-center', attachmentBoundsMeters: { x: 3.2, y: 3.4, z: 0.4 }, collisionProxyAudit: { id: 'wall-bay-audit-proxy', boundsMeters: [3.2, 3.4, 0.4] } },
  'floor-route-guide': { key: 'floor-route-guide', lod: { lod0Uri: assetUri('floor-route-guide-lod0.glb'), lod1Uri: assetUri('floor-route-guide-lod1.glb'), switchDistanceMeters: 14, hysteresisMeters: 2 }, attachment: 'floor-surface', units: 'meters', axes: { up: '+Y', front: '+Z' }, pivot: 'floor-contact-center', attachmentBoundsMeters: { x: 3.2, y: 0.08, z: 0.35 }, collisionProxyAudit: { id: 'floor-route-guide-audit-proxy', boundsMeters: [3.2, 0.08, 0.35] } },
  'central-landmark': { key: 'central-landmark', lod: { lod0Uri: assetUri('central-landmark-lod0.glb'), lod1Uri: assetUri('central-landmark-lod1.glb'), switchDistanceMeters: 22, hysteresisMeters: 3 }, attachment: 'combat-region', units: 'meters', axes: { up: '+Y', front: '+Z' }, pivot: 'combat-region-center', attachmentBoundsMeters: { x: 2.4, y: 0.2, z: 2.4 }, collisionProxyAudit: { id: 'central-landmark-audit-proxy', boundsMeters: [2.4, 0.2, 2.4] } },
})

export function validateArenaGlbManifest(manifest: Partial<Record<ArenaGlbAssetKey, ArenaGlbAssetEntry>>): readonly string[] {
  const errors: string[] = []
  for (const key of ARENA_GLB_ASSET_KEYS) {
    const entry = manifest[key]
    if (entry === undefined) { errors.push(`Missing required GLB asset: ${key}`); continue }
    if (entry.key !== key || !entry.lod?.lod0Uri || !entry.lod?.lod1Uri) errors.push(`Invalid GLB asset entry: ${key}`)
    if (entry.units !== 'meters' || entry.axes?.up !== '+Y' || entry.axes?.front !== '+Z') errors.push(`Invalid spatial contract: ${key}`)
    if (!Number.isFinite(entry.lod?.switchDistanceMeters) || entry.lod.switchDistanceMeters <= 0) errors.push(`Invalid LOD distance: ${key}`)
  }
  return errors
}
