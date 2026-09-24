# Strata / Oxblood F4 — implementation evidence

Approved direction: shared planning change `add-vector-shotgun-and-armored-hands`,
`assets/f4-02-strata-oxblood.png` (F4 №02). These PNGs show exported GLBs and
a real local macOS Development Player, not the approved concept illustration.

- `equipment-model.png`, `equipment-side.png`: independently imported authored
  shotgun GLB, including two recessed muzzles and a closed rear receiver.
- `hands-and-weapon.png`, `hand-detail.png`: independently imported animated
  first person derivative with attributed anatomical glove topology, oxblood
  cloth and pale segmented armor.
- `body-model.png`: independently imported world body derivative, same Strata
  shotgun, oxblood cloth, dark helmet and pale segmented arm/leg armor.
- `asset-audit.json`, `export-contract.json`: exact GLB hashes, 57 joint skin,
  seven clips, embedded images, LOD and manifest checks.
- `editmode.xml`, `playmode.xml`, `checks.json`: Unity checks and build result.
- `player/`: muted native Player captures at 1920×1080. `01-idle`, `02-fire`,
  `03-smoke`, `04-settled`, `06-motion`, and `05-two-seats-world` are diagnostic
  states using real session actions and two synthetic gamepads. The `complete`
  record is explicitly diagnostic, not human acceptance.

The FPS display in Player captures is affected by automated capture and is not
a performance benchmark. Physical controller/TV, artistic and performance
acceptance remain open. No simulation, damage, spread or ammunition rule was
changed.
