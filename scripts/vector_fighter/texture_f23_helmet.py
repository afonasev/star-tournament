"""Unique helmet UV atlas; bake coating, tangent relief, roughness and local AO.

Only newly authored helmet paint/steel use this atlas. Existing body/FPS maps
are not edited. Procedural master is saved separately for reproducible rebakes.
"""
import bpy,sys,json,math
import numpy as np
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).parent))
from reconstruct_f23_helmet import TARGET,OUT,activate

SIZE=2048


def master(name,paint):
    m=bpy.data.materials.new(name);m.use_nodes=True
    n=m.node_tree.nodes;l=m.node_tree.links
    bs=next(x for x in n if x.type=='BSDF_PRINCIPLED');bs.name='Principled BSDF'
    def noise(scale):
        x=n.new('ShaderNodeTexNoise');x.inputs['Scale'].default_value=scale
        x.inputs['Detail'].default_value=3;x.inputs['Roughness'].default_value=.7
        geo=n.get('surface position')
        if geo is None:geo=n.new('ShaderNodeNewGeometry');geo.name='surface position'
        l.new(geo.outputs['Position'],x.inputs['Vector'])
        return x.outputs['Fac']
    def ramp(src,lo,hi):
        x=n.new('ShaderNodeValToRGB');x.color_ramp.elements[0].color=(*([lo]*3),1)
        x.color_ramp.elements[1].color=(*([hi]*3),1)
        l.new(src,x.inputs['Fac']);return x.outputs['Color']
    def mathnode(op,a,b):
        x=n.new('ShaderNodeMath');x.operation=op
        for i,v in enumerate([a,b]):
            if isinstance(v,(float,int)):x.inputs[i].default_value=v
            else:l.new(v,x.inputs[i])
        return x.outputs[0]
    broad=noise(65);grain=noise(2900)
    vor=n.new('ShaderNodeTexVoronoi');vor.feature='DISTANCE_TO_EDGE';vor.inputs['Scale'].default_value=180
    l.new(n['surface position'].outputs['Position'],vor.inputs['Vector'])
    scratch=mathnode('LESS_THAN',vor.outputs['Distance'],.010)
    # Sparse shallow coating fractures; broad pigment is correlated across
    # all channels. These are surface detail, not a substitute for geometry.
    scratch=mathnode('MULTIPLY',scratch,mathnode('GREATER_THAN',noise(180),.56))
    # Preserve the actual coating sample's pores/scuffs instead of compressing
    # its luminance into an almost flat map as the previous pipeline did.
    sample=n.new('ShaderNodeTexImage')
    sample.image=bpy.data.images.load(str(ROOT/'scripts/vector_fighter/textures/reference-coating-albedo.png'),check_existing=True)
    sample.projection='BOX';sample.projection_blend=.18
    scale=n.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs['Scale'].default_value=4
    l.new(n['surface position'].outputs['Position'],scale.inputs[0]);l.new(scale.outputs[0],sample.inputs['Vector'])
    gray=n.new('ShaderNodeRGBToBW');l.new(sample.outputs['Color'],gray.inputs[0])
    grain_sample=mathnode('MULTIPLY',mathnode('SUBTRACT',gray.outputs[0],.035),10)
    pigment=ramp(grain_sample,.112 if paint else .035,.162 if paint else .10)
    wear=mathnode('MULTIPLY',mathnode('MAXIMUM',mathnode('SUBTRACT',n['surface position'].outputs['Pointiness'],.5),0),12)
    color=n.new('ShaderNodeMixRGB');color.blend_type='ADD';color.inputs[0].default_value=1
    l.new(pigment,color.inputs[1]);l.new(mathnode('ADD',mathnode('MULTIPLY',scratch,.012 if paint else .025),mathnode('MULTIPLY',wear,.035 if paint else .06)),color.inputs[2])
    l.new(color.outputs[0],bs.inputs['Base Color'])
    rough=ramp(grain_sample,.48,.77);l.new(rough,bs.inputs['Roughness'])
    bs.inputs['Metallic'].default_value=.12 if paint else .25
    height=mathnode('SUBTRACT',mathnode('ADD',mathnode('MULTIPLY',grain,.15),grain_sample),mathnode('MULTIPLY',scratch,.9))
    bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.65;bump.inputs['Distance'].default_value=.00065
    l.new(height,bump.inputs['Height']);l.new(bump.outputs['Normal'],bs.inputs['Normal'])
    return m,rough


