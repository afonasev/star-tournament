# Windows legacy verification kit

User selected their Windows PC for the mandatory automatic 0.1.5/test.1 update check. This kit contains read-only inventory scripts and a Russian staged scenario, no executables or installation payloads.

Runbook: [LEGACY-CHECK-RU.md](../../../../distribution/windows/LEGACY-CHECK-RU.md). Downloadable kit: [StarTournament-Windows-legacy-check.zip](StarTournament-Windows-legacy-check.zip). SHA256: `eb426412603b00ed7462b768f5ec3bfbc4f04683ea8a21fcdfed1cae8140353b`. Manifest inside covers every other member; ZIP bytes tested against manifest and source scripts.

QA: three real PowerShell scripts parsed, 23 helper assertions passed on verified PowerShell 7.6.7 macOS arm64; collector refused macOS with exit2 and no report. Initial test fixture used macOS temp symlink; rejection was correct, fixture moved to owned plain-path scratch. Final evidence hashes identify tested source. Parser errors are redacted to avoid disclosing malformed metadata.

Windows PowerShell5.1/CIM/ACL/UAC, actual legacy binary identity, startup/argv/recovery and automatic update remain NotRun. The collector's exit0 means only inventory written. No Unity, Player/installer build, update or publication ran. Old native apply can kill resident handoff processes; that implementation gap remains open. User inventory is the next input; it does not waive legacy transition or release gates.
