# Affected QA scope
Baseline2446e3f5d0a1d1a7207907a2c4d24fd887ad3580.
Diff: persistent presentation settings, settings navigation/HUD gate, additive selected-line event, shared voice source and existing SFX/music budget, imported exact existing audio candidate. Simulation/AI/fire/damage rules unchanged.

Commands selected before launch:
- `unity/tools.sh test-edit-filter BotBanterTests`: existing predicates/rarity and new selection event no replay/no simulation mutation.
- Graphics-enabled `unity-run --shared ... -runTests -testPlatform PlayMode -testFilter "BotReactionSettingsTests;BotBanterIntegrationTests;SettingsGamepadNavigationTests.AudioSettingsPersistAndRemainGamepadNavigable;SettingsGamepadNavigationTests.SectionFocusShowsParametersWithoutSubmit"`: all four channels/persistence, mouse/keyboard/gamepad routes, 1/4 viewport one voice, exact phrase/lifecycle/mix, existing HUD and neighbor audio settings/section focus. Separate isolated QA Lab history.

No full/Player build/release/deploy. Tests mute physical AudioListener while asserting real source state/volume. Screenshot is Editor settings UI, not human gameplay/audio acceptance.

Test-only corrections repeated the failing UI method; after shared InputSystem test setup changed, repeated all three BotReactionSettingsTests. No runtime changes or broader rerun. Final 3/3 passed; four unchanged PlayMode tests retain initial passing XML.
