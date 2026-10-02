# Weapon color diagnosis — 2026-10-02

Confirmed in a muted native macOS Development Player based on `706f7a849dfb6b60ee6db479da850ad13e7916b9`. Branch: `codex/weapon-color-diagnostic`. Product rendering code is unchanged; the only runtime addition is an explicit CLI diagnostic capture.

## Result

| Weapon | Other participant | First person |
| --- | --- | --- |
| Rifle | Gray; 35 material slots use `StarTournament/TrooperIdentity` | Blue/green; 35 slots use URP Lit |
| Rocket launcher | Gray; 6 slots use TrooperIdentity | Authored orange/cyan/white; glTF PBR |
| Cutter | Gray; 4 slots use TrooperIdentity | Authored yellow/copper/steel; glTF PBR |
| Shotgun | Authored dark ceramic/metal with oxblood accents; glTF PBR | Same source materials |

The shotgun becomes red during the bonus and returns to its original materials afterward. Rifle/rocket/cutter receive red property overrides in the third-person model too, but their accidentally assigned identity shader neutralizes the hue; the first-person models show the red override. This distinction is visible in the captures and is recorded in `material-summary.json`.

## Cause

`ProvingGround.Begin` calls `RebuildActors(..., false)`, then applies participant identity before activating the actor roots. `TrooperIdentityPresentation.ApplyBody` excludes mounted weapons only if `renderer.GetComponentInParent<WeaponModelPresentation>()` finds their owner. Its default lookup excludes inactive ancestors, so this check fails for the inactive third-person actors. All rifle, pulse and cutter materials are consequently replaced by body identity materials. The custom shader explicitly reduces source color to a neutral scalar for non-body material roles. The rifle also loses its `_BaseColor` value when copied into the glTF-based identity material schema.

The first-person rig is under an active camera and is correctly excluded. The shotgun is excluded independently by the renderer name `weapon:joined`, so it is not affected by this material replacement.

The earlier initial-Tint ordering observation does not explain this defect: `Begin` applies identity again after equipment initialization.

Expected fix: exclude weapon renderers regardless of actor activation (for example, include inactive ancestors in the owner lookup), preserving authored materials and the existing red bonus override. A fix has not been applied or integrated in this diagnostic branch.

## Evidence and limits

- Successful native build: `build-result.txt`; binary/source SHA-256 and base revision: `candidate.json`.
- Native run exited 0 and wrote `player/complete.json`; 24 screenshots cover four weapons, normal/boost/restored, remote and first-person views.
- Each capture has its session snapshot and full material/property-block dump. Local participant is 7 and the observed other participant is 0 (`player/participants.json`).
- Explicit frozen placement/camera fixture, eight actors, 1920×1080, muted audio. Bonus is set to five seconds and then reset through snapshots; this is a material diagnostic, not a natural pickup/expiry or human/device acceptance test. The match clock stays at 05:00.
- Unity-generated preparation/configuration diffs used for the build are retained in `build-generated.patch.gz`; they were restored after capture and are not part of the diagnostic source change.

Run from this worktree after building:

```sh
UNITY_RUN_MODE=shared unity/tools.sh run -weaponColorReview "$PWD/docs/evidence/weapon-color-diagnostic/player" -screen-width 1920 -screen-height 1080 -logFile "$PWD/docs/evidence/weapon-color-diagnostic/Player.log"
```
