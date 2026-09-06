## ADDED Requirements

### Requirement: Вариативное material coverage по секторам
Renderer SHALL выбирать совместимые wall, floor и sector material variants из versioned `clean-future-sport` presentation kit по semantic layout/sector slots, сохраняя off-white/pale-gray architectural shell, dark-navy floor и cyan/lime/orange navigation accents. Floor, ramp и raised-zone treatment MUST сохранять различимый контраст и направление маршрута; крупная поверхность MUST NOT возвращаться к flat fallback colour при доступном texture kit.

#### Scenario: Два layout recipe
- **WHEN** renderer показывает две accepted arenas с разными sector assignments
- **THEN** wall/floor panel rhythm и sector accents различаются, но participant/team colors не используются как окружение

#### Scenario: Рампа и высотная зона
- **WHEN** камера видит ramp, lower floor и raised zone одновременно
- **THEN** material treatment читаемо отделяет уровни и направление перехода без z-fighting или ложной геометрии

#### Scenario: Все стороны архитектурного модуля
- **WHEN** видны боковая, верхняя и лицевая грани wall-bay или портала
- **THEN** каждая грань имеет невырожденную UV-площадь и фактуру с согласованной плотностью по метрам; панель не выходит за host collision solid
