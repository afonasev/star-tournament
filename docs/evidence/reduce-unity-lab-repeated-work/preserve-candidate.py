from pathlib import Path
import subprocess,json,hashlib,shutil,re,plistlib
root=Path.cwd();sha=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip();app=root/'unity/Builds/StarTournamentProvingGround.app';assert app.is_dir()
out=root/'docs/evidence/reduce-unity-lab-repeated-work';entries=[]
for p in sorted(app.rglob('*')):
 if p.is_file():
  h=hashlib.sha256()
  with p.open('rb') as f:
   for b in iter(lambda:f.read(1048576),b''):h.update(b)
  entries.append({'path':str(p.relative_to(app)),'sha256':h.hexdigest(),'size':p.stat().st_size})
marker=re.findall(r'STAR_PROVING_BUILD_PASS (\d+)',(root/'.local/unity-evidence/build.log').read_text())[-1]
manifest={'source_revision':sha,'build_marker':marker,'assets_source_tree':subprocess.check_output(['git','rev-parse','HEAD:unity/Assets'],text=True).strip(),'files':entries}
raw=(json.dumps(manifest,sort_keys=True,indent=2)+'\n').encode();digest=hashlib.sha256(raw).hexdigest();dest=Path('/Users/eaafonasev/Projects/star-tournament/.local/player-candidates/lab-copy-'+sha[:8]);assert not dest.exists();dest.mkdir(parents=True)
# macOS bundles contain executable permissions and possibly symlinks; preserve both.
subprocess.run(['/usr/bin/ditto',str(app),str(dest/app.name)],check=True)
for entry in entries:
 p=dest/app.name/entry['path'];h=hashlib.sha256()
 with p.open('rb') as f:
  for b in iter(lambda:f.read(1048576),b''):h.update(b)
 assert h.hexdigest()==entry['sha256'],entry['path']
(out/'player-manifest.json').write_bytes(raw);(dest/'player-manifest.json').write_bytes(raw)
identity='local-macos-player-candidate/'+sha+'/'+digest[:16]
record={'identity':identity,'source_revision':sha,'app':str(dest/app.name),'manifest_sha256':digest,'build_marker':marker,'copy_checksums_verified':True}
(out/'candidate.json').write_text(json.dumps(record,indent=2)+'\n');print(json.dumps(record))
