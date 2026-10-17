"""Read-only identifier coverage audit. Coverage never proves implementation/acceptance."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import re
import subprocess

p=argparse.ArgumentParser()
p.add_argument('--template',type=Path,required=True)
p.add_argument('--code',type=Path,required=True)
p.add_argument('--output',type=Path,required=True)
a=p.parse_args()
t=a.template;repo=a.code
packages=('menu-base','settings','design-lab','updates','installer')
inputs=[t/'features'/x/'requirements.md' for x in packages]+[t/'features/settings/controls-help.md']
rows={}
for path in inputs:
    for match in re.finditer(r'^### ([A-Z]+(?:-[A-Z]+)+-\d+)\b',path.read_text(),re.M):
        key=match.group(1)
        if key in rows: raise ValueError('Duplicate normative ID '+key)
        rows[key]=str(path.relative_to(t))
matrices=[repo/'docs/TEMPLATE_ALIGNMENT.md',repo/'docs/evidence/standard-design-lab/requirement-matrix.md']
text='\n'.join(path.read_text() for path in matrices)
represented=set(re.findall(r'^\| ([A-Z]+(?:-[A-Z]+)+-\d+) \|',text,re.M))
# OPT-LANGUAGE is a single suffix; do not accidentally omit it from both sides.
options=set(re.findall(r'^\| (OPT(?:-[A-Z]+)+) \|',(t/'features/settings/options.md').read_text(),re.M))
optionrows=set(re.findall(r'^\| (OPT(?:-[A-Z]+)+) \|',text,re.M))
missing=sorted(rows.keys()-represented)
remote=subprocess.check_output(['git','ls-remote','origin','HEAD'],cwd=t,text=True).split()[0]
pin=subprocess.check_output(['git','rev-parse','HEAD'],cwd=t,text=True).strip()
report={
    'at':datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'codeBaseline':subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo,text=True).strip(),
    'templateLocal':pin,'templateRemoteHEAD':remote,
    'templateWorktreeStatus':subprocess.check_output(['git','status','--short'],cwd=t,text=True),
    'templateMatchesTarget':pin==remote=='60fe3d2787dfb860722372791ee67dc8453f6f66',
    'normativeCount':len(rows),'packageCounts':{name:sum('/'+name+'/' in source for source in rows.values()) for name in packages},
    'normativeIds':rows,'missingNormativeRows':missing,'extraNormativeRows':sorted(represented-rows.keys()),
    'optionsCount':len(options),'optionIds':sorted(options),'missingOptions':sorted(options-optionrows),'extraOptions':sorted(optionrows-options),
    'scope':'Structural coverage only; not behavior completion or acceptance',
    'inputs':{str(path.relative_to(t)):hashlib.sha256(path.read_bytes()).hexdigest() for path in inputs+[
        t/'features/settings/options.md',t/'features/design-lab/parameter-catalog.md',
        t/'features/installer/platforms.md',t/'features/updates/release-format.md']},
    'matrixHashes':{str(path.relative_to(repo)):hashlib.sha256(path.read_bytes()).hexdigest() for path in matrices}}
a.output.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({k:report[k] for k in ('templateMatchesTarget','normativeCount','packageCounts','missingNormativeRows','extraNormativeRows','optionsCount','missingOptions','extraOptions')},ensure_ascii=False))
if missing or options!=optionrows or represented!=rows.keys() or not report['templateMatchesTarget']:
    raise SystemExit(1)
