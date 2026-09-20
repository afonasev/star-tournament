# Правила работы над Star Tournament

<!-- BEGIN MANAGED OPEN SPEC FLOW -->
## Shared OpenSpec flow

- Канонические OpenSpec specs, changes, schemas и delivery records живут в shared planning home: `/Users/eaafonasev/Projects/star-tournament-planning` (store ID `star-tournament-planning`). Перед работой читай `workflow/project.json` в этом home; все поддерживаемые вызовы OpenSpec выполняй с `--store star-tournament-planning`.
- `openspec/` в code worktree — исторический snapshot, перенесённый в planning home. Не обновляй его и не создавай в нём новые changes; он сохраняется для воспроизводимых ссылок к старым коммитам. Новые требования, changes и lifecycle records создавай только в planning home.
- Для discovery используй `.agents/skills/flow-explore/SKILL.md`; для реализации и небольших inline fixes — `flow-apply`; для вопросов, human acceptance и finalization debt — `flow-inbox`. Не редактируй generated upstream skills.
- Перед реализацией выбери и claim change. Если пользователь не выбрал change, покажи готовые варианты и спроси, какой брать. Не возобновляй historical/cancelled browser work автоматически.
- Прочитай `.agents/references/flow/lifecycle.md` перед lifecycle-переходами; `delivery.md` — перед sync, merge, deploy и cleanup; `initiative.md` — для многоэтапной работы; `efficiency.md` — перед выбором модели или делегированием.
- Human acceptance требуется для каждого результата. Native distribution/deploy не настроены и не выполняются без отдельного решения, записанного в project profile.
- Вопросы, feedback, evidence, usage и lifecycle храни в соответствующем change. У разных worktree нет отдельной базы статуса. Не объявляй acceptance или delivery по checked task boxes, времени или отсутствию ошибок.
<!-- END MANAGED OPEN SPEC FLOW -->

## Поиск по кодовой базе

- Перед чтением файлов сузь область через `rg --files`, затем используй `rg -n` для точного текстового поиска.
- Для синтаксических конструкций и структурных шаблонов используй `ast-grep`; не заменяй им простой текстовый поиск.
- В первом проходе ограничивай выдачу 20 совпадениями и открывай только релевантные диапазоны строк.
- Соблюдай `.gitignore`; не сканируй `build`, `dist`, `coverage`, `logs`, `generated`, `vendor` и бинарные ассеты, если задача явно их не затрагивает.

## Источники решений

- `docs/GAME_SPEC.md` — утверждённый обзор продуктовых, игровых, визуальных, интерфейсных и архитектурных решений; точные актуальные OpenSpec requirements живут в shared planning home из managed flow section. Код, тесты и документация должны соответствовать обоим источникам; при расхождении сначала фиксируй согласованное решение в planning home и обновляй обзор.
- Явное новое решение пользователя имеет приоритет. После подтверждения понимания сначала обнови актуальный раздел спецификации и журнал изменений, затем реализуй.
- В основной части спецификации храни только актуальные решения. Нерешённое фиксируй в `Открытых решениях`; значимые заменённые решения кратко отмечай в журнале.
- Баланс описывай назначением правила и именованным `profile`/`version`; конкретные числа держи в конфигурации симуляции, не в narrative-спецификации.

## Условные процедуры

- Небольшую однозначную правку можно вносить напрямую без полного OpenSpec-флоу; если она меняет утверждённое решение, одновременно обнови `docs/GAME_SPEC.md`.
- Если неясно, является ли правка небольшой и однозначной, спроси пользователя до реализации.
- Перед нетривиальным изменением, OpenSpec change, работой в worktree, интеграцией, архивированием или deploy прочитай [`.agents/references/delivery-workflow.md`](.agents/references/delivery-workflow.md).
- Перед player-visible, input, split-screen, network-play или audio задачей прочитай [`.agents/references/game-qa.md`](.agents/references/game-qa.md). Для текущей Unity-версии применяй native Player QA; browser-QA относится только к historical evidence.
- Перед делегированием или выбором custom agent прочитай [`.agents/references/model-routing.md`](.agents/references/model-routing.md).
- Перед подключением MCP, плагина или внешнего сервиса прочитай [`.agents/references/external-tools.md`](.agents/references/external-tools.md).
- Для длинной задачи, batch-генерации или после первого compaction прочитай [`.agents/references/context-efficiency.md`](.agents/references/context-efficiency.md).

## Архитектурные границы

- Unity — единственный executable runtime. Действуют утверждённые уточнения `GAME_SPEC`: native physics/motor, сериализуемое gameplay state, replay по записанным состояниям без строгого детерминизма, native cameras/UI и Player acceptance. Historical browser baseline не является исходником для запуска или новой реализации.

- Симуляция является единственным источником истины: сущности, правила, таймеры, урон, оружие, боты, коллизии, матч и сериализуемое состояние не зависят от renderer-объектов.
- Unity presentation отвечает за нативную сцену, анимацию, камеры, освещение и визуальные эффекты. HUD, меню, настройки, lobby и accessibility-sensitive интерфейсы реализуются нативным Unity UI; они не владеют simulation state.
- Физические устройства отображай на явные игровые действия в одном input-модуле. Для каждого local seat отделяй игровое действие от конкретного устройства; управление камерой, меню и native focus/pause должны иметь ясную границу фокуса.
- Проектируй локальный матч, сетевую синхронизацию и replay как отдельные адаптеры одной детерминированной симуляции. Не выбирай сетевую модель до её явного утверждения в `GAME_SPEC`.
- 3D-ассеты поставляй через manifest-ключи; стандартный формат — GLB/glTF 2.0. До интеграции фиксируй единицы, origin/pivot, коллизионные прокси, LOD и освещение.

## Параметры Balance Lab

- Любое новое числовое значение, которое влияет на баланс, симуляцию, оружие, движение, камеру или читаемость действия, одновременно добавляй в именованный профиль Balance Lab. Для параметра обязательны стабильный путь, понятная группа и подпись, описание влияния, единица измерения, жёсткие минимум и максимум и шаг ввода; эти же metadata должны управлять UI и диапазонной валидацией.
- Хардкод допустим только для технического инварианта, который не должен настраиваться игроком или дизайнером; причину такого исключения кратко фиксируй рядом с кодом.
