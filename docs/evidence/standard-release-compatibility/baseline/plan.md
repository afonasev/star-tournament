# План проверки
Baseline: `codex/game-standard-alignment@f68bdb63`.
Изменение: общий baseline Windows10/19043 и macOS12; admission до Player/discovery в launcher, обеих Windows activation boundaries, macOS prepare/apply и NSIS broker extraction. Detached подпись/package inspection, recovery/uninstall остаются без глобальной launch barrier. Исправлена explicit shared dependency maintenance на GitHubDiscovery.

Команды (только affected, без Player/установщика/full):
- `dotnet run --no-restore --project distribution/tests/Launcher.Contracts.csproj -- --platform-requirements`: настоящая production policy, below/exact/above/wrong host.
- Тот же runner `--` (security default): shared source dependency / detached cross-target admission and package bytes.
- Тот же runner `--mac-recovery`: new prepare/apply boundaries, сохранность restore path; fixture processes имеют quiet arguments, не запускают Unity.
- `python3 -m unittest discover -s distribution/tests -p test_windows_installer.py`: installer routes, floor order, настоящий makensis -PPO macro expansion без installer output.
- Runner `--feedback`: объяснение OS rejection и соседние update feedback/quarantine contracts.
- `dotnet build --no-restore distribution/maintenance/Maintenance.csproj`: compilation shared sources, не publish/installer.

Windows native OS/runtime/device acceptance не доказана macOS fixtures. Target-specific signed requirements, updater/data/payload compatibility остаются следующими обязательными пунктами child. Baseline floors — не завершение полного target compatibility.
