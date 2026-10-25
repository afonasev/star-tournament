# Native test distribution

The launcher is a self-contained .NET 8 application using Velopack 1.2.158. Unity retains company/product identity and all native gameplay/UI. The launcher waits for the actual Player process; it never kills a match or modifies a live payload. Metadata checks are automatic, package downloads require the explicit `download` command from Unity, and `restart` is acknowledged before Unity requests a normal exit. A staged package is verified again after exit and before the next Player starts.

Startup confirmation is separate from installation. The launcher gives each Player a fresh `STAR_TOURNAMENT_STARTUP_READY`, `STAR_TOURNAMENT_STARTUP_VERSION` and `STAR_TOURNAMENT_STARTUP_TOKEN`. Unity atomically reports `menu-ready` after critical initialization and two player-loop frames; background Lab history does not block this signal. The two-minute deadline never kills a live game. Only an exact timely marker clears the apply-attempt journal; exit code zero does not. A first unconfirmed launch cannot auto-apply a staged update, and an unconfirmed exit leaves it staged. The recovery branch still needs data compatibility, repair/upgrade and installed platform verification before release; see `docs/evidence/standard-startup-recovery/startup-contract.md`.

`release.json` belongs to each immutable package, and the main menu gets `<version> (dd.MM.yyyy)` from that installed package. The date is its publication day, not device time. The IPC session consists of an atomic status snapshot and a single command file outside replaceable contents. Single-flight download and an installation lock prevent overlapping operations. A recorded orphaned Player PID/start time prevents an accidental replacement after a supervisor crash.

## Authentication

`latest-v2-<runtime>.json` (historically `latest.json` / `latest-<runtime>.json`) is an envelope with Base64 UTF-8 payload and RSA-PSS/SHA256 signature. The signed payload binds schema, platform/architecture, SemVer, publication day and exactly one full Velopack asset including SHA256, size, package ID and filename. The pinned public key is `update-public.pem`. HTTPS is required. Download bytes are quarantined and checked before admission to the SDK, because the Windows SDK can extract its updater during download. Existing complete cache files and staged packages are independently checked; SDK startup auto-apply stays disabled. Delta packages are excluded in this first release.

Private signing key is outside Git and build worktrees at `~/.local/share/star-tournament-distribution/keys/updates-private.pem`, directory mode 700 / key mode 600. Preserve this key for future releases; do not upload it or include it in any artifact. OS developer signing/notarization is not supplied for these test installers.

## Commands

### Lab release profiles

The regular release Player includes `Assets/StarTournament/Resources/LabReleaseCatalog.json`. This immutable Git catalogue contains full released snapshots for every profile, with a global release sequence and the original profile/revision/hash identity. Each start selects the latest packaged release of `Default`. Other packaged profiles/releases and saved local experiments remain selectable in Lab for subsequent matches.

In Lab, save a revision and use **Релизная** to mark it locally. This does not publish it; packaged profiles are read-only locally. Use **Создать копию** on the selected saved release base to create a separate editable profile; its new revisions are unmarked local experiments. Existing local revisions under a packaged profile remain available for selection/copy, but cannot be extended or renamed locally. To explicitly stage all new marked saved revisions from a reviewed history file, run `unity/tools.sh lab-stage-releases /absolute/path/to/history.json` in the owning worktree. Review and commit the catalogue diff, run the full game gate and release Player smoke, then use the normal authorized client publication flow. The source history is preserved. Do not stage another person's history or unreviewed numeric changes. Build validation rejects invalid or unsupported catalogue snapshots and retains all previous releases. Profile IDs/names are preserved; a revision is not moved into Default.

The initial catalogue is explicitly bootstrapped from the authored Player baseline with `LabReleaseBuild.Bootstrap`; the bootstrap refuses to overwrite an existing catalogue. Local history remains outside the installed payload. Startup selection resets, while saved experiments and local release marks remain available.

Install .NET SDK 8 locally; install the pinned `vpk` 1.2.158 tool using that SDK. In the owning worktree:

