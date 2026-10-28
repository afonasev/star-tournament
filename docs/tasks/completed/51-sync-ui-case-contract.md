# Synchronize the exact UI NUnit case contract

Scope authorized in chat 01a180e5-e8f4-7ac1-916b-de44d4e5feac: repair the UI case list, run minimal checks and integrate into main. Baseline: main@0615403c5f87a8d4025105ce02e4b6d0cf66ce9a. Owner: this chat; branch codex/ui-cases-sync; worktree /Users/eaafonasev/.codex/worktrees/ui-cases-sync/star-tournament. No owned game processes or builds.

Added the existing ResolutionPopupsStayAboveTheirOwnSeatAndPreserveOtherSeats NUnit fullname with multiplicity 1. The unchanged fixture selection now expects 3 EditMode and 21 PlayMode cases (5 menu + 16 pause). Updated the exact positive-test count to 21; Counter equality and negative duplicate/missing/skipped/foreign-result checks remain unchanged. No product/spec requirement changes; no OpenSpec delta is needed for this local tooling repair.

Before the repair, `PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tools -p test_check_ui.py` reproduced the missing-name failure: 8 PASS, 1 FAIL. After the repair, the same 9 tests PASS, including source-name matching and historical XML checks. `python3 tools/check_ui.py --plan` and `git diff --check` PASS. Historical XML remains historical and is not evidence of a new Unity run. Unity, full game suite, Player, build, release and deploy: NotRun, outside this task.

The worktree uses the repository's sparse storage selection plus only the two historical XML files consumed by test_check_ui. Foreign Unity settings and planning changes are outside this commit. Integration and owned-resource cleanup are reported in the chat with the resulting SHA; no human/device acceptance is inferred.
