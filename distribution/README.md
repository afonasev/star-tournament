# Native test distribution

The launcher is a self-contained .NET 8 application using Velopack 1.2.158. Unity retains company/product identity and all native gameplay/UI. The launcher waits for the actual Player process; it never kills a match or modifies a live payload. Metadata checks are automatic, package downloads require the explicit `download` command from Unity, and `restart` is acknowledged before Unity requests a normal exit. A staged package is verified again after exit and before the next Player starts.

`release.json` belongs to each immutable package, and the main menu gets `<version> (dd.MM.yyyy)` from that installed package. The date is its publication day, not device time. The IPC session consists of an atomic status snapshot and a single command file outside replaceable contents. Single-flight download and an installation lock prevent overlapping operations. A recorded orphaned Player PID/start time prevents an accidental replacement after a supervisor crash.

## Authentication

`latest.json` is an envelope with Base64 UTF-8 payload and RSA-PSS/SHA256 signature. The signed payload binds schema, platform/architecture, SemVer, publication day and exactly one full Velopack asset including SHA256, size, package ID and filename. The pinned public key is `update-public.pem`. HTTPS is required. Download bytes are quarantined and checked before admission to the SDK, because the Windows SDK can extract its updater during download. Existing complete cache files and staged packages are independently checked; SDK startup auto-apply stays disabled. Delta packages are excluded in this first release.

Private signing key is outside Git and build worktrees at `~/.local/share/star-tournament-distribution/keys/updates-private.pem`, directory mode 700 / key mode 600. Preserve this key for future releases; do not upload it or include it in any artifact. OS developer signing/notarization is not supplied for these test installers.

## Commands

Install .NET SDK 8 locally; install the pinned `vpk` 1.2.158 tool using that SDK. In the owning worktree:

```
DOTNET_ROOT=<sdk-directory> <dotnet> tool install vpk --version 1.2.158 --tool-path <tool-directory>
<dotnet> run --project distribution/tests/Launcher.Contracts.csproj
STAR_TOURNAMENT_RELEASE_OUTPUT=<absolute Player.app> unity/tools.sh build-release-mac
STAR_TOURNAMENT_RELEASE_OUTPUT=<absolute windows-directory/StarTournament.exe> unity/tools.sh build-release-win
python3 distribution/package.py --runtime win-x64 --player <windows-directory> --version <SemVer> --date <yyyy-MM-dd> --feed https://robowar.afonasev.tech/desktop/test/win-x64/ --key <private-key> --output <output> --dotnet <dotnet> --vpk <vpk> --nsis-host gfe
python3 distribution/upload_candidate.py --runtime win-x64 --releases <output/releases> --version <SemVer> --installers
```

Use `osx-arm64` / `osx-x64` and matching feed paths for macOS; macOS `--player` takes the Unity `.app`. These are separate runtime packages; building x64 is not physical Intel Mac acceptance. `package.py` never overwrites an existing version payload. The uploader fails on a remote immutable-name collision and replaces `latest.json` only after hash readback. Installer filenames from Velopack are unversioned, so publish them under a distinct per-version release directory before promoting a landing link. A pending human acceptance candidate must remain downloadable by its exact artifact URL.

Local Unity QA must pass `--qa-unity-runner "$HOME/.local/bin/unity-run"` to the launcher; this argument persists through the updater's restart and executes the real Player through the common exclusive guard. The environment variable `STAR_TOURNAMENT_QA_UNITY_RUNNER` is also supported. On the user's machine no local QA guard is required.

## Remaining acceptance gates

### Windows installation wizard

Windows packaging replaces the unpublished one-click Setup with an NSIS Modern UI 2 wizard over the complete portable layout. Default destination is system Program Files (64-bit) / Star Tournament, normally `C:\Program Files\Star Tournament`. The directory page permits only protected locations inside 64-bit Program Files. This candidate supports fresh installation; existing complete machine installations must update in-game or be normally uninstalled first. Desktop shortcut and finish launch options are checked by default and can be cleared. NSIS owns machine uninstall registration and shortcuts; `.portable` prevents the updater from taking over those records. Old per-user test installations must be closed and uninstalled normally; Unity profiles/settings remain outside payloads.

The protected `maintenance/` broker uses protocol 1 and the pinned public key. It lives outside replaceable `current/` and is refreshed by installers. Gameplay/download remain unelevated; activation prompts UAC after actual Player exit. Copied packages are authenticated in protected staging before the root updater executes. Cancelling UAC preserves the staged update and old version, suppresses automatic repeated prompts for that version, and leaves explicit restart retry available. No ordinary-user write permission is granted to executable contents. Installer finish launch uses the existing unelevated desktop Explorer and fails without an elevated fallback.

Real Windows wizard, privilege boundaries, default/opt-out options, UAC cancellation and two activation paths require the user's PC; cross-compilation and source contracts do not close those gates.

Published test landing: https://robowar.afonasev.tech/desktop/test/. Windows installer is `0.1.0-test.5` (04.10.2026), its signed feed offers `0.1.0-test.6`; macOS remains test.3 → test.4. Windows wizard/launcher source is `047eadd`, feed-only packaging tool is `0346bfc`; unchanged Unity release Player binds `5f601b06387ddbda5b34c8587c30a5787ea542dd`. These versions deliberately share game code to exercise the update lifecycle. macOS download targets Apple Silicon; Intel Mac distribution is not supplied by this candidate.

Passed: full `make check` (334 EditMode / 202 PlayMode), fresh non-development Windows/macOS builds, 14 launcher authentication and 4 publication contracts, native menu consent/progress/staging test, and real baseline Unity macOS explicit-restart and ordinary-exit update paths. See `docs/evidence/native-install-update/` for primary logs and four exact package identities.

Still pending: exact release native in-menu actions/focus and background gameplay; offline/interrupted recovery manual QA; preserved profiles/settings; macOS PKG OS installation; clean Windows installation and manual Windows gameplay/update on the user's PC. The release macOS UI run waited over ten minutes for an exclusive host guard and was cancelled before Player launch; no foreign process was changed. Packaging or macOS automation does not close Windows acceptance or authorize calling the test build a completed production release. Keep the owned branch/worktree, exact Windows test.5 installer on VPS, test.6 update, and macOS test.3/test.4 artifacts until these gates and integration are resolved.

Windows compiler: Ubuntu VPS `gfe`, official NSIS 3.10 (`3.10-2ubuntu2`). The local Homebrew 3.13 compiler failed on language-table allocation; the tested compiler route transfers only public candidate payload, verifies the returned executable hash and removes its UUID-owned temporary staging. The signing key remains local.

The actual .NET 8.0.31 Windows broker uses the static singlefilehost: its inspected bundle contains only managed assemblies/config, zero native extraction entries. Full NSIS test.5 compilation succeeded and the protected remote output was hash-verified into its immutable public location. The redundant slow SCP mirror was intentionally cancelled; `package.py` consequently exited 1 before its signed test.5 feed step. No test.5 feed was promoted. Independently completed `--update-only` test.6 packaging and its signed publication passed; source/artifact/custody evidence is in `docs/evidence/native-install-update/windows-wizard/`.
