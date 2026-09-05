# 10 — Расширяемый AI ботов и ускоренная оценка

Статус: planning готов; 4/4 артефакта, strict validation PASS. Реализация и browser acceptance не начаты.

- Change: `add-extensible-bot-ai`.
- Ветка: `codex/add-extensible-bot-ai`.
- Worktree: `/private/tmp/star-tournament-bot-ai`, исходный main `bf581bb`.
- Решения: Салага/Боец/Ветеран, per-bot setup и подпись в таблицах, активный бой с краткими отходами, ситуативные прыжки, временная поддержка, общие physics/combat правила; при нуле ammo автоматически пополняется до 20.
- Артефакты: `openspec/changes/add-extensible-bot-ai/{proposal,design,tasks}.md`, шесть delta specs.
- Следующая фаза: apply в этом же worktree по tasks.md; не создавать конкурирующую ветку для этой change.
- Приёмка: engine-backed headless suite + deterministic replay/restore + muted in-app Browser physical playtest, актуальные PNG и collision/performance gates.
- Состояние main: план не интегрирован; gameplay код и shipped profile ещё не менялись.
