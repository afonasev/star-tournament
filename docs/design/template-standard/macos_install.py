#!/usr/bin/env python3
"""Proposal 30: native Finder DMG install layout; no packaging/runtime mutation."""
from base64 import b64encode
from html import escape
from pathlib import Path
import re
from mockups import Screen, OUT, GOLD, MUTED

s = Screen('30-macos-dmg', 'УСТАНОВКА ДЛЯ macOS',
           'Предложение · DMG с приложением и ссылкой на стандартную папку «Программы»', back=False)
# Review canvas surrounds a Finder window. These controls belong to macOS, not the game.
s.rect(176, 182, 1088, 600, '#fbfbfd', '#506176', 12)
s.rect(176, 182, 1088, 72, '#e9e9ec', '#d0d1d5', 12)
s.rect(177, 220, 1086, 34, '#e9e9ec')
for x, color in [(204, '#ff6057'), (226, '#febc2e'), (248, '#28c840')]:
    s.parts.append(f'<circle cx="{x}" cy="210" r="6" fill="{color}"/>')
s.text(302, 224, '‹', 33, '#90919a')
s.text(344, 224, '›', 33, '#b0b1b8')
s.text(395, 225, 'Star Tournament · 0.1.6', 22, '#262933', True)
s.parts.append('<path d="M1148 209 h19 m-19 7 h19 m-19 7 h19" stroke="#777b85" stroke-width="2"/>')
s.parts.append('<circle cx="1216" cy="214" r="7" stroke="#777b85" stroke-width="2" fill="none"/><path d="m1221 219 7 7" stroke="#777b85" stroke-width="2"/>')
s.text(720, 322, 'Перетащите игру в «Программы»', 27, '#252936', True, anchor='middle')
s.text(720, 361, 'Затем откройте Star Tournament из этой папки', 20, '#6b7281', anchor='middle')
icon = Path(__file__).resolve().parents[3] / 'distribution/branding/app-icon.png'
encoded = b64encode(icon.read_bytes()).decode()
s.parts.append(f'<image x="420" y="414" width="140" height="140" href="data:image/png;base64,{encoded}"/>')
s.text(490, 591, 'Star Tournament', 20, '#343946', anchor='middle')
# Conventional folder shortcut; the final icon comes from the host Finder.
s.parts.append('<path d="M866 432 v-13 q0-9 9-9 h42 l17 17 h66 q10 0 10 10 v16 H866Z" fill="#83c9f8"/>')
s.parts.append('<rect x="866" y="439" width="144" height="107" rx="9" fill="#51a9e9" stroke="#3b98dd"/>')
s.parts.append('<path d="m913 513 25-46 25 46 m-39-17 h29" stroke="#d6f0ff" stroke-width="8" fill="none" stroke-linecap="round"/>')
s.parts.append('<path d="m990 529 h28 v28 h-28Z" fill="#fff" stroke="#d4d8df"/><path d="m997 549 13-13 m-10 0 h10 v10" fill="none" stroke="#4f6f94" stroke-width="3"/>')
s.text(938, 591, 'Программы', 20, '#343946', anchor='middle')
s.parts.append('<path d="M637 483 h128 m-24-21 25 21-25 21" stroke="#969eaa" stroke-width="4" fill="none" stroke-linecap="round" stroke-linejoin="round"/>')
s.text(720, 674, 'Afonasev  ·  macOS 12+  ·  Apple Silicon', 17, '#7b808c', anchor='middle')
s.rect(405, 698, 630, 44, '#fff5dc', '#f0dca9', 7)
s.text(720, 726, 'Тестовая поставка · без подписи Apple', 18, '#946b20', anchor='middle')
s.footer('Макет Finder, не готовый DMG. Версия условная; архитектура и минимум ОС будут взяты из сборки.')
s.save()
p = OUT / 'index.html'
html = re.sub(r'<div id="macos-dmg">.*?</div>', '', p.read_text(), flags=re.S)
card = '<div id="macos-dmg"><h2>30 · macOS DMG · ждёт согласования</h2><a href="30-macos-dmg.png"><img src="30-macos-dmg.png" alt="DMG: приложение и ссылка на Программы"></a></div>'
p.write_text(html.replace('</html>', card + '</html>'))
