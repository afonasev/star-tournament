# Подробные правила проекта

Читай только при триггере из AGENTS.md. Пути в тексте относительно корня проекта.

## Shared OpenSpec flow

- Канонические OpenSpec specs, changes, schemas и delivery records живут в shared planning home: `/Users/eaafonasev/Projects/star-tournament-planning` (store ID `star-tournament-planning`). Перед работой читай `workflow/project.json` в этом home; все поддерживаемые вызовы OpenSpec выполняй с `--store star-tournament-planning`.
- `openspec/` в code worktree — исторический snapshot, перенесённый в planning home. Не обновляй его и не создавай в нём новые changes; он сохраняется для воспроизводимых ссылок к старым коммитам. Новые требования, changes и lifecycle records создавай только в planning home.
- Для discovery используй `.agents/skills/flow-explore/SKILL.md`; для реализации и небольших inline fixes — `flow-apply`; для вопросов, human acceptance и finalization debt — `flow-inbox`. Не редактируй generated upstream skills.
- В `flow-inbox` нумеруй все предлагаемые пользователю варианты, чтобы на них можно было однозначно сослаться номером.
- Перед реализацией выбери и claim change. Если пользователь не выбрал change, покажи готовые варианты и спроси, какой брать. Не возобновляй historical/cancelled browser work автоматически.
- Прочитай `.agents/references/flow/lifecycle.md` перед lifecycle-переходами; `delivery.md` — перед sync, merge, deploy и cleanup; `initiative.md` — для многоэтапной работы; `efficiency.md` — перед выбором модели или делегированием.
- Human acceptance требуется для каждого результата. Явное решение о точном проверенном результате записывай через helper `accept` сразу или в другой сессии, независимо от cleanup и владельца технической работы. При отложенной приёмке используй `defer-acceptance`, продолжай разрешённую финализацию и не повторяй вопрос до возвращения пользователя к проверке. После технических gates helper `finalize` использует уже записанную приёмку либо переводит change в `awaiting-acceptance`. Согласование концепта не является приёмкой Player. Native distribution/deploy выполняй только в пределах отдельного решения, записанного в project profile; локальный кандидат не означает разрешения на внешнюю публикацию.
- Вопросы, feedback, evidence, usage и lifecycle храни в соответствующем change. У разных worktree нет отдельной базы статуса. Не объявляй acceptance или delivery по checked task boxes, времени или отсутствию ошибок.
