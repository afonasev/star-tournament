# star-tournament — рабочие правила

- Перед созданием worktree и сохранением QA-данных читай `docs/WORKTREE_STORAGE.md`: выборочный checkout, временные данные в `.local/`, постоянные evidence в общем planning-store.

## Working boundaries

- Preserve scope, product contracts and others' files/processes. Questions/reviews are read-only; create an owned code worktree only before authorized edits when isolation is needed.
- Select minimal affected checks from recorded baseline/diff/dependencies. In game projects, full tests need separate human approval for this run, even at merge/release. In non-game applications and tooling, full tests are allowed within the authorized task under project rules without separate approval. Preserve test contracts and FAIL/NotRun; never infer human/device acceptance from QA.
- Explicit model/effort wins. Authorized new chats: Sol 6.1/medium; bounded work/subagents: Luna/medium or higher. Astra needs a specific complex decision or explicit choice. No implied delegation/message permission.
- Show real visual alternatives before implementation; concepts do not authorize publication.
- Before finalizing any new or changed map object, model, or map, complete and document its applicable asset-optimization pass; preserve the approved look and gameplay/spatial contracts.
- At first QA, delivery or finalization read `.agents/references/execution-policy.md`; it includes preservation/cleanup and reporting. Report branch + SHA and integration separately. Read `.agents/references/portability.md` on a new device or missing dependency. Other references below load only on their stated trigger.
- Autonomous games start silent: `-muted`, Unity `-noaudio -muted`, web `?muted=1` before autoplay. Never change system volume; missing support remains an open gate.
- Player build only by request or justified indispensable minimal QA build. Installer/release/deploy need a separate request; inspect composite commands before running.

- Параллельный Unity: `shared` по умолчанию для автономных проверок, включая UI/EditMode/PlayMode; `exclusive` только для замеров производительности или доступа к общему экрану, фокусу, системному вводу/аудиоустройству. До запуска читай `.agents/references/project-policy/unity.md`; название UI/Player само по себе не требует эксклюзивности.

- Выбор команд и причины affected QA — в `.agents/references/qa-scope.md`; Player/release/deploy — в `.agents/references/local-development.md`. Эти references содержат действующие gates; docs/tooling без изменения Unity runtime проверяй без запуска движка.

- Planning: `/Users/eaafonasev/Projects/star-tournament/planning`, store `star-tournament-planning`; читай его `workflow/project.json`. Каталог `/planning/` исключён из Git кода и хранит собственный `.git`; все code worktree используют этот единый home в основном checkout, не создают свои planning-копии. Code `openspec/` — исторический snapshot.

Подробные правила вынесены без отмены требований. Загружай только строку с подходящим триггером; не читай все references при старте и повторно читай только изменившийся или недостающий после compaction диапазон.

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

## Changelog для деплоя (решение пользователя 2026-10-25)

- Changelog для каждого деплоя/релиза оформляй по категориям маркированными списками: одно самостоятельное изменение — один пункт; не объединяй изменения категории в сплошной абзац. Для наглядных изменений прикладывай реальные скриншоты соответствующей версии рядом с пунктами, где это возможно и формат поддерживает изображения; добавляй краткие подписи и доступные читателю пути/ссылки. Если изображения не поддерживаются, давай ссылки на скриншоты. Скриншоты дополняют текст и не заменяют описание изменений.
