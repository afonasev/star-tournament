import type { ArenaBoxSurface } from '../arena/arenaDefinition'

export const CLEAN_FUTURE_SPORT_V1 = {
  id: 'clean-future-sport-v1',
  materials: {
    floor: { color: 0x112a46, metalness: 0.22, roughness: 0.5, emissive: 0x03101f, emissiveIntensity: 0.08 },
    wall: { color: 0xe3e8ee, metalness: 0.1, roughness: 0.68, emissive: 0x182f45, emissiveIntensity: 0.1 },
    accent: { color: 0x18d1e6, metalness: 0.28, roughness: 0.36, emissive: 0x087b9c, emissiveIntensity: 0.8 },
  },
  details: {
    panel: { color: 0xb9c7d2, metalness: 0.18, roughness: 0.48 },
    trim: { color: 0x9cf4ff, metalness: 0.22, roughness: 0.3, emissive: 0x087b9c, emissiveIntensity: 1.1 },
    wallModule: { color: 0x7da4bb, metalness: 0.14, roughness: 0.42, emissive: 0x1b4b68, emissiveIntensity: 0.28 },
    routeGuide: { color: 0x42dff1, metalness: 0.16, roughness: 0.32, emissive: 0x087b9c, emissiveIntensity: 0.95 },
    portalShell: { color: 0x14bed6, metalness: 0.2, roughness: 0.3, emissive: 0x087b9c, emissiveIntensity: 0.92 },
    portalInner: { color: 0xeaf4f7, metalness: 0.12, roughness: 0.4, emissive: 0x5d8398, emissiveIntensity: 0.18 },
    landmark: { color: 0xb8ff65, metalness: 0.16, roughness: 0.28, emissive: 0x4c9b1f, emissiveIntensity: 1.25 },
  },
  background: 0xcbd9e8,
  fog: { color: 0xd7e4ef, density: 0.012 },
  participantRim: { color: 0xffffff, intensity: 1.35 },
} as const

export function arenaMaterialToken(slot: ArenaBoxSurface['material']) {
  return CLEAN_FUTURE_SPORT_V1.materials[slot]
}
