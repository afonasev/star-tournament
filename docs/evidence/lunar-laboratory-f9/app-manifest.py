#!/usr/bin/env python3
"""Byte identity: sorted regular-file hashes/sizes and symlink targets; no xattrs."""
import argparse,hashlib,json
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('app',type=Path);p.add_argument('--revision',required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();app=a.app.resolve();records=[]
for f in sorted(app.rglob('*'),key=lambda f:f.relative_to(app).as_posix()):
 path=f.relative_to(app).as_posix()
 if f.is_symlink():records.append({'path':path,'kind':'symlink','target':str(f.readlink())})
 elif f.is_file():
  h=hashlib.sha256()
  with f.open('rb') as stream:
   for block in iter(lambda:stream.read(1024*1024),b''):h.update(block)
  records.append({'path':path,'kind':'file','sha256':h.hexdigest(),'size':f.stat().st_size})
assert records and any(r['path']=='Contents/Info.plist' for r in records)
h=hashlib.sha256(json.dumps(records,sort_keys=True,separators=(',',':')).encode()).hexdigest();data={'source_revision':a.revision,'path':str(app),'tree_sha256':h,'recipe':'sha256(canonical JSON sorted records; keys sorted, separators comma/colon)','files':records};a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(data,indent=2)+'\n');print(h)
