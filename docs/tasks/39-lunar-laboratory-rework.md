# Лунная лаборатория — F1/F2

Change: add-lunar-laboratory-map. Runtime commit 0c31718; evidence commit 795aa34; canonical spec sync cb07901.

Реализованы optical sight / shot obstruction, свободные от табличек выходы, физические ящики и маршруты, стекло, Земля/фон/крыша/фасад. Последнее уточнение F2: яркое офисное освещение обоих этажей, ещё 12 ящиков внутри и 16 во дворе, четыре линии коммуникаций, ближние камни и крошка.

Проверено: make check exit 0, 314 EditMode + 188 PlayMode, build PASS; focused проходы/лестницы PASS; natural match восьми hard bots — 134 kills / 135 pickups; 23 offscreen и 24 visible PNG. Меню, single/four-view HUD и изменённые помещения/двор просмотрены. Q3 устранён после доступности Mac.

Точный локальный Player и checksum: ../evidence/lunar-laboratory-f2/candidate.json. Прежние кандидаты сохранены. Внешняя публикация не выполнялась. Human игровая/визуальная приёмка ожидается отдельно. Cleanup и актуальный lifecycle — в shared planning delivery.json.

F4 in progress: remove exactly cargo-north-spine and cargo-north-low from pictured north lower room. Own worktree star-tournament-open-centre / codex/lunar-open-centre. Geometry focused PASS; build then exact visible/natural Player and full make check pending. Previous candidates retained.

F5: surface-mounted centred signage; renderer-only. All 30 labels pass actual glyph-centre and 3x3 physical backing probes. UI gate (14), focused sign test (30 labels / 270 probes), final build and exact visible Player (54 PNG) PASS. Renderer-only; human acceptance pending.
