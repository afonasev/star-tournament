# Достижения и компактная статистика

Реализованы медали со штампом и полными нейтральными причинами всех 30 наград, компактные шапки, bot icon/difficulty без type labels, личный счёт A:B с белым/зелёным/красным цветом, независимые TAB local seats и выбор строки результатов мышью/клавиатурой/D-pad. Общая статистика, award selection, frozen snapshots и начисление очков не изменяются.

Проверки: финальный make check-ui — 3 EditMode + 13 PlayMode; core scoreboard/catalog — 41/41; affected native UI/input — 8/8. Отдельный graphics render fixture завершён успешно и сохраняет реальные UI-компоненты в Editor Play Mode: FFA/teams и TAB/results на 1280×720 и 1920×1080. Responsive catalog fixture проверяет 960×540, 1280×720, Full HD и 4K, минимум 12pt для подписей наград и полную видимость переносимых строк. Версия исходников и checksums — verification.json и planning delivery record.

PNG — рендеры UI fixtures с демонстрационными данными, не скриншоты установленной игры и не human/device acceptance. Размер action panel сохраняется после нижнего anchoring; проверяются реальные границы обеих кнопок. Векторные glyphs имеют CanvasRenderer и непустую геометрию (layout.txt).

Перед QA выполнен unity/tools.sh prepare: tracked scene исходного baseline содержала более старую версию profile; подготовка обновила производные scene/prefab/ProjectSettings только в QA worktree. Эти не относящиеся к change производные обновления не включены в implementation diff.

Player build не требовался: UI/input состояния и рендер проверены в Editor. Standalone Player, физический геймпад/TV, release, installer и публикация отложены до отдельной команды и приёмки.
