# Vector integrated ammo glass

Approved concept A, change `add-shotgun-ammo-display` (2026-10-28).

`atlas.py` authors rounded monoline digits without an external font. Run with Python/Pillow to regenerate `unity/Assets/StarTournament/Resources/ShotgunDigits.png`. The 640×128 grayscale atlas contains ten 64×128 cells. Unity imports linear data with mipmaps, bilinear filtering and clamp wrapping.

`ShotgunAmmoDisplay` adds one clipped planar mesh to `weapon:joined`. The existing rear cap encloses it; no hidden side/back geometry, collision, new lights or shadow passes. Geometry is 8 vertices / 6 triangles; mesh, atlas and opaque single-pass material are shared. Per-instance property blocks change only when the authoritative ammo value changes. Digits have minimum width two and preserve values above 99. Corpses do not retain a floating display when their weapon is hidden.

The normalized bevel, font strokes, underline and cyan palette are authored artwork. Physical placement, width, height and intensity use `presentation.shotgunDisplay.*` descriptors in the named movement profile (`shotgun-display-v1` Lab group). Existing profile values and historical hashes remain preserved by the explicit predecessor projection.

Optimization pass: one renderer/material/pass per visible screen replaces separate border, glass, underline and character renderers. No world-distance LOD is necessary for six triangles; normal camera/frustum and weapon visibility remain active. Base GLBs, rigs, clips, collision and gameplay are unchanged. Native before/after and state screenshots, exact test results and baseline failures are in shared planning `docs/evidence/add-shotgun-ammo-display`. No measured device/FPS claim is made.
