# Windows update errors

Baseline: 05c03b72. User authorized readable update failures and applicable sibling fixes on 2026-10-11 in chat 01a12a2e-c7d8-73c2-a71f-0d5ac6d1f172.

Affected paths: launcher/maintenance error reporting, durable apply-attempt journal,
network stall deadline and native menu status/retry. Preserve package signature,
SHA256, platform/channel checks and elevated-install protections.

Focused checks (no full suite):
- dotnet run --project distribution/tests/Launcher.Contracts.csproj -c Release -- --feedback: actual error classification, journal persistence/retry, authenticated package download deadlines.
- dotnet build distribution/maintenance/Maintenance.csproj -c Release: compile Windows maintenance only; no publishing/installers/Player.
- UNITY_RUN_MODE=shared unity/tools.sh test-play-filter NativeUpdateMenuTests: real native menu IPC, persistent error and correct retry action plus existing consent/identity checks.
- git diff --check.

Sibling fixes: retain errors through polling (Spacewars), persist apply attempts,
consistent authentication and bounded network stalls (Star Racing). PowerShell
quoting is inapplicable because this launcher uses ProcessStartInfo.ArgumentList.
Unsigned production verification from Star Racing must not weaken this client's
signed-only trust policy.

Windows MessageBox/UAC/restart/physical visual acceptance remain pending. No
Player build, installer, release, publication or installed-client update authorized.
