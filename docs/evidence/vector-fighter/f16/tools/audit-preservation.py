import bpy,json,hashlib,struct
from pathlib import Path
root=Path.cwd();out=root/'.local/vector-f16'
bpy.ops.wm.open_mainfile(filepath=str(root/'scripts/vector_fighter/f16-orbital-hands.blend'))
def signature(o):
 h=hashlib.sha256()
 for row in o.matrix_world:
  h.update(struct.pack('<4f',*row))
 for v in o.data.vertices:
  h.update(struct.pack('<3f',*v.co))
  for g in v.groups:h.update(o.vertex_groups[g.group].name.encode()+struct.pack('<f',g.weight))
 for p in o.data.polygons:
  h.update(struct.pack('<'+'I'*len(p.vertices),*p.vertices));h.update(struct.pack('<I',p.material_index))
 for uv in o.data.uv_layers:
  for d in uv.data:h.update(struct.pack('<2f',*d.uv))
 return h.hexdigest()
bpy.context.view_layer.update()
current={o.name:signature(o) for o in bpy.context.scene.objects if o.type=='MESH' and 'gloves' not in o.name}
bpy.ops.wm.open_mainfile(filepath=str(out/'vector-source.blend'))
bpy.context.view_layer.update()
checked=[]
for o in bpy.context.scene.objects:
 if o.name not in current:continue
 assert signature(o)==current[o.name],o.name
 checked.append(o.name)
assert len(checked)==len(current)
(out/'suit-preservation.json').write_text(json.dumps({'unchanged_mesh_objects':checked,'compared':'world matrix, vertex positions, skin bone names/weights, polygon indices/material slots, all UV layers','result':'exact'},indent=2)+'\n')
print('F16_SUIT_PRESERVATION',len(checked))
