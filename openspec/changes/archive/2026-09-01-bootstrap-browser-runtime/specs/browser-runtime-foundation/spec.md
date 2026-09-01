## Purpose

Capability задаёт наблюдаемый контракт запускаемого browser runtime и детерминированной симуляционной основы, на которой последующие игровые changes смогут строить единое локальное, replay и сетевое поведение.

## ADDED Requirements

### Requirement: Запускаемый browser runtime
Приложение SHALL запускаться как browser application, создавать один WebGL playfield и отдельный DOM-слой, а также показывать диагностический статус runtime без смешивания DOM и renderer state.

#### Scenario: Успешный запуск
- **WHEN** пользователь открывает dev или production build в совместимом браузере
- **THEN** приложение показывает диагностическую 3D-арену и DOM-статус активной симуляции без runtime errors

#### Scenario: WebGL недоступен
- **WHEN** браузер не может создать требуемый WebGL context
- **THEN** приложение показывает понятное DOM-сообщение об ошибке и не оставляет бесконечный пустой loading state

### Requirement: Детерминированный fixed-step runner
Runtime MUST обновлять игровое состояние только дискретными simulation ticks и MUST отделять simulation time от wall-clock и render time.

#### Scenario: Одинаковый поток команд
- **WHEN** два runner получают одинаковые initial snapshot, profile, arena seed и последовательность `ActionFrame`
- **THEN** после каждого соответствующего tick они создают одинаковый сериализуемый snapshot и state hash

#### Scenario: Разная частота renderer
- **WHEN** одна и та же последовательность tick-команд отображается с разным количеством render frames
- **THEN** итоговое simulation state остаётся одинаковым

### Requirement: Сериализуемые snapshots и replay
Симуляция SHALL предоставлять versioned snapshot и replay-ввод, достаточные для восстановления и повторного детерминированного выполнения без renderer-объектов.

#### Scenario: Snapshot round-trip
- **WHEN** snapshot сериализуется и затем восстанавливается поддерживаемой версией runtime
- **THEN** восстановленное состояние имеет тот же schema version, tick, данные сущностей, RNG state и state hash

#### Scenario: Повтор replay
- **WHEN** записанные `ActionFrame` повторно применяются к сохранённому initial snapshot
- **THEN** replay достигает того же конечного tick и state hash

### Requirement: Платформенно-независимая симуляция
Simulation package MUST NOT зависеть от DOM, WebGL, Three.js, React, физических устройств или wall-clock API; адаптеры SHALL передавать в неё только сериализуемые команды и данные.

#### Scenario: Проверка границы импортов
- **WHEN** выполняется автоматическая архитектурная проверка исходного дерева simulation
- **THEN** она не обнаруживает imports из renderer, UI, browser input или browser globals

### Requirement: Каноническое описание диагностической арены
Runtime SHALL загружать статическую versioned `ArenaDefinition`, которая является общим входом для simulation-side данных и renderer-adapter и не содержит Three.js objects.

#### Scenario: Одна arena identity
- **WHEN** diagnostic arena запускается в browser runtime
- **THEN** DOM-диагностика, simulation snapshot и renderer используют одну arena identity и один content hash

### Requirement: Управляемый lifecycle и resize
Runtime SHALL иметь явные операции start, stop и dispose, корректно адаптировать canvas/camera к размеру playfield и освобождать созданные renderer/UI resources при уничтожении приложения.

#### Scenario: Изменение размера
- **WHEN** размеры playfield изменяются
- **THEN** canvas и camera projection обновляются без изменения simulation state

#### Scenario: Остановка runtime
- **WHEN** вызывается dispose
- **THEN** animation loop, listeners, React root, geometry, materials и WebGL renderer освобождаются без последующих simulation ticks
