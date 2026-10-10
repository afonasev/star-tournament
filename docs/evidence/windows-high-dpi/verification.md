# Windows DPI manifest port — 2026-10-10

Baseline: `8116b624a5ae5136b88a8cebfcb6d1272878c28d`.
Source fix: star-racing `24231df47ff7c16ccd6e8a05e47281809c4f172e`.
Owned branch: `codex/windows-high-dpi`; managed worktree `windows-high-dpi/star-tournament`.

Scope: port embedded process DPI awareness and packaging validation to the .NET launcher. Racing loads Unity in its bootstrap process; Tournament launches Unity as a separate process. This change declares `PerMonitorV2, PerMonitor` and legacy `true` in the launcher's native EXE, preserving `asInvoker`. It does not establish the Unity child process's effective DPI awareness or physical 300% behavior.

Affected dependencies: .NET Windows apphost generation, Win32 manifest resources, packaging before Velopack. No Unity runtime or gameplay change. Minimum sufficient checks use the real .NET launcher and MinGW PE probes; Unity/full tests and Player builds are unnecessary for these resource contracts.

Command:

```sh
DOTNET=/Users/eaafonasev/.dotnet/dotnet DOTNET_ROOT=/Users/eaafonasev/.dotnet python3 -m unittest discover -s distribution/tests -p test_windows_dpi.py -v
```

Result: 6/6 passed, 2.431s. Includes actual self-contained Windows launcher resource inspection, preserved unelevated privilege, paths with spaces, missing manifest, incidental XML, unaware manifest and truncated executable rejection. The actual launcher QA output and probe outputs are temporary and cleaned after inspection; no installer/release was built or published.

`git diff --check` passed. Physical Windows resolution/fullscreen and 300% scaling acceptance remain open. Unity Player/release builds, distribution and full regression remain deferred to separate human commands.

Reference: [Microsoft process DPI manifests](https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process).

Integration baseline: `3f6c31bd8114a3aefc8151642146988ae771a359`; intervening diff changes first-person movement and Lab history, with no distribution dependency. Rebased without conflicts; the tested distribution sources are identical. Removed owned temporary launcher output, bin/obj and Python caches (about 146 MB); unittest temporary probes were removed automatically. Durable evidence is this committed record. Pre-existing primary-checkout Unity ProjectSettings changes are outside this task.
