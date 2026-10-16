#!/usr/bin/env python3
"""Git-bound bilingual release notes. Offline scaffold/render; never publishes."""
import argparse
import json
from pathlib import Path
import re
import subprocess

CATEGORIES = {'added': ('Новое', 'Added'), 'improved': ('Улучшения', 'Improved'), 'fixed': ('Исправления', 'Fixed')}
SEMVER = re.compile(r'(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?\Z')


def version_key(version):
    match = SEMVER.fullmatch(version)
    if not match: raise ValueError('Unsupported SemVer: ' + version)
    core = tuple(int(x) for x in match.group(1, 2, 3))
    pre = match.group(4)
    if pre is None: return core + ((1,),)
    parts = []
    for value in pre.split('.'):
        if value.isdigit() and len(value) > 1 and value[0] == '0': raise ValueError('Noncanonical prerelease number')
        parts.append((0, int(value)) if value.isdigit() else (1, value))
    return core + ((0, tuple(parts)),)


def previous_release(rows, version, channel):
    if channel not in ('test', 'production') or ('-' in version) != (channel == 'test'): raise ValueError('Channel/version mismatch')
    target = version_key(version); choices = {}
    for row in rows:
        if row.get('draft') or not row.get('published_at') or bool(row.get('prerelease')) != (channel == 'test'): continue
        tag = row.get('tag_name', '')
        if not tag.startswith('v'): continue
        try: key = version_key(tag[1:])
        except ValueError: continue
        if ('-' in tag) != (channel == 'test') or key >= target: continue
        if tag in choices: raise ValueError('Duplicate published release tag')
        choices[tag] = key
    return max(choices, key=choices.get) if choices else None


def git(repo, *args):
    result = subprocess.run(['git', '-C', str(repo), *args], text=True, encoding="utf-8", capture_output=True)
    if result.returncode: raise ValueError('Git history unavailable: ' + result.stderr.strip()[:500])
    return result.stdout.strip()


def resolve(repo, ref):
    return git(repo, 'rev-parse', '--verify', ref + '^{commit}')


def history(repo, source, previous):
    if not re.fullmatch('[0-9a-f]{40}', source): raise ValueError('Exact source SHA required')
    if git(repo, 'rev-parse', '--is-shallow-repository') != 'false': raise ValueError('Complete Git history required; fetch history before preparing notes')
    if resolve(repo, source) != source: raise ValueError('Source identity mismatch')
    if previous is not None:
        if set(previous) != {'tag', 'revision'} or not previous['tag'].startswith('v'): raise ValueError('Invalid previous release identity')
        version_key(previous['tag'][1:])
        if not re.fullmatch('[0-9a-f]{40}', previous['revision']) or resolve(repo, 'refs/tags/' + previous['tag']) != previous['revision']: raise ValueError('Previous release tag/revision mismatch')
        check = subprocess.run(['git', '-C', str(repo), 'merge-base', '--is-ancestor', previous['revision'], source], capture_output=True)
        if check.returncode: raise ValueError('Previous release must be an ancestor of source')
    range_ref = previous['revision'] + '..' + source if previous else source
    return [dict(zip(('commit', 'subject'), line.split('\0', 1))) for line in git(repo, 'log', '--reverse', '--format=%H%x00%s', range_ref).splitlines()]


def scaffold(repo, source, channel, previous):
    if channel not in ('test', 'production'): raise ValueError('Invalid channel')
    return dict(schema=1, source_ref=source, channel=channel, previous_release=previous,
                changes=[], omitted=[], pending=history(repo, source, previous))


def text(value):
    if not isinstance(value, str) or not value.strip() or len(value) > 1500 or any(ord(c) < 32 for c in value):
        raise ValueError('Each note needs a nonempty single-line player description')
    if re.search(r'[0-9a-f]{40}|(?:feat|fix|chore|docs|test|refactor)(?:\([^)]*\))?:', value, re.I):
        raise ValueError('Commit identifiers/subjects are not player release notes')
    return value.strip()


