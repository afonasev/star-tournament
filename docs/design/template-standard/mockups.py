#!/usr/bin/env python3
"""Deterministic review mockups; does not import or modify the Unity runtime."""
from pathlib import Path
from html import escape
import subprocess

OUT = Path(__file__).resolve().parent
INK, CARD, PANEL = '#0b1725', '#142638', '#102131'
GOLD, WHITE, MUTED = '#e3bd78', '#eef2f5', '#9aaebe'


class Screen:
    def __init__(self, name, title, subtitle):
        self.name = name
        self.parts = ['<svg xmlns="http://www.w3.org/2000/svg" width="1440" height="900" viewBox="0 0 1440 900">']
        self.rect(0, 0, 1440, 900, INK)
        self.text(56, 43, 'STAR TOURNAMENT', 16, GOLD, True)
        self.text(1384, 43, 'МАКЕТ · НЕ UNITY', 13, MUTED, anchor='end')
        self.line(56, 61, 1384, 61)
        self.button(56, 92, 134, 48, '‹ НАЗАД')
        self.text(226, 126, title, 32, GOLD, True)
        self.text(226, 165, subtitle, 18, MUTED)

    def rect(self, x, y, w, h, fill=CARD, stroke='none', r=3):
        self.parts.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" stroke="{stroke}"/>')

    def text(self, x, y, value, size=22, color=WHITE, bold=False, anchor='start'):
        self.parts.append(f'<text x="{x}" y="{y}" fill="{color}" font-family="Arial, sans-serif" font-size="{size}" font-weight="{700 if bold else 400}" text-anchor="{anchor}">{escape(value)}</text>')

    def line(self, x, y, x2, y2, color='#304453'):
        self.parts.append(f'<path d="M{x} {y} L{x2} {y2}" stroke="{color}"/>')

    def button(self, x, y, w, h, label, active=False, focus=False):
        self.rect(x, y, w, h, GOLD if active else CARD, GOLD if focus else '#385062')
        self.text(x+w/2, y+h/2+7, label, 19, INK if active else WHITE, True, 'middle')

    def slider(self, x, y, w, label, value, pos=.75):
        self.text(x, y, label, 22)
        self.text(x+w, y, value, 21, GOLD, anchor='end')
        self.rect(x, y+22, w, 4, '#344a5b')
        self.rect(x, y+22, int(w*pos), 4, GOLD)
        self.rect(x+int(w*pos)-5, y+15, 10, 18, WHITE)

    def row(self, x, y, w, label, value):
        self.text(x, y+30, label, 21)
        self.button(x+w-220, y, 220, 48, value)

    def footer(self, text):
        self.line(56, 824, 1384, 824)
        self.text(56, 862, text, 16, MUTED)

    def save(self):
        self.parts.append('</svg>')
        svg = OUT / (self.name+'.svg')
        svg.write_text('\n'.join(self.parts), encoding='utf-8')
        subprocess.run(['rsvg-convert', str(svg), '-o', str(OUT/(self.name+'.png'))], check=True)


def settings(variant):
    s=Screen('01-settings-a' if variant=='A' else '02-settings-b', 'НАСТРОЙКИ',
             'Вариант '+variant+' · '+('привычная левая навигация' if variant=='A' else 'компактные верхние вкладки'))
    labels=['Экран','Графика','Звук','Управление','Интерфейс']
    if variant=='A':
        s.rect(56,210,290,524,PANEL)
        for i,label in enumerate(labels): s.button(76,235+i*79,250,58,label,active=i==2)
        x,y,w=390,250,910
        s.rect(370,210,1014,524,PANEL)
    else:
        for i,label in enumerate(labels): s.button(56+i*269,210,255,52,label,active=i==2)
        s.rect(56,286,1328,448,PANEL)
        x,y,w=102,328,1236
    s.text(x,y,'ЗВУК',23,GOLD,True)
    s.text(x+w,y,'Общие для всех игроков',17,MUTED,anchor='end')
    for i,(label,value,pos) in enumerate([('Общая громкость','80 %',.8),('Музыка','60 %',.6),('Эффекты','90 %',.9),('Речь','85 %',.85)]):
        s.slider(x,y+69+i*89,w,label,value,pos)
    s.button(56,755,380,48,'Сбросить все настройки   △')
    s.text(470,786,'Изменения применяются и сохраняются автоматически',18,MUTED)
    s.footer('Здесь показан активный PlayStation-геймпад: подсказка △ исчезает при вводе мышью или клавиатурой.')
    s.save()


