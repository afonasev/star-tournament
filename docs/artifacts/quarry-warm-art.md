# Quarry warm limestone A — editable art source

Presentation identity: `quarry-warm-limestone-a@1`, applied to map revision 6 only. Geometry/placements remain `quarry-prototype-authoring-v6`. User selected A in chat `01a1983e-02ef-7ec1-bcb9-8c29777a1709`, 2026-11-01. The selected concept lives in shared planning `docs/evidence/refine-quarry-warm-art/approved-concept-a.png`; it is an art reference, not a geometry specification or game screenshot.

## Source

- `unity/Assets/StarTournament/Resources/QuarryWarmArt/{limestone,gravel}.png`: original built-in imagegen output, generated for this task; no external downloaded textures. Original 1254px images are retained; Unity import is limited to 1024, compressed, mipmapped, repeating, trilinear with anisotropy 4, CPU readback disabled.
- `Runtime/Arena/QuarryWarmDetails.cs`: editable deterministic mesh source. Coordinates are world meters; child root is identity. Shallow fittings on existing objects have no colliders. Groups are separated by object family/material, avoiding a renderer for each bolt.
- `Resources/QuarryWarmSurface.shader`: world projection, filtered surface relief, worn steel/wood/paint; uses the URP PBR light path. Tyre traces are shader detail only. Legacy QuarrySurface remains intact.
- `Runtime/Core/QuarryWarmProfile.cs`: stable metadata for material scale, relief and light. Existing palette entries carry colors. Colliders, navigation and simulation remain owned by the original frozen map.

## Generation prompts (built-in imagegen, no CLI)

Limestone:
> Seamless tileable PBR basecolor texture of warm pale cream limestone quarry cliff rock. Perfectly flat orthographic surface scan, fills entire square edge to edge. Natural horizontally bedded limestone with irregular fractured chipped faces, subtle ochre mineral seams, occasional small darker fissures and pores. Unequal broad strata, NOT brickwork, not stone masonry, not rippling wavy stripes. Restrained realistic small-scale texture for a video game. No perspective, no scene, no objects, no directional lighting, no cast shadows, no ambient occlusion baked in, no highlights, no text, no border. Albedo only, evenly lit, light creamy beige dominant, low-medium contrast, natural imperfect geology. Texture represents 6x6 meters. Square 1024x1024.

Gravel:
> Seamless tileable PBR basecolor texture of dry compacted limestone quarry ground. Perfect flat orthographic scan from above, fills entire square edge to edge. Warm beige dusty compacted earth, fine gravel aggregate, tiny limestone chips embedded into dirt with scattered small rough pale stones. Low contrast playable ground with mostly smooth compacted fine dirt and sparse micro detail. No large rocks, no plants, no roads, no tire tracks, no cracks forming tiles, no perspective, no objects, no lighting or cast shadows, no vignette, no text or border. Albedo only evenly lit realistic texture representing 5x5 meters. Warm muted sand color, not orange. Square 1024x1024.

## Optimization and verification

Results and actual rendered comparisons: shared planning `docs/evidence/refine-quarry-warm-art/`. Source editability, bounds/collision preservation, material/texture memory, grouping/static batching, shadows and mesh cleanup are checked there. No LOD simplification of the authored cover or silhouette; small fittings are grouped and shadowless. No FPS or physical-device claim. Build/Player/full/release/deploy remain separate, unrun gates.
