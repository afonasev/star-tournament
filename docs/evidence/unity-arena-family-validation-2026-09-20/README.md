# Unity ARENA-4 validation — 2026-09-20

Branch: `codex/unity-arena-family-validators`. Change: `validate-unity-arena-families`.

| Check | Result | Evidence |
| --- | --- | --- |
| Strict OpenSpec | PASS | `openspec validate validate-unity-arena-families --strict` |
| EditMode | 101/101 PASS | `.local/unity-evidence/editmode.xml`, 11:20:42Z–11:20:43Z |
| PlayMode | 68/68 PASS | `.local/unity-evidence/playmode.xml`, 11:21:57Z–11:22:46Z |

EditMode covers all five accepted families and targeted geometry, collision layer, spawn, region, duplicate-ID and transition-order diagnostics. PlayMode verifies that a rejected definition throws before `ProvingArena` creates a collider/NavMesh projection.

No Player/build/performance evidence is claimed: the change is pre-runtime data validation only and does not change player-visible rendering, interaction or gameplay. Physical devices/TV, art, target-hardware performance and final player acceptance remain open.
