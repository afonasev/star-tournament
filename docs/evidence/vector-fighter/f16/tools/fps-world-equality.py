from pathlib import Path
import json
exec(Path('docs/evidence/vector-fighter/f16/tools/compare_glb.py').read_text().split('report={}')[0])
a,ab=read(Path('.local/vector-f16/candidate-arms.glb').read_bytes());b,bb=read(Path('.local/vector-f16/candidate-body.glb').read_bytes())
an=next(n for n in a['nodes'] if n.get('name')=='vector-armored-hands');bn=next(n for n in b['nodes'] if n.get('name')=='VECTOR world forearms')
ap=a['meshes'][an['mesh']]['primitives'];bp=b['meshes'][bn['mesh']]['primitives'];assert len(ap)==len(bp)
assert [a['nodes'][i]['name'] for i in a['skins'][an['skin']]['joints']]==[b['nodes'][i]['name'] for i in b['skins'][bn['skin']]['joints']]
for x,y in zip(ap,bp):
 for k in x['attributes']:assert accessor(a,ab,x['attributes'][k])==accessor(b,bb,y['attributes'][k]),k
 assert accessor(a,ab,x['indices'])==accessor(b,bb,y['indices'])
Path('.local/vector-f16/fps-world-equality.json').write_text(json.dumps({'result':'exact','primitives':len(ap),'scope':'all arm/glove vertex attributes, indices and skin joint names match between FPS and world exports'},indent=2)+'\n')
print('FPS/world arm geometry exactly equal')
