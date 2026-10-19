#!/usr/bin/env python3
"""Render proposed menu-base0.2.0 changes only; never touch Unity or old PNGs."""
from pathlib import Path
from html import escape
import re
from mockups import Screen, OUT, GOLD, WHITE, MUTED, INK, CARD, PANEL

SCREENS = [
    ('20-lab-short-actions', '20 · Lab: короткие действия и одно логическое поле'),
    ('21-release-toggle', '21 · Релизная отметка совместимой версии'),
    ('22-historical-marked', '22 · Несовместимая версия: отметку можно снять'),
    ('23-historical-unmarked', '23 · После снятия отметки: повторное включение недоступно'),
    ('24-screen-actions', '24 · Настройки A: понятный автоподбор разрешения'),
    ('25-copy-profile', '25 · Создание копии сохранённой версии'),
]

def check(s, x, y, w, label, checked=False, enabled=True, focus=False):
    color=WHITE if enabled else MUTED
    s.rect(x,y,w,50,CARD if enabled else '#10202d',GOLD if focus else '#385062')
    s.rect(x+14,y+12,26,26,GOLD if checked and enabled else '#344a5b' if checked else INK,color)
    if checked:s.text(x+27,y+33,'✓',23,INK if enabled else WHITE,True,'middle')
    s.text(x+55,y+32,label,19,color,True)


def lab():
    s=Screen(SCREENS[0][0],'ЛАБОРАТОРИЯ ГЕЙМДИЗАЙНА','Предложение по menu-base0.2.0 · значения демонстрационные')
    check(s,1110,92,274,'В релиз',True,False)
    s.text(1110,167,'Сначала сохраните правки',16,MUTED)
    for x,w,label in [(56,280,'Мой эксперимент ▾'),(350,180,'Версия 3 ▾'),(546,180,'Сохранить'),(742,198,'Создать копию'),(956,170,'Переименовать'),(1142,242,'Удалить профиль')]:
        s.button(x,205,w,50,label,active=label=='Сохранить')
    s.rect(56,276,1328,56,CARD,'#385062');s.text(78,312,'Поиск по группе, названию, описанию или ключу…',19,MUTED)
    s.rect(56,353,284,368,PANEL)
    for i,label in enumerate(['Оружие','AI','Движение','Звук','Оформление · 1']):s.text(83,396+i*61,label,22,GOLD if i==4 else WHITE,i==4)
    s.rect(360,353,1024,368,PANEL);s.text(388,394,'Кровь',24,GOLD,True)
    s.text(388,435,'Кровь включена',22);s.text(388,461,'blood.enabled · логическое поле',15,MUTED)
    s.text(388,487,'Показывает кровь и временные следы.',17,MUTED)
    s.text(388,511,'Включено → выключено · правка не сохранена',16,GOLD)
    check(s,1060,412,194,'Выкл',False,True,True);s.button(1268,412,96,50,'Вернуть')
    for y,title,key,value,detail in [(555,'Капли за попадание','blood.drops · целое число','15','Диапазон 1–24 · шаг 1'),(651,'Время полёта','blood.flightSeconds · секунды','1,20','Диапазон 0,1–3 · шаг 0,05')]:
        s.line(388,y-30,1356,y-30);s.text(388,y,title,22);s.text(388,y+26,key,15,MUTED);s.text(388,y+52,detail,16,MUTED)
        s.button(974,y-17,52,48,'−');s.button(1040,y-17,146,48,value);s.button(1200,y-17,54,48,'+');s.button(1268,y-17,96,48,'Вернуть')
    s.text(56,753,'Для следующей игры: Мой эксперимент · версия 3',21,GOLD,True)
    s.button(56,776,310,34,'Версия по умолчанию');s.text(386,800,'Default · последняя релизная версия',17,MUTED)
    s.button(955,748,190,48,'Все изменения');s.button(1160,748,224,48,'Сбросить правки')
    s.footer('A / Enter — переключить · «Вернуть» — значение выбранной версии · правки действуют после сохранения.')
    s.save()


