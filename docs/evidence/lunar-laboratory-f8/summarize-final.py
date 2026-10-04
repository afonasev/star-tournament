#!/usr/bin/env python3
"""Summarize retained F8 raw reports without trimming frames or inferring acceptance."""
import json
from pathlib import Path

root = Path(__file__).resolve().parent
runs = {}
for key in ('exact-frozen', 'exact-focus-repeat', 'exact-live-baseline'):
    runs[key] = json.loads((root / key / 'performance.json').read_text())
records = [{**sample, 'run': key} for key, report in runs.items() for sample in report['samples']]
by_name = {}
for sample in records:
    by_name.setdefault(sample['name'], []).append(sample)

def selected(name):
    # Keep every rejected sample in records; use the explicitly allowed valid repeat.
    return next((s for s in reversed(by_name[name]) if s['focused']), None)

pairs = []
invariants = ('feet', 'yaw', 'pitch', 'fieldOfView', 'renderScale', 'width', 'height', 'quality', 'cameras', 'matchTime')
def same_state(a, b):
    if isinstance(a, dict) and isinstance(b, dict):
        return a.keys() == b.keys() and all(same_state(a[k], b[k]) for k in a)
    if isinstance(a, list) and isinstance(b, list):
        return len(a) == len(b) and all(same_state(x, y) for x, y in zip(a, b))
    if isinstance(a, float) and isinstance(b, float):
        return abs(a - b) <= 0.00001
    return a == b
for name in ('yard', 'hall', 'north-lower', 'upper', 'lab-a', 'lab-b', 'west-stair', 'east-stair', 'west-opening', 'east-opening', 'north-door', 'south-door', 'west-stair-low', 'east-stair-low'):
    a, b = selected(name + '-baseline'), selected(name + '-candidate')
    assert a and b, name + ': missing valid focused sample'
    differences = [k for k in invariants if a[k] != b[k]]
    assert not differences, (name, differences)
    assert same_state(json.loads(a['state']), json.loads(b['state'])), name + ': materially different state'
    pairs.append({'view': name, 'baseline': a['run'], 'candidate': b['run'], 'same_pose_settings': True,
                  'state_exact_equal': a['state'] == b['state'], 'state_float_tolerance': 0.00001,
                  'separate_runs': a['run'] != b['run'], 'mean_reduction_percent': 100 * (1 - b['frameMs']['mean'] / a['frameMs']['mean'])})

for key, report in runs.items():
    assert report['vsync'] == 1 and report['refreshHz'] == 120 and report['seed'] == 20261003, key
    assert len(report['lights']) == 90, key
    for s in report['samples']:
        assert (s['width'], s['height'], s['cameras'], s['renderScale']) == (3456, 2160, 1, 1), s['name']

summary = {'status': 'DIAGNOSTIC_NOT_ACCEPTANCE', 'source_revision': 'f3776259fdef1ac05590055fb9f45859de7e7bd0',
           'player_manifest': 'exact-player-manifest.json', 'raw_frames_trimmed': 0, 'records': records, 'pairs': pairs,
           'rejected_focus_samples': [s['name'] for s in records if not s['focused']],
           'natural_match': json.loads((root / 'exact-visual/complete.json').read_text()),
           'limits': ['GPU FrameTiming at this resolution is not a trustworthy budget.',
                      'Live runs have the same seed/pose/lighting plan but differing simulation time at sample start; compare as separate real runs, not identical-state causal pairs.',
                      'Lab B baseline is a later focused repeat; its candidate is from the original run. This cross-run comparison has lower confidence.',
                      'No stable 120 FPS or human visual/game acceptance is established.']}
(root / 'final-summary.json').write_text(json.dumps(summary, indent=2))
lines = ['# F8 — финальная диагностика', '', 'Точный Player: `exact-player-manifest.json`, source `f377625`. 3456×2160 fullscreen, VSync1/120Hz, seed20261003, одна камера, 8 hard bots. Все 90 источников сохранены. Raw CSV/PNG и невалидные samples сохранены без обрезания.', '',
         '## Парные frozen виды', '', '| Вид | mean baseline → candidate, мс | p99 baseline → candidate, мс | Δ mean |', '| --- | ---: | ---: | ---: |']
for p in pairs:
    a, b = selected(p['view'] + '-baseline'), selected(p['view'] + '-candidate')
    lines.append(f"| {p['view']} | {a['frameMs']['mean']:.2f} → {b['frameMs']['mean']:.2f} | {a['frameMs']['p99']:.2f} → {b['frameMs']['p99']:.2f} | {p['mean_reduction_percent']:.1f}% |")
lines += ['', 'У всех выбранных frozen пар совпадают pose, FOV, resolution, quality, renderScale, cameras и matchTime. State совпадает побитно кроме south-door: float settling Position.y различается на 5.96e-8 м, Velocity.y на 2.98e-6 м/с; числовой допуск анализатора1e-5, raw states сохранены. Верхний зал и Lab A используют единственный разрешённый повтор после потери фокуса. Lab B — сравнение между разными запусками, уверенность ниже. Небольшое изменение двора и верхнего зала нельзя трактовать как надёжную экономию.', '',
          '## Все samples, включая потерянный фокус', '', '| Run / sample | Focus | mean | p95 | p99 | max | >16.67 / >25 / >50 мс |', '| --- | --- | ---: | ---: | ---: | ---: | --- |']
for s in records:
    f = s['frameMs']
    lines.append(f"| {s['run']} / {s['name']} | {s['focusedFrames']}/{s['focusedFrames'] + s['unfocusedFrames']} | {f['mean']:.2f} | {f['p95']:.2f} | {f['p99']:.2f} | {f['max']:.2f} | {f['hitches16']} / {f['hitches25']} / {f['hitches50']} |")
lines += ['', '## Вывод и ограничения', '',
          'Нижний зал: 21.85→15.56 мс в этом прогоне (≈46→64 FPS), ранее валидно 17.89→11.30 мс. Live baseline→candidate: 17.56→9.45 мс (≈57→106 FPS); p99 33.66→18.94 мс. Live стартует с разным simulation time и не является identical-state pair. Все хвосты сохранены: верхний candidate в повторе имеет max 50.70 мс и один кадр >50 мс. Стабильные 120 FPS не доказаны.', '',
          'Валидные верхние группы: baseline13.81 мс; без lower-point9.94, lower-spot13.96, upper-point10.99, upper-spot12.10, stairs-point13.99, stairs-spot17.26. Это отдельные последовательные samples, не аддитивный budget; рост последнего sample также сохранён. Старые CPU tails и потеря фокуса остаются в прежних reports и не заменяются этой серией.', '',
          'Просмотр агента: `final-visible/floors.png`, `stairs.png`, `openings.png`, `native-f7-f8.png`; исходные PNG рядом. Яркий офисный вид, геометрия, материалы и читаемость сохранены; местами локальные блики сильнее. Это агентская visual проверка, не human acceptance.', '',
          'Natural match PASS: seed20261003, 149 kills, 136 collections, muted. Полный автоматический gate ранее PASS334 EditMode +201 PlayMode, zero skipped/failed; exact build и весь exclusive QA batch exit0. Human visual/game acceptance открыта; archive запрещён.', '',
          'Полные CPU/GPU/main/render/present-wait/draw/setpass distributions и счётчики доступны в `final-summary.json` и исходных `performance.json`. GPU FrameTiming не используется как доказательство 8.33-мс budget.', '']
(root / 'final-report.md').write_text('\n'.join(lines))
print('PASS: 14 frozen pairs; all samples retained; no acceptance inferred')
