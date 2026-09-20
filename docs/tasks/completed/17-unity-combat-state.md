# 17 — Unity: основа состояния боя

Статус: domain-only срез завершён и проверен; отдельный commit. Integration/archive не выполнены.
Change `add-unity-combat-state`, ветка `codex/unity-combat-state`, поверх FPS commit `f283e99`. Работа продолжена последовательно в том же изолированном worktree; предыдущая FPS ветка сохранена, main/Stage 0 checkout не изменялись.

## Результат

`CombatLife` владеет здоровьем, ammo, cooldown, held-fire, death и respawn readiness. `Read` возвращает независимый serializable value DTO. Resolved damage ограничен оставшимся здоровьем и scoped по target life. Умершая жизнь не стреляет/не получает повторной смерти; respawn увеличивает identity и очищает held-fire. Killer participant/life сохраняются для будущей killcam. При нуле боезапаса continuous refill сохраняет cooldown.

Профиль `unity-combat-state-v1` version 1 использует существующий registry: здоровье, start/refill ammo, cooldown и killcam delay, min/max/step/group/unit/description. Ammo integral validation читается из unit metadata; значения замораживаются при создании CombatLife. Browser tuning источник: `src/profiles/gameDesignProfile.ts`, baseline исходной ветки `26ce3ff`; это стартовый tuning, не обещание physics parity.

## Доказательства

EditMode 15/15 (семь новых domain tests), PlayMode 9/9, Mac Development build PASS; muted native setup/keyboard join/diagnostic live/pause/resume/FPS regression выполнена. Strict OpenSpec validation PASS. [Evidence](../../evidence/unity-combat-state-2026-09-19/README.md). Полные XML/логи сохранены в `.local/unity-evidence/combat-state-final/`.

## Граница результата и следующий шаг

В Player остаётся Stage 0 с diagnostic ray hit и FPS. CombatLife пока не вызывает Physics, не меняет Transform/capsules, не выбирает spawn и не рисует killcam/HUD. JSON DTO не является replay/restore API. Полного локального матча нет.

Следующий change: подключить weapon hit-zone/occlusion resolver и core к четырём local seats; адаптер обязан отключать dead capsule, замораживать simulation ticks на паузе, очищать held-fire на focus/reconnect, выбирать свободный spawn при readiness и сразу выполнять respawn. Добавить health/ammo HUD и approved killcam. При этом исправить ownership runtime scene objects: текущий proving-ground Start создаёт roots в active scene, что даёт дубли при additive reload tests. Затем отдельный scoring/results/repeat срез, teams/bots и layouts.

Stage 0 gates остаются открытыми: четыре физических устройства/TV, согласованные reference PC/quality/internal scale, длительный foreground performance. Ни эти domain tests, ни FPS screenshot не заменяют приёмку. Integration/archive/deploy не выполнены.
