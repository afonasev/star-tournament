"""B01 unique UV bake for the new armor and textile; immutable F02/FPS maps.

Reuse the proven atlas bake machinery, with a distinct body master and target.
"""
import bpy,sys,math
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


# Same nonoverlapping UV, tangent normals and AO/roughness bake as F02. The
# transformed script operates only on B01-tagged objects and its three new mats.
s=Path(recipe.__file__).read_text()
s=s[s.index('def main():'):s.index("if __name__=='__main__'")]
s=s.replace("oldpaint=bpy.data.materials['vector-fighter-identity-reference'];oldsteel=bpy.data.materials['vector-reference-coated-steel']", "oldpaint=bpy.data.materials['vector-fighter-identity-body-b01'];oldsteel=bpy.data.materials['vector-body-b01-graphite'];oldcloth=bpy.data.materials['vector-body-b01-textile']")
s=s.replace("master('vector-fighter-identity-f23-helmet',True)","body_master('vector-fighter-identity-body-b01-baked','paint')").replace("master('vector-f23-helmet-coated-steel',False)","body_master('vector-body-b01-graphite-baked','steel')\n    cloth,cr=body_master('vector-body-b01-textile-baked','textile')")
s=s.replace("o.name.startswith('VECTOR F23R')", "o.get('body_reconstruction')=='B01'").replace('(oldpaint,oldsteel)', '(oldpaint,oldsteel,oldcloth)')
s=s.replace("slots.append(0 if o.data.materials[p.material_index]==oldpaint else 1)","slots.append(0 if o.data.materials[p.material_index]==oldpaint else 1 if o.data.materials[p.material_index]==oldsteel else 2)")
s=s.replace('data.materials.append(steel)', 'data.materials.append(steel);data.materials.append(cloth)')
s=s.replace('elif m==oldsteel:o.data.materials[i]=steel','elif m==oldsteel:o.data.materials[i]=steel\n            elif m==oldcloth:o.data.materials[i]=cloth')
s=s.replace("and not (len(o.vertex_groups)==1 and o.vertex_groups[0].name=='Head')", "and not o.get('body_reconstruction')=='B01'")
s=s.replace('(paint,steel)', '(paint,steel,cloth)').replace('[(paint,pr),(steel,sr)]','[(paint,pr),(steel,sr),(cloth,cr)]')
s=s.replace('F23-helmet','B01-body').replace('F23 unique atlas','B01 unique atlas').replace('F23 atlas bake target','B01 atlas bake target').replace('helmet-procedural-master','body-procedural-master')
s=s.replace("normal.inputs['Strength'].default_value=.23","normal.inputs['Strength'].default_value=.35")
s=s.replace("'body_maps':'unchanged'", "'helmet_and_FPS_maps':'unchanged'")
s=s.replace("orm[:,2]=.18", "orm[:,2]=.18")
s=s.replace("'unique nonoverlapping helmet UV islands, shared color/normal/ORM'", "'unique nonoverlapping new body UV islands, shared color/normal/ORM'")
# Ensure master() sees existing image cache and global helpers.
ns=dict(recipe.__dict__);ns.update(TARGET=TARGET,OUT=OUT,activate=activate,body_master=body_master,SIZE=2048)
exec(compile(s,__file__,'exec'),ns);ns['main']()