```
DOTNET_ROOT=<sdk-directory> <dotnet> tool install vpk --version 1.2.158 --tool-path <tool-directory>
<dotnet> run --project distribution/tests/Launcher.Contracts.csproj
STAR_TOURNAMENT_RELEASE_OUTPUT=<absolute Player.app> unity/tools.sh build-release-mac
STAR_TOURNAMENT_RELEASE_OUTPUT=<absolute windows-directory/StarTournament.exe> unity/tools.sh build-release-win
gh auth token | python3 distribution/publish_release.py --version <SemVer> --date <yyyy-MM-dd> --channel test --windows-player <exact-windows-Player-directory> --mac-player <exact-Player.app> --output <immutable-output> --key <private-key> --dotnet <dotnet> --vpk <vpk> --source-ref <reviewed-public-mirror-commit> --changelog-review <reviewed-notes.json> --nsis-host gfe
```

This single command packages the paired Windows x64/macOS arm64 installers and full updates, signs each `latest-<runtime>.json`, checks custody/signatures/SHA256/size, uploads a draft, verifies the complete nine-asset set, publishes as a prerelease, then streams anonymous real HTTPS download readbacks. Credential is stdin only and never forwarded to download redirects. `--resume` validates the same packaged output and resumes a failed draft/upload/readback; immutable collisions stop without replacement. Keep both Player inputs and the output unchanged during a run. Signing key stays local; `gfe` receives only public installer payload for the pinned NSIS compiler. Existing low-level uploader remains available for historical asset work.

### Git-derived Russian/English release notes

Before an explicitly authorized publication, the preparing agent resolves the previous **published release of the same channel**, fetches its exact tag into the release source checkout, and checks the Git range. Run from the clean exact public source commit used to package the Players; keep review/output outside the checkout or under ignored `.local/`. A private-source SHA and a different public-mirror SHA are not interchangeable.

Offline scaffold and preview (these commands do not publish or build):

```sh
python3 distribution/release_changelog.py scaffold --source-ref <full-source-SHA> --channel test --previous-tag <previous-published-tag> --output <review.json>
python3 distribution/release_changelog.py render --source-ref <full-source-SHA> --channel test --previous-tag <same-tag> --review <review.json> --output <preview.md>
```

Omit `--previous-tag` for a first release. The scaffold's `pending` list is the actual Git history, including merged branch commits. During release preparation, the agent reviews that evidence and replaces it with `changes` (category `added`, `improved` or `fixed`, paired `ru` and `en`, list of full `commits`) and justified `omitted` entries (`commits`, technical `reason`). Clear `pending` only after every commit is covered exactly once. Write player outcomes in both languages; raw commit subjects, internal IDs, promises about unverified behavior and fabricated translations do not replace editorial review. The renderer groups both languages from the same entries and omits empty categories.

The publisher independently resolves the actual prior GitHub Release by channel/SemVer, checks its lightweight or annotated tag SHA against local Git, rejects shallow/divergent history, renders the reviewed range **before packaging/uploads**, and requires package custody to match the same clean source. The body uses `## Русский` / `## English`, matching the game's history parser. A missing translation, uncovered commit or stale range stops preparation without posting a release.

`release-notes.md`, `release-notes.identity.json` and the exact `release-notes.review.json` are preserved in the output with SHA256/source/channel/base identity. Resume requires identical input and GitHub body. Published release text and assets are never silently overwritten. New text for an already published identity requires a new release. The helper does not translate arbitrary Git subjects or certify semantic equivalence; paired player descriptions are authored/reviewed as part of the already authorized release preparation.

```json
{
  "schema": 1,
  "source_ref": "<full-source-SHA>",
  "channel": "test",
  "previous_release": {"tag": "<published-vSemVer>", "revision": "<full-base-SHA>"},
  "pending": [],
  "changes": [{"commits": ["<full-commit-SHA>"], "category": "improved", "ru": "Понятное изменение для игрока.", "en": "The same player-facing change."}],
  "omitted": []
}
```

