# star-tournament — рабочие правила

- Planning: `/Users/eaafonasev/Projects/star-tournament-planning`, store `star-tournament-planning`; читай его `workflow/project.json`. Code `openspec/` — исторический snapshot.
- Явное решение пользователя имеет приоритет. Сохраняй утверждённые требования, exact scope, историю и чужую незакоммиченную работу; новые продуктовые развилки согласуй до реализации.
- Изменения выполняй в принадлежащем задаче Git worktree/ветке. Не возобновляй другой change автоматически. Не выдавай автоматические проверки за human/device/visual/audio acceptance; deploy/archive требуют существующей авторизации и gates.
- Ищи сначала `rg --files`, затем узким `rg -n`; соблюдай gitignore, не сканируй generated/Library/build/dist/logs и бинарные ассеты без необходимости. Читай только нужные диапазоны.
- Понятная ограниченная разработка: **GPT-6 Luna/medium**. Сложный scope и архитектура — по `.agents/references/model-routing.md`; явный model/effort пользователя важнее профиля. Делегирование не обязательно для каждого патча.
- После второго compaction — только завершение текущего среза, уже нужные проверки, commit и короткий handoff; затем остановись. Подробности `.agents/references/context-efficiency.md`.
- Координатор ждёт события завершения/вмешательства через `wait_threads` с `timeoutMs: 120000` и `afterCursor`; неизменившийся таймаут не повод перечитывать чат. Подробности `.agents/references/coordination.md`.

Подробные правила вынесены без отмены требований. Загружай только строку с подходящим триггером; не читай все references при старте и не перечитывай неизменившееся в одном срезе.

| Когда читать | Файл |
|---|---|
| Выбор модели или делегирование | `.agents/references/model-routing.md` |
| Длинная задача, первый compaction, batch или handoff | `.agents/references/context-efficiency.md` |
| Координация нескольких чатов | `.agents/references/coordination.md` |
| OpenSpec, claim, lifecycle, вопросы или приёмка | `.agents/references/project-policy/planning.md` |
| До удаления ресурсов, финализации или архива | `.agents/references/project-policy/cleanup.md` |
| Структурный или широкий поиск | `.agents/references/project-policy/search.md` |
| Новые продуктовые решения или изменение spec | `.agents/references/project-policy/specifications.md` |
| При выборе процедуры под тип задачи | `.agents/references/project-policy/conditional-procedures.md` |
| Изменение архитектуры, правил, состояния или asset pipeline | `.agents/references/project-policy/architecture.md` |
| Новое число баланса, поведения, камеры или читаемости | `.agents/references/project-policy/balance.md` |
| До любого запуска Unity Editor/Player | `.agents/references/project-policy/unity.md` |
| Нетривиальная реализация, worktree или поставка | `.agents/references/delivery-workflow.md` |
| Player-visible/input/audio/browser QA | `.agents/references/game-qa.md` |
| До выбора проверок и integration gate | `.agents/references/qa-scope.md` |
