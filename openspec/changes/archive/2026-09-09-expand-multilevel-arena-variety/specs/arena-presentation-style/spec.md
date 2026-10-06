## ADDED Requirements

### Requirement: Представление локальных потолков
Renderer SHALL отображать canonical потолки и их нижние поверхности с читаемыми материалами и локальным освещением. Новые карты MUST NOT накрываться единой декоративной коробкой вместо геометрии помещений; прежний общий потолок MAY сохраняться для historical definitions.

#### Scenario: Вид из закрытого верхнего помещения
- **WHEN** camera находится в закрытой комнате верхнего этажа
- **THEN** видны собственные стены и потолок этой комнаты, а нижний зал не виден вне объявленных проёмов

### Requirement: Semantic presentation multi-level sectors
Renderer SHALL выводить hall, gallery, balcony и basement presentation только из canonical semantic sector/support slots. Балконные guards MAY быть movement-only canonical barriers; renderer MUST NOT создавать false routes, collision или projectile occlusion.

#### Scenario: Обзор с балкона
- **WHEN** camera видит balcony над большим залом
- **THEN** surface, guard и ramp read остаются визуально различимыми, а collision и projectile behavior совпадают с canonical semantics

### Requirement: Плавная камера на ступенях
Камера SHALL смягчать вертикальные толчки лестницы без дополнительной раскачки. Сглаживание MUST оставаться presentation-only, не проникать в потолок и сохранять соответствие прицела authoritative направлению выстрела.

#### Scenario: Бег по ступеням под потолком
- **WHEN** игрок поднимается или спускается по лестнице
- **THEN** камера движется плавно, не пересекает геометрию и прицел остаётся достоверным
