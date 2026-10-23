# Historical evidence recovery

Completed, accepted evidence payloads were removed from the current checkout after byte-for-byte recovery verification. Text reports, original results and reusable source files remain.

Index: `/Users/eaafonasev/Projects/star-tournament/planning/docs/evidence/storage-cleanup-20261023/docs--evidence--improve-tunnel-wayfinding.json` (planning commit `b701d0de065ca676bf4ebacdfa039d975aed5279`). Each entry records the old path, size, Git blob ID and SHA-256.

The immutable source revision `1fecdc09120ec1db9174f4311a53c3ca4ea15f51` remains in main ancestry. Restore only the needed file outside the repository:

```sh
git -C /Users/eaafonasev/Projects/star-tournament show 1fecdc09120ec1db9174f4311a53c3ca4ea15f51:docs/evidence/improve-tunnel-wayfinding/<file> > /absolute/recovery/<file>
```

Historical relative image/raw-log links in this package refer to the indexed original paths. No test result or acceptance was changed. No history rewrite or object pruning is authorized by this cleanup.
