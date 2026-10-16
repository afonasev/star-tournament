#!/usr/bin/env python3
"""Saved proposals for LAB-TEST-01; no Unity source changes."""
from pathlib import Path
from mockups import Screen, OUT, GOLD, WHITE, MUTED, PANEL

def card(s,x,y,w,h,small=False,player=1):
    s.rect(x,y,w,h,PANEL,'#385062',10)
    s.text(x+w/2,y+(30 if small else 48),'ПАУЗА',26 if small else 34,GOLD,True,'middle')
    s.text(x+w/2,y+(58 if small else 83),'Игрок '+str(player),20 if small else 25,WHITE,True,'middle')
    s.text(x+w/2,y+(84 if small else 117),'Баланс матча: Мой эксперимент',15 if small else 19,WHITE,False,'middle')
    s.text(x+w/2,y+(105 if small else 143),'Версия 8 · 17.10.2026',15 if small else 19,MUTED,False,'middle')
    gap=39 if small else 70
    for i,label in enumerate(['Продолжить','Настройки','Начать заново','В главное меню']):
        s.button(x+24,y+(121 if small else 166)+i*gap,w-48,33 if small else 48,label,focus=i==0)

s=Screen('26-match-balance-identity','ВЕРСИЯ БАЛАНСА В ПАУЗЕ','Предложение LAB-TEST-01 · сведения о зафиксированной конфигурации текущего матча',back=False)
card(s,350,210,740,540)
s.footer('Видны профиль, версия и известная дата. Подпись не меняет баланс и не открывает Lab во время матча.')
s.save()
s=Screen('27-match-balance-split','ПАУЗА В РАЗДЕЛЁННОМ ЭКРАНЕ','Предложение · одна и та же версия матча во всех локальных карточках паузы',back=False)
for x in [56,734]:
    for y in [205,512]:
        s.rect(x,y,650,290,'#0a1420','#304453')
# Show the two paused players with actual compact-card geometry; other viewports remain unobstructed.
card(s,120,212,522,280,True)
card(s,798,524,522,276,True,4)
s.text(1059,360,'Игрок 2 продолжает игру',19,MUTED,False,'middle')
s.text(381,667,'Игрок 3 продолжает игру',19,MUTED,False,'middle')
s.footer('Макет размещения: компактные кнопки и подпись проверяются отдельно на 720p, 1–4 viewport и длинных именах.')
s.save()
s=Screen('28-author-history-fallback','ИСТОРИЯ ПРОЕКТА НЕДОСТУПНА','Предложение · исходный файл сохранён, запись в Lab заблокирована',back=False)
s.rect(56,215,1328,538,PANEL,'#385062',8)
s.text(90,267,'Не удалось открыть авторский баланс',28,GOLD,True)
s.text(90,314,'Default · версия 2: well.fill не соответствует допустимому шагу.',21,WHITE)
s.text(90,357,'Файл: DesignLab/authoring-history-v1.json',18,MUTED)
s.line(90,387,1350,387)
s.text(90,434,'Ваши сохранённые значения, версии и релизные отметки не изменены.',22,WHITE)
s.text(90,480,'Можно вернуться к файлу проекта или продолжить на Default из поставки.',22,WHITE)
s.text(90,521,'Временный выбор действует только в этой сессии и не заменяет авторскую историю.',19,MUTED)
s.button(90,625,330,54,'Назад',focus=True)
s.button(450,625,525,54,'Использовать Default из поставки')
s.text(90,721,'Обычный старт остаётся заблокирован до явного выбора. После перезапуска выбор повторяется.',17,MUTED)
s.footer('Макет состояния ошибки: переход на встроенную конфигурацию требует явного действия; сохранение недоступно.')
s.save()
p=OUT/'index.html';text=p.read_text();marker='</html>'
section='<div id="match-balance-identity"><h2>Предложения LAB-TEST-01/STORAGE-01 · ждут согласования</h2>'
for name,title in [('26-match-balance-identity','26 · Версия баланса в паузе'),('27-match-balance-split','27 · Подпись в локальной паузе'),('28-author-history-fallback','28 · Ошибка истории проекта и временный Default')]:
    section+=f'<section><h2>{title}</h2><a href="{name}.png"><img src="{name}.png" alt="{title}"></a></section>'
section+='</div>'
import re
text=re.sub(r'<div id="match-balance-identity">.*?</div>','',text,flags=re.S)
p.write_text(text.replace(marker,section+marker))
