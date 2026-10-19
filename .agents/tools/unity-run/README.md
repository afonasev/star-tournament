# unity-run

Локальный запуск Unity для одного OS account на этой машине. Python 3 stdlib, один lockfile `~/.local/state/unity-run/host.lock`, без постоянного сервиса, runtime lease JSON и ручной очереди сессий.

Замените `Project.Tests.AffectedFixture` на существующий fixture, выбранный по diff и зависимостям.

```sh
~/.local/share/unity-run/install.sh
~/.local/bin/unity-run --status
~/.local/bin/unity-run --shared --project /absolute/project -- /Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -noaudio -muted -projectPath /absolute/project -runTests -testPlatform EditMode -testFilter Project.Tests.AffectedFixture -testResults /absolute/edit.xml -logFile /absolute/edit.log
~/.local/bin/unity-run --shared -- /absolute/Game.app/Contents/MacOS/Game -muted
```

- `--shared`: режим по умолчанию, до четырёх автономных Editor/Player-команд с независимыми logs, builds, QA data, портами и save/config paths. Включает UI fixtures, программные input events и автономные screenshots, если они не используют общий экран, системный фокус, физические устройства или замеры производительности.
- `--exclusive`: одна команда без других guarded запусков; замеры производительности или использование общего экрана, системного фокуса, физического ввода либо аудиоустройства. Перед выбором укажи конкретный общий ресурс или метрику; наличие UI/Player само по себе не требует exclusive. После захвата admission новые shared ждут завершения exclusive; FIFO не обещается.
- `--project`: сериализация одного canonical project path, включая symlink aliases. Editor `-projectPath` определяется автоматически. У каждого worktree собственная Library; общие симлинки Library не допускаются.
- `--timeout N`: необязательный лимит ожидания; exit 75 при занятости. Без лимита helper ждёт внутри процесса, без опроса соседних agent-сессий.
- Exit code команды сохраняется; сигналы дают 128+signal. Normal cancellation останавливает только собственную process group и освобождает capacity после exit/reap.

На invocation создаётся временный guard. Он держит POSIX byte-range locks, запускает direct executable и ждёт его выхода. SIGKILL клиента после старта не снимает защиту живого Unity. Client exit до старта отменяет queued работу. Никаких файлов владельцев или ручного recover нет; OS снимает locks при exit guard.

Не удаляйте, не заменяйте и не ротируйте `host.lock`. Не запускайте через `open -n`, Unity Hub или инструменты, которые возвращают управление раньше своего workload. Прямые обходные запуски, SIGKILL самого guard и отделённые потомки, живущие дольше direct executable, вне гарантий механизма. Начатые до установки команды не подхватываются автоматически.

Planning/integration leases нужны только для Git/specs; освобождайте их перед ожиданием Unity. Старый `unity-runtime` и coordinator ACK/HOLDNEXT не используются. Игровая координация и human acceptance остаются отдельными.

Проверки: `python3 -m unittest -v test_unity_run.py`. `UNITY_RUN_LOCK_FILE` предназначен только для изолированных тестов; обычные проекты используют default файл. Подключение в проектах: `unity/tools.sh` у Star Tournament, `tools/unity.sh shared|exclusive ...` / `tools/unity.sh player EXECUTABLE ...` у остальных. Для автономного Player/Editor выбирай shared; `UNITY_RUN_MODE=shared` используется в поддерживающих его обёртках. Exclusive нужен только для конкретного общего ресурса или замера производительности по `.agents/references/project-policy/unity.md`. Runner не добавляет mute-флаги: Unity запускай с `-noaudio -muted`, Player с `-muted`. Примеры не разрешают полный набор тестов, сборку или поставку; выбирай affected fixtures, а full запускай только после отдельного разрешения.
