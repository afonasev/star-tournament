from pathlib import Path
import shutil,json,hashlib,gzip,xml.etree.ElementTree as E
from PIL import Image,ImageDraw,ImageFont
root=Path('.local/vector-f11');out=Path('docs/evidence/vector-fighter/f11-result');frames=out/'frames';qa=out/'qa';frames.mkdir(exist_ok=True)
for p in (root/'final-native').iterdir():
 if p.suffix in ('.png','.csv'):shutil.copyfile(p,frames/p.name)
for src,name in [('final-native/results.xml','native-results.xml'),('final-edit-results.xml','import-results.xml'),('forms-report.json','forms-report.json'),('glb-verification.json','glb-verification.json'),('wrist-angles.json','wrist-angles.json')]:shutil.copyfile(root/src,qa/name)
for src,name in [('final-native/editor.log','native-editor.log.gz'),('final-edit.log','import-editor.log.gz'),('final-import.log','refresh.log.gz'),('export-v12.log','export.log.gz')]:
 with gzip.open(qa/name,'wb') as f:f.write((root/src).read_bytes())
checks={}
for k,p in [('native',qa/'native-results.xml'),('import',qa/'import-results.xml'),('history_and_initial_import',qa/'history-and-initial-import-results.xml')]:
 r=E.parse(p).getroot();checks[k]={key:r.get(key) for key in ['result','total','passed','failed','skipped','duration']};assert r.get('result')=='Passed'
for k,p in json.loads((qa/'runtime-source-hashes.json').read_text()).items():assert hashlib.sha256(Path(k).read_bytes()).hexdigest()==p
for kind,info in json.loads((qa/'glb-verification.json').read_text()).items():assert hashlib.sha256((Path('unity/Assets/StarTournament/Art')/f'trooper-{kind}-animated.glb').read_bytes()).hexdigest()==info['sha256']
font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',24)
weapons=['Rifle','Shotgun','RocketLauncher','Cutter'];canvas=Image.new('RGB',(1600,1040),(24,28,35));d=ImageDraw.Draw(canvas)
for i,w in enumerate(weapons):
 x=(i%2)*800;y=(i//2)*520
 im=Image.open(frames/(w+'-fps.png')).crop((200,740,1200,1350)).resize((800,488),Image.Resampling.LANCZOS)
 canvas.paste(im,(x,y+32));d.text((x+12,y+3),w+' | F11 actual FPS',font=font,fill='white')
canvas.save(out/'fps-four-weapons.png')
canvas=Image.new('RGB',(1600,520),(24,28,35));d=ImageDraw.Draw(canvas)
for i,(label,path) in enumerate([('F9 | baseline',Path('docs/evidence/vector-fighter/f9/fps-result/frames/Cutter-fps.png')),('F11 | current',frames/'Cutter-fps.png')]):
 im=Image.open(path).crop((200,740,1200,1350)).resize((800,488),Image.Resampling.LANCZOS);canvas.paste(im,(i*800,32));d.text((i*800+12,3),label,font=font,fill='white')
canvas.save(out/'cutter-before-after.png')
report={'baseline':'ba8bf6b7e2e92140d4f9d98cfd05b646295ebe94','checks':checks,'raw_frames':len(list(frames.glob('*.png'))),'capture':'actual Unity seat-camera projection; factory @17; no overrides; collages are identical crops/resizes only','source_sha256':hashlib.sha256(Path('scripts/vector_fighter/f11-fps-forms.blend').read_bytes()).hexdigest(),'wrist_angles_degrees':json.loads((qa/'wrist-angles.json').read_text()),'not_run':['full suite','Player build','integration','release/deploy','hardware/performance/human acceptance'],'retention':'All continuation resources retained by explicit user request.'}
(out/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n');print(json.dumps(checks));print('raw frames',report['raw_frames'])
