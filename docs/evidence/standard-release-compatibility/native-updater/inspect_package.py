"""Reproduce read-only released package/updater identity without executing payload."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import zipfile
sys.path.insert(0,str(Path(__file__).resolve().parents[4]/'distribution'))
import publish_release as publisher

root=Path(__file__).resolve().parents[4]
channel=sys.argv[1]
version={'production':'0.1.5','test':'0.1.5-test.1'}[channel]
metadata=root/'docs/evidence/standard-release-compatibility/transition'/channel/'latest-win-x64.json'
data,asset=publisher.verify_manifest(metadata,version,'win-x64',channel,schema=1)
local=root/'.local/legacy-native-updater-01a13fa0'/channel
local.mkdir(parents=True,exist_ok=True)
package=local/asset['FileName']
if not package.exists():
    subprocess.run(['gh','release','download','v'+version,'--repo',publisher.REPO,'--pattern',asset['FileName'],'--dir',str(local)],check=True)
with package.open('rb') as stream:digest=hashlib.file_digest(stream,'sha256').hexdigest()
if (package.stat().st_size,digest)!=(asset['Size'],asset['SHA256'].lower()):raise ValueError('Actual published full package differs from signed identity')
entries=[]
with zipfile.ZipFile(package) as archive:
    for info in archive.infolist():
        if Path(info.filename).name.lower() in ('update.exe','squirrel.exe') or info.filename.endswith('.nuspec'):
            if info.file_size>32*1024*1024:raise ValueError('Unexpected native metadata size')
            raw=archive.read(info)
            path=local/Path(info.filename).name
            if path.exists() and path.read_bytes()!=raw:raise ValueError('Output basename collision')
            path.write_bytes(raw)
            entries.append(dict(path=info.filename,size=len(raw),sha256=hashlib.sha256(raw).hexdigest()))
result=dict(channel=channel,version=version,package=dict(name=asset['FileName'],size=package.stat().st_size,sha256=digest),entries=entries,executed=False)
output=Path(__file__).parent/(channel+'.json');output.write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(result))
