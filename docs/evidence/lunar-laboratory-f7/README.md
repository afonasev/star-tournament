# Lunar Laboratory F7

Изменение: обе закрывающие стены внутренних лестниц стоят заподлицо с соседними. Плоскость получается из canonical bounds соседней стены, верх — из нижней поверхности перекрытия. Заполнение renderer/collision/navigation использует один solid. Проходы по маршам и закрытый низ сохранены. Map identity: lunar-laboratory-v1@6. Предыдущая F6-геометрия поддержана в Lab history через BeforeLunarFlush.

[Кандидат и checksum](candidate.json) · [Проверки](verification.json) · [Диагностика FPS и предложение](performance-report.md) · [Протокол](protocol.md).

## Осмотр

- [Западная стена](visible/f7-west.png)
- [Восточная стена](visible/f7-east.png)
- [Четыре viewport](visible/17-four-views.png)
- [Завершение естественного матча](visible/complete.json)

Снимки scripted camera, seed20261003, 1920×1080, muted audio. Агент просмотрел обе изменённые стены и four-viewport screenshot. Скриншотный FPS overlay не является performance-замером: таблицы получены отдельным exclusive-процессом. Human acceptance остаётся открытой.

## Проверяемость

- [Манифест источников v2](tested-source-v2.json), [сверка](source-verification.json), [generated build patch](build-generated-v2.patch.gz).
- Измерения performance-vsync и performance-saved-display сделаны одним неизменным v2 Player. В performance хранится ранняя v1 серия, ограниченная недостоверными GPU/stale timing records; точный ранний source диагностического компонента сохранён отдельно.
- Полные сырые CSV/JSON оставлены. [Список удалённых повторных PNG](evidence-cleanup.json) содержит hashes и причины; контрольные виды сохранены.
- Первый полный gate был остановлен владельцем для улучшения диагностического timing-кода; он не засчитывается. Успешный gate записан отдельно. Первые две focused попытки обнаружили ошибки компиляции диагностического кода. Следующая выявила ошибочную верхнюю test probe выше существующей стены: probe приведена к фактической высоте стены, без изменения игрового контракта. Итоговый focused gate проверяет обе плоскости и сохранение traversal.

F6 решения, штатное освещение, материалы и gameplay не оптимизировались. Специальные performance-флаги работают только в dev/editor диагностике и не сохраняют настройки пользователя. Локальная сборка для проверки не означает внешнюю публикацию или приёмку.
