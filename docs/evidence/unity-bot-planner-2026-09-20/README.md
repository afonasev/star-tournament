# Unity bot planner — verified internal slice

Change `add-unity-bot-planner`, worktree `/private/tmp/star-tournament-unity-bot-planner`, branch `codex/unity-bot-planner`, base `9f207d2`. It adds independent Bot-kind perception, navigation, tactical decisions and common-session combat. Shipping add/remove/difficulty setup is deliberately the next change.

## Automated and runtime checks

- **EditMode 74/74, PlayMode 60/60, Mac Development build PASS** with Unity 6000.3.23f1. The reviewed behavior profile is `unity-bot-behavior-v1@1`; Astra's final read-only review found no remaining blockers.
- Default eight-participant, unforced 300-second natural runs completed: FFA seed151: 1721 shots, 1045 damaging events, 184 deaths/respawns, maximum sampled driver 4.904 ms and 116 tactical queries; Teams seed157: 1277/779/140/138, 5.605 ms and 98 queries.
- Focused, muted Development Player journeys at seed20260920 are retained in `fhd/` and `4k/`: 1–4 FFA views, teams, pause/resume, Repeat, natural results and ordinary human setup. FFA 1-view records 77 shots, 888.33 damage, 7 deaths and 6 respawns; teams records 109 shots, 1328.33 damage, 12 deaths and 10 respawns. The diagnostic passive 1v1 has zero shots/damage and is not combat proof.
- A separate `controls-final-2/` keyboard/mouse-versus-seven-bots run is focused, muted and FHD. Short CUA `W`, click and mouse drag produced the visible shot; at `controls-3`, local `player-1` has 19 ammo from 20, a nonzero velocity, and the match has 90 real shots. This is a short automated control smoke, not sustained physical-device acceptance.

## Player observations

| Capture | Resolution | Frame p95/p99/worst | Driver CPU p95/p99/worst |
|---|---:|---:|---:|
| FHD | 1920×1080 | 10.706 / 13.381 / 100.874 ms | 0.640 / 1.271 / 3.882 ms |
| 4K | 3840×2160 | 10.264 / 11.767 / 229.786 ms | 0.794 / 1.569 / 3.163 ms |

The Player harness samples frame time and bot-driver CPU only; GPU and allocation metrics are unavailable. These figures are development-pilot evidence, not a target-60-FPS or visual-art acceptance. Screenshots show the native scene and multi-view team standings.

## Still open

Independent paired difficulty evaluation, shipping bot setup, generated-arena validation, physical controllers/TV/art/reference hardware and long target-hardware performance acceptance remain open. No main integration, archive or distribution was performed.
