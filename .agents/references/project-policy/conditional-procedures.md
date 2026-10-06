# Подробные правила проекта

Читай только при триггере из AGENTS.md. Пути в тексте относительно корня проекта.

## Условные процедуры

- Небольшую однозначную правку можно вносить напрямую без полного OpenSpec-флоу; если она меняет утверждённое решение, одновременно обнови `docs/GAME_SPEC.md`.
- Если неясно, является ли правка небольшой и однозначной, спроси пользователя до реализации.
- Перед нетривиальным изменением, OpenSpec change, работой в worktree, интеграцией, архивированием или deploy прочитай [`.agents/references/delivery-workflow.md`](../../../.agents/references/delivery-workflow.md).
- Перед player-visible, input, split-screen, network-play или audio задачей прочитай [`.agents/references/game-qa.md`](../../../.agents/references/game-qa.md). Для текущей Unity-версии применяй native Player QA; browser-QA относится только к historical evidence.
- Перед делегированием или выбором custom agent прочитай [`.agents/references/model-routing.md`](../../../.agents/references/model-routing.md).
- Перед подключением MCP, плагина или внешнего сервиса прочитай [`.agents/references/external-tools.md`](../../../.agents/references/external-tools.md).
- Для длинной задачи, batch-генерации или после первого compaction прочитай [`.agents/references/context-efficiency.md`](../../../.agents/references/context-efficiency.md).
