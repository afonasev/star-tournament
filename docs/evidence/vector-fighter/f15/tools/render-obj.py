import bpy,sys,csv
from pathlib import Path
from mathutils import Vector,Matrix
root=Path(sys.argv[sys.argv.index('--')+1]);weapon=sys.argv[sys.argv.index('--')+2]
bpy.ops.wm.read_factory_settings(use_empty=True)
verts=[];parts={};name=''
for line in (root/(weapon+'-actual-mesh.obj')).read_text().splitlines():
 a=line.split()
 if not a:continue
 if a[0]=='o':name=a[1];parts[name]=[]
 elif a[0]=='v':verts.append(tuple(map(float,a[1:])))
 elif a[0]=='f':parts[name].append(tuple(int(i)-1 for i in a[1:]))
for name,faces in parts.items():
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);mat=bpy.data.materials.new(name);mat.diffuse_color=(.45,.5,.55,1) if 'hands' in name else (.13,.12,.10,1);mesh.materials.append(mat)
 for p in mesh.polygons:p.use_smooth='hands' in name
axes={a['name']:a for a in csv.DictReader((root/(weapon+'-axes.csv')).open())}
scene=bpy.context.scene;scene.render.engine='BLENDER_WORKBENCH';scene.display.shading.light='STUDIO';scene.display.shading.studio_light='paint.sl';scene.display.shading.color_type='MATERIAL';scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True;scene.display.shading.cavity_type='BOTH';scene.display.shading.background_type='WORLD';scene.world=bpy.data.worlds.new('Diagnostic');scene.world.color=(.08,.08,.08);scene.render.resolution_x=1100;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG'
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=.78
for side in ['Right','Left']:
 a=axes[side+'MiddleProximal'];center=Vector([float(a[k]) for k in ['px','py','pz']]);center.y-=.03
 for sign in [-1,1]:
  cam.location=center+Vector((sign*.8,.1,-.45));forward=(center-cam.location).normalized();right=forward.cross(Vector((0,1,0))).normalized();up=right.cross(forward);cam.rotation_euler=Matrix((right,up,-forward)).transposed().to_euler();scene.render.filepath=str(root/(weapon+'-'+side+'-clay-'+str(sign)+'.png'));bpy.ops.render.render(write_still=True)