def versions(name, old=False, marked=False):
    s=Screen(name,'СОХРАНЁННЫЕ ВЕРСИИ','Предложение · одна релизная отметка · просмотр не меняет базу, черновик и активную игру',back=False)
    s.rect(56,205,645,556,PANEL);s.rect(725,205,659,556,PANEL)
    for i,(title,detail) in enumerate([('Версия 7 · 16.10.2026','Выбранная база · в релизе проекта'),('Версия 6 · 14.10.2026','Совместима · без отметки'),('Версия 5 · 12.10.2026',('В релизе проекта' if marked or not old else 'Без отметки')+' · прежняя схема'),('Версия 4 · дата неизвестна','Прежняя схема · без отметки')]):
        y=232+i*116;selected=i==(2 if old else 1);s.rect(78,y,601,100,CARD,GOLD if selected else '#385062');s.text(101,y+37,title,23,GOLD if selected else WHITE,True);s.text(101,y+72,detail,18,MUTED)
    s.text(752,251,'Default · версия '+('5' if old else '6'),27,GOLD,True)
    s.text(752,300,'Запуск недоступен' if old else 'Совместима с текущей игрой',22,'#e79585' if old else WHITE,True)
    s.text(752,342,'Изменились ограничения well.fill.' if old else 'Можно выбрать для следующей игры.',20)
    s.text(752,378,'Исходное значение и дата сохранены.' if old else 'Отметка не переключает выбранную базу.',19,MUTED)
    check(s,752,419,605,'В релиз',marked,enabled=(not old or marked))
    if old:
        s.text(752,510,'Снять отметку можно; включить снова — нельзя.' if marked else 'Не отмечена. Включение недоступно:',19,MUTED)
        s.text(752,544,'Версия несовместима с текущей игрой.',19,MUTED)
    else:
        s.text(752,510,'Включает эту версию в следующий выпуск.',19,MUTED)
        s.text(752,544,'Повторное нажатие снимает отметку.',19,MUTED)
    if old:
        s.rect(752,598,605,48,'#172632','#304453');s.text(1054,629,'Выбрать',19,MUTED,True,'middle')
    else:s.button(752,598,605,48,'Выбрать')
    s.button(1107,681,250,52,'Закрыть · B',focus=True)
    s.text(56,797,'Для следующей игры: Default · версия 7. Черновик остаётся без изменений.',20,GOLD)
    s.footer('↑↓ — просмотр · A / Enter — действие · B / Esc — закрыть. Снятие отметки не удаляет версию.')
    s.save()


def screen_settings():
    s=Screen(SCREENS[4][0],'НАСТРОЙКИ','Предложение · привычный раздел A · понятная подпись автоподбора')
    s.rect(56,210,290,524,PANEL);s.rect(370,210,1014,524,PANEL)
    for i,label in enumerate(['Экран','Графика','Звук','Управление','Интерфейс']):s.button(76,235+i*79,250,58,label,active=i==0)
    s.row(398,247,952,'Режим экрана','Полный экран')
    s.row(398,346,952,'Разрешение','1920 × 1080')
    s.text(398,435,'Автоподбор по текущему экрану',17,MUTED);s.button(1130,404,220,48,'Автоматически')
    check(s,398,506,952,'Вертикальная синхронизация',True)
    s.row(398,600,952,'Ограничение FPS','Без ограничения')
    s.text(398,697,'Режим и разрешение меняются с подтверждением.',17,MUTED)
    s.button(56,755,380,48,'Сбросить все настройки');s.text(470,786,'Остальные параметры применяются и сохраняются сразу.',18,MUTED)
    s.footer('A / Enter — выбрать · B / Esc — назад. Для автоподбора используется текущий экран.')
    s.save()


def copy_profile():
    s=Screen(SCREENS[5][0],'НОВЫЙ ПРОФИЛЬ','Предложение · короткий заголовок, источник копии указан рядом',back=False)
    s.rect(180,220,1080,532,PANEL)
    s.text(222,274,'Копия сохранённой версии',25,GOLD,True)
    s.text(222,316,'Мой эксперимент · версия 3',23)
    s.text(222,360,'Несохранённые правки не копируются.',20,MUTED)
    s.text(222,419,'Имя профиля',20)
    s.rect(222,444,996,62,CARD,GOLD);s.text(244,485,'Новый профиль│',24)
    s.text(222,554,'Если есть правки, перед созданием появится подтверждение.',19,MUTED)
    s.text(222,590,'У копии будет своя история. Релизная отметка не переносится.',19,MUTED)
    s.button(222,649,340,54,'Отмена');s.button(878,649,340,54,'Создать',active=True)
    s.footer('Поле имени поддерживает клавиатуру и экранный ввод с геймпада. Исходная сохранённая версия остаётся прежней.')
    s.save()


def gallery():
    path=OUT/'index.html';html=path.read_text()
    html=re.sub(r'<div id="menu-actions-060">.*?</div>','',html,flags=re.S)
    intro='A и макеты03–19 согласованы. B — прежний альтернативный вариант. Новые макеты20–25 по menu-base0.2.0 ожидают согласования. Это предложения, не Unity UI; значения и версии демонстрационные.'
    html=re.sub(r'<p>.*?</p>','<p>'+intro+'</p>',html,count=1,flags=re.S)
    html=html.replace('18 · Предложение:','18 · Согласованный прежний вариант:').replace('19 · Предложение:','19 · Согласованный прежний вариант:')
    cards=''.join('<section><h2>'+escape(title)+'</h2><a href="'+name+'.png"><img src="'+name+'.png" alt="'+escape(title)+'"></a></section>' for name,title in SCREENS)
    html=html.replace('</html>','<div id="menu-actions-060"><h2>Предложения по menu-base0.2.0 · ждут согласования</h2>'+cards+'</div></html>')
    path.write_text(html)

if __name__=='__main__':
    lab();versions(SCREENS[1][0]);versions(SCREENS[2][0],old=True,marked=True);versions(SCREENS[3][0],old=True);screen_settings();copy_profile();gallery()
