# 10 — Расширяемый AI ботов и ускоренная оценка

Статус: результат принят пользователем 2026-09-05; закрытие с явно согласованными исключениями. 327 tests, build, strict validation и независимые 418 AI матчей PASS. Пользователь отдельно подтвердил выполнение ручной AI-приёмки: move/look/jump/fire и pause/resume. Это пользовательская проверка, не результат автоматизации.

- Change: `add-extensible-bot-ai`.
- Ветка: `codex/add-extensible-bot-ai`.
- Worktree: `/private/tmp/star-tournament-bot-ai`, исходный main `bf581bb`.
- Решения: Салага/Боец/Ветеран, per-bot setup и подпись в таблицах, активный бой с краткими отходами, ситуативные прыжки, временная поддержка, общие physics/combat правила; при нуле ammo автоматически пополняется до 20.
- Архив: `openspec/changes/archive/2026-09-05-add-extensible-bot-ai/`, шесть синхронизированных specs.
- Решение о закрытии: пользователь явно разрешил sync, main integration и archive с ограничениями physical-input и PNG evidence, оставив деплой открытым. Позднее пользователь явно подтвердил, что провёл оставшуюся физическую проверку.
- Приёмка: engine-backed headless suite + deterministic replay/restore + muted in-app Browser physical playtest, актуальные PNG и collision/performance gates.
- Интеграция: feature commit `c9d51e8` fast-forward включён в main. Синхронизация и архивирование выполнены коммитом `a4bcb64`, включённым в main. Публикация не выполнена: remote и deployment configuration не настроены.
- Открытый остаток: отдельный physical combat/killcam PNG, настройка публикации и post-deploy проверка. Пункты 5.2 и 5.4 закрыты с учётом ручной проверки пользователя; 5.3 и 5.5 остаются неполными.
- Инструкции расширения и CLI: `docs/AI.md`; подробные результаты/identities: `docs/evidence/add-extensible-bot-ai/verification.md`.
- Исторический dev URL: `http://127.0.0.1:4193/?muted=1`; `prototype-v1@6`, `fnv1a64-v1:b7ca910122e09e91`.