def render(review, repo, source, channel, previous):
    if review.get('schema') != 1 or review.get('source_ref') != source or review.get('channel') != channel or review.get('previous_release') != previous:
        raise ValueError('Review is bound to a different Git range/channel')
    commits = {row['commit'] for row in history(repo, source, previous)}
    covered = set(); groups = {key: [] for key in CATEGORIES}
    if review.get('pending'): raise ValueError('Review still contains unreviewed commits')
    for kind in ('changes', 'omitted'):
        rows = review.get(kind)
        if not isinstance(rows, list): raise ValueError('Review changes/omitted lists required')
        for row in rows:
            refs = row.get('commits')
            if not isinstance(refs, list) or not refs: raise ValueError('Every entry needs Git evidence')
            for ref in refs:
                if ref not in commits or ref in covered: raise ValueError('Foreign/duplicate commit evidence: ' + str(ref))
                covered.add(ref)
            if kind == 'omitted':
                text(row.get('reason'))
            else:
                if row.get('category') not in CATEGORIES: raise ValueError('Unknown change category')
                groups[row['category']].append((text(row.get('ru')), text(row.get('en'))))
    if covered != commits: raise ValueError('Unreviewed Git commits: ' + str(len(commits - covered)))
    sections = []
    for language, title in enumerate(('Русский', 'English')):
        lines = ['## ' + title, '']
        for category, names in CATEGORIES.items():
            if groups[category]: lines += ['### ' + names[language], ''] + ['- ' + pair[language] for pair in groups[category]] + ['']
        if not any(groups.values()): lines += [('Техническое обслуживание; изменений для игрока не заявлено.', 'Maintenance release; no player-facing changes are listed.')[language], '']
        sections.append('\n'.join(lines).strip())
    body = '\n\n'.join(sections) + '\n'; validate_body(body)
    return body


def validate_body(body):
    if not isinstance(body, str) or len(body.encode("utf-16-le")) > 131072: raise ValueError('Bilingual release body required (max65536 characters)')
    if body.count('## Русский\n') != 1 or body.count('## English\n') != 1 or not body.startswith('## Русский\n'):
        raise ValueError('Canonical Russian and English release sections required')
    ru, en = body[len('## Русский\n'):].split('## English\n')
    if not ru.strip() or not en.strip() or ru.count('\n- ') != en.count('\n- '): raise ValueError('Incomplete bilingual release body')
    def counts(section,language):
        result={}
        for heading,content in re.findall(r'^### ([^\n]+)\n(.*?)(?=^### |\Z)',section,re.M|re.S):
            category=next((key for key,names in CATEGORIES.items() if names[language]==heading),None)
            if category is None or category in result: raise ValueError('Unknown or duplicate release category')
            result[category]=content.count('\n- ')
        return result
    if counts(ru,0)!=counts(en,1): raise ValueError('Bilingual categories describe different sets of changes')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['scaffold', 'render'])
    parser.add_argument('--repo', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--source-ref', required=True)
    parser.add_argument('--channel', choices=['test', 'production'], required=True)
    parser.add_argument('--previous-tag', help='Exact published tag supplied by release preparation; omit for first release')
    parser.add_argument('--review', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    previous = dict(tag=args.previous_tag, revision=resolve(args.repo, 'refs/tags/' + args.previous_tag)) if args.previous_tag else None
    if args.action == 'scaffold': result = json.dumps(scaffold(args.repo, args.source_ref, args.channel, previous), ensure_ascii=False, indent=2) + '\n'
    else:
        if not args.review: parser.error('--review required for render')
        result = render(json.loads(args.review.read_text()), args.repo, args.source_ref, args.channel, previous)
    args.output.write_text(result, encoding='utf-8')
    print(args.output)


if __name__ == '__main__': main()
