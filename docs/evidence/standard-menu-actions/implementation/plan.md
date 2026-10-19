Baseline codex/game-standard-alignment@fa13ca57; approved Q5/20–25. Minimum8 affected Editor PlayMode cases: new and symmetric toggle/copy behavior, existing boolean draft/save/focus and all viewport assertions, saved original identity and incompatible historical inspected-profile unmark, Default hint recalculation, discard clear/delete guard, settings Auto copy/navigation. All unaffected gameplay/balance/source persistence assertions retained; obsolete Off/On navigation becomes toggle→reset, with A toggle twice added.
DesignLabWorkspaceTests.DraftValidationFocusAndSavedSelectionRemainIndependentOfLiveMatch
DesignLabWorkspaceTests.BooleanSwitchesKeepDraftSeparateAndSaveOnlyOffOrOn
LabAuthoringRuntimeTests.ReleaseToggleAndCopyUseSavedSourceWithoutActivatingDraft
LabAuthoringRuntimeTests.HistoricalSourceIdentityAndPerRowIncompatibilityStayVisibleWithoutActivation
LabAuthoringRuntimeTests.HistoricalInspectionAndUnmarkPreserveActiveBaseDraftAndOriginal
LabAuthoringRuntimeTests.CatalogueHintsUseMarkedDefaultAndDeclareNextGameWithoutPublishingDraft
LabDiscardWarningTests.ClearAndDeleteRequireDiscardConsentBeforeTheirOriginalActions
SettingsGamepadNavigationTests.SettingsAreFullyNavigableWithGamepad
Shared graphics Editor -noaudio -muted from start, isolated history/screenshots. No full suite, Player, installer, build, deploy or physical/human acceptance.
Initial8/8 passed. Render review prompted local copy/history typography correction, safe initial copy name Новый профиль (avoids appending beyond48-char limit), and restoration of original rename geometry. Final affected rerun: ReleaseToggleAndCopyUseSavedSourceWithoutActivatingDraft;HistoricalSourceIdentityAndPerRowIncompatibilityStayVisibleWithoutActivation;HistoricalInspectionAndUnmarkPreserveActiveBaseDraftAndOriginal;MissingDefaultKeepsLabEditableAndFlagsOrRenameDoNotActivatePreviousExperiment. Add actual unchecked-incompatible PNG/assertions.9 unique cases total if all pass; not12 unique. Other initial behavior/source hashes retained; changes after first run limited to typography/copy initial name/rename placement. No full/build/deploy.
