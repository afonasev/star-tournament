# 04 — Adopt Clean Future Sport visual direction

## Scope

Adopted `clean-future-sport-v1` for the existing procedural arena renderer: Team A/Team B presentation identities, an expandable participant palette, bright material and lighting treatment, and a renderer-native architectural kit. The slice leaves gameplay, topology, collision, navigation, spawn allocation and input unchanged.

## Implementation

- Canonical team IDs are `team-a` and `team-b`; UI labels are Team A and Team B. FFA uses an expandable vetted palette with unique active colors.
- Renderer tokens establish navy floor, light shells, cyan/lime/orange spatial accents, soft lighting and participant rim readability.
- Existing analytical arena surfaces receive renderer-only panel bays, buttresses, segmented floor route guides and curved two-tone portal facades only around real wall gaps.
- All presentation geometry remains outside deterministic simulation, collision, navigation, spawn and arena hashes.

## Verification

- `npm test`: 44 files / 273 tests passed.
- `npm run typecheck`: pass.
- `npm run build`: pass; the pre-existing Vite chunk-size warning is informational.
- `openspec validate adopt-clean-future-sport --strict`: pass.
- `npm run perf:browser`: passed all phases, including paused zero-work and active cadence gates, after the architectural pass.
- Muted in-app Browser visual smoke at confirmed `http://localhost:5188/` passed for small, medium and large arenas with no console errors. The Browser policy rejects pointer lock, so physical input/pause/scoreboard/results interaction was not available there; the user explicitly accepted this limitation for the visual slice.

## Evidence

- `docs/evidence/adopt-clean-future-sport/team-setup.png`
- `docs/evidence/adopt-clean-future-sport/detailed-small-playfield.png`
- `docs/evidence/adopt-clean-future-sport/detailed-medium-cropped.png`
- `docs/evidence/adopt-clean-future-sport/detailed-large-playfield.png`
- `docs/evidence/adopt-clean-future-sport/rounded-portals-seed-1234.png`
- `docs/evidence/adopt-clean-future-sport/architectural-kit-seed-1234.png`
