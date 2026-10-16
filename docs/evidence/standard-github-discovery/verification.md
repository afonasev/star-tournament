# GitHub API discovery/cache

Baseline codex/game-standard-alignment@2a27584e, template60fe3d2. Nonvisual source changes: both pinned channels use public API; Link-driven bounded semantic/channel/paired-file selection, exact signed full asset presence, no stale admission. Atomic bounded per-page ETag/body/hash/next-link cache in launcher data; cached representation accepted only on304, no offline-current fallback. Retry-After date/delta and exhausted-primary reset respect server deadlines across launches; no sleeping/retries. Legacy UpdateChannel=null untouched.

Docs: Context7 `npx --yes ctx7@latest library github ...` failed fetch. Read official GitHub Releases API, Best practices, API versions17.10.2026. API2022-11-28 remains supported until2028-03-10; no unnecessary API migration. Anonymous304 not claimed quota-exempt. Pinned repo redirects fail closed instead of allowing remote source relocation. Cache is untrusted locator data, not package trust.

Before execution minimum affected .NET groups: new --github-discovery actual HttpMessageHandler/RSA/filesystem fixtures; existing default security contracts (Program main body only, not all mode-selected fixtures) for signature/redirect/admission/package/cancellation; --update-session for one-check/cancel/retry/match-drain. Actual production C# compiled by test runner; no Python copy of logic, external publication/network needed for pass, Unity/full/Player/build/installer/release/deploy.

Commands: /Users/eaafonasev/.dotnet/dotnet run --no-restore --project distribution/tests/Launcher.Contracts.csproj -- --github-discovery ; same without extra selector runs only existing security fixture; -- --update-session runs only session fixture. Logs retained separately. Source package SDK unchanged; test runner build is not Player/release build.

First launch NotRun: removed obj/project.assets.json; offline local NuGet restore succeeded. First executed attempt failed in cache fixture: Query.Contains("page=1") also matched per_page=100 and returned a repeated page2 link; production cycle guard correctly refused. Fixture now matches the complete page parameter and selects corrupted cache by stored URI, independent of filesystem order. No production guard/assertion weakened. Logs initial-notrun.log/restore.log/fixture-pagination.log retained.

First complete new fixture:63 checks passed. Source review added fresh Link-header merging on304 (page may gain a next page without changed body), and cooldown on successful remaining0 responses before another page. New cases exercise both. The whole new fixture is repeated because the shared API-response path changed; existing security/session neighbors run after it.

One compile-only attempt NotRun: nested local next conflicted with later outer next under C# scope rules. Renamed local cachedNext; no behavior/assertion changed. compile-next-notrun.log retained.

Final response-path fixture initially67 checks Passed; existing security49 and update-session21 Passed on same runtime source. Added a real API403→UpdateSession integration case to prove repeated Start/check/menu/download routes make exactly1 request and no download/stage; repeat new fixture only (test addition, runtime unchanged). Existing security/session results remain valid on exact runtime snapshot.

Actual API→UpdateSession integration passed;69 new discovery checks total. Final review moved disk cache read/hash/JSON parsing to Task.Run (cache enabled only), so a persisted multi-MB page cannot occupy the launch path before first HTTP await. This is source async routing, not a performance claim/benchmark. Repeat69 discovery +49 security +21 session on final runtime source; no broadened game/full suite.

Final exact runtime snapshot:69 GitHub discovery +49 security +21 update-session contract checks Passed (139 selected checks), exits0. Existing startup fixture nullable warnings remain in test project; no new production compilation error, no assertions weakened. These are console contract checks, not NUnit test-case counts or installed acceptance. No Unity/Player/full/release/installer build/publication.

Official references: [GitHub best practices](https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api), [Releases](https://docs.github.com/en/rest/releases/releases), [API versions](https://docs.github.com/en/rest/about-the-rest-api/api-versions). Anonymous requests are supported;304 is not described here as quota-exempt.

Next DMG work must preserve compatibility with existing hard-PKG client discovery (e.g. keep legacy PKG as additional transition asset alongside required DMG); do not make old clients unable to discover the next update by silently dropping its required asset. Current selection still requires the current paired installers and two full packages plus exact signed current asset; source matrix keeps DMG gap open. Native/OS/updater/data gates are not closed by HTTP tests.

Final platform review added explicit ManifestName presence before candidate ranking. Primary paired-file policy alone did not exclude a newer row missing the current optional architecture manifest (e.g.osx-x64). New case selects an older complete matching architecture rather than fetching an absent newer manifest; does not claim native Intel acceptance. Rerun the3 selected groups after selector change; no other source scope expanded.

Final after architecture guard:71 discovery +49 security +21 session =141 actual C# contract checks, all exit0. Source hashes in verification.json bind the tested final snapshot. Cache cooldown does not authorize any update; full package remains signature/identity/version/platform/channel/size/hash protected. Installed app/OS compatibility gates remain open.
