import { expect, it } from 'vitest'
import historicalProfile from '../../balance/history/wall-clearance-prototype-v1-v9.json'
import { requireProceduralArena } from './proceduralArena'
import type { ArenaSize } from './arenaDefinition'
import type { ValidatedGameDesignProfile } from '../profiles'

// Captured from published main f8d4daa before integration.
const hashes = {
  "small/0": "fnv1a64-v1:c7bad18527285736",
  "small/1": "fnv1a64-v1:08dac0c617182418",
  "small/2": "fnv1a64-v1:2cd802231f315c25",
  "small/3": "fnv1a64-v1:cc15f242c64d3e70",
  "small/1398030674": "fnv1a64-v1:0350e04b4e34bfe8",
  "small/4294967295": "fnv1a64-v1:da9bb44dcc1015b9",
  "medium/0": "fnv1a64-v1:0f9cc0efdcb51d42",
  "medium/1": "fnv1a64-v1:8dddcdcdf2202579",
  "medium/2": "fnv1a64-v1:ce8501d0b1a0f408",
  "medium/3": "fnv1a64-v1:e8adb701872d3313",
  "medium/1398030674": "fnv1a64-v1:4a4eb589174076c0",
  "medium/4294967295": "fnv1a64-v1:dd7b59d374afcd45",
  "large/0": "fnv1a64-v1:f776400aeb8e94c0",
  "large/1": "fnv1a64-v1:51aee022c80d1a5c",
  "large/2": "fnv1a64-v1:3c7efc0d0e371391",
  "large/3": "fnv1a64-v1:6e03f118d4d0efc2",
  "large/1398030674": "fnv1a64-v1:fcbaff7f579cc794",
  "large/4294967295": "fnv1a64-v1:b166cd762e5b1e33"
}

it('preserves published wall-clearance maps after the stair merge', () => {
  for (const [key, hash] of Object.entries(hashes)) {
    const [size, seed] = key.split('/')
    expect(requireProceduralArena({size: size as ArenaSize, seed: Number(seed), profile: historicalProfile as unknown as ValidatedGameDesignProfile}).contentHash).toBe(hash)
  }
}, 30000)
