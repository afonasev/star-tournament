# Повторные обновления собственных экспериментов

Новые технические представления non-package/non-Default профилей содержат точную ссылку на исходную сохранённую версию. При загрузке они пересчитываются с новым Default, сохраняя storage key. Source/hash/дата и явные0/false сохраняются. Save/Create создаёт самостоятельный полный снимок; явная отметка фиксирует текущий полный снимок. Старым строкам без provenance происхождение не назначается. Выбор профиля использует последнюю сохранённую версию или её представление; более высокий ключ кэша не подменяет её.

Codec: Schema3 — checksummed envelope с прежним Schema2 payload и отдельными source refs; Schema2 DTO/Integrity не изменены. Старые клиенты отказываются от неизвестной Schema3. Строго проверяются outer/inner integrity, duplicates, missing/self/chained refs. Метаданные не помещаются в параметры/legacy overrides.

75 unique affected EditMode cases passed по объединению последнего envelope-набора74 и отдельного exact legacy1. После добавления selector API повторены15 model cases, затем2 PlayMode passed через реальный UI и асинхронную атомарную запись. Два прежних XML имеют failure предусловия exact fixture (body NotRun); повтор отдельно с явным owned fixture прошёл. Начальная реализация provenance пересмотрена до окончательной Schema3. Полного набора/Player build/релиза/выкатки не было.

Exact legacy: исходник160126874bytes,16 исходных версий; новый компакт1008146bytes,28 строк с представлениями. Все исходные версии/hash/даты/designer values и точная резервная копия проверены; полное состояние до/после compaction/reload совпало. Пользовательский файл сохранил SHA2567241dbd29c3d2abb7679532fa7a0478cfde7514d39b94b30e4d85f3c862b938b. Это функциональные проверки, не performance/device/human acceptance.

LAB-UPGRADE остаётся открытым: политика Default/пакетных исторических версий и type/range/resource incompatibility требуют отдельного аудита, как и полный05/parent matrix.
