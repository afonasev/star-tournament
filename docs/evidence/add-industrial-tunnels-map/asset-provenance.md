# Assets / authoring

- `approved-style.png`: выбранный пользователем concept style-2-v2, built-in image_gen, 2026-10-02. Это направление, не скриншот игры.
- `original-plan.png`: выбранный план 1; последняя коррекция удаляет скорость и переносит ракетницу на её место, остальные точки сохраняет.
- `industrial-tunnel-panels`: built-in image_gen, 2026-10-02; плоская повторяемая текстура потёртых графитовых панелей с тонкими швами/грязью, без запечённого направленного света. 1254×1254 source, Unity mipmaps/texture import. Manifest в Art/asset-manifest.json.
- Runtime service kit: native generated mesh batches, по существующему OrbitalLeaguePresentation pattern, без внешнего asset download. Единицы — метры, +Y up; world-space pivot origin, local solid centers. Collision только из IndustrialTunnelsCatalog; трубы/кабели/рамы/грязь renderer-only. Статические сетки объединены по материалам, трубы имеют 12 граней. Авторские сечения kit фиксированы как mesh asset encoding; spacing и читаемость имеют descriptors. Отдельный GLB не нужен для этого renderer-owned kit, как у существующего orbital kit.
- Реальные white/amber lights, верхний чистый металл, URP lit panel/floor surfaces, mesh grille. Плесень — локальная shader-маска только на нижних поверхностях.

## Texture prompt

Use case: stylized-concept. Asset type: seamless tileable PBR base color texture for a 3D sci-fi industrial maintenance tunnel, flat orthographic material scan. Square image entirely filled with dark warm grey painted steel panels, subtle uneven graphite paint, thin recessed panel seams, restrained fine scratches, accumulated brown-gray dirt in seams and around small inset screws. Broad clean readable panel faces. Medium-dark albedo, not black. Seamless on all four edges. No lighting or cast shadows, no perspective, no scene, no text, no watermarks, no bevel silhouettes at outer edge. High detail restrained realistic game material. This is a flat tile texture, not a rendering of a corridor.
