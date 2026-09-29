# Unified body damage / friendly fire — native evidence

Implementation branch: `codex/unify-body-damage-and-friendly-fire`.
The final check receipts, Player identity and reviewed PNGs are recorded in the shared planning change `unify-body-damage-and-friendly-fire`.

## Contracts

- Rifle and shotgun retain independent full damage; head/torso/arm/leg contacts use that weapon's one value. Pulse keeps one capped explosion and falloff. Cutter keeps independent contact ticks.
- `damage.friendlyMultiplier` and `damage.selfMultiplier` apply after weapon/boost/falloff/cap, before shield. Defaults are 0.5. Hitscan does not add a self target; Cutter continues through allied participants.
- `DamageResult.HealthLost` and `ArmorLost` sum to `Applied`: shield-only hits count, overkill does not. Enemy dealt, `AllyDamageDealt`, self and received remain distinct.
- Teamkill clears chain length/base for the active source life; self death clears its chain. Neither lethal event pays kill/assist rewards or reduces ordinary Kills. Old-life rockets retain attribution without resetting a new-life chain.
- The neighboring scoreboard change owns V4 icons, red allied suffixes and score penalty. Current damage uses its `originalSource`, `AllyDamageDealt` and exact resumable match projection contracts.
- Native Lab exposes `rifle.damage`, `shot.damage`, and both damage-policy coefficients. Historical zone revisions/hashes are retained; migration appends compatible revisions from their torso damage.

## Native QA journey

Development Player CLI: `-unifiedDamageReview ABSOLUTE_DIRECTORY` with separate QA profile/history files and muted audio.

The scripted fixture journey captures Rifle/Shotgun/coefficient Lab groups, FFA and teams live/final tables for 1/3/4 local views with eight participants, shield-only damage, ally/self accounting, capped lethal damage and Repeat. Screenshots and sidecar snapshots are evidence of native rendering and owned simulation state.

The evidence does not establish human acceptance, physical controller acceptance, TV readability or target hardware performance.
