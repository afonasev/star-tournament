# Windows inventory kit QA

Baseline: cdf0b41b, branch codex/game-standard-alignment.
Changed behavior: new read-only Windows collector, bounded PE/metadata observations, report writer. No runtime/updater changes.

Affected checks only: PowerShell Parser on both shipped scripts and test; execute distribution/tests/Test-LegacyInventory.ps1 with verified PowerShell 7.6.7 macOS arm64; invoke collector on macOS and require exit 2 and absent output. These check real helper code and platform refusal. Windows PowerShell 5.1, CIM permissions, ACLs, UAC, actual installed binary identity and automatic update remain NotRun. No Unity, Player build, installer build, publication or full test suite.
