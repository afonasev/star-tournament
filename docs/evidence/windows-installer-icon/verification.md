# Windows installer icon correction — 2026-10-09

Scope: local NSIS packaging configuration; preserve the approved app icon and
all installer behavior. No Unity runtime, gameplay, payload or visual redesign.
Baseline: 5789034. Source: user reported missing Windows installer app icon.

Modern UI's first page expands MUI_INTERFACE, which emits Icon and UninstallIcon
again using its defaults unless MUI_ICON / MUI_UNICON were defined first.
The old direct commands therefore lost the app icon. Both settings now use
`${INPUT_DIR}\app-icon.ico` before any page insertion.

Primary documentation: https://nsis.sourceforge.io/Docs/Modern%20UI%202/Readme.html
(interface configuration and MUI_ICON / MUI_UNICON). Locally verified against
NSIS 3.13 Contrib/Modern UI 2/Interface.nsh, lines 49–50 and 118–119.

Verification:
- Regression assertion on real `makensis -PPO` output failed before the fix:
  effective Icon commands included `modern-install.ico`.
- After the fix, `PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s
  distribution/tests -p test_windows_installer.py`: 16/16 pass, no skips.
  The preprocessor test checks every expanded Icon / UninstallIcon command
  points to the app ICO and retains checked finish actions.
- ICO header/directory: valid 32-bit entries at 16, 24, 32, 48, 64, 128, 256 px;
  every entry stays within file bounds. Asset unchanged.
- `git diff --check`: pass.

No real installer or Unity Player was built; unit-test compiler calls are mocked
and the real NSIS invocation is preprocessing only. Building/publishing a new
installer and checking its Explorer/wizard appearance on Windows remain deferred
until explicitly requested. Published installer bytes are unchanged.

Tests use TemporaryDirectory cleanup; no build artifacts or caches are retained.
