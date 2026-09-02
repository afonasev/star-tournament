## 1. Presentation tokens and palette contract

- [x] 1.1 Add immutable `clean-future-sport-v1` renderer tokens and an expanded vetted participant palette; verify focused unit tests cover slot assignment, palette size, uniqueness and contrast-safe team pair defaults.
- [x] 1.2 Apply the material, lighting, haze and participant-rim treatment to the existing ArenaDefinition-derived renderer without altering spatial projections; verify renderer tests preserve arena identity and resource disposal.
- [x] 1.3 Add a renderer-only modular detail layer (flush panel seams/frames, floor trims and an overhead combat-region landmark) derived from existing surfaces; verify it adds no simulation or collision objects and preserves renderer disposal.
- [x] 1.4 Detect existing collinear wall gaps and render rounded portal frames around them without adding false doorways or collision objects; verify renderer tests cover a real gap and a continuous wall.
- [x] 1.5 Replace thin-detail emphasis with large-scale renderer-only architectural massing: two-tone portal shells, inset wall modules and wide floor route guides derived from existing surfaces; verify their presence and disposal in renderer tests.
- [x] 1.6 Build the renderer-native architectural kit: curved portal facades with massive buttresses, repeated wall bays and segmented route lanes derived from existing surfaces; verify style objects remain presentation-only and performance stays within the gate.

## 2. Team and participant identity migration

- [x] 2.1 Migrate canonical team identifiers, default configuration and validation from `red`/`blue` to `team-a`/`team-b` with Team A/Team B labels and explicit team-color pair; verify configuration tests accept the new contract and reject legacy IDs/invalid color assignments.
- [x] 2.2 Update serializable snapshots, replay compatibility, fixtures and standings projections for resolved authoritative colors; verify deterministic replay/configuration tests and state-hash contracts pass.
- [x] 2.3 Update setup, scoreboard and results presentation to show Team A/Team B and authoritative colors; verify component tests cover team totals, winner labels and full FFA participant-color uniqueness.

## 3. Automated verification

- [x] 3.1 Update renderer, runtime and UI tests for the Clean Future Sport material/detail kit and participant silhouette contract; verify `npm test` passes.
- [x] 3.2 Run `npm run typecheck`, `npm run build`, `openspec validate adopt-clean-future-sport --strict` and the production browser performance gate; record outcomes and preserve lifecycle/presentation budget evidence.

## 4. Muted browser acceptance and handoff

- [x] 4.1 Start the dev stand from this worktree, verify its actual HTTP URL, and run the in-app Browser with game audio disabled; inspect representative small/medium/large arena frames plus paused, scoreboard and results states for material/readability and console errors. User accepted the visual evidence with the known in-app Browser pointer-lock limitation; physical input remained unavailable in that browser policy.
- [x] 4.2 Save current branch screenshots for every changed visual state under `docs/evidence/adopt-clean-future-sport/`; verify they demonstrate the approved style, Team A/Team B labels and visible participant contrast.
- [x] 4.3 Update the handoff task, mark verified OpenSpec tasks complete, commit this one change on its isolated branch, and report the commit and browser evidence paths.
