# Startup quiet-audio contract — 2026-10-14

Baseline: local `main` at `21f8f3ec5950314506d1eecb4e7d92a5629bbd6f`; fetched `github/main` was behind and is not this checkout's baseline. The primary checkout had unrelated `ProjectSettings` changes and was not edited. Worktree: `/Users/eaafonasev/.codex/worktrees/startup-quiet-audio/star-tournament`, branch `codex/startup-quiet-audio`.

Scope: parse `-muted` before scene behaviours and pass Unity `-noaudio`, hold the session mute against review/audio overrides, make tool wrappers quiet by default with a clearly explicit human-audio opt-in, ensure QA launcher and update relaunch preserve quiet mode, and document exact invocations. No simulation, balance, input, or effects behavior changes.

Affected QA, selected before execution:

- `python3 -m unittest tools.test_unity_tools`: wrapper argument routing, default quiet mode, explicit human opt-in and flag precedence. No Unity runtime needed.
- `./unity/tools.sh test-edit-filter 'QuietLaunchWinsBeforeReviewsAndHumanAudioIsAnExplicitOptIn;QuietListenerCaptureRetainsFileSamplesAndClearsTheDeviceBuffer'`: verify `-noaudio -muted` precedence and the offline capture filter retains file samples while zeroing the outgoing DSP buffer. Uses Editor batch mute from process launch and does not enable audible output.
- `./unity/tools.sh test-play-filter 'NativeAudioStartupMuteTests'`: compile and exercise the actual `BeforeSceneLoad` callback and verify a `-noaudio -muted` PlayMode session cannot lift the listener mute. This is the minimal Unity lifecycle check for the change; a Player build is not needed because the contract is implemented in shared runtime code and the same callback runs before scene behaviours in Editor/Player.
- `dotnet build distribution/launcher/Launcher.csproj --no-restore`: compile the launcher forwarding change, if the local target/runtime is available.

Full suite, Player/release build, publication, deploy, installed-game update, and human/device/audio acceptance are outside this change's authorization.

## Results

- `PYTHONDONTWRITEBYTECODE=1 python3 -m unittest tools.test_unity_tools`: **10/10 passed**; wrapper argument recording confirmed both `-noaudio` and `-muted`, the interactive opt-in, review/test mute priority and no duplicate flags.
- `./unity/tools.sh test-edit-filter 'QuietLaunchWinsBeforeReviewsAndHumanAudioIsAnExplicitOptIn;QuietListenerCaptureRetainsFileSamplesAndClearsTheDeviceBuffer'`: **2/2 passed** under Editor batch `-noaudio -muted`. Evidence: `.local/unity-evidence/focused-editmode.Khxz4D/`.
- `./unity/tools.sh test-play-filter 'NativeAudioStartupMuteTests'`: **1/1 passed** under Editor batch `-noaudio -muted`; asserts both launch args, that the `BeforeSceneLoad` callback ran, and that review/listener overrides cannot unmute the session. Evidence: `.local/unity-evidence/focused-playmode.BnWVyk/`.
- The first broader `NativeMusicTests` run found one stale test expectation that `-humanAudio` could unmute a `-musicReview` run; that expectation contradicted the required auto-review mute rule. It was corrected, then the two affected tests above passed.
- `sh -n unity/tools.sh tools/unity.sh` and `git diff --check` passed after restoring Editor-generated `ProjectSettings` serialization changes.
- `dotnet build distribution/launcher/Launcher.csproj --no-restore`: **NotRun** because `dotnet`/SDK is not installed on this host. Launcher edits were inspected but not compiled.
- Native MusicReview WAV render, Player build, installed-game launch, device/audio listening and human acceptance were not run. If Unity `-noaudio` disables the DSP callbacks, the WAV review must remain NotRun under the documented rule; the focused EditMode test only verifies sample retention/output clearing in the filter implementation.

Full suite and all Player/release/publication/deploy gates remain deferred.
