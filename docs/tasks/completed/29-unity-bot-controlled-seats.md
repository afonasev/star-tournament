# 29 — Unity bot-controlled split-screen seats

Change `add-unity-bot-controlled-seats`; branch `codex/unity-bot-controlled-seats`, worktree `/Users/eaafonasev/.codex/worktrees/34be/star-tournament`, verified base `f29c449`.

Реализовано и проверено: ordinary human/AI на каждом из 1–4 views, individual AI difficulty/team, настоящий bot driver, human-only devices и sparse human, all-AI operator Escape/Start, общий TrooperVisual/lifecycle, frozen Repeat и возврат draft. Максимум восемь участников. Один human + 1/2/3 AI и все AI проверены в native Player.

Read-only astra_architect memo: разрешить Bot в существующем participant→view mapping и добавить human mask в input; отдельный spectator pipeline избыточен, подмена Kind на LocalHuman неверна. Риски action leakage, implicit human count и lifecycle/culling закрыты focused tests/Player evidence. Core/input и review/test срезы делегированы Terra/medium по новому указанию пользователя; основной агент проверил реализацию и evidence.

Проверки: **81/81 EditMode, 63/63 PlayMode, Mac Development build PASS, OpenSpec strict PASS**. **26 PNG+JSON**, все focused/muted, фактические FHD/4K; natural AI death/killcam/respawn в обоих прогонах. Operator pause/resume/Repeat/exit без назначенных игровых устройств; human disconnect/reconnect автоматически проверен. Общие presentation/driver/семь v2 clips неизменны относительно base. [Evidence](../../evidence/unity-bot-seats-2026-09-20/README.md).

Финальная пользовательская приёмка после реализации. Physical devices/TV/art/reference-performance/full migration остаются открытыми. Replay отложен; поставка обсуждается отдельно. Не архивировать, не интегрировать main, не deploy; другие этапы не начаты. Один commit содержит этот проверенный срез; точную ревизию возвращает `git log -1` на указанной ветке.
