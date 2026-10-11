# Harmonize arena lighting and surfaces

Scope: оба согласованных пункта пользователя: согласованное освещение зон и отдельный характер бликов стен/пола/металла. Baseline `fba20397b4fd019b8b02540e5e99abb29ae21eb6`; implementation `64df074b3ab8bc78d46b69ce48e8b142f87b905a`; detailed checks in verification.json.

`unity-proving-ground-v1@18` retains the existing Lab paths and limits. New per-arena local lamp scales apply only to existing arena-owned Light components. Equipment-fill is outside this registry; participant and equipment materials are not edited.

| Parameter | Before | After target at 100% |
| --- | ---: | ---: |
| ambient scale | 0.75 | 0.95 |
| diffuse ambient lift (authored tint) | 0.00 | 0.18 |
| key scale | 1.25 | 1.00 |
| arena-local lamp scale (each arena) | 1.00 | 0.80 |
| wall smoothness / metallic | 0.20 / 0.05 | 0.08 / 0.00 |
| floor smoothness / metallic | 0.35 / 0.10 | 0.42 / 0.05 |
| metal smoothness / metallic | 0.55 / 0.65 | 0.72 / 0.75 |

At the shipped 50% strengths these targets interpolate with the authored arena baseline: fill 0.975× plus 0.09 of the authored ambient tint, key 1.00×, local lamps 0.90×. This preserves local contrasts while lowering direct-light peaks and slightly restoring background readability. Lunar dust-grey paving uses Floor rather than Wall; its geometry and texture stay unchanged. Bloom, grading, AO, reflection settings, camera poses, participant identities and equipment-fill remain at the same values in each before/after pair.

Migration appends compatible revisions and recognizes the trusted v17 predecessor; old snapshots/hashes remain intact. Prior untouched visual targets adopt v18 defaults; custom values, effect strength and off choices remain. All prior descriptor ranges and input steps are unchanged. New lamp controls have ratio unit, range 0–2 and step 0.05; ambientLift uses ratio, range 0–0.4, step 0.01.

## Review

`comparisons.html` provides all three maps, two zone groups, four weapons and matching before/after screenshots (four viewports per image). PNG contact sheets offer a compact rifle comparison. The GPU Editor captures include the world and own equipment; overlay HUD is rendered separately and its sources are unchanged.

Zone groups: Combat Bowl ring approaches / lower technical space + upper hall; Industrial Tunnels warm corridors / lit rooms; Lunar Laboratory exterior / lower and upper rooms. Cameras are placed at actual floor-relative eye heights. The existing four participants are placed as fixed subjects in the views, without adding models; their identities/poses remain equal within each pair. Time is frozen and the same camera positions/rotations are reapplied immediately before each RenderTexture capture.

## Verification limits

Affected Unity metadata/history and PlayMode rendering/restore checks are selected in qa-scope.md. Editor images and agent inspection do not establish human, Player, device or FPS acceptance. Full suite, natural matches, Player/release/installer builds, publication, deploy and installed-app updates are deferred pending a separate human command.

## Candidate selection

Candidate 1 reduced local direct-light peaks and restored the previous ambient reduction, but the Editor images retained a large exterior/interior contrast in Lunar. Candidate 2 adds a bounded diffuse floor with the existing arena ambient tint. Keep candidate-1/ rifle images and 3/3 XML as the earlier comparison. Participant placement is improved in the final review images; each individual before/after pair keeps identical poses. Agent artistic inspection is a selection rationale, not human acceptance.

Final checks: 8/8 affected EditMode and 3/3 GPU PlayMode. Initial broader Lab history + visual metadata fixture: 60/60 before the additive ambient floor. No assertions or timing contracts were weakened.
