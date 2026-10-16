# Lab requirements review

Pin `0c2d4562683252d54736f294dc1af463050eac17` (design-lab0.2.0). Baseline `9edd6c35`; source inspection plus current draft-warning patch. This is a gap inventory, not a completion certificate. Source-review rows require final requirement-matched evidence; test presence is not a Passed result. No native/human/device acceptance inferred.

Authority: `Runtime/ProvingGroundDesignLab.cs`, `Core/DesignLabHistory.cs`, `Core/LabParameterStorage.cs`, `Core/LabReleaseCatalog.cs`, `Editor/LabReleaseBuild.cs`. Candidate fixtures: DesignLabWorkspaceTests, DesignLabHistoryTests, LabReleaseCatalogTests; run only affected sets after semantic changes.

| Requirement | State | Finding / next evidence |
| --- | --- | --- |
| LAB-SCOPE-01 | Source review | Separate main-menu Lab; preferences and match setup separate. Verify final route05. |
| LAB-SCOPE-02 | Source review | LabBundle.IsEditable/IsVisible excludes placement and technical paths; workspace tests assert hidden paths. Complete catalog applicability remains. |
| LAB-GROUPS-01 | Source review | LabGroup/GroupLabel and grouped tree; approved05 rendering still to reconcile. |
| LAB-CATALOG-01 | Open audit | NumericDescriptor supplies key/group/label/description/unit/bounds/step/default. Audit application timing and all types/consumers. |
| LAB-CATALOG-02 | Open audit | LabBundle validation/storage/runtime binding exist; do not infer every consumer from registration alone. |
| LAB-FIND-01 | Source review | RefreshLabWorkspace searches full registry by group/name/description/path; all-changes independent. Affected workspace fixture exercises search. |
| LAB-EDIT-01 | Source review | LabInput/StepLab/SetLab, labRaw preserve intermediate invalid input. Verify complete typed editors/catalog. |
| LAB-DRAFT-01 | Source review | labBaseline/labDraft separated from saved/active; Changed and all-changes rows. Current patch preserves raw and exact return behavior. |
| LAB-DRAFT-02 | Implemented; affected QA passed | Current patch: full diff06, scroll rows and safe cancel; guard Clear/Delete plus existing selection/leave/quit. See discard/qa-scope.md. |
| LAB-VALIDATE-01 | Source review | LabIssues combines draft.Validate and raw input; save disabled; ShowLabErrors targets path. Complete relation validation audit pending. |
| LAB-PROFILES-01 | Source review | DesignLabHistory.Create copies selected saved snapshot into fresh ID/version1 with no release mark. |
| LAB-PROFILES-02 | Partial | Rename/DeleteSelected protect packaged profiles; deletion selects Default. Dev Default rename needs real dev authority. |
| LAB-VERSIONS-01 | Source review | Save appends max revision+1, preserves old snapshots. No inherited release mark. Verify empty historical date handling. |
| LAB-VERSIONS-02 | Partial | Profile chooser uses max revision; revision chooser explicit. Dev startup/reset from current author files missing. |
| LAB-DEV-01 | Missing runtime mode | Runtime always loads packaged releases; no author mode enabling Default/packaged-profile revisions. |
| LAB-DEV-02 | Missing | Runtime uses persistentDataPath; no Git-tracked project-authoring source/destination or honest save destination UI. |
| LAB-PROD-01 | Source review | RequireLocalProfile/SelectedProfileReadOnly and catalog rebinding protect shipped content beyond UI; copies remain local. |
| LAB-RELEASE-01 | Partial | MarkSelectedForRelease is local-only; LabReleaseBuild.PromoteMarked appends union and cannot remove previously staged marks. Dev invalid/no-marked Default recovery missing. |
| LAB-RELEASE-02 | Partial | SelectLatestDefault uses catalog Sequence, not explicitly highest marked Default revision. No explicit reset-to-release action; dev source missing. |
| LAB-RELEASE-03 | Partial | Runtime marks do not publish; explicit staging separate. Need author flags stored in project and complete package generation from current flags. |
| LAB-ACTIVE-01 | Source review | Selected saved identity cached; next match applies saved snapshot. Error paths preserve prior selection. Audit final activation/error state. |
| LAB-MATCH-01 | Source review | OpenDesignLab rejects match phase; existing workspace test freezes session DesignProfile. No live authoring extension requested. |
| LAB-STORAGE-01 | Source review | Atomic temp/flush/publish; mutation flag; StorageError disables writes. Single launcher ownership; no multi-process-write feature claimed. |
| LAB-UPGRADE-01 | Open implementation audit | MigrateCompatibility starts from shipped registry; verify missing values use latest marked Default rather than raw descriptor defaults and zero/false retention. |
| LAB-UPGRADE-02 | Source review | Trusted schema validation/StorageError preserve bytes; incompatible versions disabled. Verify visible reasons and complete old history policy. |
| LAB-TEST-01 | Source review | Next match uses selected saved identity and preparation screen. Existing workspace fixture asserts match identity; final display evidence pending. |
| LAB-UX-01 | Partial | Focus helpers/scroll/hints exist.05 final viewport/navigation review and physical devices remain open. |
| LAB-EXTEND-01 | Applicability audit | No new mandatory preview/export/deterministic tools. Existing extensions must retain project contracts; no implied new features. |

All28 normative Lab IDs accounted for. Remaining dev/storage/Default/flags/migration work remains in child tasks; source data isolation from9edd6c35 does not close these contracts.
