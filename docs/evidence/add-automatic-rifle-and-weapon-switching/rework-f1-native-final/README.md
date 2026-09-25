# Rifle rework F1 — native Player diagnostic

Muted local macOS Development Player, 3456×2160, one viewport. Ordinary PNGs and JSON snapshots were captured by `NativeCombatReview` in the Player. This is a diagnostic, not physical-device, performance, or human acceptance.

| File | Observation |
| --- | --- |
| `lab-switch.png` | Balance Lab shows `weapon.switchSeconds = 1`; diagnostic changed it by one step and restored it. |
| `lab-tracer-color.png` | Rifle tracer RGB channel and combined HEX color are visible. `lab-coverage.txt` records the other requested labels. |
| `rifle-fire.png` | Authoritative rifle shot count 1, ammo 99; no green spherical flash at the muzzle or endpoint. The thin transient tracer is not resolved in this still. |
| `switching.png` | Old rifle and ammo 99 remain visible with `PendingWeapon=Shotgun`, `SwitchRemaining=1`. |
| `shotgun-selected.png` | After more than one simulation second, shotgun is selected with its own ammo 20. |

The desktop runner did not give the Player input focus (`focused=false`). The log records `NATIVE_RIFLE_REVIEW_DIRECT_ACTION_FALLBACK` and `NATIVE_RIFLE_REVIEW_DIRECT_SWITCH_FALLBACK`: the diagnostic invoked the same authoritative session actions after synthetic gamepad input was ignored. This run verifies Player rendering and simulation state, not the physical input route. EditMode 112/112, PlayMode 113/113, and macOS build passed separately.
