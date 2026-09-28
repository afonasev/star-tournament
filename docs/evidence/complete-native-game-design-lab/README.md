# Native Game Design Lab evidence · 2026-09-28

Automated verification, not human acceptance. Unity 6000.3.23f1 macOS Development Player, audio muted, 1920×1080. The change delivery record binds this evidence to the exact code commit and immutable candidate.

- EditMode: 121/121; persistent immutable full snapshots, old-base append, hashes, duplicate paths, invalid values, cross-field rules, protected deletion, corrupt history, atomic replacement failure without data loss.
- PlayMode: 116/116. Final setup identity layout: focused 1/1 after its layout adjustment. Synthetic gamepad submit/navigation/cancel and focus isolation; no physical gamepad acceptance.
- Player: direct input, two-group diff, invalid Save, dirty exit/revision guards, old-base v3 append, rename/delete, read-only authored geometry, exact setup hash, restart. Completion files and regular PNGs retained. Scripted UI does not establish human acceptance.
- Manual native window: mouse opened Lab; keyboard typed jumpSpeed; mouse + changed 8.5→8.6, counts/diff appeared; Escape cancelled dirty guard; mouse reset restored base and Back returned to menu. Used isolated copy of QA history.
- Full registry audit: 12 profiles, 514 editable descriptors, 4068 authoring descriptors read-only; two unused diagnostic paths excluded.

The transient FPS widget in scripted screenshots includes history serialization and mass row creation; it is not a performance benchmark. Performance on reference hardware and physical-controller acceptance are not claimed.

One earlier Player search focus crash was fixed and superseded by these complete runs. An earlier full PlayMode run exposed three fixture/selection regressions; all were repaired while preserving assertions and the full suite passed. Two final-layout launches stopped before tests because the default licensing IPC channel was absent; connecting to the existing versioned client resolved startup.

The full test-only history is preserved outside Git with the immutable local candidate; history-inventory.json records hashes and selection. No normal player history was overwritten. No external distribution is performed.
