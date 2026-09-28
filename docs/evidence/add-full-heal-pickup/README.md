# Полное исцеление — implementation evidence

Approved scope: одно красное сердце Crystal Runes на `heal-basement`, support `support:armor-corridor`, вместо нижней брони. Combat Bowl `combat-bowl-v1@15`; lifecycle `unity-combat-state-v1@4`, presentation `unity-proving-ground-v1@5`, snapshot schema v4. Geometry и остальные пять pickups сохраняются.

Asset: `Assets/StarTournament/Art/heal-pickup-crystal-runes-lod0.glb`; source `generate-heal-pickup.py`, audit `asset-audit.json`. 12 meshes, 1932 triangles, пять пар двустороннего рельефа; bounds 0.883 × 0.915 × 0.157 m; collision nodes отсутствуют. GLB повторяет выбранную геометрию макета 2; Blender source/audit не являются Player acceptance.

EditMode проверяет 1/50/99/100 HP, armor, dead/full policy, caps, ranges/metadata, timers/JSON. PlayMode проверяет independent inventory, semantic participant order, native physics ceiling, snapshot/Repeat и pause owner boundary. Generic bot tactics сохраняют прежние route anchors и не получают новой стратегии.

Durable native Player screenshots, logs, tested commit/build identity и human acceptance guide хранятся в каноническом change `add-full-heal-pickup` shared planning home. Scripted QA uses muted native macOS Player; synthetic devices/manual staging не являются физической или human acceptance. Внешняя distribution не выполняется.

Registry migration: native Lab uses `design-lab-history-v2.json` for the new versioned profile set. Previous v1 histories and immutable hashes remain untouched; their cross-registry conversion is not performed. New saved revisions include all heal descriptors and exact profile identity in match snapshots; Repeat retains its original frozen revision.