For the first release, `previous_release` is `null`; its complete Git history still requires coverage. Generating notes is not permission to publish, build a Player or run the full game suite.

Production uses `--channel production` and a stable SemVer, explicitly marks the release Latest and requires separate production authorization. Test uses a prerelease SemVer and `make_latest=false`; it never turns a test into production. New configurations pin `updateChannel` and GitHub locator, with no VPS fallback. Production: `https://github.com/afonasev/star-tournament/releases/latest/download/latest-<runtime>.json`. Test: pinned public Releases API, bounded pagination, highest SemVer prerelease carrying both platform manifests and both versioned installers. The signed schema-1 payload binds `channel` and exact repository/version/package URL; schema 1 preserves old Windows broker authentication compatibility. Old brokers ignore channel but still verify RSA/identity/hash/size; fresh installers enforce channel too. No automatic channel switch or UI selector is introduced.

For bridge migration only, after public readback extend the fixed legacy relay manifest with the new exact package routes, verify the legacy HTTPS stream, then atomically promote the compatible signed `latest.json` on both old VPS feeds. Preserve old feeds/relay/releases. This is a one-time migration operation; future publication needs no VPS update. New launcher startup rejects missing channel configuration and obtains metadata/packages exclusively from GitHub.

Landing JavaScript fetches only CORS-enabled `api.github.com` metadata and uses normal versioned asset download links, preserving the installer filename. It does not fetch GitHub asset bytes/manifests in the browser: the initial asset redirect lacks CORS. If the public API is offline/rate limited (60 unauthenticated requests/IP/hour), visible links open Releases so the user can choose an installer; the tradeoff is one extra page. Deploy this landing script once; each later release is discovered without SSH. Landing prefers a complete production Latest release after its separately authorized publication, and shows the latest complete test prerelease while no production exists. The installed test client remains in its test channel.

macOS x64 can still be packaged with `package.py --channel ...`, but the paired publisher's agreed supplied macOS platform is arm64. Universal Player bytes do not prove physical Intel Mac acceptance.

Local Unity QA must pass `--qa-unity-runner "$HOME/.local/bin/unity-run"` to the launcher; this argument persists through the updater's restart and executes the real Player through the common shared guard. The environment variable `STAR_TOURNAMENT_QA_UNITY_RUNNER` is also supported. On the user's machine no local QA guard is required.

## Remaining acceptance gates

### Windows installation wizard

Windows packaging replaces the unpublished one-click Setup with an NSIS Modern UI 2 wizard over the complete portable layout. Default destination is system Program Files (64-bit) / Star Tournament, normally `C:\Program Files\Star Tournament`. The directory page permits only protected locations inside 64-bit Program Files. This branch connects same-channel same-version repair and newer upgrade through a protected sibling stage and rollback; retained startup backups now transfer with a monotonic floor and archived older snapshots until startup-authorized cleanup. Published installers are identified separately from this source branch. Desktop shortcut and finish launch options appear together on the last screen, are checked by default and can be cleared independently. The shortcut uses the common Desktop known folder and the production icon; creation failure is visible. NSIS owns machine uninstall registration and shortcuts; `.portable` prevents the updater from taking over those records. Old per-user test installations must be closed and uninstalled normally; Unity profiles/settings remain outside payloads.

New installer layouts use protected `maintenance/` broker protocol 2 and the pinned public key. Protocol 1 installations can still play, but safe activation now requires the new installer; the agreed existing-install repair/upgrade flow is still pending. It lives outside replaceable `current/` and is refreshed by installers. Gameplay/download remain unelevated; activation prompts UAC after actual Player exit. Copied packages are authenticated in protected staging before the root updater executes. Cancelling UAC preserves the staged update and old version, suppresses automatic repeated prompts for that version, and leaves explicit restart retry available. No ordinary-user write permission is granted to executable contents. Installer finish launch uses the existing unelevated desktop Explorer and fails without an elevated fallback.

