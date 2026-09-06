import { deepFreeze } from '../core/deepFreeze'

export const ARENA_GLB_ASSET_KEYS = ['wall-lamp', 'portal-facade', 'wall-bay', 'floor-route-guide', 'central-landmark', 'light-sport-robot', 'double-barrel-shotgun', 'first-person-shotgun'] as const
export type ArenaGlbAssetKey = (typeof ARENA_GLB_ASSET_KEYS)[number]

export interface ArenaGlbAssetEntry {
  readonly key: ArenaGlbAssetKey
  readonly lod: Readonly<{ readonly lod0Uri: string; readonly lod1Uri: string; readonly switchDistanceMeters: number; readonly hysteresisMeters: number }>
  readonly attachment: 'doorway-gap' | 'wall-surface' | 'floor-surface' | 'combat-region' | 'participant-root' | 'participant-hands' | 'camera-viewmodel'
  readonly units: 'meters'
  readonly axes: Readonly<{ readonly up: '+Y'; readonly front: '+Z' }>
  readonly pivot: 'doorway-plane-center' | 'wall-base-center' | 'floor-contact-center' | 'combat-region-center' | 'feet-center' | 'hand-grip-center' | 'camera-weapon-grip'
  readonly attachmentBoundsMeters: Readonly<{ readonly x: number; readonly y: number; readonly z: number }>
  readonly collisionProxyAudit: Readonly<{ readonly id: string; readonly boundsMeters: readonly [number, number, number] }>
}
export type ArenaGlbManifest = Readonly<Record<ArenaGlbAssetKey, ArenaGlbAssetEntry>>
// Keep the two source paths explicit. Vite statically rewrites these asset roots
// for the browser build; a single dynamic `assets/${name}` root bypasses that
// rewrite and makes the required cache fail during startup.
const arenaAssetUri = (name: string) => new URL(`../../public/assets/arena/${name}`, import.meta.url).href
const participantAssetUri = (name: string) => new URL(`../../public/assets/participants/${name}`, import.meta.url).href

export const CLEAN_FUTURE_SPORT_GLB_V1: ArenaGlbManifest = deepFreeze({
  'wall-lamp': { key:'wall-lamp',lod:{lod0Uri:arenaAssetUri('wall-lamp-lod0.glb'),lod1Uri:arenaAssetUri('wall-lamp-lod1.glb'),switchDistanceMeters:16,hysteresisMeters:2},attachment:'wall-surface',units:'meters',axes:{up:'+Y',front:'+Z'},pivot:'wall-base-center',attachmentBoundsMeters:{x:0.8,y:0.3,z:0.16},collisionProxyAudit:{id:'wall-lamp-audit-proxy',boundsMeters:[0.8,0.3,0.16]} },
  'portal-facade': { key: 'portal-facade', lod: { lod0Uri: arenaAssetUri('portal-facade-lod0.glb'), lod1Uri: arenaAssetUri('portal-facade-lod1.glb'), switchDistanceMeters: 18, hysteresisMeters: 2 }, attachment: 'doorway-gap', units: 'meters', axes: { up: '+Y', front: '+Z' }, pivot: 'doorway-plane-center', attachmentBoundsMeters: { x: 4, y: 4, z: 0.8 }, collisionProxyAudit: { id: 'portal-facade-audit-proxy', boundsMeters: [4, 4, 0.8] } },
  'wall-bay': { key: 'wall-bay', lod: { lod0Uri: arenaAssetUri('wall-bay-lod0.glb'), lod1Uri: arenaAssetUri('wall-bay-lod1.glb'), switchDistanceMeters: 16, hysteresisMeters: 2 }, attachment: 'wall-surface', units: 'meters', axes: { up: '+Y', front: '+Z' }, pivot: 'wall-base-center', attachmentBoundsMeters: { x: 3.2, y: 3.4, z: 0.4 }, collisionProxyAudit: { id: 'wall-bay-audit-proxy', boundsMeters: [3.2, 3.4, 0.4] } },
  'floor-route-guide': { key: 'floor-route-guide', lod: { lod0Uri: arenaAssetUri('floor-route-guide-lod0.glb'), lod1Uri: arenaAssetUri('floor-route-guide-lod1.glb'), switchDistanceMeters: 14, hysteresisMeters: 2 }, attachment: 'floor-surface', units: 'meters', axes: { up: '+Y', front: '+Z' }, pivot: 'floor-contact-center', attachmentBoundsMeters: { x: 3.2, y: 0.08, z: 0.35 }, collisionProxyAudit: { id: 'floor-route-guide-audit-proxy', boundsMeters: [3.2, 0.08, 0.35] } },
  'central-landmark': { key: 'central-landmark', lod: { lod0Uri: arenaAssetUri('central-landmark-lod0.glb'), lod1Uri: arenaAssetUri('central-landmark-lod1.glb'), switchDistanceMeters: 22, hysteresisMeters: 3 }, attachment: 'combat-region', units: 'meters', axes: { up: '+Y', front: '+Z' }, pivot: 'combat-region-center', attachmentBoundsMeters: { x: 2.4, y: 0.2, z: 2.4 }, collisionProxyAudit: { id: 'central-landmark-audit-proxy', boundsMeters: [2.4, 0.2, 2.4] } },
  'light-sport-robot': { key: 'light-sport-robot', lod: { lod0Uri: participantAssetUri('light-sport-robot-lod0.glb'), lod1Uri: participantAssetUri('light-sport-robot-lod1.glb'), switchDistanceMeters: 14, hysteresisMeters: 2 }, attachment: 'participant-root', units: 'meters', axes: { up: '+Y', front: '+Z' }, pivot: 'feet-center', attachmentBoundsMeters: { x: 1.2, y: 2.2, z: 0.8 }, collisionProxyAudit: { id: 'light-sport-robot-audit-proxy', boundsMeters: [0.7, 1.9, 0.7] } },
  'double-barrel-shotgun': { key: 'double-barrel-shotgun', lod: { lod0Uri: participantAssetUri('double-barrel-shotgun-lod0.glb'), lod1Uri: participantAssetUri('double-barrel-shotgun-lod1.glb'), switchDistanceMeters: 16, hysteresisMeters: 2 }, attachment: 'participant-hands', units: 'meters', axes: { up: '+Y', front: '+Z' }, pivot: 'hand-grip-center', attachmentBoundsMeters: { x: 0.55, y: 0.35, z: 1.4 }, collisionProxyAudit: { id: 'double-barrel-shotgun-audit-proxy', boundsMeters: [0.55, 0.35, 1.4] } },
  'first-person-shotgun': { key: 'first-person-shotgun', lod: { lod0Uri: participantAssetUri('first-person-shotgun-lod0.glb'), lod1Uri: participantAssetUri('first-person-shotgun-lod1.glb'), switchDistanceMeters: 1, hysteresisMeters: 0.1 }, attachment: 'camera-viewmodel', units: 'meters', axes: { up: '+Y', front: '+Z' }, pivot: 'camera-weapon-grip', attachmentBoundsMeters: { x: 1.3, y: 0.8, z: 1.6 }, collisionProxyAudit: { id: 'first-person-shotgun-audit-proxy', boundsMeters: [1.3, 0.8, 1.6] } },
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
