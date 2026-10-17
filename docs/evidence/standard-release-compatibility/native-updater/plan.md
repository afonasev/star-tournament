# Actual released native updater inventory
Baseline codex/game-standard-alignment@3fc5c735; UPDATES-INSTALL-02 mandatory legacy migration/recovery still OPEN.
Read-only download of signed descriptor-bound production0.1.5 and test0.1.5-test.1 Windows full packages. Reverify actual envelope RSA-PSS/pinned key; check full file SHA256/size before extracting updater. Inspect version/source identity statically, never execute Windows payload. Derive backup/callback behavior from exact matching upstream primary source when identity is established. Library NuGet1.2.158 alone is not native updater identity. No Player/installer build, game launch, full suite, release mutation or deploy.
Owned temporary full packages/extracted binary live only in .local/legacy-native-updater-01a13fa0; preserve compact evidence/hashes before removing reproducible downloads. Original published artifacts remain immutable remotely.

Reproduce actual bytes (downloads only, no native execution):
```
python3 docs/evidence/standard-release-compatibility/native-updater/inspect_package.py production
python3 docs/evidence/standard-release-compatibility/native-updater/inspect_package.py test
python3 docs/evidence/standard-release-compatibility/native-updater/compare_updater.py
```
The comparison script fetches only the immutable official1.2.158 release/tag metadata and vpk archive when absent. It verifies the archive's published SHA256/size and all non-resource PE sections. Findings are source/static evidence, not native runtime acceptance. Cleanup only the exact owned `.local/legacy-native-updater-01a13fa0/` after preserving outputs.
