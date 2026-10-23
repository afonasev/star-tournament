# VECTOR F4/F5 — unfinished checkpoint, 2026-10-21

Workspace: `/Users/eaafonasev/.codex/worktrees/vector-grip-refinement/star-tournament`.
Branch: `codex/vector-fighter`. Restored snapshot fc7dc529, recovery baseline 6f139b7f.
Current source is the commit containing this checkpoint; exact tested file hashes
are in `f4-qa/source-hashes.json`. No integration or publication. Previous ignored
F3 Player app was lost during external archive-cleanup and has NOT been restored.

## Current result
F4: per-weapon/per-digit joint and spread settings, improved support-wrist direction,
and cached descriptor paths are saved. Integer-degree defaults obey Lab metadata;
@12 reconstruction retains original defaults and omits new per-digit descriptors.
Captured aim wrist deviation is Rifle 8.47°, Shotgun 11.27°, RocketLauncher 22.08°,
Cutter 17.81° (forearm axis vs wrist-to-middle-MCP axis; not full anatomy acceptance).

**Visual FAIL / unfinished:** RocketLauncher/Cutter support hands still have gaps
and intersect their forward handles. They need a true front-handle fit, not another
shared casing-support target. Thumb shape and glove seam spikes also need review.
Do not call all four grips fixed. Rejected forward-offset experiments and neutral
bone/axis data are in `f4-diagnostics/`; do not restore those offsets silently.

F5: user provisionally selected service armor, then rejected the primitive assembly
itself. Extra service attachments are not authorized for shipping. New procedural
surface and source-cloth-overlay studies were inspected and are also not acceptable:
schematic shell forms, unfinished shoulder contours and intersecting retained gear.
**No production body/arms GLB was changed.** Editable primary-form study is
`scripts/vector_fighter/primary-form-study.blend`; production source remains separate.
The next art step is proper mesh remodeling against `approved-vector-concept.png`,
with natural garment/joint topology and designed armor surfaces. More primitives,
more subdivisions or a smooth mannequin do not satisfy the user's request.

## Verification and actual images
- Initial EditMode: 26/27, FAIL retained. Decimal angles violated descriptor step=1.
- Corrected EditMode: 18/18 (17 weapon contracts + exact BeforeAnatomicalGrips case).
- Other nine saved-history cases passed in the initial run; mechanisms unchanged.
- Graphical PlayMode: 4/4 (walk/independent view, frozen grip, immediate reset/death,
  death/respawn/repeat). Current factory profile; no overrides for final captures.
- `f4-current/`: actual left/right closeups and FPS for every weapon; capture now
  yields one normal frame before rendering so skinning consumes current bones.
- Full suite, new Player build, integration/release, device/performance/human: NotRun.
- Planning change validated with `openspec validate ... --strict`.

## Preservation / resources
First compaction checkpoint preserved. Source, diagnostics, failed studies and
required actual frames are tracked. No task Unity/Blender process remains after QA.
Owned reproducible intermediates `.local/vector-fighter`, `.local/vector-grip-f4`
and Python cache are removed after commit. Worktree and warm Unity Library retained
for the specifically unfinished anatomical-grip and primary-form continuation;
clean after that work is integrated/accepted or explicitly cancelled. No other
worktree, foreign process, main settings or unrelated planning artifact was changed.