def update():
    s=Screen('03-updates','ОБНОВЛЕНИЕ ИГРЫ','Состояния компактного блока главного меню · демонстрационные версии')
    def card(y,title,body,button):
        s.rect(56,y,1328,142,PANEL)
        s.text(86,y+38,title,24,GOLD,True)
        s.text(86,y+78,body,19)
        s.button(1090,y+34,264,48,button)
    card(204,'Доступна версия 0.1.5','Можно продолжить играть на установленной версии.','Обновить')
    s.button(86,302,220,32,'Что нового')
    card(368,'Загрузка · 42 %','При начале матча загрузка приостановится и продолжится в меню.','Отменить')
    s.rect(86,476,1030,5,'#344a5b');s.rect(86,476,433,5,GOLD)
    card(532,'Версия 0.1.5 готова','Установится после выхода из игры. Можно продолжить играть.','Перезапустить')
    s.rect(56,696,1328,96,PANEL)
    s.text(86,734,'Не удалось проверить обновления',22,GOLD,True)
    s.text(86,767,'Игра доступна. Следующая проверка — при следующем запуске.',18,MUTED)
    s.footer('В обычном меню виден один актуальный блок. При текущей версии остаются номер установленной игры и «Что нового».')
    s.save()


def history():
    s=Screen('04-release-history','ЧТО НОВОГО','История версий · текст из описаний GitHub Releases на языке меню')
    s.rect(56,210,335,580,PANEL);s.rect(415,210,969,580,PANEL)
    for i,(v,sub) in enumerate([('0.1.5','15.10.2026 · пример'),('0.1.4','Установлена · пример'),('0.1.3','Предыдущая версия')]):
        y=235+i*112;s.button(76,y,295,64,v,active=i==0);s.text(92,y+90,sub,16,MUTED)
    s.text(451,263,'Версия 0.1.5',30,GOLD,True)
    s.text(451,301,'15 октября 2026 · демонстрационное наполнение',17,MUTED)
    s.text(451,363,'Новое',25,GOLD,True)
    s.text(451,405,'Здесь будут возможности, вошедшие в выбранный релиз.',21)
    s.text(451,474,'Улучшения',25,GOLD,True)
    s.text(451,516,'Понятное игроку описание изменений поведения.',21)
    s.text(451,585,'Исправления',25,GOLD,True)
    s.text(451,627,'Описание исправленных проблем и их эффекта для игры.',21)
    s.rect(1364,236,4,528,'#344a5b');s.rect(1364,236,4,260,GOLD)
    s.footer('Прокручивается только описание. Установленная версия доступна, даже если не входит в последние выпуски.')
    s.save()


def lab():
    s=Screen('05-design-lab','ЛАБОРАТОРИЯ ГЕЙМДИЗАЙНА','Макет production · профили пакета защищены; свои эксперименты доступны для редактирования')
    s.button(56,205,280,50,'Мой эксперимент ▾');s.button(350,205,180,50,'Версия 3 ▾')
    s.button(546,205,180,50,'Сохранить',active=True);s.button(742,205,198,50,'Создать копию')
    s.button(956,205,170,50,'Переименовать');s.button(1142,205,242,50,'Удалить профиль')
    s.rect(56,276,1328,56,CARD,'#385062');s.text(78,312,'Поиск по группе, названию, описанию или ключу…',19,MUTED)
    s.rect(56,353,284,368,PANEL)
    for i,text in enumerate(['Оружие  · 2','AI','Движение','Звук','Оформление']): s.text(83,396+i*61,text,22,GOLD if i==0 else WHITE,i==0)
    s.rect(360,353,1024,368,PANEL)
    s.text(388,394,'Винтовка',24,GOLD,True)
    for i,(label,key,value,diff) in enumerate([('Урон','rifle.damage','20','18 → 20'),('Скорость снаряда','rifle.speed','80','75 → 80')]):
        y=432+i*130;s.text(388,y,label,23);s.text(388,y+33,key,16,MUTED)
        s.button(975,y-26,56,48,'−');s.button(1044,y-26,160,48,value);s.button(1218,y-26,56,48,'+')
        s.text(388,y+67,diff+' · изменение в черновике',17,GOLD)
    s.text(56,758,'Для следующей игры: Мой эксперимент · версия 3',21,GOLD,True)
    s.button(56,779,310,34,'Сбросить на релизный Default')
    s.button(955,748,190,48,'Все изменения');s.button(1160,748,224,48,'Отменить черновик')
    s.footer('В dev дополнительно: авторские профили в Git, редактирование Default и релизные отметки. Числа здесь — примеры.')
    s.save()


