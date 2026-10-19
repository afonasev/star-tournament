# Windows first-start supervision — source slice

Baseline `2fe7a308`, branch `codex/game-standard-alignment`. This extends the protected transaction, not a Windows device acceptance result. No UI design changed.

## Supported entry and lifecycle

New protocol-2 installer layouts place the exact self-contained maintenance binary at the root as `Star Tournament.exe`, with the game icon and WinExe output type. Root entry forwards argv to fixed `maintenance/StarTournamentMaintenance.exe --launch-stable`. The stable normal-user host acquires a per-install/per-user observer lock, reads the protected pending attempt even if `current/release.json` is broken, and launches fixed `current/StarTournamentLauncher.exe`. After native apply, the elevated broker reinstalls the root stable entry from its protected maintenance binary before it commits installed. The previous root entry remains in the verified bootstrap snapshot for rollback. Regular full-update packages do not replace maintenance itself; protocol upgrade/repair still needs the installer flow07.

Immediate update restart uses the same stable observer. Deferred first launch through the supported root entry is also guarded before replacement launcher initialization. Launching an intact current launcher directly additionally starts the stable watcher through an exact PID/start-time/path/session ACK handshake. A concurrent helper sees the existing observer lock and exits after ACK; it cannot perform a second recovery.

The observer returns on the exact protected transaction's append-only ready latch. If the launcher fails to start or exits before confirmation, the observer waits for the recorded exact orphan Player to exit. There is no kill or elapsed-time permission to overwrite live files. Before restoration it rechecks readiness and conservatively preserves a target already recorded healthy in the per-user journal. The UAC worker separately rechecks exact supervisor, frozen transaction, monotonic latch, install lease, protected paths and cross-user process absence. An access/read failure preserves backup and files. Cancelled/failed UAC neither relaunches nor repeats the prompt. A successful restore relaunches previous code with the same argv, including `-muted -noaudio` when chosen.

For each valid, timely Player marker, the protected Windows latch is now persisted **before** per-user journal confirmation. Invalid tokens cannot invoke the persistence hook. Failure to persist leaves the same launch pending and retries observation without abandoning the live Player; a successfully persisted latch conservatively prevents rollback even if the subsequent journal write fails. Mac uses the same journal/monitor with the default no-op precommit hook; its process fixtures were rerun.

## Child process output and authority

`WindowsDetachedProcess` invokes a fixed executable directly, with quoted argv and a `STARTUPINFOEX` inherited handle list containing only an inheritable NUL handle for stdin/stdout/stderr. The ACK pipe and other parent handles are excluded. The returned process handle provides exact child liveness without PID reuse. Disposing it does not terminate the child. No shell, command interpolation, elevated game or system sound change is used.

Primary API documentation: [CreateProcessW](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createprocessw), [UpdateProcThreadAttribute](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute), [redirected child handles](https://learn.microsoft.com/en-us/windows/win32/procthread/creating-a-child-process-with-redirected-input-and-output). These support the implementation choice; native execution is still NotRun on macOS.

## Verification and outstanding gates

See `windows-watch-verification.json` for exact logs, commands and source SHA256. Actual C# observer logic10, protected filesystem34, startup health15, journal29, Mac process fixtures10 and feedback34 passed. Python stable-entry5 + installer-tooling16 passed; compiler operations in tooling are mocked or NSIS preprocessor-only. Maintenance Debug win-x64 compilation passed with zero warnings/errors. The test harness's pre-existing CS8625 nullable negative-case warning is not a broker warning.

NotRun: native Windows `CreateProcessW`/handle-list/ACL/UAC and installed root entry; immediate/deferred update on an actual Windows device; real Unity Player, installer/release build and publication; human/device acceptance. Windows07 existing-install repair/upgrade, data-format snapshot/migration/rollback and cross-channel preferences remain implementation work before full recovery closes. Root bootstrap size grows because it contains a second self-contained stable binary; no release artifact was produced here.

Windows device acceptance, after a separately requested candidate build: use the supported root entry with `-muted -noaudio`; verify immediate restart and deferred first start, including a replacement launcher unable to initialize. Verify no inherited ACK pipe failure, live orphan prevents replacement, UAC cancellation produces one prompt/no relaunch, confirmed startup prevents manual/cross-user downgrade, and rollback restores bootstrap/payload/argv without touching profiles. These scenarios are pending, not inferred from portable fixtures.
