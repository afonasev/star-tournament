# Unity bot-controlled seats — проверенный срез

Change `add-unity-bot-controlled-seats`, branch `codex/unity-bot-controlled-seats`, base `f29c449`. Изолированный worktree `/Users/eaafonasev/.codex/worktrees/34be/star-tournament`.

## Проверки

- Unity 6000.3.23f1: **81/81 EditMode**, **63/63 PlayMode**, **Mac Development build PASS**, OpenSpec strict PASS. Сводка `checks.json`; локальные XML/логи `.local/unity-evidence/`.
- Mixed/all-AI composition, individual difficulty, восемь уникальных bot identities, frozen restore, human-only actions, sparse human на ненулевом месте, освобождение устройства при human→AI, disconnect/reconnect, operator Escape, общий TrooperVisual, pause/Repeat/exit.
- Исправлена регрессия постороннего gamepad Start: в human-матче паузу вызывают только назначенные gamepads; в all-AI любой Start. Headless keyboard tests временно направляют synthetic input в runtime и восстанавливают editor settings; обычная focus protection Player сохранена.
- `fhd/` и `4k/`: по **13 PNG+JSON**, actual 1920×1080 и 3840×2160; все focused/muted. Ordinary setup, synthetic operator keyboard и один synthetic gamepad только в mixed-сценариях; all-AI без назначенных игровых устройств. StartBotReview и diagnostic Begin не используются.
- 2/3/4 views проверены для одного human + 1/2/3 AI и all-AI. Четыре all-AI views включают восемь реальных участников, Team A/B и разные сложности. Каждое отображаемое AI остаётся Bot с действующим planner; общий session/combat/presentation.
- Оба прогона получили **естественные death/killcam/respawn** отображаемого бота, fallback fixture не потребовался. FHD seed `1105221410`: 81 выстрел к смерти на 16.06 s, respawn 19.08 s. 4K seed `633888758`: 69 выстрелов к смерти на 14.78 s, respawn 17.80 s. Repeat сохраняет seed/composition и сбрасывает shots/clock.
- Все 26 PNG осмотрены в contact sheets; отдельно осмотрен FHD first-person mixed кадр. Видны руки/оружие/HUD каждого live view, обычные death/killcam/respawn, persistent standings третьего экрана, команды/сложности в setup, доступные operator pause/resume/Repeat/menu. Обнаруженных обрезаний нового setup нет.
- `identity.json` подтверждает неизменность общего TrooperVisual, CombatPresentation, BotDriver и семи исходных v2 clips относительно baseline; root motion остаётся выключен. Unity Prepare изменял только generated scene IDs/whitespace; этот служебный шум исключён из commit.

## Диагностические кадры

`review-summary.json` содержит mean/p95 unscaled frames. Короткая выборка 4-view/8-participant live: FHD mean 8.37 ms / p95 9.30 ms, 4K mean 8.37 ms / p95 9.07 ms. Это разные random seeds, короткие прогоны с rebuild/capture overhead, не paired benchmark и не подтверждение 60 FPS на целевом железе. Внутренний счётчик FPS на PNG также не acceptance.

## Оставшиеся gates

Финальная пользовательская приёмка — после реализации. Physical devices/reconnect/TV, art, reference hardware/quality/длинный performance gate и full migration открыты. Replay отложен пользователем; поставка обсуждается отдельно. Archive, main integration и deploy не выполнялись.

## Галерея FHD

![01-two-views-mixed-live](fhd/01-two-views-mixed-live.png)

![02-two-views-all-ai-live](fhd/02-two-views-all-ai-live.png)

![03-three-views-sparse-human-live](fhd/03-three-views-sparse-human-live.png)

![04-three-views-all-ai-live](fhd/04-three-views-all-ai-live.png)

![05-four-views-mixed-live](fhd/05-four-views-mixed-live.png)

![06-four-views-all-ai-teams-live](fhd/06-four-views-all-ai-teams-live.png)

![07-four-views-viewed-bot-natural-death](fhd/07-four-views-viewed-bot-natural-death.png)

![07-four-views-viewed-bot-natural-killcam](fhd/07-four-views-viewed-bot-natural-killcam.png)

![07-four-views-viewed-bot-natural-respawn](fhd/07-four-views-viewed-bot-natural-respawn.png)

![08-all-ai-operator-paused](fhd/08-all-ai-operator-paused.png)

![09-all-ai-resumed](fhd/09-all-ai-resumed.png)

![10-all-ai-repeat](fhd/10-all-ai-repeat.png)

![11-return-setup](fhd/11-return-setup.png)

## Галерея 4K

![01-two-views-mixed-live](4k/01-two-views-mixed-live.png)

![02-two-views-all-ai-live](4k/02-two-views-all-ai-live.png)

![03-three-views-sparse-human-live](4k/03-three-views-sparse-human-live.png)

![04-three-views-all-ai-live](4k/04-three-views-all-ai-live.png)

![05-four-views-mixed-live](4k/05-four-views-mixed-live.png)

![06-four-views-all-ai-teams-live](4k/06-four-views-all-ai-teams-live.png)

![07-four-views-viewed-bot-natural-death](4k/07-four-views-viewed-bot-natural-death.png)

![07-four-views-viewed-bot-natural-killcam](4k/07-four-views-viewed-bot-natural-killcam.png)

![07-four-views-viewed-bot-natural-respawn](4k/07-four-views-viewed-bot-natural-respawn.png)

![08-all-ai-operator-paused](4k/08-all-ai-operator-paused.png)

![09-all-ai-resumed](4k/09-all-ai-resumed.png)

![10-all-ai-repeat](4k/10-all-ai-repeat.png)

![11-return-setup](4k/11-return-setup.png)
