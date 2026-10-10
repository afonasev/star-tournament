# Native loading screens — implementation evidence

Approved concept: variant B at ac600f96, with subsequent copy corrections; user authorized implementation on 2026-10-10. This concept approval is separate from runtime acceptance.

Runtime source baseline: d83cfd450cc029e396322604244b3647ed98515a. The startup Canvas appears in Awake before bootstrap work. Detached history validation runs on a worker; arena projection, actors and spawn search yield across frames, with asynchronous NavMesh baking. A match remains unticked until the loading pipeline completes. Duplicate launch, held input, focus/device loss, recoverable errors and scene destruction retain guarded lifecycle behavior.

The cyclic indicator reports activity and has no percentage or artificial minimum duration. Existing menu illustration is shared across screens; it is not a map preview. History loading does not rewrite the saved history.

## QA scope

- UI gate: menu/seat/pause fixtures.
- Complete PlayMode regression: the asynchronous scene-ready contract affects common bootstrap and arena setup.
- Focused EditMode: history, arena/spawn contracts, cancellation. Spawn allocation has an independent recursive oracle retained from a1db27a7.
- Native graphical Editor captures: startup and match preparation.
- Large-history copy: animation continues during validation; the copied file's SHA-256 stays unchanged. Original file remains untouched.

Results and artifact paths are recorded after each run. Player builds, installers, release, deployment, installed-game update, physical device acceptance and human visual acceptance are deferred. No Player build is required to verify these Editor-rendered loading screens.

## Final results

Gameplay pipeline revision d83cfd45; final loading presentation f1726cbd; test harness e7af528e. Initial full PlayMode ran 261 cases: 248 passed, 10 failed because lifecycle fixtures inspected asynchronous launches too early or used the obsolete results-table hierarchy, and 3 unrelated hit-feedback capture tests were explicitly skipped without graphics. Reruns preserved the assertions, waited real readiness/presentation and fixed the results-frame path. Combined latest results: **258 passed, 3 explicitly skipped, no unresolved failure**. This is complete PlayMode coverage plus affected reruns, not a claim of one all-green full invocation. EditMode history/arena/cancellation: **62/62**; UI gate **16/16**. After the final presentation correction, graphics-enabled loading fixtures: **8/8**.

The copied existing history (160,126,874 bytes) loaded while the indicator updated, remained writable, and retained its hash. The original SHA-256 was checked before/after and remained `7241dbd29c3d2abb7679532fa7a0478cfde7514d39b94b30e4d85f3c862b938b`. No data reset or write was used. Timing in the log is diagnostic and does not establish a benchmark or device acceptance.

Actual native Canvas captures (not HTML mockups), 1600×900: [startup](native/startup.png), [match](native/match.png). Batch-mode ScreenCapture did not emit images; the final harness renders the same Canvas through a temporary camera texture, immediately restoring overlay mode/layers. Both PNGs were inspected. Custom graphics now explicitly require CanvasRenderer; card art is clipped to rounded corners. RenderTexture capture validates layout and graphics, not the physical desktop/window swapchain.

XML, useful final logs, exact revisions, skipped reasons and latest-case mappings: [QA summary](qa/summary.json). Initial failures and subsequent reruns remain visible as evidence. No Player, release or deployment was performed.
