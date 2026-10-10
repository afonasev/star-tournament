# Bot reaction settings

Source scope: two independent installation-wide persisted controls in Settings → Interface. Default text ON, voice OFF. Mouse, keyboard and gamepad navigation follow existing settings controls. Personal settings read the same values.

Voice is deliberately partial: only the exact selected line `Даже не поцарапал!` maps to the existing radio/chuckle candidate. Other variants remain silent. A single source starts from `LineSelected`, independent of viewport count. It uses the effects/mix budget; OFF, pause/results, mute, zero effects, restore/rebind/dispose and speaker death stop it; enable/resume/unmute never replay an old line. Disabled outputs do not change selection/history/cooldowns.

## Validation
Baseline, affected selection and commands: `scope.md`. Results and source hashes: `verification.json`. NUnit XML and rendered 1920×1080 settings image are retained here. No full suite, Player build, release or deployment was requested/run. Tests mute the physical listener; source/mix assertions do not prove perceptual balance in gameplay.

The first UI test read the decorative check glyph instead of the caption; fixed to exact `label` child. Subsequent focused reruns exposed test keyboard routing/setup: mouse click changes selection, and batch Editor keyboard events require `AllDeviceInputAlwaysGoesToGameView`/`IgnoreFocus` (existing cursor test pattern). The test now explicitly selects the voice control before keyboard Enter, preserves/restores InputSystem settings, and checks that the text control is unchanged. Runtime implementation did not change during these test fixes. Final repeated fixture covers all three settings tests; unchanged neighboring/HUD tests are retained from the initial run. Failed XML remains explicit historical evidence.

## Human review
Check all four combinations and persistence in Settings → Interface, including personal pause settings. Listen to the single voiced phrase in a real combat mix. Effects volume should affect voice; switching it off or pausing should stop it immediately with no repeat on resume. Other phrases currently have no voice asset. Editor screenshot is visual evidence, not Player/device/audio acceptance. Build and installed-game update await separate human commands.
