from pathlib import Path
import hashlib
r=Path(__file__).parent
exec((r.parent/'death-v2/build.py').read_text().split('hit=read(')[0])
bank=[];stats=[]
def tone(out,hz,start,duration,gain,attack=.008):
 for i in range(int(duration*R)):
  j=int(start*R)+i
  if j>=len(out):break
  t=i/R;env=min(1,t/attack)*min(1,(duration-t)/.055)
  out[j]+=gain*math.sin(2*math.pi*hz*t)*env
for k in range(1,4):
 x=[0.]*int(R*.9)
 if k==1:
  # Two clean diagnostic beeps followed by a low confirmation pulse.
  tone(x,740,0,.115,.24);tone(x,990,.18,.16,.22);tone(x,180,.39,.14,.11,.012)
 elif k==2:
  # Warm consonant restoration motif with bell-like envelopes, no air/noise layer.
  for hz,start in [(262,0),(330,.10),(392,.20),(524,.3)]:
   for i in range(int(.52*R)):
    j=int(start*R)+i
    if j<len(x):
     t=i/R;e=(1-math.exp(-t*100))*math.exp(-t*8)*min(1,(.52-t)/.07)
     x[j]+=.19*(math.sin(2*math.pi*hz*t)+.16*math.sin(2*math.pi*hz*2*t))*e
 else:
  # Dry mechanical applicator, plunger and release. No pneumatic hiss.
  soft=norm(low(read(src/'impactSoft_heavy_000.ogg'),850));click=norm(low(read(src/'impactPlate_light_001.ogg'),1600))
  add(x,click,.14,0,.055);add(x,soft,.32,.08,.12);add(x,soft,.18,.2,.09);add(x,click,.09,.34,.045)
  tone(x,380,.43,.105,.09)
 peak=max(map(abs,x));x=[v*.5/peak for v in x];p=r/f'heal-{k}.wav';bank.append(write(p,x));stats.append({'variant':k,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'duration':.9,'peak':max(map(abs,x))})
page=r.parent/'bonus-review/index.html';old=page.read_text();banks=json.loads(old.split('const bank=',1)[1].split(';\nconst types=',1)[0]);banks['heal']=bank
s=(r.parent/'bonus-review/template.html').read_text()
s=s.replace("'Инъектор: срабатывание, впрыск и завершение.'","'Новые направления без шипения: прибор, восстановление, механический инъектор.'")
s=s.replace("[['Пневмоинъектор','Короткий щелчок и сжатый воздух.'],['Мягкий впрыск','Более длинный, приглушённый впрыск.'],['Автоинъектор','Быстрое срабатывание и возврат механизма.']]","[['Медицинский прибор','Два чистых сигнала и низкое подтверждение процедуры.'],['Восстановление','Тёплый мягкий мотив из четырёх нот.'],['Механический инъектор','Сухой щелчок, ход поршня и короткое подтверждение.']]")
s=s.replace('st-bonus-choices-v1','st-bonus-choices-v1')
s=s.replace("saved.shield=2;","saved.shield=2; delete saved.heal;")
# Preserve new healing choice across reloads separately from rejected first batch.
s=s.replace('delete saved.heal;',"delete saved.heal; try{const h=localStorage.getItem('st-heal-v2');if(h)saved.heal=+h}catch{}")
s=s.replace("saved[b.dataset.pick]=+b.dataset.n;localStorage", "saved[b.dataset.pick]=+b.dataset.n;if(b.dataset.pick==='heal')localStorage.setItem('st-heal-v2',b.dataset.n);localStorage")
s=s.replace('Все бонусы в одной прослушке','Лечение: три новых направления')
s=s.replace('Для каждого типа — три варианта. Слушайте в любом порядке и отмечайте понравившиеся. Выбор сохраняется в этом браузере; затем отправьте номера в чат.','Лечение полностью переделано: без шипения, три разных подхода. Выбранные оружие 2, урон 3, скорость 1 и щит 2 сохранены.')
# Show healing first, preserving existing IDs and other choices.
s=s.replace('const root=document',"types.unshift(types.splice(types.findIndex(t=>t[0]==='heal'),1)[0]);const root=document")
(r/'template.html').write_text(s);page.write_text(s.replace('BANK_DATA',json.dumps(banks)))
(r/'manifest.json').write_text(json.dumps({'event':'healing-pickup','status':'new alternatives, not selected, runtime unchanged','sources':'1/2 original synthesized tones; 3 Kenney Impact Sounds CC0 with filtering, trim, gain, plus synthesized tone','clips':stats},indent=2))
root=r.parent;p=root/'approved-selections.json';d=json.loads(p.read_text())
for kind,n,name,path in [('weapon',2,'Тяжёлый механизм','weapon-pickup/pickup-2.wav'),('damage',3,'Пульс силы','bonus-review/damage-3.wav'),('speed',1,'Светлый разгон','bonus-review/speed-1.wav')]:
 f=root/path;d[kind+'-pickup']={'event':kind+'-pickup','selected_variant':n,'name':name,'status':'selected-preview; game integration pending','selection_evidence':'User confirmed all except healing; browser summary: Оружие 2 · Урон 3 · Скорость 1 · Щит 2','path':str(f),'sha256':hashlib.sha256(f.read_bytes()).hexdigest()}
p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n');(r/'other-bonus-selections.json').write_text(json.dumps({k:v for k,v in d.items() if k in ['weapon-pickup','damage-pickup','speed-pickup','shield-pickup']},ensure_ascii=False,indent=2))
print('Saved weapon 2, damage 3, speed 1. Built three hiss-free healing alternatives; updated shared page.')
