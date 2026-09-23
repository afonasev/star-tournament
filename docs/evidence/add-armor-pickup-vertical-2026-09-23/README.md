# Armor pickup vertical rework — 2026-09-23

Baseline: `35c89c1a2247d07940ac2f8e9a339dfb032778f4`.

The armor pickup's cyan shield and white core now use a vertical silhouette plane.
The presentation root retains its existing profile-driven hover height and rotation
around the world vertical axis. `ArmorPickupSilhouetteIsVerticalAboveItsSupport`
guards the orientation contract.

Automated evidence:

- EditMode: 109/109 passed.
- PlayMode: 68/68 passed.
- macOS Player build: `STAR_PROVING_BUILD_PASS 459534209`.

The local Player opened, but its diagnostic run returned to the setup UI and showed
`NullReferenceException` in the development console before a pickup-focused visual
acceptance capture could be made. This is recorded as a separate native visual QA
blocker; no human acceptance is claimed by this note.
