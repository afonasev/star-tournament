# Подробные правила проекта

Читай только при триггере из AGENTS.md. Пути в тексте относительно корня проекта.

## Запуски Unity на общей машине

- Все Unity Editor/Player запускай через `~/.local/bin/unity-run` или проектную обёртку `tools/unity.sh` (Star Tournament: `unity/tools.sh`). Общий файл `~/.local/state/unity-run/host.lock` находится вне репозиториев; установка и описание — `~/.local/share/unity-run/README.md`.
- `--shared`: до четырёх batch tests/build или автономных diagnostic процессов. `--exclusive`: performance, проверка UI, фокуса, мыши/клавиатуры/геймпадов. Один canonical Unity project path используется одним Editor; worktree имеют собственные Library, logs, builds и QA data.
- Старые `unity-runtime` JSON leases, ручные ACK/HOLDNEXT и резервирование за сессией заменены этим механизмом. Не создавать вопросы/зависимости OpenSpec ради ожидания runtime и не опрашивать соседние сессии. Обёртка сама ждёт ресурс только на время команды; planning/integration leases перед ожиданием освобождаются.
- Запускай direct executable, не `open -n`/Unity Hub. Не удаляй/не заменяй lockfile и не останавливай чужие процессы. При прерывании client живой Unity остаётся защищённым до выхода; ожидающая команда отменяется. Старые worktree должны получить актуальную обёртку перед следующим запуском.
