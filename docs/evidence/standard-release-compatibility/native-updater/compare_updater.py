"""Static PE comparison against the upstream version; no native execution."""
import hashlib
import json
from pathlib import Path
import re
import struct
import subprocess
import zipfile

root=Path(__file__).resolve().parents[4]
local=root/'.local/legacy-native-updater-01a13fa0'
local.mkdir(parents=True,exist_ok=True)
for name,endpoint in [('upstream-release.json','repos/velopack/velopack/releases/tags/1.2.158'),('upstream-tag.json','repos/velopack/velopack/git/ref/tags/1.2.158')]:
    path=local/name
    if not path.exists():
        result=subprocess.run(['gh','api',endpoint],check=True,capture_output=True)
        json.loads(result.stdout)
        path.write_bytes(result.stdout)
upstream=json.loads((local/'upstream-release.json').read_text())
archive=local/'vpk.1.2.158.nupkg'
asset=next(a for a in upstream['assets'] if a['name']==archive.name)
if not archive.exists():
    subprocess.run(['gh','release','download','1.2.158','--repo','velopack/velopack','--pattern',archive.name,'--dir',str(local)],check=True)
with archive.open('rb') as f:digest=hashlib.file_digest(f,'sha256').hexdigest()
assert (archive.stat().st_size,'sha256:'+digest)==(asset['size'],asset['digest'])
with zipfile.ZipFile(archive) as z:official=z.read('vendor/update_x64.exe')
actual=(local/'production/Squirrel.exe').read_bytes()
assert actual==(local/'test/Squirrel.exe').read_bytes()

def pe(b):
    assert b[:2]==b'MZ'
    start=struct.unpack_from('<I',b,0x3c)[0]
    assert b[start:start+4]==b'PE\0\0' and struct.unpack_from('<H',b,start+4)[0]==0x8664
    count=struct.unpack_from('<H',b,start+6)[0]
    optional=start+24
    assert struct.unpack_from('<H',b,optional)[0]==0x20b
    table=optional+struct.unpack_from('<H',b,start+20)[0]
    sections=[]
    for n in range(count):
        offset=table+40*n;name=b[offset:offset+8].rstrip(b'\0').decode()
        size,raw=struct.unpack_from('<II',b,offset+16)
        assert raw+size<=len(b)
        sections.append(dict(name=name,size=size,offset=raw,header=offset,characteristics=struct.unpack_from('<I',b,offset+36)[0],sha256=hashlib.sha256(b[raw:raw+size]).hexdigest()))
    resource=next(x for x in sections if x['name']=='.rsrc')
    assert resource['characteristics']&0x20000000==0
    contents=b[resource['offset']:resource['offset']+resource['size']]
    fixed=[m.start() for m in re.finditer(re.escape(struct.pack('<I',0xFEEF04BD)),contents)]
    assert len(fixed)==1
    info=struct.unpack_from('<13I',contents,fixed[0])
    def version(ms,ls):return '.'.join(map(str,[ms>>16,ms&65535,ls>>16,ls&65535]))
    return dict(sections=sections,resource=resource,resourceDirectorySizeOffset=optional+112+2*8+4,
                fixedVersion=dict(file=version(info[2],info[3]),product=version(info[4],info[5])))

actual_pe,official_pe=pe(actual),pe(official)
assert len(actual)==len(official)
for a,b in zip(actual_pe['sections'],official_pe['sections']):
    assert (a['name'],a['offset'],a['size'],a['characteristics'])==(b['name'],b['offset'],b['size'],b['characteristics'])
    if a['name']!='.rsrc':assert a['sha256']==b['sha256']
r=actual_pe['resource']
allowed=set(range(actual_pe['resourceDirectorySizeOffset'],actual_pe['resourceDirectorySizeOffset']+4))|set(range(r['header']+8,r['header']+12))
outside=[dict(offset=i,actual=a,upstream=b) for i,(a,b) in enumerate(zip(actual,official)) if a!=b and not r['offset']<=i<r['offset']+r['size']]
assert all(x['offset'] in allowed for x in outside)
assert actual_pe['fixedVersion']==official_pe['fixedVersion']==dict(file='1.2.158.0',product='1.2.158.0')
tag=json.loads((local/'upstream-tag.json').read_text())
assert tag['object']==dict(sha='3c7f52c1bf17d10ad21b794b006d5ebd1a879a3b',type='commit',url='https://api.github.com/repos/velopack/velopack/git/commits/3c7f52c1bf17d10ad21b794b006d5ebd1a879a3b')
result=dict(version='1.2.158',sourceCommit=tag['object']['sha'],upstreamArchive=dict(name=archive.name,size=archive.stat().st_size,sha256=digest),
 upstreamEntry='vendor/update_x64.exe',upstreamSha256=hashlib.sha256(official).hexdigest(),actualSha256=hashlib.sha256(actual).hexdigest(),
 productionAndTestExactByteMatch=True,upstreamWholeFileExactMatch=actual==official,
 allNonResourceSectionsExactMatch=True,outsideResourceDifferences=outside,actual=actual_pe,upstream=official_pe,executed=False)
out=Path(__file__).parent
(out/'upstream-match.json').write_text(json.dumps(result,indent=2)+'\n')
print('Actual production/test updater identical. All non-resource sections match upstream1.2.158; whole files differ in resources and their header sizes. Static only.')
