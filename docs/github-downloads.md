# GitHub installer downloads

Scope: explicit user request 2026-11-03 to apply the Bricks War single-repository download presentation. Baseline main `919b2181c301b9b984c6d158e0610948a90af012`; owned branch `codex/github-installer-links`.

The README starts with Windows/macOS installer links. The filtered player release list is https://github.com/afonasev/star-tournament/releases?q=tag%3Av0%20prerelease%3Afalse. Ordinary human-readable release notes receive the same download block during future publication, keeping canonical bilingual headings. Preserve all JSON channel pointers, release IDs/tags/assets/digests and existing visibility. No installer or game build/deploy is part of this presentation change. On a new native version refresh the README links to the verified published installers; keep its version label explicit.

Affected QA selected before implementation: execute the actual presentation helper, idempotence, correct native asset names, version/URL validation and existing notes/publisher tests. No Unity/full/gameplay/physical install checks; those remain NotRun. Public application, readback and cross-project evidence: `/Users/eaafonasev/Projects/bricks_war/evidence/all-github-downloads/`.

Verification: local presentation helper tests PASS; affected existing notes/publisher checks PASS. Wild Spirits first delivery check failed because sparse checkout omitted its branding PNG; the exact test passed after adding that source file, without test changes. Original FAIL and final PASS are retained in the central evidence. Game/runtime/install acceptance remains NotRun.
