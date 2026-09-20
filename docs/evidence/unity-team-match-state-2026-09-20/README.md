# Unity team match state — 2026-09-20

База `9b21406`, ветка `codex/unity-next-migration-slice`, worktree 83e9. Unity 6000.3.23f1; профиль `unity-native-match-v1@1`. Это внутренний gameplay срез этапа 2; Player остаётся FFA 2–4 с trooper v2.

## Проверки

- EditMode **41/41**, включая 10 новых tests: roster validation/copies, 8 participants, суммарная цель без individual target, unequal simultaneous crossings, final-tick target tie, длительный overtime, assist tie-break, time winner по team sum, JSON/frozen result/fresh match и FFA regression.
- PlayMode **28/28**: весь существующий native combat/match/layout/trooper набор.
- Mac Development build PASS; reported 459135536 bytes. Binary hash и XML summaries: `summary.json`.
- Команды последовательно: `unity/tools.sh prepare`, `test-edit`, `test-play`, `build`. Только один Unity Editor; все завершились с exit 0. Полные логи в `.local/unity-evidence/` этого worktree. XML приложены.
- Bounded reducer diagnostic: 10000 ticks, 8 participants, 80000 applied damage events, Editor M2 Pro; p50=0.000200ms, p95=0.000400ms, p99=0.001100ms, worst=0.038800ms. Это маленькая domain workload без motors/renderer/AI и без hardware acceptance threshold. Не доказывает 8 native bodies или 60 FPS.
- Strict OpenSpec validation PASS; исходные trooper/scene/settings не изменены. Unity prepare/build переиздали scene object IDs и YAML whitespace; только эти собственные generated изменения восстановлены после проверки.

## Muted native Player QA

Обычный Player текущей ветки открыт через CUA. `ProvingGround.Awake` устанавливает `AudioListener.volume=0`. Проверены Space keyboard join (P1 готов, остальные ждут), явный diagnostic запуск 4 камер, Escape pause, Repeat с новым таймером/полным здоровьем/боезапасом, возврат в setup с сохранённым P1 и штатный Exit. Это stationary diagnostic и UI interaction, не физический multiplayer playtest.

Окно сначала выходило за доступную область desktop; CUA click временно отвечал noWindowsAvailable. Keyboard action активировало Player; штатный window zoom уместил окно. Сохранённые итоговые PNG показывают полное окно после zoom; их размеры — desktop capture, не FHD/4K performance output. Это восстановление QA, без изменений игрового кода.

- `setup.png`: setup после возврата, P1 joined, 4 seats.
- `pause.png`: общая diagnostic pause.
- `repeat-live.png`: четыре viewport после Repeat, trooper body/arms и HUD. Seed отсутствует: фиксированная proving arena.

PNG — regression evidence неизменённого FFA Player, не демонстрация playable teams. FPS в HUD не используется как benchmark.

## Оставшиеся gates

Native teams setup/HUD/colors/combat/spawn, боты, solo и восемь scene participants не подключены. Реальные gamepads/TV, художественная приёмка trooper, reference hardware/quality/internal scale и long foreground 60 FPS остаются открытыми. JSON DTO — inspection, не replay/restore. Main/archive/deploy не выполнялись.
