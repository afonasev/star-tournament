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
gh auth token | python3 distribution/publish_release.py --version <SemVer> --date <yyyy-MM-dd> --channel test --windows-player <exact-windows-Player-directory> --mac-player <exact-Player.app> --output <immutable-output> --key <private-key> --dotnet <dotnet> --vpk <vpk> --source-ref <reviewed-public-mirror-commit> --nsis-host gfe
```

This single command packages the paired Windows x64/macOS arm64 installers and full updates, signs each `latest-<runtime>.json`, checks custody/signatures/SHA256/size, uploads a draft, verifies the complete eight-asset set, publishes as a prerelease, then streams anonymous real HTTPS download readbacks. Credential is stdin only and never forwarded to download redirects. `--resume` validates the same packaged output and resumes a failed draft/upload/readback; immutable collisions stop without replacement. Keep both Player inputs and the output unchanged during a run. Signing key stays local; `gfe` receives only public installer payload for the pinned NSIS compiler. Existing low-level uploader remains available for historical asset work.

Production uses `--channel production` and a stable SemVer, explicitly marks the release Latest and requires separate production authorization. Test uses a prerelease SemVer and `make_latest=false`; it never turns a test into production. New configurations pin `updateChannel` and GitHub locator, with no VPS fallback. Production: `https://github.com/afonasev/star-tournament/releases/latest/download/latest-<runtime>.json`. Test: pinned public Releases API, bounded pagination, highest SemVer prerelease carrying both platform manifests and both versioned installers. The signed schema-1 payload binds `channel` and exact repository/version/package URL; schema 1 preserves old Windows broker authentication compatibility. Old brokers ignore channel but still verify RSA/identity/hash/size; fresh installers enforce channel too. No automatic channel switch or UI selector is introduced.

For bridge migration only, after public readback extend the fixed legacy relay manifest with the new exact package routes, verify the legacy HTTPS stream, then atomically promote the compatible signed `latest.json` on both old VPS feeds. Preserve old feeds/relay/releases. This is a one-time migration operation; future publication needs no VPS update. New launcher startup rejects missing channel configuration and obtains metadata/packages exclusively from GitHub.

Landing JavaScript fetches only CORS-enabled `api.github.com` metadata and uses normal versioned asset download links, preserving the installer filename. It does not fetch GitHub asset bytes/manifests in the browser: the initial asset redirect lacks CORS. If the public API is offline/rate limited (60 unauthenticated requests/IP/hour), visible links open Releases so the user can choose an installer; the tradeoff is one extra page. Deploy this landing script once; each later release is discovered without SSH. Landing prefers a complete production Latest release after its separately authorized publication, and shows the latest complete test prerelease while no production exists. The installed test client remains in its test channel.

macOS x64 can still be packaged with `package.py --channel ...`, but the paired publisher's agreed supplied macOS platform is arm64. Universal Player bytes do not prove physical Intel Mac acceptance.

Local Unity QA must pass `--qa-unity-runner "$HOME/.local/bin/unity-run"` to the launcher; this argument persists through the updater's restart and executes the real Player through the common exclusive guard. The environment variable `STAR_TOURNAMENT_QA_UNITY_RUNNER` is also supported. On the user's machine no local QA guard is required.

## Remaining acceptance gates

### Windows installation wizard

Windows packaging replaces the unpublished one-click Setup with an NSIS Modern UI 2 wizard over the complete portable layout. Default destination is system Program Files (64-bit) / Star Tournament, normally `C:\Program Files\Star Tournament`. The directory page permits only protected locations inside 64-bit Program Files. This candidate supports fresh installation; existing complete machine installations must update in-game or be normally uninstalled first. Desktop shortcut and finish launch options appear together on the last screen, are checked by default and can be cleared independently. The shortcut uses the common Desktop known folder and the production icon; creation failure is visible. NSIS owns machine uninstall registration and shortcuts; `.portable` prevents the updater from taking over those records. Old per-user test installations must be closed and uninstalled normally; Unity profiles/settings remain outside payloads.

