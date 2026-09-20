# unity-team-match-state Specification

## Purpose
Переносит утверждённые правила командного завершения в независимое от устройств и renderer состояние Unity-матча с сериализуемым составом и результатами.

## Requirements

### Requirement: Состав участников
Gameplay SHALL принимать 2–8 стабильных participant slots в FFA без команд либо в teams с ровно двумя непустыми Team A и Team B. Состав SHALL быть immutable после создания и не зависеть от local-seat count.

#### Scenario: Восемь участников и защита состава
- **WHEN** caller создаёт командный состав и затем меняет исходный массив
- **THEN** принятый состав остаётся прежним; отсутствующая команда, неизвестная identity и число вне диапазона отвергаются

### Requirement: Командное завершение
После всех events tick gameplay SHALL сравнить суммы personal scores команд. Единоличный лидер завершает матч по цели либо времени; ничья SHALL переходить в overtime до следующего единоличного лидера. Цель SHALL иметь приоритет на последнем основном tick, исходный trigger сохраняется в overtime. Личные scoring/assists/chains SHALL сохранять существующий applied-event contract.

#### Scenario: Командная цель без индивидуального победителя
- **WHEN** суммарный счёт команды достиг цели, хотя ни один участник её не достиг
- **THEN** результат указывает победившую команду, а не participant

#### Scenario: Одновременное достижение
- **WHEN** обе команды достигают цели в одном tick
- **THEN** сравниваются полные суммы; равенство запускает overtime без сброса счёта и без дополнительного лимита

### Requirement: Независимый snapshot и совместимость FFA
Gameplay SHALL выдавать сериализуемый snapshot с mode, составом, personal standings и team totals. Мутация snapshot SHALL не менять матч. Finished SHALL замораживать результат. Старый native FFA путь SHALL сохранять индивидуальный winner и правила завершения.

#### Scenario: Результат и Repeat foundation
- **WHEN** прочитанный результат изменён caller либо из того же roster/config создан новый reducer
- **THEN** исходный результат неизменен, новый матч имеет нулевой счёт и чистые ledger/chains/timer
