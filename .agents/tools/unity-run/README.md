# unity-run

Локальный запуск Unity для одного OS account на этой машине. Python 3 stdlib, один lockfile `~/.local/state/unity-run/host.lock`, без постоянного сервиса, runtime lease JSON и ручной очереди сессий.

```sh
~/.local/share/unity-run/install.sh
~/.local/bin/unity-run --status
~/.local/bin/unity-run --shared --project /absolute/project -- /Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath /absolute/project -runTests -testPlatform EditMode -testResults /absolute/edit.xml -logFile /absolute/edit.log
~/.local/bin/unity-run --exclusive -- /absolute/Game.app/Contents/MacOS/Game
```

- `--shared`: до четырёх одновременно работающих команд; tests/build или автономные diagnostics с независимыми logs, builds и QA data.
- `--exclusive`: одна команда без других guarded запусков; performance, UI/focus/keyboard/mouse/gamepad QA. После захвата admission новые shared ждут завершения exclusive; FIFO не обещается.
- `--project`: сериализация одного canonical project path, включая symlink aliases. Editor `-projectPath` определяется автоматически. У каждого worktree собственная Library; общие симлинки Library не допускаются.
- `--timeout N`: необязательный лимит ожидания; exit 75 при занятости. Без лимита helper ждёт внутри процесса, без опроса соседних agent-сессий.
- Exit code команды сохраняется; сигналы дают 128+signal. Normal cancellation останавливает только собственную process group и освобождает capacity после exit/reap.

На invocation создаётся временный guard. Он держит POSIX byte-range locks, запускает direct executable и ждёт его выхода. SIGKILL клиента после старта не снимает защиту живого Unity. Client exit до старта отменяет queued работу. Никаких файлов владельцев или ручного recover нет; OS снимает locks при exit guard.

Не удаляйте, не заменяйте и не ротируйте `host.lock`. Не запускайте через `open -n`, Unity Hub или инструменты, которые возвращают управление раньше своего workload. Прямые обходные запуски, SIGKILL самого guard и отделённые потомки, живущие дольше direct executable, вне гарантий механизма. Начатые до установки команды не подхватываются автоматически.

Planning/integration leases нужны только для Git/specs; освобождайте их перед ожиданием Unity. Старый `unity-runtime` и coordinator ACK/HOLDNEXT не используются. Игровая координация и human acceptance остаются отдельными.

Проверки: `python3 -m unittest -v test_unity_run.py`. `UNITY_RUN_LOCK_FILE` предназначен только для изолированных тестов; обычные проекты используют default файл. Подключение в проектах: `unity/tools.sh` у Star Tournament, `tools/unity.sh shared|exclusive ...` / `tools/unity.sh player EXECUTABLE ...` у остальных. `UNITY_RUN_MODE=shared` разрешён для автономного Player diagnostic; default player/editor UI — exclusive.
