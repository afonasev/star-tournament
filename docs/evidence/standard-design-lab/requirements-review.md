# Lab requirements review

Pin `0c2d4562683252d54736f294dc1af463050eac17` (design-lab0.2.0). Baseline `9edd6c35`; source inspection plus current draft-warning patch. Later authoring-model/ and runtime/ evidence supersede the original dev/Sequence/name findings below; updated rows identify actual Editor progress. This remains a gap inventory, not a completion certificate. Source-review rows require final requirement-matched evidence; test presence is not a Passed result. No native/human/device acceptance inferred.

Authority: `Runtime/ProvingGroundDesignLab.cs`, `Core/DesignLabHistory.cs`, `Core/LabParameterStorage.cs`, `Core/LabReleaseCatalog.cs`, `Editor/LabReleaseBuild.cs`. Candidate fixtures: DesignLabWorkspaceTests, DesignLabHistoryTests, LabReleaseCatalogTests; run only affected sets after semantic changes.

| Requirement | State | Finding / next evidence |
| --- | --- | --- |
| LAB-SCOPE-01 | Source review | Separate main-menu Lab; preferences and match setup separate. Verify final route05. |
| LAB-SCOPE-02 | Source review | LabBundle.IsEditable/IsVisible excludes placement and technical paths; workspace tests assert hidden paths. Complete catalog applicability remains. |
| LAB-GROUPS-01 | Source review | LabGroup/GroupLabel and grouped tree; approved05/18 workspace rendered in1080p/720p/short viewport (approved-ui/); full catalog audit remains. |
| LAB-CATALOG-01 | Metadata verified; consumer audit open | catalogue/: actual638 visible fields across20 profiles,155 Integer/481 Decimal/2 Boolean,14974 exclusions. Computed type and NextGame contract; focus shows actual highest marked project/package Default, missing Default explicit.24 EditMode +2 PlayMode cover read-only lookup, authority/marks and refresh without draft activation;720p PNG viewed. Consumer effect audit remains. |
| LAB-CATALOG-02 | Open audit | LabBundle validation/storage/runtime binding exist; do not infer every consumer from registration alone. |
| LAB-FIND-01 | Source review | RefreshLabWorkspace searches full registry by group/name/description/path; all-changes independent. Affected workspace fixture exercises search. |
| LAB-EDIT-01 | Typed data and approved18 Editor UI verified | typed-values/:19 EditMode boolean0/1 guards; approved-ui/:protected two-state buttons, gamepad focus/submit, draft isolation and0/1 save readback. Numeric controls retained. Complete catalogue/type audit remains. |
| LAB-DRAFT-01 | Source review | labBaseline/labDraft separated from saved/active; Changed and all-changes rows. Current patch preserves raw and exact return behavior. |
| LAB-DRAFT-02 | Implemented; affected QA passed | Current patch: full diff06, scroll rows and safe cancel; guard Clear/Delete plus existing selection/leave/quit. See discard/qa-scope.md. |
| LAB-VALIDATE-01 | Source review | LabIssues combines draft.Validate and raw input; save disabled; ShowLabErrors targets path. Complete relation validation audit pending. |
| LAB-PROFILES-01 | Source review | DesignLabHistory.Create copies selected saved snapshot into fresh ID/version1 with no release mark. |
| LAB-PROFILES-02 | Source + affected QA | Author Default rename and undeletable root; package edit/delete only in author role. Production protected; delete and clear guards preserve draft until consent. Native/device gate open. |
| LAB-VERSIONS-01 | Source review | Save appends max revision+1, preserves old snapshots. No inherited release mark. Verify empty historical date handling. |
| LAB-VERSIONS-02 | Editor selection verified; identity audit open | Profile chooser uses latest saved revision or its technical view, not highest cache key (repeated-upgrade/ PlayMode). Dev startup/reset use project marks (runtime/). Source/execution identities and final05 rendering still require reconciliation. |
| LAB-DEV-01 | Editor implemented; native pending | Actual project author mode loads before menu; Default edit/rename/protection and package authority covered by model+runtime tests. Compiled dev Player not built/executed. |
| LAB-DEV-02 | Editor + tracked source | Real unity/DesignLab/authoring-history-v1.json bootstrapped/read back and preserved in task Git; save destination/commit distinction tested. Compiled dev path unit-tested, native gate open. |
| LAB-PROD-01 | Source review | RequireLocalProfile/SelectedProfileReadOnly and catalog rebinding protect shipped content beyond UI; copies remain local. |
| LAB-RELEASE-01 | Project projection/staging verified | Explicit canonical-project staging replaces append-only union with all-and-only marked saved identities; unmark removes future inclusion without deleting history; published original/hash/date preserved. Owned-file fixtures and actual project no-op passed; no Player build performed. |
| LAB-RELEASE-02 | Editor/prod source verified | Greatest marked project Default loaded critically; highest package Revision for production; reset with discard guard tested. Physical/dev Player acceptance remains open. |
| LAB-RELEASE-03 | Editor source/staging verified | UI writes project file only; explicit staging updates Resources separately, does not regenerate/save scene or build/publish. Actual no-op preserved author/catalogue/scene SHA256. Native delivery stays separately gated. |
| LAB-ACTIVE-01 | Affected Editor QA passed | Save/select/reset activate saved configuration for next game; viewing/cancel/flags/rename preserve active selection. Canonical saved version labels retained for interpreted package views. |
| LAB-MATCH-01 | Source review | OpenDesignLab rejects match phase; existing workspace test freezes session DesignProfile. No live authoring extension requested. |
| LAB-STORAGE-01 | Source review | Atomic temp/flush/publish; mutation flag; StorageError disables writes. Single launcher ownership; no multi-process-write feature claimed. |
| LAB-UPGRADE-01 | Defaults source fixed; lifecycle audit open | Latest packaged Default by Revision / actual marked project Default supplies absent fields; 51 affected cases passed including 0/false, immutable originals, read-only bytes, explicit complete Save/reload. New derived local-view lifecycle fixed: source provenance, Schema3 envelope, repeated Default fill vs explicit save, latest-saved profile choice;75 affected EditMode +2 PlayMode. Default/package and type/range/resource audit still open; see repeated-upgrade/. |
| LAB-UPGRADE-02 | Refusal diagnostics verified; version UX open | compatibility-diagnostics/:20 affected EditMode +2 PlayMode, changed constraint/unknown schema epoch/resource/field report identity/path, preserve bytes and Default; writes disabled, Back available, plain text fits720p. Approved19 historical inspection/unmark now verified in Editor (approved-ui/): original/hash/date, active base and draft preserved, incompatible selection disabled. Full historical transform/type policy remains open. |
| LAB-TEST-01 | Source review | Next match uses selected saved identity and preparation screen. Existing workspace fixture asserts match identity; final display evidence pending. |
| LAB-UX-01 | Partial | Focus helpers/scroll/hints exist.05/18 workspace viewport and boolean gamepad navigation verified (approved-ui/). Broader navigation/catalog and physical devices remain open. |
| LAB-EXTEND-01 | Applicability audit | No new mandatory preview/export/deterministic tools. Existing extensions must retain project contracts; no implied new features. |

All28 normative Lab IDs accounted for. Remaining dev/storage/Default/flags/migration work remains in child tasks; source data isolation from9edd6c35 does not close these contracts.
