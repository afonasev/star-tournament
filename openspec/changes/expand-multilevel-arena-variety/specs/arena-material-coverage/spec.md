## ADDED Requirements

### Requirement: Variants для vertical spatial roles
Presentation material kit SHALL выбирать compatible textured wall/floor/decal variants для hall, gallery, balcony и basement sectors. Texture residency и quality degradation MUST оставаться presentation-only и MUST NOT изменять arena identity, collision, navigation или participant colors.

#### Scenario: Контраст ярусов
- **WHEN** camera одновременно видит lower floor, ramp и upper gallery
- **THEN** текстурный treatment отчётливо различает направление и роль каждого сектора без flat fallback или z-fighting
