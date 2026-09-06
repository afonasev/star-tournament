import { expect, it } from 'vitest'
import { arenaFloorHeight, arenaSupportHeight, supportedPointOn } from './elevation'
import type { ArenaDefinition } from './arenaDefinition'

const identity = { x: 0, y: 0, z: 0, w: 1 }
const overlapping = {
  surfaces: [
    { id: 'floor-basement', kind: 'box', center: { x: 0, y: -2.5, z: 0 }, size: { x: 8, y: 1, z: 8 }, orientation: identity, material: 'floor', semantics: { classification: 'floor', movement: true, projectile: true, sector: 'basement' } },
    { id: 'floor-gallery', kind: 'box', center: { x: 0, y: 2.5, z: 0 }, size: { x: 8, y: 1, z: 8 }, orientation: identity, material: 'floor', semantics: { classification: 'floor', movement: true, projectile: true, sector: 'gallery' } },
  ],
} as Pick<ArenaDefinition, 'surfaces'>

it('keeps overlapping basement and gallery supports distinct', () => {
  expect(arenaFloorHeight(overlapping, 0, 0)).toBe(3)
  expect(arenaSupportHeight(overlapping, 'floor-basement', 0, 0)).toBe(-2)
  expect(arenaSupportHeight(overlapping, 'floor-gallery', 0, 0)).toBe(3)
  expect(supportedPointOn(overlapping, 'floor-basement', { x: 0, y: 0, z: 0 })).toEqual({ x: 0, y: -2, z: 0 })
})