def main():
    bpy.ops.wm.open_mainfile(filepath=str(TARGET))
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');rig.data.pose_position='REST'
    oldpaint=bpy.data.materials['vector-fighter-identity-reference'];oldsteel=bpy.data.materials['vector-reference-coated-steel']
    paint,pr=master('vector-fighter-identity-f23-helmet',True)
    steel,sr=master('vector-f23-helmet-coated-steel',False)
    selected=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith('VECTOR F23R') and any(m in (oldpaint,oldsteel) for m in o.data.materials)]
    # Aggregate only for atlas/bake; transfer per-loop UVs back to editable parts.
    vertices=[];faces=[];slots=[];smoothing=[];offsets=[]
    for o in selected:
        offsets.append((o,len(faces)))
        start=len(vertices);vertices.extend(o.matrix_world@v.co for v in o.data.vertices)
        for p in o.data.polygons:
            faces.append(tuple(start+i for i in p.vertices))
            slots.append(0 if o.data.materials[p.material_index]==oldpaint else 1)
            smoothing.append(p.use_smooth)
    data=bpy.data.meshes.new('F23 unique atlas');data.from_pydata(vertices,[],faces);data.update()
    target=bpy.data.objects.new('F23 atlas bake target',data);bpy.context.collection.objects.link(target)
    data.materials.append(paint);data.materials.append(steel)
    for p,slot,smooth in zip(data.polygons,slots,smoothing):p.material_index=slot;p.use_smooth=smooth
    activate(target);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(55),island_margin=.008,area_weight=.6,correct_aspect=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    for o,offset in offsets:
        for p,src in zip(o.data.polygons,data.polygons[offset:offset+len(o.data.polygons)]):
            for li,si in zip(p.loop_indices,src.loop_indices):o.data.uv_layers.active.data[li].uv=data.uv_layers.active.data[si].uv
        for i,m in enumerate(o.data.materials):
            if m==oldpaint:o.data.materials[i]=paint
            elif m==oldsteel:o.data.materials[i]=steel
        o.hide_render=True
    # Bake target sees local shell/visor/gaskets, not distant costume surfaces.
    hidden=[]
    for o in bpy.context.scene.objects:
        if o.type=='MESH' and o!=target and not (len(o.vertex_groups)==1 and o.vertex_groups[0].name=='Head'):
            hidden.append(o);o.hide_render=True
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16
    scene.render.bake.margin=12;scene.render.bake.use_clear=True
    maps={}
    def bake(key,kind,color=False):
        im=bpy.data.images.new('F23-helmet-'+key,width=SIZE,height=SIZE,alpha=False)
        im.colorspace_settings.name='sRGB' if color else 'Non-Color'
        for mat in (paint,steel):
            node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=im
            mat.node_tree.nodes.active=node
        activate(target)
        bpy.ops.object.bake(type=kind,pass_filter={'COLOR'} if kind=='DIFFUSE' else set())
        im.pack();maps[key]=im
        return im
    bake('albedo','DIFFUSE',True)
    bake('normal','NORMAL')
    bake('AO','AO')
    # Emission bake stores roughness without lighting or color management.
    links=[]
    for mat,rough in [(paint,pr),(steel,sr)]:
        nodes=mat.node_tree.nodes;out=next(n for n in nodes if n.type=='OUTPUT_MATERIAL')
        links.append((mat,out,out.inputs['Surface'].links[0].from_socket))
        emission=nodes.new('ShaderNodeEmission');mat.node_tree.links.new(rough,emission.inputs['Color'])
        mat.node_tree.links.new(emission.outputs[0],out.inputs['Surface'])
    bake('roughness','EMIT')
    for mat,out,socket in links:mat.node_tree.links.new(socket,out.inputs['Surface'])
    # Save the editable procedural recipe in a compact helmet-only master.
    bpy.data.libraries.write(str(OUT/'helmet-procedural-master.blend'),{target},compress=True)
    ao=np.empty(SIZE*SIZE*4,dtype=np.float32);maps['AO'].pixels.foreach_get(ao)
    rough=np.empty_like(ao);maps['roughness'].pixels.foreach_get(rough)
    orm=np.ones((SIZE*SIZE,4),dtype=np.float32);orm[:,0]=ao.reshape(-1,4)[:,0]
    orm[:,1]=rough.reshape(-1,4)[:,0];orm[:,2]=.18
    packed=bpy.data.images.new('F23-helmet-ORM',width=SIZE,height=SIZE,alpha=False)
    packed.colorspace_settings.name='Non-Color';packed.pixels.foreach_set(orm.ravel());packed.pack()
    for mat in (paint,steel):
        nodes=mat.node_tree.nodes;nodes.clear();l=mat.node_tree.links
        out=nodes.new('ShaderNodeOutputMaterial');bs=nodes.new('ShaderNodeBsdfPrincipled');bs.name='Principled BSDF'
        l.new(bs.outputs[0],out.inputs['Surface'])
        def texture(im):
            node=nodes.new('ShaderNodeTexImage');node.image=im;return node.outputs['Color']
        l.new(texture(maps['albedo']),bs.inputs['Base Color'])
        normal=nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.23
        l.new(texture(maps['normal']),normal.inputs['Color']);l.new(normal.outputs[0],bs.inputs['Normal'])
        sep=nodes.new('ShaderNodeSeparateColor');l.new(texture(packed),sep.inputs['Color'])
        l.new(sep.outputs['Green'],bs.inputs['Roughness']);l.new(sep.outputs['Blue'],bs.inputs['Metallic'])
        group=bpy.data.node_groups.get('glTF Material Output')
        if group is None:
            group=bpy.data.node_groups.new('glTF Material Output','ShaderNodeTree')
            group.interface.new_socket(name='Occlusion',in_out='INPUT',socket_type='NodeSocketFloat')
        node=nodes.new('ShaderNodeGroup');node.node_tree=group;l.new(sep.outputs['Red'],node.inputs['Occlusion'])
    bpy.data.objects.remove(target,do_unlink=True)
    for o in selected+hidden:o.hide_render=False
    rig.data.pose_position='POSE'
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(TARGET),compress=True)
    report={'resolution':SIZE,'atlas':'unique nonoverlapping helmet UV islands, shared color/normal/ORM','objects':len(selected),'body_maps':'unchanged','identity':'albedo authored for existing detailGain=6; no shared shader/profile change','AO_range':[float(orm[:,0].min()),float(orm[:,0].max())]}
    (OUT/'textures.json').write_text(json.dumps(report,indent=2))


if __name__=='__main__':main()
