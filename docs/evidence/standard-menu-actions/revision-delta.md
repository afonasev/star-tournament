# Новая редакция menu-base0.2.0

17.10.2026: общий game-spec-templates HEAD и origin HEAD равны60fe3d2787dfb860722372791ee67dc8453f6f66, каталог0.6.0. До этого integration pin0c2d4562683252d54736f294dc1af463050eac17, каталог0.5.0. Diff включает только README и features/menu-base/{README,requirements,acceptance,usage}. Остальные четыре пакета неизменны. Новые требования входят в прежнюю полную цель; это не готовность текущего UI.

| Новый ID | Контракт | Текущая конкретная проверка/следующий шаг |
| --- | --- | --- |
| MENU-ACTIONS-01 | Короткое понятное действие, явная область очистки, единые подписи, объяснения рядом | Проверить Lab «Сбросить на релизный Default», create-from-saved dialog, reset/search/draft и остальные согласованные экраны; сохранить доступность смысла и focus hints. Изменённый дизайн сначала preview. |
| MENU-ACTIONS-02 | Нет дублирующих кнопок в одном контексте; shortcuts требуют project rationale | Сопоставить профиль/версию/reset-to-release. Default reset имеет особую семантику greatest marked revision (не latest unmarked), сохранение истории/guard; не удалять как duplicate без фактического анализа. Проверить остальные окна и возврат. |
| MENU-ACTIONS-03 | Один переключатель обратимого признака с явным текстовым/shape состоянием, без обхода guard | Сверить согласованные18 Off/On поля и19 release/unmark с новым правилом. Предложить saved макеты одиночного boolean/release переключателя до изменения UI; не считать approval18–19 автоматическим approval нового вида. |

В текущем mixed-history срезе меняются только данные подписей/причин в прежнем согласованном19, а не новая структура действий. Отдельная следующая работа: полная action inventory → реальные сохранённые previews → согласование новых visual changes → implementation/affected focus/input/rendered QA. Full/Player/build/publication остаются отдельно.
