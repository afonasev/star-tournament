## ADDED Requirements

### Requirement: Semantic presentation multi-level sectors
Renderer SHALL выводить hall, gallery, balcony и basement presentation только из canonical semantic sector/support slots. Балконные guards MAY быть movement-only canonical barriers; renderer MUST NOT создавать false routes, collision или projectile occlusion.

#### Scenario: Обзор с балкона
- **WHEN** camera видит balcony над большим залом
- **THEN** surface, guard и ramp read остаются визуально различимыми, а collision и projectile behavior совпадают с canonical semantics
