"""B01 unique UV bake for the new armor and textile; immutable F02/FPS maps.

Reuse the proven atlas bake machinery, with a distinct body master and target.
"""
import bpy,sys,math,json
import numpy as np
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).parent))
import texture_f23_helmet as recipe
from reconstruct_body import TARGET,OUT,activate


def body_master(name,kind):
    # The coating uses the calibrated unsaturated identity range. Textile is
    # a woven surface with coherent warp/weft relief, not random surface noise.
    if kind!='textile':return recipe.master(name,kind=='paint')
    m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;n.clear();l=m.node_tree.links
    bs=n.new('ShaderNodeBsdfPrincipled');bs.name='Principled BSDF';out=n.new('ShaderNodeOutputMaterial');l.new(bs.outputs[0],out.inputs['Surface'])
    tex=n.new('ShaderNodeTexCoord')
    values=[]
    for direction,scale in [('X',1700),('Z',1800)]:
        w=n.new('ShaderNodeTexWave');w.bands_direction=direction;w.inputs['Scale'].default_value=scale
        w.inputs['Distortion'].default_value=1.4;w.inputs['Detail Scale'].default_value=3
        l.new(tex.outputs['Object'],w.inputs['Vector']);values.append(w.outputs['Fac'])
    mul=n.new('ShaderNodeMath');mul.operation='MULTIPLY';l.new(values[0],mul.inputs[0]);l.new(values[1],mul.inputs[1])
    color=n.new('ShaderNodeValToRGB');color.color_ramp.elements[0].color=(.009,.012,.014,1);color.color_ramp.elements[1].color=(.045,.052,.057,1)
    l.new(mul.outputs[0],color.inputs[0]);l.new(color.outputs[0],bs.inputs['Base Color'])
    bump=n.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.00032;bump.inputs['Strength'].default_value=.55
    l.new(mul.outputs[0],bump.inputs['Height']);l.new(bump.outputs[0],bs.inputs['Normal'])
    rough=n.new('ShaderNodeValue');rough.outputs[0].default_value=.84;l.new(rough.outputs[0],bs.inputs['Roughness'])
    return m,rough.outputs[0]


SIZE=2048

def main():
    bpy.ops.wm.open_mainfile(filepath=str(TARGET))
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');rig.data.pose_position='REST'
    oldpaint=bpy.data.materials['vector-fighter-identity-body-b01'];oldsteel=bpy.data.materials['vector-body-b01-graphite'];oldcloth=bpy.data.materials['vector-body-b01-textile']
    paint,pr=body_master('vector-fighter-identity-body-b01-baked','paint')
    steel,sr=body_master('vector-body-b01-graphite-baked','steel')
    cloth,cr=body_master('vector-body-b01-textile-baked','textile')
    selected=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.get('body_reconstruction')=='B01' and any(m in (oldpaint,oldsteel,oldcloth) for m in o.data.materials)]
    # Aggregate only for atlas/bake; transfer per-loop UVs back to editable parts.
    vertices=[];faces=[];slots=[];smoothing=[];offsets=[]
    for o in selected:
        offsets.append((o,len(faces)))
        start=len(vertices);vertices.extend(o.matrix_world@v.co for v in o.data.vertices)
        for p in o.data.polygons:
            faces.append(tuple(start+i for i in p.vertices))
            slots.append(0 if o.data.materials[p.material_index]==oldpaint else 1 if o.data.materials[p.material_index]==oldsteel else 2)
            smoothing.append(p.use_smooth)
    data=bpy.data.meshes.new('B01 unique atlas');data.from_pydata(vertices,[],faces);data.update()
    target=bpy.data.objects.new('B01 atlas bake target',data);bpy.context.collection.objects.link(target)
    data.materials.append(paint);data.materials.append(steel);data.materials.append(cloth)
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
            elif m==oldcloth:o.data.materials[i]=cloth
        o.hide_render=True
    # Bake target sees local shell/visor/gaskets, not distant costume surfaces.
    hidden=[]
    for o in bpy.context.scene.objects:
        if o.type=='MESH' and o!=target and not o.get('body_reconstruction')=='B01':
            hidden.append(o);o.hide_render=True
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16
    scene.render.bake.margin=12;scene.render.bake.use_clear=True
    maps={}
    def bake(key,kind,color=False):
        im=bpy.data.images.new('B01-body-'+key,width=SIZE,height=SIZE,alpha=False)
        im.colorspace_settings.name='sRGB' if color else 'Non-Color'
        for mat in (paint,steel,cloth):
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
    for mat,rough in [(paint,pr),(steel,sr),(cloth,cr)]:
        nodes=mat.node_tree.nodes;out=next(n for n in nodes if n.type=='OUTPUT_MATERIAL')
        links.append((mat,out,out.inputs['Surface'].links[0].from_socket))
        emission=nodes.new('ShaderNodeEmission');mat.node_tree.links.new(rough,emission.inputs['Color'])
        mat.node_tree.links.new(emission.outputs[0],out.inputs['Surface'])
    bake('roughness','EMIT')
    # Bake material identity into the packed metallic channel. Textile must
    # remain dielectric; a single constant would incorrectly metallize fabric.
    for mat in (paint,steel,cloth):
        emission=next(n for n in mat.node_tree.nodes if n.type=='EMISSION')
        for link in list(emission.inputs['Color'].links):mat.node_tree.links.remove(link)
        metal=0 if mat==cloth else .18 if mat==paint else .25
        emission.inputs['Color'].default_value=(metal,metal,metal,1)
    bake('metallic','EMIT')
    for mat,out,socket in links:mat.node_tree.links.new(socket,out.inputs['Surface'])
    # Save the editable procedural recipe in a compact helmet-only master.
    bpy.data.libraries.write(str(OUT/'body-procedural-master.blend'),{target},compress=True)
    ao=np.empty(SIZE*SIZE*4,dtype=np.float32);maps['AO'].pixels.foreach_get(ao)
    rough=np.empty_like(ao);maps['roughness'].pixels.foreach_get(rough)
    orm=np.ones((SIZE*SIZE,4),dtype=np.float32);orm[:,0]=ao.reshape(-1,4)[:,0]
    metal=np.empty_like(ao);maps['metallic'].pixels.foreach_get(metal)
    orm[:,1]=rough.reshape(-1,4)[:,0];orm[:,2]=metal.reshape(-1,4)[:,0]
    packed=bpy.data.images.new('B01-body-ORM',width=SIZE,height=SIZE,alpha=False)
    packed.colorspace_settings.name='Non-Color';packed.pixels.foreach_set(orm.ravel());packed.pack()
    for mat in (paint,steel,cloth):
        nodes=mat.node_tree.nodes;nodes.clear();l=mat.node_tree.links
        out=nodes.new('ShaderNodeOutputMaterial');bs=nodes.new('ShaderNodeBsdfPrincipled');bs.name='Principled BSDF'
        l.new(bs.outputs[0],out.inputs['Surface'])
        def texture(im):
            node=nodes.new('ShaderNodeTexImage');node.image=im;return node.outputs['Color']
        l.new(texture(maps['albedo']),bs.inputs['Base Color'])
        normal=nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.35
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
    report={'resolution':SIZE,'atlas':'unique nonoverlapping new body UV islands, shared color/normal/ORM','objects':len(selected),'helmet_and_FPS_maps':'unchanged','identity':'albedo authored for existing detailGain=6; no shared shader/profile change','AO_range':[float(orm[:,0].min()),float(orm[:,0].max())]}
    (OUT/'textures.json').write_text(json.dumps(report,indent=2))



if __name__=='__main__':main()
