## Purpose

Capability определяет, какие крупные architectural panels являются deterministic collision geometry, а какие мелкие детали остаются presentation-only.

## ADDED Requirements

### Requirement: Крупные панели блокируют capsule
ArenaDefinition SHALL включать collision thickness крупных wall bays и portal facades в canonical wall surfaces. Эти surfaces MUST участвовать в collision, navigation, spawn validation, replay и state hash. Мелкие trims, seams, decals, route guides и landmarks MUST NOT создавать collision proxy.

#### Scenario: Крупная панель в узком проходе
- **WHEN** participant capsule достигает крупной architectural panel
- **THEN** collision query блокирует движение на её видимой внешней границе, а validator принимает arena только при сохранённом capsule clearance routes

#### Scenario: Мелкая деталь
- **WHEN** participant пересекает trim, seam, decal, route guide либо landmark
- **THEN** collision result не меняется
