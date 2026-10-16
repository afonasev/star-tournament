# Сохранение и восстановление payload — проверенный core
Baseline codex/game-standard-alignment@2b582de6; не интегрирован в main. Часть child align-standard-startup-recovery, не весь UPDATES-RECOVERY-01.

`PayloadRecovery.Capture` сохраняет полный каталог под новым id, сверяет source и копию и привязывает snapshot к абсолютному install path, версии и app/platform/arch/channel из настоящего release.json. Backup store должен быть отдельным от payload. Повтор id не меняет старую копию. `Verify` сравнивает независимую файловую инвентаризацию с манифестом: SHA256, полный набор entry, внутренние относительные ссылки и mode. Пути из manifest не используются для открытия/удаления внешних файлов. Запрещены linked store/ancestor, absolute/escaping/dangling links и reparse на Windows. Case aliases macOS не позволяют скопировать store внутрь приложения.

На macOS используется /usr/bin/ditto --rsrc --extattr --acl без shell: сохраняются bundle metadata, resource forks, extended attributes и ACL. Реальные framework links и user xattr проверены на временном fixture. Коды подписи/настоящий Unity bundle/installed launch не проверены. Манифест проверяет файлы/links/modes, не криптографическую подлинность snapshot и не все xattr. На Windows используется managed file copy с наследованием ACL защищённого store; Windows runtime/ACL проверка ещё предстоит.

`Restore` создаёт соседний `.next`, проверяет его, переименовывает candidate в `.failed`, ставит backup на место и повторно проверяет. Trusted restore intent связывает snapshot digest, install path и фазу. Повтор после interruption завершает тот же шаг, сохраняет snapshot, удаляет только собственный failed candidate. Чужие staging siblings/повреждённый intent не заменяются. Проверено, что удаление failed candidate не следует его ссылке в пользовательские данные.

После readback `StartupRecovery.MarkRestored` теперь оставляет фазу restored и BackupId. `ConfirmBackupRemoved` забывает backup только после физической очистки; блокировка target сохраняется. Это закрывает interruption между восстановлением, удалением backup и подтверждением очистки. PermitRetry до cleanup запрещён.

Все операции требуют внешнего per-install lease и подтверждённого завершения точного Player. API не останавливает процессы и не доказывает, что они остановлены. Для elevated Windows store, journal, code и fixed install root должны быть защищены и проверены stable broker; user-writable metadata не предоставляет elevated restore authority. Стабильный supervisor, bootstrap files Windows, Unity ready, args/data/migrations и launcher activation ещё не подключены. Бинарники нового launcher могут быть повреждены, поэтому запуск recovery не должен зависеть от них.

Проверки — настоящие .NET исходники и локальная файловая система macOS, без Unity и звука. `payload-process-verified.log`:34 passed, включая4 отдельных дочерних процесса, завершившихся Environment.Exit(91) в restore checkpoints; новый экземпляр восстанавливает файлы и завершает cleanup. Это process interruption, не физическое отключение питания/fsync storage acceptance. `core-retention.log`:29 passed. Предыдущие проходы23/27/30 сохранены; после относящихся правок выполнен последний affected набор. Full/Player/build/release/deploy не запускались.

```
/Users/eaafonasev/.dotnet/dotnet restore distribution/tests/Launcher.Contracts.csproj --source /Users/eaafonasev/.nuget/packages --ignore-failed-sources
/Users/eaafonasev/.dotnet/dotnet run --no-restore --project distribution/tests/Launcher.Contracts.csproj -- --payload-recovery
/Users/eaafonasev/.dotnet/dotnet run --no-restore --project distribution/tests/Launcher.Contracts.csproj -- --startup-recovery
```
Выбор affected: только новое filesystem recovery и изменённый durable journal/cleanup; simulation/UI/input не менялись. Дальнейшая интеграция обязана проверять реальные platform и Unity lifecycle, не только эти fixtures.
