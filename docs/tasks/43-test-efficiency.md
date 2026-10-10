# Test efficiency and explicit full-run authorization

Owner: Codex chat `01a1249b-5f0f-7722-8023-342d59e281a9`, branch `codex/test-efficiency`, managed worktree `test-efficiency/star-tournament`. Baseline: `dd8af9e2`. User scope: minimize test selection, forbid full suites without separate human confirmation including production, inspect recent slow sessions and remove unnecessary execution cost.

No game/runtime/spec behavior changes. Owned files: QA wrappers, Makefile, their Python contracts, local QA/delivery instructions and README, this task and the efficiency report. Shared planning profile updated under planning lease; foreign delivery metadata preserved. Foreign main ProjectSettings are outside task scope.

## Selected validation

Before execution: `make check-tooling` selects `test_unity_tools.py`, `test_local_workflow.py`, `test_check_ui.py`. Reason: shell arguments/environment, full-run guards, make routing and existing XML validation are the affected contracts. Recording runner never starts Unity; confirmation flags inside these tests only exercise a fake runner. `sh -n unity/tools.sh`, `sh -n tools/unity.sh`, `git diff --check`, JSON parse of changed planning profile. No `test_flow.py`, distribution suite, full Unity or Player build: those runtime implementations are unchanged.

Runtime history override already exists in `ProvingGroundDesignLab.cs`; wrappers supply a unique test path by default and preserve an explicit override. History-specific/migration fixtures retain their own data and assertions. Builds, ordinary Editor launches and Player paths remain unchanged. Full suites are sequential even with parallel make, and require separate human confirmation. No benchmark build or assertion/timeout/seed reduction.

## Evidence and review

[Report](../evidence/test-efficiency/README.md) includes the measured saved runs, costs, limits and route checks. Verified: 19/19 selected Python contracts (2.468 s unittest body; ~2.6 s command), shell syntax and diff check passed; planning profile parsed and committed as `0cd9eb0`. Full suite, Unity runtime execution, Player/build/release/deploy and device/visual/audio/human acceptance are not performed by this tooling task. No timing improvement for the new patch is claimed without a new runtime measurement.
