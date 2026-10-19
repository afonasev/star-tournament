# Профиль: star-tournament

Пути ниже — от корня code-репозитория. Сначала прочитай актуальный `AGENTS.md`; профиль не содержит команды на запуск исторической инициативы или разрешения на публикацию.

- Это игровой проект. Канонические planning_root/store и main_branch разрешай из `planning/workflow/project.json`. Не копируй planning в каждый code worktree.
- Используй существующие `flow-explore`/`flow-apply`/`flow-inbox` и `.agents/references/flow/initiative.md`. N/M считай по уникальным обязательным конечным задачам текущей инициативы; техническую готовность отделяй от human acceptance.
- QA выбирай по `.agents/references/qa-scope.md` и diff/зависимостям. Полный игровой набор — только с отдельным явным разрешением на этот прогон. Команды в workflow-профиле — кандидаты для проверки маршрута, не автоматический gate.
- До Unity читай `.agents/references/project-policy/unity.md`: соблюдай актуальные shared/exclusive/ownership правила и используй wrapper проекта. Не ставь глобальную эксклюзивную блокировку только из-за названия UI/Player. Запуск с самого старта `-noaudio -muted`; Player/installer/release/deploy без нужной отдельной команды не запускай.
- Перед интеграцией/cleanup используй `.agents/references/project-policy/planning.md` и применимые delivery/git правила; человеческая приёмка и её evidence независимы от технического счётчика.
- Перед финализацией читай `.agents/references/execution-policy.md`; отчёт содержит branch + SHA и отдельно проверенный факт интеграции. Очищай только собственные временные ресурсы после сохранения.
