from pathlib import Path
import hashlib,json
r=Path(__file__).parent
exec((r/'build.py').read_text().split('hit=read(')[0])
x=[0.] * int(1.3*R)
soft=norm(read(src/'impactSoft_heavy_000.ogg'));soft2=norm(read(src/'impactSoft_heavy_001.ogg'))
cloth=norm(read(src/'footstep_carpet_000.ogg'));cloth2=norm(read(src/'footstep_carpet_001.ogg'))
fall=norm(low(low(pitch(soft,.72),1450),1450));settle=norm(low(pitch(soft2,.85),1100))
add(x,low(cloth,2100),.10,.27,.22)
add(x,fall,.58,.48,.37);add(x,settle,.27,.565,.24);add(x,low(cloth2,1800),.10,.64,.25)
assert 0 < max(map(abs,x)) < 1
write(r/'death-fall-only.wav',x)
selection={'event':'death','preview_version':'death-v2','status':'selected-preview; game integration pending','selected_variants':[2,3,'fall-only'],'playback':'Randomly select one of the three variants per death. Random selection is a runtime requirement, not yet integrated.','weights':'unspecified by user','contact_seconds':.48,'fall':'identical soft heavy suit fall in all three, no metal','user_feedback':'2 и 3 можно взять, выбирать случайный из них плюс можно смерть без голоса еще добавить просто мягкое падение для разнообразия','files':[{'path':str(r/name),'sha256':hashlib.sha256((r/name).read_bytes()).hexdigest()} for name in ['death-2.wav','death-3.wav','death-fall-only.wav']]}
(r/'selection.json').write_text(json.dumps(selection,ensure_ascii=False,indent=2)+'\n')
p=r.parent/'approved-selections.json';d=json.loads(p.read_text());d['death']=selection;p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'selected':selection['selected_variants'],'fall_peak':max(map(abs,x)),'duration':len(x)/R,'saved':str(r/'selection.json')},ensure_ascii=False))
