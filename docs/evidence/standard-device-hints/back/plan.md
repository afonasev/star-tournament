Baseline codex/game-standard-alignment@76b9e43d. Approved Q8/29. First red reproduction only2 PlayMode: SettingsGamepadNavigationTests.ProfilesBackReturnsToMainOnOneEastPress;HistoryBackAcceptsANewPadsFirstNonSubmitPress. Expected observable Back contract, not implementation copies. Runtime unchanged for red. Then actual shared Back routing/one-frame consumption, input-device capture before routing, explicit target hint predicates. Must preserve text editing/top modal/held opening/assigned-seat ownership and SettingsBack parameter→navigation→close. Planned green affected UI route/control tests, shared hint unit tests and actual family/transition renders. Shared muted Editor, no full/Player/installer/build/deploy/physical acceptance.
Confirmed red2/2 actual UI failures, runtime unchanged. Green scope:2 red routes,3 virtual advertised families (owned derived Gamepad layouts; parent text modal/East and extra glyphs), Settings real Back target/reset, keyboard field Escape, nested Settings→Setup one-frame consumption, new-pad rightStick .2 above existing .15 threshold, operator Resume, per-seat confirmations/cancel focus, Lab discard, roster editor/picker, installed-release badge preservation and existing text hold/foreign pad/handover. Shared Back consumption does not alter independent seat input. Source uses same Back target predicates and exact actors. Controller glyphs reserve installed badge/dropdown arrow space.15 affected Edit cases and selected16 Play cases below; no full/Player/installer/build/deploy.
Selected18 green Play methods (not16):
SettingsGamepadNavigationTests.ProfilesBackReturnsToMainOnOneEastPress
SettingsGamepadNavigationTests.HistoryBackAcceptsANewPadsFirstNonSubmitPress
SettingsGamepadNavigationTests.BackFromSettingsOpenedOverSetupDoesNotAlsoPopSetup
SettingsGamepadNavigationTests.HistoryScrollCapturesSmallRightStickFromANewPadBeforeRouting
SettingsGamepadNavigationTests.FamilyHintsAndBackStayBoundAcrossProfilesTextModalAndHandover
SettingsGamepadNavigationTests.SettingsBackHintNamesOnlyTheActualCurrentTarget
SettingsGamepadNavigationTests.ProfilesEscapeFirstEndsKeyboardEditingThenNavigatesBack
SettingsGamepadNavigationTests.AllAiOperatorOpensTheSameSettingsAndReturnsToPause
SettingsGamepadNavigationTests.MenuCursorFollowsGamepadAndMouseAcrossScreens
SettingsGamepadNavigationTests.GamepadCanCancelDisplayConfirmation
IndependentPauseMenuTests.GamepadNavigationSubmitsOnlyItsOwnViewport
IndependentPauseMenuTests.PersonalSettingsAndGlobalConfirmationStayScoped
NativeTextEntryTests.OpeningHoldForeignPadAndCancelDoNotMutateTarget
NativeTextEntryTests.KeyboardHandoverAndApplyUseExistingProfilePersistence
LabDiscardWarningTests.HiddenAndInvalidChangesAreListedCancelKeepsDraftConfirmOnlyNavigates
NativeUpdateMenuTests.HistorySelectsInstalledReleaseScrollsAndRestoresMenuFocus
RosterCardsTests.GamepadCanEditDifficultyCancelAndReturnToCard
LabAuthoringRuntimeTests.BrokenAuthorHistoryRequiresExplicitSessionDefaultAndPreservesOriginalAcrossRestart
Right-stick test uses controlled long ScrollRect geometry, not live release data. Virtual fixture registration/removal protected by finally even if setup fails. Author fallback Back now uses actual East; existing exact-source/write/restart assertions retained.
Initial green18:15 passed/3 failed. Precise corrections: unfocused Resume legitimately advertises East (assert exact Правая/no South instead of no hint); rightStick raw .2 is attenuated by the existing processor, supply raw from configured deadzone and assert processed(.15,.5) plus actual scroll; Settings target fixture uses advertised Xbox matching29 because generic positional word is intentionally omitted when narrow (no layout/assertion relaxation). Add assigned-personal-settings hint eligibility preserving original controller authority, LastMap fixture restoration, and diagnostic Escape same-frame pause regression. Next targeted6 includes corrected3 + owner policy + diagnostic + two-pad History; diagnostic expected to expose remaining re-pause before final one-condition fix.
