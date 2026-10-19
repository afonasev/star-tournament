# Windows protected recovery — промежуточный срез

Baseline `7b5fd153`, ветка `codex/game-standard-alignment`. Это часть UPDATES-RECOVERY-01; автоматическое восстановление после неудачного первого старта ещё не подключено.

Перед запуском фиксированного защищённого `root/Update.exe` broker сохраняет полный `current/` и `.portable`, `Star Tournament.exe`, `Update.exe`. Состояние хранится в `maintenance/recovery/state.json`, привязано к корню установки и appId. Фазы capturing/prepared/installed/restoring/restored отделяют незавершённую копию от разрешённого изменения. Повреждённая метаинформация, чужой корень, reparse paths, изменённая копия и неизвестная фаза останавливают операцию. Ошибка updater или readback восстанавливает payload и bootstrap; прерванное восстановление повторяемо. Сбойная версия блокируется для всех пользователей до явного retry.

UAC worker проверяет точный PID/start-time/path/session неадминистративного supervisor, защищённый root, install lease, отсутствие активных процессов и SHA256 выбранного подписанного envelope. Elevated код не принимает executable/path из пользовательского recovery JSON. Новые recovery-файлы и каталоги создаются сразу с защищёнными owner/DACL; их Windows API ещё требует проверки на Windows.

После подтверждённого menu-ready обычный launcher может только дописать байт в защищённый `<id>.ready`: он не может обнулить, заменить или удалить файл. Непустой либо потерянный marker запрещает downgrade. Он не разрешает запись приложения и сам по себе не разрешает cleanup; elevated cleanup также сверяет аттестацию текущего startup и installed identity. Этот консервативный запрет действует между пользователями установки. Ошибка записи marker логируется без потери наблюдения за живым Player.

Ручное восстановление из PowerShell, когда все игровые процессы закрыты:

```powershell
& "$env:ProgramFiles\Star Tournament\maintenance\StarTournamentMaintenance.exe" --recover
```

Путь заменяется фактическим защищённым каталогом установки. Нормальный процесс фиксирует transaction ID до UAC, elevated worker повторно проверяет его перед изменением. Команда не зависит от работоспособности `current/` и не запускает игру после восстановления. Это реализованный маршрут, но не проверенная Windows-инструкция на устройстве.

Новые installer layouts содержат maintenance protocol 2. Protocol 1 ещё позволяет игру, но новая активация требует нового установщика; unsafe fallback отсутствует. Согласованный repair/upgrade экран07 и обновление существующей установки ещё предстоят. Нынешний installer предназначен для fresh install.

## Проверки и границы

`windows-verification.json` содержит команды, исходные SHA256 и результаты. 30 actual C# portable filesystem contracts, 40 shared payload contracts, 10 Mac process fixtures и 34 feedback contracts прошли; maintenance Debug win-x64 компилируется с 0 warnings/errors. 16 installer tooling cases используют mocked compiler и NSIS preprocessor, не создают поставочный installer. Первая ошибка Windows fixture была сравнением массивов по ссылке; исправлено на полное структурное сравнение сериализованного состояния, assertions не ослаблены. Исторические неудачные логи сохранены.

Не выполнены: реальный UAC/ACL/PInvoke, normal-user Windows watchdog/first-launch automatic restore, изоляция унаследованных ACK stdout handles при restart, exact orphan Player exit, Windows repair/upgrade, user-data/format migration и rollback compatibility, channel switching, реальная установленная пара launcher/Player, device/human acceptance. Unity/Player/installer/release build, full suite и публикация не запускались. UI не менялся.

## Проверенные первичные источники

Velopack 1.2.158 nuspec указывает commit `3c7f52c1bf17d10ad21b794b006d5ebd1a879a3b`. [UpdateManager.cs](https://github.com/velopack/velopack/blob/3c7f52c1bf17d10ad21b794b006d5ebd1a879a3b/src/lib-csharp/UpdateManager.cs), строки284–311: download извлекает Update.exe. [WindowsVelopackLocator.cs](https://github.com/velopack/velopack/blob/3c7f52c1bf17d10ad21b794b006d5ebd1a879a3b/src/lib-csharp/Locators/WindowsVelopackLocator.cs), строки122–147: readonly install root переносит updater/packages в per-user fallback. Поэтому elevated apply использует фиксированный защищённый root updater и сохраняет bootstrap до activation.

Права append и security descriptor создания: [File security and access rights](https://learn.microsoft.com/en-us/windows/win32/fileio/file-security-and-access-rights), [CreateFileW](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew). Эти документы обосновывают выбранные права, а не доказывают исполнение Windows API на macOS.
