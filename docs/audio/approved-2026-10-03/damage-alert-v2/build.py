from pathlib import Path
import hashlib
r=Path(__file__).parent
exec((r.parent/'death-v2/build.py').read_text().split('hit=read(')[0])
root=r.parent;basepath=root/'remaining/spawn-3.wav';base=read(basepath)
def layer(x,f,at,d,g,decay=9,attack=.003):
 for i in range(int(R*d)):
  j=int(at*R)+i
  if j<len(x):
   t=i/R;x[j]+=g*math.sin(2*math.pi*f*t)*min(1,t/attack)*math.exp(-t*decay)*min(1,(d-t)/.035)
bank=['data:audio/wav;base64,'+base64.b64encode(basepath.read_bytes()).decode()];stats=[]
for k in range(1,4):
 x=list(base)
 for at in [0,.16,.32]:
  if k==1:
   layer(x,560,at,.085,.16,17);layer(x,840,at,.055,.055,23)
  elif k==2:
   layer(x,560,at,.15,.19,9);layer(x,796,at,.13,.075,12)
  else:
   layer(x,280,at,.12,.10,14);layer(x,560,at,.08,.09,18)
 if k==1:layer(x,560,.51,.25,.10,10)
 elif k==2:
  layer(x,560,.51,.34,.20,8);layer(x,796,.51,.25,.08,10)
 else:
  layer(x,560,.51,.4,.29,7);layer(x,840,.51,.27,.16,11);layer(x,140,.51,.3,.17,9)
 # Preserve the recognizable original rhythm and low layer; same target RMS as reference.
 rms=lambda a:(sum(v*v for v in a)/len(a))**.5
 gain=min(rms(base)/rms(x),.8/max(map(abs,x)));x=[v*gain for v in x]
 p=r/f'alert-{k}.wav';bank.append(write(p,x));stats.append({'variant':k,'duration':len(x)/R,'peak':max(map(abs,x)),'rms':rms(x),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
p=root/'remaining/index.html';banks=json.loads(p.read_text().split('const bank=',1)[1].split(';\nconst types=',1)[0]);banks['alert']=bank
s=(root/'damage-alert/template.html').read_text()
s=s.replace("['Низкий рёв','Тревожный аккорд','Обратный отсчёт']","['Резкая атака','Яркий тревожный пульс','Финальный акцент']")
s=s.replace('Другой игрок взял усиление урона — сигнал опасности для остальных.','Тот же мотив появления бонуса, усиленный до сигнала опасности.')
s=s.replace('Свой подбор — сравнение','Появление — исходный пульс')
s=s.replace('Новый отдельный сигнал для остальных игроков: варианты 1–3. №0 — выбранный звук собственного подбора для сравнения. Остальные решения сохранены ниже.','№0 — выбранный «Тревожный пульс» появления бонуса. Варианты 1–3 сохраняют его ритм и делают чужой подбор заметнее. Кнопки «Появление → алерт» дают сравнение в паре.')
s=s.replace('}catch{};',"}catch{};delete saved.alert;try{const v=localStorage.getItem('st-damage-alert-v2');if(v)saved.alert=+v}catch{};")
s=s.replace('saved[b.dataset.pick]=+b.dataset.n;localStorage',"saved[b.dataset.pick]=+b.dataset.n;if(b.dataset.pick==='alert')localStorage.setItem('st-damage-alert-v2',b.dataset.n);localStorage")
s=s.replace('<button data-pick="${id}" data-n="${n}">Выбрать ${n}</button>', '${id===\'alert\'&&n===0?\'\':`<button data-pick="${id}" data-n="${n}">Выбрать ${n}</button>`}${id===\'alert\'&&n>0?`<button data-pair="${n}">Появление → алерт ${n}</button>`:\'\'}')
s=s.replace("document.querySelectorAll('[data-play]').forEach", "document.querySelectorAll('[data-pair]').forEach(b=>b.onclick=()=>play('alert',[0,+b.dataset.pair]));document.querySelectorAll('[data-play]').forEach")
(r/'template.html').write_text(s);p.write_text(s.replace('BANK_DATA',json.dumps(banks)))
(r/'manifest.json').write_text(json.dumps({'event':'damage-powerup-other-player-alert','status':'not selected; preview only','base':str(basepath),'base_sha256':hashlib.sha256(basepath.read_bytes()).hexdigest(),'processing':'Original pulse timing 0/.16/.32/.51 unchanged. Additive upper harmonics/attack/final layers. Match RMS to source for timbre comparison.','clips':stats},indent=2))
p=root/'audio-requirements.json';d=json.loads(p.read_text());d['other_player_damage_pickup']['timbre_direction']='Use selected spawn 3 Тревожный пульс as motif; brighter alert for others when picked up';p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
print(stats)
