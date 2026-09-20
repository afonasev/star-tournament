# 22 — Unity: командное состояние матча

Статус: внутренний gameplay срез реализован и проверен; отдельный commit. Change `add-unity-team-match-state`, ветка `codex/unity-next-migration-slice`, worktree 83e9.

## База и выбор среза

Начальный checkout был чистым detached main 26ce3ff. Проверены status, worktree list и ancestry; новая ветка создана прямо на актуальном native 9b2140610bee2793e1a275f26a20b4cda60fe58c. Cherry-pick дубликатов нет, чужие worktrees/main не изменялись. Handoffs 17–21 и trooper evidence прочитаны; browser backlog не возобновлялся. Shipping trooper уже в Git, vault/source восстанавливать не потребовалось.

После утверждённых layouts/trooper выбран ограниченный domain-срез командного матча этапа 2. GAME_SPEC §2/§5 и match-session-lifecycle уже определяют Team A/B, состав до восьми, командную сумму и atomic overtime. Новых продуктовых решений не добавлено.

## Реализация

- `NativeMatchRoster` проверяет 2–8 participant slots, FFA без команд либо две непустые Team A/B; копирует вход и выдаёт независимый DTO. Participant slot — identity внутри session, не индекс устройства/viewport. Uneven nonempty teams разрешены существующим контрактом.
- `NativeMatchState` принимает immutable roster; прежний constructor создаёт FFA. После всех events tick сравниваются personal либо team totals с тем же target/time/overtime алгоритмом. Personal damage/assists/chains не менялись.
- Snapshot содержит roster, отсортированные team totals и `WinnerTeam`. `Winner` обозначает participant только в FFA; в teams остаётся −1. Историческое поле `NativeStanding.Seat` сохранено для native callers, но документировано как participant slot. Отдельная external-ID/network binding схема не вводилась.
- Profile `unity-native-match-v1@1` сохранён; новых числовых tuning нет. Две команды и лимит участников — structural product constraints. DTO остаётся inspection, без обещания replay/restore compatibility.
- Reducer принимает уже разрешённые damage events. Он не определяет friendly fire, не фильтрует shotgun rays и не изменяет health. Это контракт существующего scoring reducer, а не готовый team combat adapter.

## Проверки

EditMode **41/41**, PlayMode **28/28**, Mac Development build PASS (reported 459135536 bytes). Все существующие combat/match/layout/trooper regressions прошли. Десять новых tests закрывают roster validation/copy, 8 participants, team target, atomic unequal/tied crossing, time/overtime/assist, JSON/frozen result, fresh reducer и FFA. Bounded 10000-tick reducer diagnostic записан отдельно и не является Player performance acceptance.

Muted native UI проверена через CUA: Space join → явный diagnostic 4 viewport → Escape pause → Repeat → setup → Exit. Скриншоты показывают неизменённый FFA Player с trooper, не командный UI. Build/tests запускались одним Editor последовательно. Собственные generated scene IDs/YAML whitespace восстановлены; shipping assets и scene semantics неизменны.

[Evidence, XML, PNG, build hash и ограничения](../../evidence/unity-team-match-state-2026-09-20/README.md). Strict OpenSpec и `git diff --check` PASS.

## Следующий handoff и граница приёмки

Следующий native team integration должен подключить mode/assignments к setup и immutable session config, team-aware combat/spawn, identity colours, grouped live/results и clean Repeat. До реализации сверить approved shot/friendly-fire и spawn правила с источниками; новые действительно открытые развилки согласовать. Не считать добавленный roster готовым device-to-participant mapping и не добавлять скрытых людей/ботов. Боты, playable solo и восемь scene participants остаются отдельными срезами roadmap.

Trooper body/arms/clips и `unity-trooper-presentation-v1@1` сохранены. Художественная приёмка, physical gamepads/TV, reference hardware/quality/internal scale и long foreground 60 FPS остаются открытыми. Этап 2 не завершён. Main integration, archive и deploy не выполнялись.
