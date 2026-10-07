# F1 — доработка по десяти скриншотам

Кандидат реализует верхнюю кнопку возврата, нижние отступы профилей/диалогов, компактные кнопки, геометрические миниатюры трёх опубликованных карт, графическую справку клавиатуры/мыши/геймпада со стрелками, квадратные галочки, полные списки разрешений и устройств с прокруткой, имя в паузе без брендинга, один общий FPS. Статус готовности устройства находится в той же строке.

Превью строятся из ArenaDefinition и не меняют геометрию карты. Справка иллюстрирует существующие привязки. FPS агрегирует персональные предпочтения по правилу «включён хотя бы у одного локального человека», без изменения формата сохранений. Все обязательные проверки и 6 native сценариев прошли. Результаты: verification.json. Галерея: gallery.html. Кандидат: /Users/eaafonasev/Projects/star-tournament/.local/player-candidates/menu-F1-2e3ae8c/StarTournamentProvingGround.app. Предыдущая галерея относится к 34f610a.

QA: make check-ui; SettingsGamepadNavigationTests; NativeMenuPresentationTests; RosterCardsTests; DesignLabWorkspaceTests; NativeScoreboardLayoutTests; build; muted native menu/setup/pause1/pause4/results/Lab screenshots. Human visual/device acceptance остаётся открытой.
