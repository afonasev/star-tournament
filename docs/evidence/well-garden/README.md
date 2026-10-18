# Well natural garden

User chose concept 2 and specified that stone paths must start at the actual entrances from the buildings. Implementation authorized in chat 01a14e45-3ff3-7802-bdb5-59c07145d82b, 2026-10-18.

`selected-concept.png` is an AI paintover of `../well-daylight/full-visual/atrium-corner.png`, created using the built-in imagegen tool. It is a direction reference, not runtime evidence. Prompt: preserve camera/architecture/accepted lighting; replace exposed brown soil only with low grass/clover, a gentle irregular gray flagstone path, sparse small pale flowers; keep sightlines, corridors and landing open; retain existing beds; no trees, new lights or obstacles. Following user correction, actual implementation connects the two real east/west entrances rather than using an arbitrary foreground path.

`views/*.png` are real Unity PlayMode renders, HDR render target under stock Well v5 / NativeVisualPresentation. See `qa-scope.md`, XML and verification for actual results. Player/build/full/release/deploy and human acceptance are separate.

Final grass cutout is a built-in imagegen asset saved unchanged at `unity/Assets/StarTournament/Resources/WellMeadowGrass.png`; prompt/import settings are recorded in `grass-asset.json`. Intermediate render drafts and caches are disposable; final PNGs and XML are retained. See verification iterations for the separate visual failures that required refinement despite green geometry tests.