Real Windows wizard, privilege boundaries, default/opt-out options, UAC cancellation and two activation paths require the user's PC; cross-compilation and source contracts do not close those gates.

Current landing: https://robowar.afonasev.tech/. Public source: https://github.com/afonasev/star-tournament. GitHub-only installers/full updates are `0.1.0-test.9` (06.10.2026), with signed `latest-win-x64.json` and `latest-osx-arm64.json`. Both legacy feeds now deliver the same compatible bridge. Previous test.7 installers/test.8 updates and corrected test.7 installer revision 2 remain immutable and downloadable. Exact source/Player identities and checksums are recorded in the GitHub release notes and local `docs/evidence/github-distribution/`. The public source mirror preserves the initial MIT LICENSE and third-party notices; internal QA captures/local history remain in the local repository. Apple Silicon is the supplied macOS installer; universal Player compilation is not Intel Mac acceptance.

Previous test.7/test.8 verification: full `make check` (342 EditMode / 207 PlayMode / Development build), Windows and universal macOS release builds, 28 C# security and 22 Python contracts, muted actual macOS menu smoke, all eight new asset URL/digest readbacks, real current-client direct GitHub download and original-client compatible download of signed test.8 packages. Windows installation/shortcut/UAC, OS installation of macOS PKG, in-game background download/two activation paths/profile/offline acceptance remain explicit device gates; compilation does not close them.

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

## GitHub-only bridge test.9

Delivered prerelease: https://github.com/afonasev/star-tournament/releases/tag/v0.1.0-test.9. Eight anonymous full asset readbacks and both real GitHub-only verifier downloads passed; recorded requests contain only GitHub API/repository/CDN hosts. Original pre-redirect verifier downloaded both actual bridge packages via the retained diskless relay; old feeds then promoted once and were re-read/verified. The relay retains all prior 14 routes plus two exact bridge routes. No distribution binaries were added to VPS.

Full gate: 342 EditMode / 207 PlayMode, Development build; 49 C# and 34 Python contracts. Paired Player files match published test.8 exactly, keeping this a distribution transition. Real draft resume uses authenticated release listing because by-tag lookup does not resolve an unpublished draft; a temporary real draft probe and duplicate cleanup are recorded. Published releases/tags/assets were never rewritten. Production happy path is covered by fixtures; no production release was published. Actual macOS complete-layout activation evidence and any host-capacity blocker are in `docs/evidence/github-only-updates/DELIVERY.md`; physical Windows/OS PKG acceptance remains pending.


### macOS recovery branch
Before an update handoff the launcher retains the complete healthy `.app` and starts supervision from that snapshot, outside the replaced bundle. The retained updater runs with `--norestart`; the supervisor starts the replacement with the original arguments and observes the version-bound startup confirmation. Failed installation/start restores the old payload only after the launcher and any orphaned Player have stopped. The failed version stays blocked until explicit retry after cleanup.

Until confirmation, `<launcher-state>/<appId>.recovery-<installation-id>/Restore-<id>.command` remains available. Close the game and execute that command to restore when the new launcher itself cannot start, then open the game normally. The command uses the retained runtime and does not need the broken executable. A confirmed release is not automatically downgraded later. Backup cleanup waits for the stable host to exit, and remains resumable after interruption. This branch still requires protected Windows/data integration and real installed-platform verification before publication; see `docs/evidence/standard-startup-recovery/mac-supervisor.md`.

### Windows recovery branch (partial)
Protected activation now snapshots `current/` and fixed root bootstrap files before apply, restores on updater/readback failure, and retains a machine-wide failed-target block. The manual `maintenance/StarTournamentMaintenance.exe --recover` normal-user entry freezes its transaction before UAC and works without the current launcher. An append-only startup latch prevents rollback after confirmed startup across users. Stable normal-user first-start supervision and the root entry now guard immediate and deferred launches in source; existing-install repair/upgrade, native Windows process/ACL/UAC checks and data compatibility remain open; see `docs/evidence/standard-startup-recovery/windows-protected-recovery.md`. No installer or Player release was built for this slice.

