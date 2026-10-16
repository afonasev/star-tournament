#!/usr/bin/env python3
"""Proposal 31: a quality selector in approved settings A; no production mutation."""
from mockups import Screen, OUT, PANEL, GOLD, MUTED
import re
s=Screen('31-graphics-quality','НАСТРОЙКИ · ГРАФИКА','Предложение · три уровня детализации, отдельные настройки эффектов сохраняются')
s.rect(56,210,290,524,PANEL);s.rect(370,210,1014,524,PANEL)
for i,label in enumerate(['Экран','Графика','Звук','Управление','Интерфейс']):
    s.button(76,235+i*79,250,58,label,active=i==1)
rows=[('Качество графики','Высокое'),('Сглаживание','SMAA · среднее'),('Тени','Вкл'),('Затемнение стыков (AO)','Вкл'),('Свечение (Bloom)','Вкл')]
for i,(label,value) in enumerate(rows):
    y=245+i*76
    s.text(398,y+31,label,21)
    s.button(1130,y,220,48,value,focus=i==0)
s.text(398,660,'Детализация моделей и фильтрация текстур.',17,MUTED)
s.rect(1364,232,4,430,'#344a5b');s.rect(1364,232,4,238,GOLD)
s.text(398,697,'Другие эффекты — ниже в списке.',17,MUTED)
s.button(56,755,380,48,'Сбросить все настройки')
s.text(470,786,'Применяется и сохраняется автоматически',18,MUTED)
s.footer('Низкое → Среднее → Высокое. По умолчанию сохраняется текущее высокое качество; баланс Lab не меняется.')
s.save()
p=OUT/'index.html';html=re.sub(r'<div id="graphics-quality">.*?</div>','',p.read_text(),flags=re.S)
section='<div id="graphics-quality"><h2>31 · Графика: три уровня качества · ждёт согласования</h2><a href="31-graphics-quality.png"><img src="31-graphics-quality.png" alt="Страница Графика с тремя уровнями качества"></a></div>'
p.write_text(html.replace('</html>',section+'</html>'))