The protected `maintenance/` broker uses protocol 1 and the pinned public key. It lives outside replaceable `current/` and is refreshed by installers. Gameplay/download remain unelevated; activation prompts UAC after actual Player exit. Copied packages are authenticated in protected staging before the root updater executes. Cancelling UAC preserves the staged update and old version, suppresses automatic repeated prompts for that version, and leaves explicit restart retry available. No ordinary-user write permission is granted to executable contents. Installer finish launch uses the existing unelevated desktop Explorer and fails without an elevated fallback.

Real Windows wizard, privilege boundaries, default/opt-out options, UAC cancellation and two activation paths require the user's PC; cross-compilation and source contracts do not close those gates.

Current landing: https://robowar.afonasev.tech/. Public source: https://github.com/afonasev/star-tournament. Installers for both platforms are `0.1.0-test.7` (06.10.2026); authenticated update target is `0.1.0-test.8`. Exact source/Player identities and checksums are recorded in the GitHub release notes and local `docs/evidence/github-distribution/`. The public source mirror preserves the initial MIT LICENSE and third-party notices; internal QA captures/local history remain in the local repository. Apple Silicon is the supplied macOS installer; universal Player compilation is not Intel Mac acceptance.

Passed: full `make check` (342 EditMode / 207 PlayMode / Development build), Windows and universal macOS release builds, 28 C# security and 22 Python contracts, muted actual macOS menu smoke, all eight new asset URL/digest readbacks, real current-client direct GitHub download and original-client compatible download of signed test.8 packages. Windows installation/shortcut/UAC, OS installation of macOS PKG, in-game background download/two activation paths/profile/offline acceptance remain explicit device gates; compilation does not close them.

The signed descriptor additionally binds `downloadUrl` to the admitted version and filename in the pinned GitHub repository. New clients follow bounded HTTPS redirects only to GitHub asset CDN. Old published clients reject redirects; `legacy_release_relay.py` and the systemd unit preserve their exact original routes using a fixed manifest and streaming bytes without disk storage. Twelve old assets were archived with exact hashes and removed from VPS after identity/route readback. Keep the archived GitHub URLs for compatibility and pending historical acceptance. VPS retains legacy migration metadata, landing and relay, zero distribution binaries. New GitHub-only clients do not read that metadata.

Windows compiler: Ubuntu VPS `gfe`, official NSIS 3.10 (`3.10-2ubuntu2`). The local Homebrew 3.13 compiler failed on language-table allocation; the tested compiler route transfers only public candidate payload, verifies the returned executable hash and removes its UUID-owned temporary staging. The signing key remains local.

Historical test.5 build evidence: the actual .NET 8.0.31 Windows broker uses the static singlefilehost: its inspected bundle contains only managed assemblies/config, zero native extraction entries. Full NSIS test.5 compilation succeeded and the protected remote output was hash-verified into its immutable public location. The redundant slow SCP mirror was intentionally cancelled; `package.py` consequently exited 1 before its signed test.5 feed step. No test.5 feed was promoted. Independently completed `--update-only` test.6 packaging and its signed publication passed; source/artifact/custody evidence is in `docs/evidence/native-install-update/windows-wizard/`.

## Windows uninstall path repair (installer revision 2)

NSIS launches a temporary uninstaller copy, while its original installation root is already in `$INSTDIR`. The corrected wrapper preserves that root; registry equality, protected owned-root validation and process checks stay in place. Neither update packages nor gameplay were changed. Existing installations contain the earlier root uninstaller until a fresh installation; a game update does not replace this file.

For an existing copy at the default location, close the game and use Win+R:

```text
"C:\Program Files\Star Tournament\Uninstall.exe" _?=C:\Program Files\Star Tournament
```

Confirm UAC. `_?=` is the documented NSIS option preventing the temporary copy, so the old path assignment no longer points at the temp folder. Keep the final argument unquoted; use the actual installation folder in both places if it differs. Deleting the running uninstaller itself may require reboot. This is a manual Windows workaround, not a verified device result. After reinstalling with the corrected wrapper, verify normal Windows Settings/menu uninstall, profile preservation, and rejection of moved/mismatched or busy installations.
