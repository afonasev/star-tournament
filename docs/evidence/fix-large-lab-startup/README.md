# Fast Default startup and Lab history diagnosis

Approved startup uses validated latest packaged Default, preserving published tuning and exact compatibility rules. Local history starts after first menu frame; Lab waiting/error state has Back and disabled mutations. Late completion installs history only, preserving active profiles and frozen matches. Read-only loader cancels on destruction/quit.

The input is160126874bytes,2 profiles,16 revisions,14 hashes and4 schemas. Three identical immutable authored-map payloads repeated16times account for92.6% of compact JSON. Full local-history validation is unnecessary for Default/menu; it remains mandatory before mutation of the current monolithic file. A new deduplicated file format is deferred.

Measured before per-instance cache/fingerprint:111.52s; reading+parse1.23s, inclusive historical lookup78.39s, hashes2.62s,13 generations,240 Gen0 collections. After:44.12s, historical lookup15.43s,3 generations,69 Gen0; all62 hash/snapshot validation passes unchanged. Measurements used the exact copied history and passing read-only startup fixture; no byte changes. Candidate cache is bounded8 sets per instance and2 globally; full trusted metadata SHA256 reduces oversized keys. Stored values/validity are never cached.

Source baseline main0b7ed007; working branch codex/fix-lab-startup. Player QA and integration pending at this evidence commit. See verification.json for XML, source hashes and diagnostics.
