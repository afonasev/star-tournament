# Загрузка обновления — согласованный макет03

Change `align-standard-update-download-flow`, baseline `ea6502dc`, ветка `codex/game-standard-alignment`; game-spec-templates@0c2d4562683252d54736f294dc1af463050eac17, каталог0.5.0.

Launcher: одна discovery за запуск; согласие на конкретную версию, отмена с drain, запрет позднего staging, коррелированный match request/ack и возобновление только прежнего согласия в меню. Unity: FIFO команд, фактическое ожидание ack до подготовки мира/Running, версия/процент/полоса/проверка/готовность и понятные ошибки без ручной повторной проверки. «Что нового»04 остаётся следующим этапом.

Минимальный набор выбран по diff: настоящий production C# UpdateSession и AuthenticatedSource с отменяемым HTTP; NativeUpdateMenuTests (InputSystem submit, все состояния, FIFO, stale ack, реальный QueueMatch и возврат); соседний FourPlayerMenuTests.StartLaunchesReadyRosterWithoutFocusAndDoesNotPauseWhileHeld. Полный набор и длительные матчи не запускались. Unity shared -noaudio -muted, профили/Lab изолированы. PNG — Editor RenderTexture 1920×1080, не Player/TV/human acceptance.

SDK закреплён Velopack1.2.158. Использован CancellationToken DownloadUpdatesAsync и linked token в собственном IUpdateSource.GetReleaseFeed: у SDK CheckForUpdatesAsync параметра отмены нет. Сверено с XML установленного пакета и [официальным справочником UpdateManager](https://docs.velopack.io/reference/cs/Velopack/UpdateManager). Внешняя сеть не использовалась для доказательства прохождения тестов: metadata cancellation fixture использует отменяемый HttpMessageHandler.

Открыты: «Что нового»/GitHub RU/EN история04, startup health rollback и целая установка; проверка совместно собранной установленной пары launcher/Player на Windows/macOS, UAC, реальные устройства и человеческая визуальная приёмка. При старом launcher, не поддерживающем ack, запуск матча завершается явной ошибкой подготовки, а не ложным подтверждением. Публиковать следует только совместимую пару после отдельного разрешённого build/platform QA. Автоматических Player build/release/deploy не было.
