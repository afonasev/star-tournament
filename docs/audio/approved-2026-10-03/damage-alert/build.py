from pathlib import Path
import hashlib
r=Path(__file__).parent
exec((r.parent/'death-v2/build.py').read_text().split('hit=read(')[0])
root=r.parent
p=root/'approved-selections.json';d=json.loads(p.read_text())
choices=[('spawn',3,'damage-bonus-spawn','Тревожный пульс'),('jump',1,'jump','Лёгкий толчок'),('land',1,'land','Мягкий удар'),('move',3,'menu-move','Лёгкий тональный'),('confirm',3,'menu-confirm','Светлое подтверждение'),('back',3,'menu-back','Мягкий спад')]
for key,n,event,name in choices:
 f=root/f'remaining/{key}-{n}.wav';s={'event':event,'selected_variant':n,'preview_version':'remaining','name':name,'status':'selected-preview; game integration pending','selection_evidence':'User confirmed selected; browser summary read on this turn','path':str(f),'sha256':hashlib.sha256(f.read_bytes()).hexdigest()};d[event]=s
p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
(root/'remaining/selection.json').write_text(json.dumps({event:d[event] for _,_,event,_ in choices},ensure_ascii=False,indent=2)+'\n')
p=root/'audio-requirements.json';req=json.loads(p.read_text());req['other_player_damage_pickup']={'requirement':'Separate threatening signal to other players when someone picks up damage power-up; distinct from collector sound and bonus spawn','collector_sound':'bonus-review damage 3 selected','status':'required; new preview variants pending; not integrated','mix_policy':'listener routing and shared local audio mix pending implementation'};p.write_text(json.dumps(req,ensure_ascii=False,indent=2)+'\n')
bank=['data:audio/wav;base64,'+base64.b64encode((root/'bonus-review/damage-3.wav').read_bytes()).decode()];stats=[]
for k in range(1,4):
 D={1:1.15,2:.95,3:1.1}[k];x=[];phase=0
 for i in range(int(D*R)):
  t=i/R
  if k==1:
   f=105-37*min(1,t/.8);phase+=2*math.pi*f/R
   env=(1-math.exp(-t*75))*math.exp(-t*2.8)*min(1,(D-t)/.12)
   v=math.tanh(1.8*(math.sin(phase)+.36*math.sin(phase*1.48)+.2*math.sin(phase*3)))*env
  elif k==2:
   f=220-80*min(1,t/.45);phase+=2*math.pi*f/R
   env=(1-math.exp(-t*170))*math.exp(-t*4.5)*min(1,(D-t)/.1)
   v=(.5*math.sin(phase)+.33*math.sin(phase*1.059)+.26*math.sin(phase*.5))*env
  else:
   v=0
   for at,f in [(0,125),(.20,118),(.40,105),(.62,75)]:
    u=t-at
    if u>=0:v+=(math.sin(2*math.pi*f*u)+.35*math.sin(2*math.pi*f*1.414*u))*(1-math.exp(-u*150))*math.exp(-u*(17 if at<.6 else 7))
   v*=min(1,(D-t)/.1)
  x.append(v)
 peak=max(map(abs,x));x=[v*.58/peak for v in x];p=r/f'alert-{k}.wav';bank.append(write(p,x));stats.append({'variant':k,'duration':D,'peak':max(map(abs,x)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
p=root/'remaining/index.html';banks=json.loads(p.read_text().split('const bank=',1)[1].split(';\nconst types=',1)[0]);banks['alert']=bank
s=(root/'remaining/template.html').read_text()
s=s.replace("const types=[", "const types=[['alert','Чужое усиление','Другой игрок взял усиление урона — сигнал опасности для остальных.',['Низкий рёв','Тревожный аккорд','Обратный отсчёт']],")
s=s.replace("['Текущий',...names]","[id==='alert'?'Свой подбор — сравнение':'Текущий',...names]")
s=s.replace('Оставшиеся звуки — всё сразу','Кто-то взял усиление урона')
s=s.replace('Шесть событий, для каждого 0 — текущий звук и три новых варианта. Отметки сохраняются в браузере. Шаги не меняем.','Новый отдельный сигнал для остальных игроков: варианты 1–3. №0 — выбранный звук собственного подбора для сравнения. Остальные решения сохранены ниже.')
s=s.replace("else if(id==='spawn')", "else if(id==='alert'){g.fillStyle='#b9778d';g.fillRect(575,52,40,52);g.beginPath();g.arc(595,33,16,0,Math.PI*2);g.fill();g.fillStyle='#8caec4';g.fillRect(345,52,40,52);g.beginPath();g.arc(365,33,16,0,Math.PI*2);g.fill();g.fillStyle='#c1d4e6';g.fillText('ВЫ',350,132);g.fillText('ДРУГОЙ ИГРОК',535,132);if(active){g.strokeStyle='#ef799e';g.lineWidth=3;g.beginPath();g.ellipse(595,69,43,60,0,0,Math.PI*2);g.stroke();g.fillStyle='#f7a5b7';g.font='bold 26px system-ui';g.fillText('!',440,75);g.font='16px system-ui';g.fillText('УРОН УСИЛЕН',685,105)}}else if(id==='spawn')")
(r/'template.html').write_text(s);p.write_text(s.replace('BANK_DATA',json.dumps(banks)))
(r/'manifest.json').write_text(json.dumps({'event':'damage-powerup-other-player-alert','status':'preview only, not selected or integrated','source':'original synthesized harmonic signals','reference_0':'../bonus-review/damage-3.wav, selected collector sound','clips':stats},indent=2))
print('Saved six selections. Added separate other-player alert with three candidates; reference 0 is collector sound.')
