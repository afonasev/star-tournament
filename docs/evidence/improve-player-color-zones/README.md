# Крупные зоны цвета игрока

Source: `e5cface32dfb246d86665bd9612361f505ebc6e5` (включает main `7be90a2`). Кандидат и tree checksum — release.json.

Фиксированный бордовый albedo заменён нейтральным графитом; крупные зоны корпуса, ног и шлема получают frozen participant/team color. Сохранены source texture, normal, metallic/roughness/AO maps, geometry, animations, palette RGB and weapon boost contract. Параметры поверхности находятся в `unity-participant-presentation-v1@2`; прежняя история и пользовательские цвета сохраняются.

В native Player `CopyPropertiesFromMaterial` оставлял новый Shader с объявленными identity properties, но Material.HasProperty возвращал false для них. Рабочий перенос общих properties по типу сохраняет material schema, и native кадры показывают все восемь цветов. Логи обоих случаев сохранены для воспроизводимости; failed previews не являются успешной visual QA. API contract: https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Material.CopyPropertiesFromMaterial.html .

Verification и screenshots — technical evidence. Human acceptance pending. Позиции/камеры фиксированы диагностическим компонентом; это не playable-bot, physical-controller или performance acceptance. QA muted, 1920x1080. FPS на замороженных кадрах не является измерением производительности.
