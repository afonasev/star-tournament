# Vector equipment candidate

Selected direction: concept 02 Vector, approved for implementation by the user on 2026-09-23, with replacement armored hands. The reference is retained in the shared planning change `add-vector-shotgun-and-armored-hands/assets/02-vector-approved-direction.png`.

- `equipment-model.png`: Blender render of the actual authored equipment, not a game screenshot.
- `asset-audit.json`: GLB topology, material names, animation names, muzzle nodes and SHA-256 hashes.
- `player/`: native Development Player captures from the explicit `-vectorReview` diagnostic; exact fire/smoke sample times use accepted combat actions, not fabricated shot events.
- `editmode.xml`, `playmode.xml`: automated check results for the integrated candidate.

Runtime profiles: `unity-proving-ground-v1@1`, `unity-trooper-presentation-v1@1`; current authored Combat Bowl catalog. The diagnostic has one human viewport plus a stationary fixture, then two local viewports. Audio is muted. No gameplay randomness is introduced by equipment/VFX. Particle systems use a fixed presentation seed; authored geometry is deterministic.

The first native draft exposed disconnected elbow cutoffs and oversized inherited pellet streaks; the candidate extends sleeves behind the lower screen edge and reduces profile-controlled streak width/lifetime. A PlayMode identity regression was fixed by restoring the `identity-vector-status` material channel; no test contract was relaxed. Original persisted Unity animation clips are byte-for-byte preserved.

Automated checks and these captures do not establish human artistic acceptance, physical controller acceptance or target-hardware performance. Review the model/hand silhouette, grip and brief twin shot effect in the immutable Player candidate linked from the planning change's acceptance guide.
