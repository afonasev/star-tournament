# 19 — Unity: native счёт, результаты и Repeat

Статус: реализация, automated и muted native Player проверки завершены; отдельный commit. Change `add-unity-native-match-loop`, ветка `codex/unity-native-match-loop`.

## База и границы

Worktree `/Users/eaafonasev/.codex/worktrees/b7af/star-tournament` начался с чистого main `26ce3ff`. Проверены ancestry и `git cherry`: четыре недостающих commit `4f65038`, `12316f3`, `a3a95e9`, `e735de7` перенесены fast-forward. Main и предыдущие worktrees не редактировались.

Четыре local seats, фиксированная двухэтажная арена, FFA. Scoring/standings/timer/results/Repeat подключены к native Player. Teams/bots, восемь участников и остальные 1–4 layouts остаются следующими срезами, этап 2 целиком не закрыт.

## Контракт

- `NativeMatchState`: data-only ticks, capped applied damage, life-local assist ledger, cumulative kill-chain deltas, inclusive windows, all-dead chains reset после batch. Источник правил — GAME_SPEC §5 и утверждённый `match-session-lifecycle`; tuning — `prototype-v1@12` → `unity-native-match-v1@1`.
- `NativeCombatSession`: timers/motors → общий snapshot targets/admitted shots → все shot queries → stable applied-damage/death reduction → end evaluation → respawn только если матч продолжается. Mutual lethal допускается; цель имеет приоритет на последнем tick, tie → overtime без сброса; исходный trigger сохраняется.
- Result содержит independent ordered value rows/winner/trigger/final tick. Finished и stopped session отвергают Tick/ApplyDamage. JSON — inspection DTO, не restore/replay API.
- Setup duration/target получает диапазоны и шаги из общего descriptor registry; тот же registry используется в Inspector и validation. Настройки frozen при Begin, Repeat сохраняет их и assignments. FPS preference продолжает сохраняться отдельно.
- Tab/View показывает таблицу своего viewport, включая killcam. Damage округляется только при выводе. Все строки используют одну сетку, identity palette, human/diagnostic marker. Таймер сверху каждого viewport; итоговая таблица и pause дают Repeat/menu.
- Repeat заново создаёт core/session/presenter; motor poses/velocity/lives/ammo/input/shot sequence/clock/score/death anchors/corpses очищаются. Presenter отписывается и немедленно скрывает старые corpses перед deferred Destroy. Scene/arena/NavMesh/cameras/EventSystem/AudioListener сохраняют единственное ownership.
- Disconnect/focus/pause замораживают gameplay и очищают input. При отсутствующем устройстве доступен menu, Repeat/Resume ждут ready assignments. Setup может переназначить устройства. Выход останавливает прежнюю session и освобождает presentation.

## Проверки

Read-only architecture review до реализации и после неё; найденные late-seat suppression, hidden disconnect selection и overtime remaining-timer исправлены с тестами.
Mac Development build PASS; muted native Player сценарий завершён при фактических 1920×1080 и 3840×2160. Обычные keyboard join, mouse duration, Escape pause, Repeat и setup/Exit дополнительно проверены native UI.

EditMode **25/25**, PlayMode **21/21**: scoring windows/chains, fractional damage, early/final target tie, overtime trigger, result copy, mutual lethal через настоящий resolver, killcam View, paused clocks, repeated Repeat/setup, held-fire suppression, frozen config и disconnect→gamepad navigation→reconnect/resume.

Воспроизведение: `unity/tools.sh prepare`, `test-edit`, `test-play`, `build` — последовательно. Mac Player: `unity/Builds/StarTournamentProvingGround.app`. Evidence: [native match](../../evidence/unity-native-match-2026-09-19/README.md).

## Открытые gates

Четыре физических устройства/TV, reference hardware/quality/internal scale и длительный foreground performance не проверены. Синтетические input и screenshots не подтверждают 60 FPS acceptance. Integration/archive/deploy не выполнялись; Stage 0 остаётся открытым.
