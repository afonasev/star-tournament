# Исторический эскиз — заменён уточнениями пользователя 15.10.2026

Актуальное решение: approved-layout.json. Выбран вариант 1; урон перенесён точно в центр; 10 угловых спавнов; ровно 9 предметов; комнаты второго сдвинуты к двору. proposal.png сохраняется как история выбора, не финальный план.

# Переработка структуры Колодца

Follow-up to add-well-arena. Source: user feedback delegated from chat 01a12c6a-d930-7361-a947-8548a3a8ad37 on 2026-10-15. Human-tested release test0.1.5-test.1 / runtime 23b21e95; this is limited map feedback, not general acceptance.

Authorized outcome: taller rooms and thicker slabs, damage on lowest floor, rocket at second-floor center, coherent building walls and meaningful pickup placement. Preserve pickup inventory, lift semantics, existing movement and core gameplay. No detailed decoration or release authorized.

Baseline: local main 3fcffe879c77ce8fe20167a1c2778cd410f729d1 (contains github/main eab6bdd310b1e24be7e24ef499bc9c261617b5da after fetch).

Structural proposal is pending visual approval: docs/evidence/well-structure/proposal.png and render.py in codex/well-structure. Recommended 7.2m storey / .8m slab / 6.4m clear; alternative 6.4 / .6 / 5.8. Both use aligned perimeter rooms, interior ring circulation and cardinal lobbies. Damage at garden (6,-6); rocket center floor 2. Existing 12-pickup inventory retained. Heal remains top center, so heal-to-rocket drop remains possible; all-pickup vertical chain is broken by off-axis damage and wing items. No promise to eliminate every possible aerial pickup sequence.

Exact room openings, eight sheltered spawns and staircase dimensions must be validated in implementation. Preserve authored identity revisioning and immutable authoring; geometry remains excluded from editable Lab fields per GAME_SPEC.

## Next steps
- [ ] Human confirms structural proposal and height variant (Q1).
- [ ] Update canonical spec and GAME_SPEC with approved decision; claim implementation.
- [ ] Implement geometry, aligned presentation, pickup/spawn layout, stairs and lift height dependencies.
- [ ] Run affected Well edit/physics/navigation/lift/bot/match/visual checks, without full suite or delivery build.
- [ ] Save exact Editor views, XML/log evidence and commit; report integration and remaining human/Player gates.
