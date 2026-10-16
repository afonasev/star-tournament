# Startup recovery — этап 1, журнал
Baseline: edd095aedbd5bb695d93088a8ea184f25ce253b8. Реализован actual C# durable journal. Он пока не подключён к launcher/Unity и не восстанавливает payload: UPDATES-RECOVERY-01 остаётся открытым до platform integration.

Команды:
```
/Users/eaafonasev/.dotnet/dotnet restore distribution/tests/Launcher.Contracts.csproj --source /Users/eaafonasev/.nuget/packages --ignore-failed-sources
/Users/eaafonasev/.dotnet/dotnet run --no-restore --project distribution/tests/Launcher.Contracts.csproj -- --startup-recovery
```
`core-first.log`: NotRun, отсутствовал project.assets.json. После offline restore: `core-second.log`22 passed. `core-final.log`28 passed после дополнительных invariants/negative cases. Никаких Unity/Player/full/build/release/deploy. Это проверки настоящего исходника .NET; не доказательство platform/Unity/human acceptance.

Журнал требует внешнего install lease. Snapshot id — ссылка на уже проверенный platform snapshot, не доказательство его существования. Не вызывай Prepare до физического сохранения backup, MarkRestored до проверки файлов и ConfirmBackupRemoved до ready. Marker готовности обязан создаваться Player после меню; токен не заменяет Unity lifecycle verification.

## Следующий срез: payload retention
Реализован и проверен snapshot/restore core,34 filesystem checks и29 обновлённых journal checks. См. [payload-recovery.md](payload-recovery.md). Runtime пока не подключён; исходные28 checks относятся к предыдущему snapshot2b582de6.

## Подключён startup health
Launcher+Unity health handshake подключён (startup-contract.md):12 .NET+33 feedback+4 unique native Editor cases; final3 native tests повторены после cleanup fix. Snapshot/restore activation и stable supervisor всё ещё НЕ подключены. Текущий authoritative протокол описан в startup-contract.md и health-verification.json, не в историческом core-only срезе.
