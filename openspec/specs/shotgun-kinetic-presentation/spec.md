# shotgun-kinetic-presentation Specification

## Purpose

Кинетическая presentation-подача делает отдельные дробины, их траектории и завершения мгновенно читаемыми в скоростном первом лице, не превращая sporting sci-fi арену в жестокую или милитаристскую сцену.

## Requirements

### Requirement: Кинетический след дробового выстрела
После versioned `shotgun-shot` event renderer SHALL представить выстрел как кинетический: краткий тёплый двойной muzzle burst из обоих стволов, быстро рассеивающийся дым и короткие видимые трассы отдельных дробин. Для local first-person shot каждая трасса MUST начинаться из соответствующего видимого muzzle mount и следовать коротким presentation segment в направлении already-authoritative impact/range endpoint; frame timing и renderer RNG MUST NOT менять combat result. Эффекты MUST быть presentation-only и MUST NOT показываться без успешного shot event.

#### Scenario: Успешный выстрел
- **WHEN** local participant получает versioned `shotgun-shot` event
- **THEN** оба ствола дают кинетический muzzle burst, а дробины получают короткие трассы в их фактических направлениях без изменения ammo, cooldown, hit result или damage

#### Scenario: Dry fire
- **WHEN** fire press не создаёт `shotgun-shot` event
- **THEN** renderer не создаёт muzzle burst, дым или трассы как подтверждение выстрела

### Requirement: Material-aware завершение дробины
Для каждой дробины, закончившейся на видимой world surface или живом participant, renderer SHALL показать короткий локальный impact, привязанный к существующей authoritative hit position: твёрдая архитектура получает искры и мелкий скол, а robot shell — искры и небиологические фрагменты покрытия. Effect MUST оставаться без крови, gore, persistent decals или новых collision/occlusion объектов и MUST деградировать по существующему graphics-quality budget без потери combat feedback.

#### Scenario: Попадание в архитектуру
- **WHEN** дробина завершает hitscan на видимой arena surface
- **THEN** в её hit position кратко видны искры и скол без создания persistent geometry или изменения projectile query

#### Scenario: Попадание в робота
- **WHEN** дробина наносит damage живому participant
- **THEN** в соответствующей presentation hit position кратко видны искры и фрагменты покрытия без крови и без дополнительного damage event

#### Scenario: Скрытое завершение
- **WHEN** дробина завершилась вне current camera frustum либо за пределами presentation budget
- **THEN** renderer вправе не создавать её world effect, сохраняя hitmarker, HUD и simulation result
