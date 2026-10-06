## Purpose

Независимо подготовить и проверить анимационного humanoid-кандидата до отдельного решения о его использовании в игре.

## ADDED Requirements

### Requirement: Изолированное происхождение
Pipeline SHALL сохранять нетронутый source, его checksum, attribution и лицензию, не меняя approved player manifest/runtime.

#### Scenario: Повторная подготовка
- **WHEN** pipeline получает ожидаемый исходный GLB
- **THEN** локальные derivatives сопровождаются provenance, а source checksum и production assets неизменны

### Requirement: Проверяемая skin-анимация
Кандидат SHALL иметь humanoid hierarchy и нормализованный skinning с проверкой покрытия и деформаций; минимальный набор idle/walk/run/aim/fire/hit/death SHALL существовать либо заменяться документированным свободным совместимым набором с проверкой retarget.

#### Scenario: Проверка экспорта
- **WHEN** derivative GLB повторно импортируется независимым audit
- **THEN** skeleton, веса и все клипы доступны, длительности и loop/one-shot семантика известны, обнаруженные дефекты не маркируются как принятые

### Requirement: Честная визуальная оценка
Pipeline SHALL сохранять локальные PNG rest pose и фаз клипов и отделять technical validation от artistic, Unity и shipping acceptance.

#### Scenario: Передача кандидата
- **WHEN** подготовлены результаты
- **THEN** handoff содержит PNG, воспроизводимые команды, реальные ограничения и незакрытые gates