New protocol-2 installer layouts use the self-contained maintenance binary as the root `Star Tournament.exe` entry, forwarding argv into stable normal-user supervision before the replacement launcher starts. Elevated apply now stages and activates the authenticated full current payload inside WindowsRecoveryStore, without native Update.exe apply. The installer-owned stable root entry, updater and registration stay intact. Children inherit only NUL for standard I/O, excluding the supervisor ACK pipe. Windows startup latch persistence precedes per-user healthy confirmation. See `docs/evidence/standard-startup-recovery/windows-watch.md`; source fixtures and cross-compilation do not close real Windows runtime acceptance.

Windows07 source integration: the system wizard confirms repair/upgrade, privileged commands bind a live installer owner, snapshot fixed product registration values, stage outside the current installation and finalize before creating ordinary shortcuts. Dead marked protected broker-source directories are cleaned conservatively. Retained backups and the first upgrade without a journal now preserve a startup fallback; legacy playback protocol configuration is restored without replacing the new broker binary. See `docs/evidence/standard-installer-repair/integration/verification.json`; native Windows runtime/render/Registry acceptance and backup-transfer implementation remain open.

Windows installer startup transfer source: unconfirmed targets preserve the original fallback, confirmed floors capture a newer fallback while retaining older archives, and first manual upgrade also snapshots previous current/bootstrap. Same-version repair preserves floor semantics. Restoring a legacy launcher restores protocol1 configuration while keeping the new broker; legacy updates then use the new installer. Native file-sharing/ACL/UAC/Player and data-format compatibility remain open. See `docs/evidence/standard-installer-repair/transfer/verification.json`.

## Current GitHub startup discovery contract

Both pinned channels discover public releases through the GitHub Releases API, follow bounded trusted Link pagination and filter canonical semantic versions/channel/paired files. The signed manifest version and full asset must match the selected release listing. The existing updater comparison decides whether the admitted release is newer than the installed version; the GitHub latest alias is not used for discovery.

`launcher/release-discovery-v1/` under the existing per-user launcher data directory holds bounded atomic API page caches and a server-directed cooldown. ETags are conditional across launches,304 retains or updates pagination links, and errors never turn stale cached data into a successful current-version result. Retry-After and exhausted-quota reset are honored without sleeping/retrying discovery this session. There is no embedded shared GitHub token; the cache is locator data and never grants package trust. Reading/parsing cached pages is off the initial Player launch path. Legacy configuration without a channel keeps its historical compatibility route.

Focused actual C# evidence: `docs/evidence/standard-github-discovery/verification.json` (71 discovery,49 security,21 session checks). This is source/HTTP verification, not installed native acceptance. Minimum OS/component/data admission and macOS DMG packaging remain open in `docs/TEMPLATE_ALIGNMENT.md`; new Player/installer/release builds and publication require a separate human command.

## Минимальные ОС текущего Player

Unity6000.3 Player: Windows10 21H1 (сборка19043) или новее, macOS12 или новее. macOS target в проекте —12.0. Launcher проверяет host до discovery/запуска; native activation и Windows installer проверяют до замены файлов. Откат и удаление не блокируются общим launch guard. Detached проверка подписи/пакета не зависит от host. Основание: https://docs.unity3d.com/6000.3/Documentation/Manual/system-requirements.html (Desktop Player, не Editor).

Новые выпуски используют signed schema2 с target-specific OS/reader/payload/data requirements и binding к release.json внутри пакета. Старые schema1 неизменны; переход старых readers требует нового installer. Контракт и границы: [COMPATIBILITY.md](COMPATIBILITY.md). Baseline evidence: `docs/evidence/standard-release-compatibility/baseline/`; новый source этап: `signed-target/`.


