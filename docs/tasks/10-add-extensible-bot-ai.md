# 10 — Расширяемый AI ботов и ускоренная оценка

Статус: код реализован; 327 tests, build, strict validation и независимые 418 AI матчей PASS. Physical browser acceptance остаётся открытой из-за отказа pointer lock; synthetic browser QA не подменяет её.

- Change: `add-extensible-bot-ai`.
- Ветка: `codex/add-extensible-bot-ai`.
- Worktree: `/private/tmp/star-tournament-bot-ai`, исходный main `bf581bb`.
- Решения: Салага/Боец/Ветеран, per-bot setup и подпись в таблицах, активный бой с краткими отходами, ситуативные прыжки, временная поддержка, общие physics/combat правила; при нуле ammo автоматически пополняется до 20.
- Артефакты: `openspec/changes/add-extensible-bot-ai/{proposal,design,tasks}.md`, шесть delta specs.
- Следующая фаза: закрыть normal in-app Browser move/look/jump/fire и pause/resume в этом же worktree, затем завершить handoff/integration/archive. Не создавать конкурирующую ветку для этой change.
- Приёмка: engine-backed headless suite + deterministic replay/restore + muted in-app Browser physical playtest, актуальные PNG и collision/performance gates.
- Состояние main: ветка не интегрирована, не архивирована и не опубликована. Feature commit сохраняет готовый код с явным pending acceptance, а task остаётся в активной директории.
- Инструкции расширения и CLI: `docs/AI.md`; подробные результаты/identities: `docs/evidence/add-extensible-bot-ai/verification.md`.
- Dev: `http://127.0.0.1:4193/?muted=1`; `prototype-v1@6`, `fnv1a64-v1:b7ca910122e09e91`.
