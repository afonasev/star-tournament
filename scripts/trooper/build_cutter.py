"""Original industrial Cutter direction 01; meters, +Y up, +Z muzzle, grip pivot.
Rigid GLB LODs, no collision proxies. Lighting uses standard metallic PBR.
"""
# Reuse only the geometric/export helpers, without executing the Pulse generator.
from pathlib import Path
exec(Path(__file__).with_name('build_pulse.py').read_text().split('def make(lod):')[0])
for lod in (0,1):
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 yellow=mat('cutter industrial yellow',(.88,.47,.035),.6,.4)
 dark=mat('cutter graphite',(.035,.043,.048),.75)
 copper=mat('cutter copper conductors',(.62,.25,.07),.85)
 steel=mat('cutter steel',(.35,.39,.4),.85)
 box('yellow receiver',(0,.08,.12),(.16,.18,.4),yellow)
 box('lower graphite chassis',(0,-.025,.10),(.13,.07,.39),dark)
 box('upper rail',(0,.187,.09),(.075,.035,.42),dark)
 box('stock back',(0,.035,-.27),(.095,.20,.07),yellow)
 box('stock top',(0,.12,-.18),(.08,.055,.22),dark)
 box('stock bottom',(0,-.067,-.16),(.065,.025,.23),yellow)
 box('main grip',(0,-.12,0),(.055,.18,.07),dark)
 box('support grip',(0,-.12,.29),(.055,.18,.065),dark)
 box('trigger guard',(0,-.10,.08),(.065,.025,.10),yellow)
 for z in (.32,.43):tube('power collar',z,.035,.13,.07,yellow,32 if lod==0 else 16)
 tube('massive muzzle',.51,.20,.115,.06,dark,32 if lod==0 else 16)
 tube('conductive muzzle ring',.615,.016,.087,.059,copper,32 if lod==0 else 16)
 for side in (-1,1):
  for y in (.01,.15):
   box('copper conductor',(side*.113,y,.36),(.027,.027,.20),copper)
   box('muzzle jaw',(side*.092,y,.58),(.047,.045,.22),dark)
  box('receiver armor',(side*.086,.085,.105),(.018,.115,.27),yellow)
  if lod==0:
   for z in (-.015,.08,.19):
    box('recess vent',(side*.098,.04,z),(.006,.023,.027),dark)
   for z in (-.025,.24):box('steel bolt',(side*.10,.128,z),(.012,.019,.019),steel)
 groups={}
 for o in list(bpy.context.scene.objects):
  if o.type=='MESH':groups.setdefault(o.data.materials[0].name,[]).append(o)
 for name,objects in groups.items():
  bpy.ops.object.select_all(action='DESELECT')
  for o in objects:o.select_set(True)
  bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();objects[0].name=name
 for name,pos in [('cutter-grip',(0,-.09,0)),('cutter-muzzle',(0,.08,.69)),('cutter-support-grip',(0,-.1,.29))]:
  bpy.ops.object.empty_add(location=pos);bpy.context.object.name=name
 axis=Matrix.Rotation(math.pi/2,4,'X')
 for o in bpy.context.scene.objects:o.matrix_world=axis@o.matrix_world
 path=ART/f'cutter-lod{lod}.glb';bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_apply=True);preserve_pbr(path)
manifest=ART/'asset-manifest.json';doc=json.loads(manifest.read_text());doc['entries']=[e for e in doc['entries'] if not e['key'].startswith('cutter-')]
for lod in (0,1):
 path=ART/f'cutter-lod{lod}.glb';doc['entries'].append(dict(key=f'cutter-lod{lod}',path='Assets/StarTournament/Art/'+path.name,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),units='meters',up='+Y',forward='+Z',pivot='cutter-grip',lod=lod,source='Original industrial Cutter direction 01; scripts/trooper/build_cutter.py; renderer-only, no collision proxy.'))
manifest.write_text(json.dumps(doc,ensure_ascii=False,indent=2)+'\n')