### Modern/legacy migration publication gate
For the schema2 generation, `package.py` emits `latest-v2-<runtime>.json`. Ordinary paired publication requires `--legacy-bridge /absolute/path/to/paired-bootstrap-metadata`; the publisher validates the signatures, same-package legacy/modern pair and actual published tag/readback before packaging or draft mutation. It freezes those signed inputs for the remainder of the run. Production preserves exact legacy descriptor aliases to the bootstrap while Latest points to the current release; subsequent test releases omit old metadata names. Landing supports modern pairs. See [COMPATIBILITY.md](COMPATIBILITY.md#publication-routing-source-stage-2026-10-17) for the complete contract and open implementation boundary. **There is no completed first-bootstrap producer/broker migration yet**, so this source stage does not enable a new release or authorize publication. Historical0.1.5 is not a compatible bootstrap. Player/installer builds and publication still require separate human commands.

### Q9: macOS DMG source pipeline (17.10.2026)

Новые macOS first-install артефакты — `Star-Tournament-<version>-macOS-<arch>.dmg`. Для явно заказанной упаковки используйте отдельный Python environment с `pip install -r distribution/macos/requirements.txt`, затем запускайте `package.py`/`publish_release.py` именно этим Python. `--update-only` оставляет только update payload/metadata и не создаёт DMG. Само наличие source pipeline не разрешает сборку или публикацию.

Pinned Velopack1.2.158 создаёт complete portable bundle с launcher/updater, а `--noInst` исключает PKG. Внешний plist задаёт действительную минимальную ОС до упаковки; прежний bundle identifier и short-core/full-version contract сохраняются. ZIP admission предшествует native ditto extraction с сохранением вложенных bundle/file attributes и links. dmgbuilder проверяет read-only mounted image: полный tree SHA256/modes/links и Applications link, затем отпускает immutable output. Detach failure сохраняет owned mount path для восстановления, не удаляет mounted tree. Этот readback source проверен recording fixtures; настоящий DMG пока не создавался.

Modern paired collector требует DMG; исторические PKG остаются допустимы для опубликованного bootstrap и complete legacy landing. Если DMG присутствует и повреждён, fallback на PKG запрещён. Лендинг показывает инструкцию по фактически выбранному формату. Подписанные metadata, byte-frozen bridge и custody guards сохраняются. Evidence: `docs/evidence/standard-macos-dmg/integration/README.md`. Actual DMG/Finder→Applications/Spotlight/minOS/Gatekeeper/update/data acceptance остаются NotRun до отдельной команды на installer build. Legacy automatic Windows bootstrap всё ещё не завершён и блокирует выпуск.

### DMG discovery compatibility

The published0.1.6/0.1.7 reader requires a versioned macOS `.pkg` in the GitHub listing before ranking a candidate, so a DMG-only release is invisible to it. New releases retain the DMG as the main macOS installer and also publish a genuine Velopack-built `.pkg` with the same payload/version; this is a ninth custody-bound asset, never a renamed DMG or placeholder. New readers accept either DMG or PKG while retaining complete platform pairs and authenticated package requirements. Published0.1.7 stays immutable; native update discovery failure and the corrected release identity are recorded in the delivery evidence. Schema1/protocol1 migration remains a separate gate.

### macOS ad-hoc integrity gate

Unsigned distribution means no Apple Developer ID/notarization. The final bundle is nevertheless sealed using Velopack `--signAppIdentity - --signDisableDeep` before producing update/portable/PKG outputs. Packaging verifies `codesign --verify --deep --strict` on the extracted full update, portable and compatibility PKG; DMG creation verifies the input and the mounted output. Failed signatures stop before authenticated metadata/publication. Ad-hoc integrity does not make Gatekeeper trust the publisher: a quarantined first install may still require the per-app Open Anyway flow. Never remove quarantine or disable Gatekeeper as a packaging acceptance shortcut.

macOS bundles put non-code payload under `Contents/Resources/payload` and retain runtime/recovery paths with internal symlinks. Existing Unity/Mach-O signatures are preserved. Update readback restores Velopack's existing `.__symlink` representation before checking the complete seal; directory link spelling retains the trailing slash used by Velopack. Existing reader2 metadata and package-config authentication remain compatible.