def discard():
    s=Screen('06-draft-warning','ЛАБОРАТОРИЯ · НЕСОХРАНЁННЫЙ ЧЕРНОВИК','Один и тот же диалог перед уходом, сменой профиля или сбросом')
    s.rect(182,220,1076,552,CARD,'#536478')
    s.text(224,274,'Отбросить изменения и вернуться в меню?',29,GOLD,True)
    s.text(224,321,'Сохранённая версия 3 и история профиля останутся без изменений.',20)
    for i,(a,b,c) in enumerate([('Винтовка · Урон','18','20'),('Винтовка · Скорость снаряда','75 м/с','80 м/с'),('AI · Время реакции','0,4 с','0,5 с')]):
        y=378+i*73;s.rect(222,y-29,996,61,PANEL);s.text(240,y+9,a,21);s.text(1184,y+9,b+' → '+c,21,GOLD,anchor='end')
    s.text(224,645,'Несохранённые изменения будут потеряны.',19,MUTED)
    s.button(224,681,510,52,'Продолжить редактирование',focus=True)
    s.button(754,681,464,52,'Отбросить и выйти')
    s.footer('Первый фокус — безопасное продолжение. Длинный diff прокручивается внутри диалога; действия остаются доступны.')
    s.save()


def installer():
    s=Screen('07-installer-repair','УСТАНОВКА И ВОССТАНОВЛЕНИЕ','Схема содержания штатного Windows wizard · оформление мастера остаётся системным')
    s.rect(260,227,920,530,'#f4f5f6','#83909b')
    s.rect(260,227,920,70,'#ffffff');s.text(298,273,'Star Tournament — установка',25,'#182838',True)
    s.text(302,347,'Обнаружена установленная игра',26,'#182838',True)
    s.text(302,394,'C:\\Program Files\\Star Tournament',20,'#3b4c5b')
    s.text(302,449,'Установлена та же версия',21,'#182838')
    s.text(302,490,'Восстановить файлы приложения.',21,'#182838',True)
    s.text(302,535,'Настройки, профили и история сохранятся.',20,'#3b4c5b')
    s.text(302,580,'Для более новой версии здесь будет действие «Обновить».',18,'#3b4c5b')
    s.button(302,668,230,48,'Отмена');s.button(816,668,320,48,'Восстановить',True)
    s.footer('Нужные права и занятые файлы проверяются до замены. Этот мастер не появляется при штатном обновлении из игры.')
    s.save()


def remaining_settings():
    pages=[
        ('08-screen','ЭКРАН',0,[('Режим экрана','Полный экран'),('Разрешение','Auto'),('Вертикальная синхронизация','Вкл'),('Ограничение FPS','Без ограничения')],'При включённом VSync частота ограничена частотой экрана.'),
        ('09-graphics','ГРАФИКА',1,[('Сглаживание','SMAA · среднее'),('Тени','Вкл'),('Затемнение стыков (AO)','Вкл'),('Свечение (Bloom)','Вкл')],'Другие доступные эффекты — во внутреннем списке. Тонкие коэффициенты остаются в Lab.'),
        ('10-input','УПРАВЛЕНИЕ',3,[('Чувствительность мыши','0,2 °/px'),('Геймпад: горизонталь','180 °/с'),('Геймпад: вертикаль','120 °/с'),('Мёртвая зона стика','15 %'),('Инверсия взгляда X','Выкл'),('Инверсия взгляда Y','Выкл')],'Автовыравнивание и задержка сохраняются; внизу — «Справка» и «Устройства».'),
        ('11-interface','ИНТЕРФЕЙС',4,[('Масштаб интерфейса','100 %'),('Повышенный контраст','Выкл'),('Показывать FPS','Вкл'),('Реакции ботов: текст / голос','Вкл / Вкл')],'Масштаб и контраст — общие; личные параметры сохраняют область текущего профиля.')]
    for name,title,selected,rows,hint in pages:
        s=Screen(name,'НАСТРОЙКИ · '+title,'Вариант A · предлагаемый состав страницы, значения демонстрационные')
        s.rect(56,210,290,524,PANEL);s.rect(370,210,1014,524,PANEL)
        for i,label in enumerate(['Экран','Графика','Звук','Управление','Интерфейс']):s.button(76,235+i*79,250,58,label,active=i==selected)
        for i,(label,value) in enumerate(rows):
            y=247+i*(72 if len(rows)>4 else 99)
            if selected==3 and i<4 or label=='Масштаб интерфейса':s.slider(398,y+20,952,label,value)
            else:s.row(398,y,952,label,value)
        s.rect(1364,232,4,430,'#344a5b');s.rect(1364,232,4,270,GOLD)
        s.text(398,686,'Заголовок, разделы и основные действия остаются на месте.',17,MUTED)
        s.button(56,755,380,48,'Сбросить все настройки')
        s.text(470,786,'Применяется и сохраняется автоматически',18,MUTED)
        s.footer(hint);s.save()
    s=Screen('12-settings-reset','СБРОС ВСЕХ НАСТРОЕК','Единственное действие сброса · перед изменением режима экрана действует отдельное безопасное подтверждение')
    s.rect(210,220,1020,542,CARD,'#536478')
    s.text(254,280,'Сбросить все пользовательские настройки?',29,GOLD,True)
    for i,t in enumerate(['Будут восстановлены настройки экрана, графики, звука,',
                          'управления и интерфейса, включая личные предпочтения.',
                          'Профили, рекорды, история матчей и лаборатория сохранятся.',
                          'Согласие на отправку статистики не изменится.']):s.text(254,350+i*54,t,21)
    s.button(254,646,446,56,'Отмена',focus=True);s.button(720,646,466,56,'Сбросить все настройки')
    s.footer('Shortcut верхней кнопки pad действует только в настройках, без активного текстового ввода, списка или другого диалога.')
    s.save()


