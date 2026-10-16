#!/usr/bin/env python3
"""Proposal fragments for device-aware hints; no Unity mutation."""
from mockups import Screen, OUT, GOLD, WHITE, MUTED, PANEL
s=Screen('29-device-menu-hints','ПОДСКАЗКИ ПО УСТРОЙСТВУ','Предложение · фрагменты существующих меню, без изменения их маршрутов и компоновки',back=False)
for x,pad in [(56,False),(734,True)]:
    s.rect(x,210,650,345,PANEL,'#385062',8)
    s.text(x+28,253,'ГЕЙМПАД XBOX' if pad else 'МЫШЬ И КЛАВИАТУРА',23,GOLD,True)
    s.text(x+28,291,'Главное меню · выбранное действие «Битва»',18,MUTED)
    for i,title in enumerate(['Битва','Настройки игры','Выход']):
        s.button(x+28,315+i*68,594,54,title+(' · A' if pad and i==0 else ''),active=i==0,focus=pad and i==0)
    s.rect(x,580,650,196,PANEL,'#385062',8)
    s.text(x+28,619,'История версий',23,GOLD,True)
    s.button(x+28,639,250,48,'Назад'+(' · B' if pad else ''),focus=False)
    if pad:s.text(x+28,737,'Правый стик — прокрутка описания',19,MUTED)
    else:s.text(x+28,737,'Обычные названия действий без легенды клавиш',18,MUTED)
s.footer('Подсказки геймпада только при его вводе. Нижняя кнопка — на текущем фокусе; семейство меняет обозначения.')
s.save()
p=OUT/'index.html';text=p.read_text();import re
text=re.sub(r'<div id="device-menu-hints">.*?</div>','',text,flags=re.S)
section='<div id="device-menu-hints"><h2>29 · Подсказки по активному устройству · ждёт согласования</h2><a href="29-device-menu-hints.png"><img src="29-device-menu-hints.png" alt="Подсказки по активному устройству"></a></div>'
p.write_text(text.replace('</html>',section+'</html>'))
