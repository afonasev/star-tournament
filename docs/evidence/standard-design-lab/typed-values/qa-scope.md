Baseline06fd5de9 (code unchanged from3d99af6f), branch codex/game-standard-alignment.

Scope: Unit=boolean is exactly0/1; range0..1/step1/default0or1 metadata. Reject fraction/nonfinite without coercion, block invalid Save, preserve bad legacy bytes, valid false/true roundtrip. Registry source search finds only blood.enabled/hit.enabled. Numeric floats/count behavior and balance defaults unchanged. UI proposals18–19 await approval; no editor layout implementation.

Minimal affected command: unity/tools.sh test-edit-filter 'LabParameterTypeTests;BloodProfileTests.BloodFieldsAreEditablePresentationWithValidMetadataAndSteps;HitFeedbackProfileTests.ContactControlsAreEditablePresentationAndHaveValidMetadata'. Real C#/JsonUtility contracts need Editor; shared -noaudio -muted from startup. No full/Player/build/deploy. Draft/UI state integration checked separately if these pass.

Additional trust-boundary checks: LabParameterTypeTests.CallerMetadataCannotBypassBooleanStorageValidation (both keys), and PlayMode DesignLabWorkspaceTests.BooleanFractionCannotSaveOrActivateAndValidOffRemainsSaveable. UI remains current numeric layout: raw0,5 retained with explicit error, no source/active changes; valid0 saves. No implementation of pending preview18.

Audit correction: LabBundle.Validate already rejected fractional switches by declared step1; ProvingProfile.Validate alone lacked the semantic boolean check. This slice adds explicit typed metadata/value validation and clearer reason, not a claim that normal Lab Save previously accepted0.5. Spoof test now changes both caller Unit and Step so the actual trusted-registry boundary is exercised.