def text_keyboard():
    s=Screen('15-text-keyboard','ИМЯ ПРОФИЛЯ','Макет ввода с геймпада · пример имени, до 24 символов')
    s.rect(156,204,1128,110,PANEL)
    s.text(192,244,'Новое имя',17,MUTED)
    s.text(192,286,'Пилот |',30,GOLD,True)
    s.text(1244,286,'5 / 24',18,MUTED,anchor='end')
    rows=['1234567890','ЙЦУКЕНГШЩЗХЪ','ФЫВАПРОЛДЖЭ','ЯЧСМИТЬБЮЁ']
    for row,letters in enumerate(rows):
        x=156+(1128-len(letters)*94)/2
        for i,c in enumerate(letters):s.button(x+i*94,340+row*67,84,56,c,focus=row==1 and i==0)
    s.button(156,620,150,52,'RU / EN')
    s.button(318,620,150,52,'А / а')
    s.button(480,620,130,52,'←')
    s.button(622,620,260,52,'Пробел   Y')
    s.button(894,620,130,52,'→')
    s.button(1036,620,248,52,'Удалить   X')
    s.button(156,731,350,56,'Отмена   B')
    s.button(934,731,350,56,'Применить   Menu',active=True)
    s.footer('Xbox: нижняя A вводит выбранный символ; B отменяет черновик. При смене устройства имя и позиция курсора сохраняются.')
    s.save()


def main():
    settings('A');settings('B');update();history();lab();discard();installer();remaining_settings();text_keyboard()
    names=[('01-settings-a','A · Настройки с левой навигацией'),('02-settings-b','B · Настройки с верхними вкладками'),('03-updates','Состояния обновления'),('04-release-history','История версий'),('05-design-lab','Лаборатория'),('06-draft-warning','Защита черновика'),('07-installer-repair','Восстановление Windows')]
    names += [('08-screen','A · Экран'),('09-graphics','A · Графика'),('10-input','A · Управление'),('11-interface','A · Интерфейс'),('12-settings-reset','Общий сброс настроек')]
    names += [('13-help-keyboard','Справка · клавиатура и мышь'),('14-help-gamepad','Справка · Xbox, эквиваленты PlayStation/Switch рядом в UI'),('15-text-keyboard','Экранная клавиатура · пример Xbox')]
    cards=''.join(f'<section><h2>{escape(title)}</h2><a href="{name}.png"><img src="{name}.png" alt="{escape(title)}"></a></section>' for name,title in names)
    (OUT/'index.html').write_text('<!doctype html><html lang="ru"><meta charset="utf-8"><title>Star Tournament · Макеты стандарта</title><style>body{margin:40px;background:#0b1725;color:#eef2f5;font:18px Arial}h1,h2{color:#e3bd78}p{max-width:1000px;line-height:1.6}section{margin:40px 0}img{width:100%;max-width:1440px;border:1px solid #385062}a{color:#e3bd78}</style><h1>Star Tournament · Макеты перед реализацией</h1><p>Рекомендация: A, с привычной левой навигацией. B — альтернативная компоновка. Показаны будущие состояния, а не работающий Unity UI. Значения и версии демонстрационные. Макеты 13–15 предложены для согласования: справка содержит только игровые назначения, экранная клавиатура позволяет вводить текст с геймпада. Геймпад на иллюстрации явно Xbox; эквиваленты PlayStation/Switch выводятся рядом средствами UI.</p>'+cards+'</html>',encoding='utf-8')


if __name__=='__main__': main()
