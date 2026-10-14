# Fast Default startup and Lab history diagnosis

Approved startup uses validated latest packaged Default, preserving published tuning and exact compatibility rules. Local history starts after first menu frame; Lab waiting/error state has Back and disabled mutations. Late completion installs history only, preserving active profiles and frozen matches. Read-only loader cancels on destruction/quit.

The input is160126874bytes,2 profiles,16 revisions,14 hashes and4 schemas. Three identical immutable authored-map payloads repeated16times account for92.6% of compact JSON. Full local-history validation is unnecessary for Default/menu; it remains mandatory before mutation of the current monolithic file. A new deduplicated file format is deferred.

Measured before per-instance cache/fingerprint:111.52s; reading+parse1.23s, inclusive historical lookup78.39s, hashes2.62s,13 generations,240 Gen0 collections. After:44.12s, historical lookup15.43s,3 generations,69 Gen0; all62 hash/snapshot validation passes unchanged. Measurements used the exact copied history and passing read-only startup fixture; no byte changes. Candidate cache is bounded8 sets per instance and2 globally; full trusted metadata SHA256 reduces oversized keys. Stored values/validity are never cached.

Source baseline main0b7ed007; working branch codex/fix-lab-startup. macOS Player QA source codex/fix-lab-startup@6adfaa87 passed exit0 and LAB_STARTUP_REVIEW_PASS; menu13.47s, copied history45.03s, Default active/reference and audio preferences unchanged. Minimal standalone build required to verify the actual reported Player startup and packaged resources; no release/deploy. Integration recorded in planning delivery.json after final evidence commit. See verification.json for XML, source hashes and diagnostics.

Player cache retention after diagnostic controlled GC:776237056 managed bytes (whole process). Per-history cache5 sets,1060 candidate bundles,1371 unique trusted profile objects,730717 descriptors. Memory is still substantial; no claim that history format/storage is solved. Full format deduplication is a separate future change.

Native batch screenshot callbacks did not produce PNGs; UI review uses explicitly rendered GPU Editor Canvas views saved in editor-views/, not native/device acceptance. All autonomous launch routes use -noaudio from start. Exact silent retained-player launch: `UNITY_RUN_MODE=shared UNITY_PLAYER_APP=/Users/eaafonasev/Projects/star-tournament/.local/player-builds/2026-10-14-lab-startup-6adfaa87/StarTournament.app unity/tools.sh run -noaudio`.
