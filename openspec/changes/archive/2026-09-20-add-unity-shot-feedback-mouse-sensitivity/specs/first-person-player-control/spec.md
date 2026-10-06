## MODIFIED Requirements

### Requirement: First-person orientation и камера
Normalized look actions SHALL обновлять сериализуемые yaw и pitch; yaw MUST поддерживать непрерывный поворот, pitch MUST быть ограничен profile-defined диапазоном, а renderer SHALL выводить camera transform и FOV из snapshot и активного profile. Для keyboard/mouse seat сохранённый user-selected sensitivity override в единице profile-defined base sensitivity MUST заменять base sensitivity только после descriptor validation; при отсутствии либо невалидности override используется profile default. Preference MUST NOT входить в snapshot и MUST NOT менять gamepad action mapping.

#### Scenario: Mouse look
- **WHEN** pointer lock активен и пользователь перемещает мышь
- **THEN** следующий action frame содержит normalized look delta с сохранённым валидным override либо profile-defined default, а viewport поворачивается в соответствующую сторону

#### Scenario: Сохранённая чувствительность
- **WHEN** keyboard/mouse игрок меняет sensitivity в normal Settings UI и начинает новый match
- **THEN** новый action frame использует сохранённое валидное значение без изменения pose, combat state или gamepad action frame

#### Scenario: Ограничение pitch
- **WHEN** накопленный vertical look превышает верхнюю или нижнюю границу
- **THEN** pitch остаётся на соответствующей profile-defined границе без camera flip

#### Scenario: Resize
- **WHEN** viewport меняет aspect ratio
- **THEN** camera projection обновляется, сохраняя profile FOV, snapshot orientation и simulation state
