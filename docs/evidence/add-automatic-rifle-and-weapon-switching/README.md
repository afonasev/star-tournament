# Automatic rifle native Player review

Rework F1 (2026-09-25) evidence is in [`rework-f1-native-final/README.md`](rework-f1-native-final/README.md). The captures below are the previous candidate and retain its original visual behavior.

Muted macOS development Player, synthetic local gamepads, 1920×1080 capture. These captures are diagnostic evidence, not physical-device, performance, or human acceptance.

| Capture | Observation |
| --- | --- |
| `native-review-current-1p/rifle-live.png` | Rifle visible with retained first-person hands, ammo 100, one viewport and bot opponent. |
| `native-review-current-1p/rifle-fire.png` | Confirmed rifle shot, ammo 99 and recorded shot event. |
| `native-review-current-1p/shotgun-selected.png` | D-pad up selects shotgun and its independent ammo 20. |
| `native-review-current-layouts/2-rifle-live.png` | Rifle, ammo and crosshair visible in both viewports. |
| `native-review-current-layouts/3-rifle-live.png` | Three rifle viewports and persistent standings in the fourth quadrant. |
| `native-review-current-4p/live.png` | Rifle and ammo visible in four viewports. |

Each PNG has a same-named JSON snapshot. `native-review-current-1p` was captured with `-combatReview -oneViewportReview -noaudio`; the two and three viewport captures used `-layoutReview -rifleLayoutReview -noaudio`; four viewports used `-combatReview -noaudio`. The Player log markers were `NATIVE_RIFLE_REVIEW_COMPLETE`, `NATIVE_RIFLE_LAYOUT_REVIEW_COMPLETE`, and `NATIVE_COMBAT_REVIEW_COMPLETE` respectively.

Open review gates: physical keyboard/mouse and controller operation (including wheel and reconnect), rifle empty-ammo behavior in Player, and measured performance at each local seat count. The simulation and input adapter have automated coverage, but these screenshots do not close those gates.
