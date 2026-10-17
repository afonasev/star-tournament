"""Compose saved Unity captures without changing their exposure or colour."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
FONT = '/System/Library/Fonts/Supplemental/Arial.ttf'
title_font = ImageFont.truetype(FONT, 28)
label_font = ImageFont.truetype(FONT, 20)
variants = [
    ('0-current', '0 · Сейчас — штатный свет'),
    ('1-laboratory', '1 · Светлая лаборатория — объём от потолочных ламп'),
    ('2-daylight', '2 · Дневной свет — направленный рисунок и прохладная тень'),
    ('3-practicals', '3 · Приборный свет — локальные островки и цвет этажей'),
]
views = [('room-0', '01 · Биология'), ('room-1', '02 · Физика'),
         ('room-2', '03 · Электроника'), ('atrium', 'Атриум')]
for directory, title in variants:
    canvas = Image.new('RGB', (1600, 1054), '#171d24')
    draw = ImageDraw.Draw(canvas)
    draw.text((18, 12), title, font=title_font, fill='white')
    draw.text((18, 49), 'Реальный Editor render · одинаковые камеры / материалы / геометрия', font=label_font, fill='#c3cbd5')
    for i, (name, label) in enumerate(views):
        x, y = (i % 2) * 800, 90 + (i // 2) * 482
        draw.text((x + 16, y + 5), label, font=label_font, fill='white')
        with Image.open(ROOT / directory / (name + '.png')) as source:
            canvas.paste(source.resize((800, 450), Image.Resampling.LANCZOS), (x, y + 32))
    canvas.save(ROOT / (directory + '-board.jpg'), quality=94, subsampling=0)

# Full-width atrium comparison keeps the shared architecture visible at useful scale.
canvas = Image.new('RGB', (1920, 1160), '#171d24')
draw = ImageDraw.Draw(canvas)
for i, (directory, title) in enumerate(variants):
    x, y = (i % 2) * 960, (i // 2) * 580
    draw.text((x + 16, y + 12), title.split('—')[0], font=title_font, fill='white')
    with Image.open(ROOT / directory / 'atrium.png') as source:
        canvas.paste(source.resize((960, 540), Image.Resampling.LANCZOS), (x, y + 40))
canvas.save(ROOT / 'atrium-comparison.jpg', quality=94, subsampling=0)
