# Crystal Runes detail pass — feedback F2

User feedback: the slim bonuses need more detailed models. This pass adds paired front/rear enamel facets and etched paths to the armor shield, speed chevrons, and damage flame. Outer bounds remain bit-for-bit equal to the prior audited GLBs; source `SILHOUETTE_SCALE=0.82` and `DEPTH_SCALE=0.40` are unchanged. No runtime presentation or gameplay values changed.

## Evidence

- `asset-audit.json`: armor 28 meshes / 1432 triangles, speed 23 / 1842, damage 23 / 2562; front/rear mesh pairs, zero degenerate normals and zero collider nodes. All three min/max bounds equal the previous `docs/evidence/unify-bonus-pickup-crystal-runes-2026-09-23/asset-audit.json` exactly.
- `crystal-runes-front.png`, `back.png`, `side.png`, `quarter.png`, `distant.png`, eight `turn-*.png`, and `grayscale.png`: authored Blender 5.2.1 visual checks.
- `player-bowl/`: muted Development macOS Player, 1728x1080, two viewports, `combat-bowl-v1@6`, armor and damage. `review.json` records arena and sound state; three screenshots show distant, near and rotated views. Player log completed with `BONUS_VISUAL_REVIEW_COMPLETE` and no exception.
- `player-speed/`: same diagnostic on procedural Wide Hall Circuit, armor and speed.
- Unity 6000.3.23f1 EditMode 114/114 and PlayMode 73/73 passed. Development macOS Player build passed with marker `STAR_PROVING_BUILD_PASS 460087769`.

This is visual diagnostic evidence. User visual and physical playability acceptance remain open.
