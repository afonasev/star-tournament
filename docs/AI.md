# Bot AI v1

AI produces ordinary action frames. `stepPlayableSimulation` moves every living
participant and resolves all shooters against the same pre-damage combat state;
the reducer applies canonical damage, scoring, deaths and respawns. Humans and
bots share capsules, health, movement, weapon damage and cooldown. Ammo reaching
zero refills from `rules.weapons.doubleBarrelShotgun.emptyRefillAmmo`.

## Boundaries and extensions

- `src/ai/planner.ts`: `ObservationProvider` supplies FOV/LOS-limited sightings,
  timestamped memories and delayed allied reports. Never give a goal evaluator
  the raw enemy snapshot. Rear-damage scanning uses only the bot's own health.
- `GoalEvaluator` returns id, position and utility. Register additional evaluators
  for future pickups/objectives through `BotExtensions.goals`; the test goal
  demonstrates integration without changing movement/combat.
- `WeaponPolicy` supplies an aim point and readiness decision by weapon semantic
  id. Register future weapon policies through `BotExtensions.weapons`; actual
  firing, cooldown, ammo and damage remain authoritative combat rules.
- `NavigationCapabilities` provides destinations, capsule clearance and routes.
  The current derived graph supports the shipped single-level arenas; a future
  multilayer/jump-link implementation can replace it without renderer imports.
- `BotState` contains a separately seeded RNG, memories, intention, path,
  personality and tick deadlines. It is versioned and serialized. No wall clock,
  `Math.random`, DOM, renderer object or Rapier handle belongs in bot state.
- Defaults and numeric metadata live together in `src/profiles/botRules.ts` and
  the full immutable `prototype-v1` revision 6. No weapon/health/speed advantage
  is granted by difficulty. Actual stance and timing vary within the profile.
- `float32-transcendentals-v1` normalizes platform math before it can diverge
  across browser/Node replay. It is a numerical compatibility invariant, not a
  designer-controlled parameter.

## Headless commands

```sh
npm run ai:simulate -- --seed 101 --levels easy,normal,hard --mode ffa --size small --output test-results/ai-smoke
npm run ai:simulate -- --seed 103 --levels easy,hard,normal,hard --mode teams --size medium
npm run ai:evaluate -- --output test-results/ai-evaluation
npm run ai:simulate -- --replay test-results/ai-smoke/replay.json
```

The runner uses the shipped Rapier backend and the same fixed tick as the
browser. It does not approximate combat with a separate duel formula. Output
includes identities, timings/speedup, damage/score, contact/movement/jump/support
metrics and checkpoint hashes. `--ticks` is a safety budget, not an automatic
winner. Timeout/stall is never silently counted as a completed game. A replay
can be stopped at an exact tick with `--ticks`; `--output` then saves its snapshot.

`ai:evaluate` writes its suite and jobs before running them. It uses mirrored
participant identities/spawn sides, 200 games per neighboring difficulty pair,
Wilson confidence intervals, and FFA/team scenarios with 2/4/8 participants on
all three arena sizes. Small `--seeds` runs are pilots, not acceptance. Bug seeds
10036/10060 are regressions; final heldouts start at 20001. Never tune on a final
holdout and then call the same range independent.

## Browser QA

Use `?muted=1` for normal physical playtesting. The existing diagnostic query
`?muted=1&performanceBenchmark=1` supplies synthetic human commands, while bots
make real decisions. It is not evidence of working keyboard/mouse pointer lock.
After completion, the playfield exposes `data-ai-replay`, `data-ai-final-hash`,
`data-ai-final-snapshot`, sampled `data-ai-checkpoints`, and the performance
report. Export the full replay, run it through `ai:simulate --replay`, and compare
the exact final hash. Presentation-only timing does not enter replay identity.

For a full one-minute diagnostic match, `performanceSoakSeconds=60` can be used
with the one-minute setup duration. Do not measure an already-finished match as
running work: a 65-second window around a 60-second match intentionally fails
the running-cadence gate because the final seconds correctly perform no work.

Acceptance still requires normal in-app Browser move/look/jump/fire,
pause/resume, killcam, roster/results and Repeat checks with audio muted.
