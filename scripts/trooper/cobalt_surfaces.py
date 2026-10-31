"""COBALT authored layered surfaces and standard glTF PBR atlas baking.

Procedural source uses physical world coordinates, so joined export and editable
parts retain the same pattern density. No random per-object UV noise textures.
"""
import bpy
import math


def material(name, color, metal, rough, grain=.025):
    m = bpy.data.materials.new('cobalt / '+name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    nodes = m.node_tree.nodes
    links = m.node_tree.links
    nodes.clear()
    def node(kind, label):
        n = nodes.new(kind); n.label = label; return n
    def mathnode(op, a, b, label):
        n = node('ShaderNodeMath', label); n.operation = op
        for i, v in enumerate((a, b)):
            if isinstance(v, (float, int)): n.inputs[i].default_value = v
            else: links.new(v, n.inputs[i])
        return n.outputs[0]
    def ramp(value, low, high, label):
        n = node('ShaderNodeMapRange', label)
        links.new(value, n.inputs['Value'])
        n.inputs['From Min'].default_value = low
        n.inputs['From Max'].default_value = high
        n.clamp = True
        return n.outputs[0]
    def mix(fac, a, b, label):
        n = node('ShaderNodeMixRGB', label)
        links.new(fac, n.inputs[0])
        for i, v in enumerate((a, b), 1):
            if isinstance(v, tuple): n.inputs[i].default_value = (*v, 1)
            else: links.new(v, n.inputs[i])
        return n.outputs[0]
    geo = node('ShaderNodeNewGeometry', 'Physical surface coordinates and convex edges')
    def noise(scale, label, vector=None):
        n = node('ShaderNodeTexNoise', label)
        links.new(vector or geo.outputs['Position'], n.inputs['Vector'])
        n.inputs['Scale'].default_value = scale
        n.inputs['Detail'].default_value = 3
        n.inputs['Roughness'].default_value = .7
        return n.outputs['Fac']
    macro = noise(28, 'Broad rubbed coating / finger grease')
    micro = noise(1450, 'Fine physical grain')
    stretch = node('ShaderNodeVectorMath', 'Longitudinal tool scratches')
    stretch.operation = 'MULTIPLY'
    links.new(geo.outputs['Position'], stretch.inputs[0])
    stretch.inputs[1].default_value = (1700, 35, 700)
    scratches = ramp(noise(1, 'Sparse elongated scratches', stretch.outputs[0]), .68, .78, 'Scratch mask')
    edge = node('ShaderNodeAttribute', 'Authored bevel faces only; no interpolated wear across flat panels')
    edge.attribute_name = 'cobalt_edge_wear'
    convex = edge.outputs['Fac']
    broken = ramp(noise(650, 'Irregular edge chips'), .36, .70, 'Broken chip boundary')
    wear = mathnode('MULTIPLY', convex, broken, 'Convex edge wear')
    wear = mathnode('ADD', wear, mathnode('MULTIPLY', scratches, .42, 'Flat scuffs'), 'Combined surface wear')
    base = mix(macro, tuple(c*.67 for c in color), tuple(c*1.1 for c in color), 'Layered coating tone')
    is_rubber = name == 'grip rubber'
    if is_rubber:
        cells = node('ShaderNodeTexVoronoi', 'Moulded rubber cells')
        cells.feature = 'DISTANCE_TO_EDGE'
        links.new(geo.outputs['Position'], cells.inputs['Vector'])
        cells.inputs['Scale'].default_value = 550
        height = cells.outputs['Distance']
        base = mix(macro, (.009, .012, .015), (.032, .037, .040), 'Dry and polished rubber')
        roughness = mathnode('ADD', .66, mathnode('MULTIPLY', macro, .22, 'Rubber roughness'), 'Rubber finish')
        metallic = 0.0
    else:
        base = mix(wear, base, (.27, .30, .32), 'Exposed steel / rubbed edges')
        height = mathnode('SUBTRACT', mathnode('MULTIPLY', micro, .35, 'Fine coating relief'), scratches, 'Scratched finish relief')
        roughness = mathnode('ADD', rough-.12, mathnode('MULTIPLY', macro, .30, 'Patchy polish'), 'Finish roughness')
        roughness = mathnode('SUBTRACT', roughness, mathnode('MULTIPLY', wear, .18, 'Polished scuffs'), 'Worn roughness')
        metallic = mathnode('ADD', metal, mathnode('MULTIPLY', wear, 1-metal, 'Exposed metallic substrate'), 'Metallic layers')
    # F2 tactile stock inset: a finite panel in physical metres, not tiled across the gun.
    stock = node('ShaderNodeAttribute', 'Authored stock surfaces only')
    stock.attribute_name = 'cobalt_stock_texture'
    xyz = node('ShaderNodeSeparateXYZ', 'Stock pattern in physical coordinates')
    links.new(geo.outputs['Position'], xyz.inputs[0])
    x, y = xyz.outputs['X'], xyz.outputs['Z' if is_rubber else 'Y']
    mask = stock.outputs['Fac']
    if not is_rubber:
        mask = mathnode('MULTIPLY', mask, mathnode('LESS_THAN', mathnode('ABSOLUTE', x, 0, 'Panel width'), .033, 'Blue side border'), 'Inset side mask')
        mask = mathnode('MULTIPLY', mask, mathnode('GREATER_THAN', y, .170, 'Front blue border'), 'Inset front mask')
        mask = mathnode('MULTIPLY', mask, mathnode('LESS_THAN', y, .326, 'Rear blue border'), 'Inset rear mask')
    cuts = []
    for op in ('ADD', 'SUBTRACT'):
        diagonal = mathnode(op, x, y, 'Opposed diagonal direction')
        wave = mathnode('SINE', mathnode('MULTIPLY', diagonal, 1256.637, 'Five millimetre repeat'), 0, 'Diamond tool path')
        cuts.append(mathnode('LESS_THAN', mathnode('ABSOLUTE', wave, 0, 'Symmetric groove'), .23, 'Narrow recessed cut'))
    grooves = mathnode('MAXIMUM', cuts[0], cuts[1], 'Crossed diamond knurl')
    stock_color = mix(grooves, (.027, .040, .052), (.006, .012, .020), 'Graphite diamond lands and dark cuts')
    base = mix(mask, base, stock_color, 'Stock tactile inset with retained blue frame')
    roughness = mathnode('ADD', mathnode('MULTIPLY', roughness, mathnode('SUBTRACT', 1, mask, 'Existing finish outside panel'), 'Existing roughness'), mathnode('MULTIPLY', mask, .68, 'Matte tactile panel'), 'Panel roughness')
    stock_bump = node('ShaderNodeBump', 'Stock diamond relief')
    stock_bump.inputs['Strength'].default_value = .7
    stock_bump.inputs['Distance'].default_value = .00055
    links.new(mathnode('MULTIPLY', mask, mathnode('SUBTRACT', 1, grooves, 'Raised diamond lands'), 'Masked stock relief'), stock_bump.inputs['Height'])
    b = node('ShaderNodeBsdfPrincipled', 'COBALT layered PBR')
    links.new(base, b.inputs['Base Color'])
    links.new(roughness, b.inputs['Roughness'])
    if isinstance(metallic, float): b.inputs['Metallic'].default_value = metallic
    else: links.new(metallic, b.inputs['Metallic'])
    bump = node('ShaderNodeBump', 'Fine tactile relief')
    bump.inputs['Strength'].default_value = .55
    bump.inputs['Distance'].default_value = .00035 if is_rubber else .00018
    links.new(height, bump.inputs['Height']); links.new(bump.outputs['Normal'], stock_bump.inputs['Normal']); links.new(stock_bump.outputs['Normal'], b.inputs['Normal'])
    out = node('ShaderNodeOutputMaterial', 'Authored material output')
    links.new(b.outputs['BSDF'], out.inputs['Surface'])
    return m


def bake_atlas(obj, material, directory):
    """Three maps per surface; ORM channels R=1/G=roughness/B=metallic."""
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True); bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.009)
    bpy.ops.object.mode_set(mode='OBJECT')
    size = 2048 if 'blue anodized' in material.name else 1024
    slug = material.name.split(' / ')[1].replace(' ', '-')
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'; scene.cycles.samples = 8
    scene.render.bake.margin = 8
    nodes = material.node_tree.nodes; links = material.node_tree.links
    bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    out = next(n for n in nodes if n.type == 'OUTPUT_MATERIAL')
    emission = nodes.new('ShaderNodeEmission')
    target = nodes.new('ShaderNodeTexImage')
    maps = {}
    for kind in ('basecolor', 'orm', 'normal'):
        image = bpy.data.images.new('COBALT '+slug+' '+kind, width=size, height=size, alpha=False)
        image.colorspace_settings.name = 'sRGB' if kind == 'basecolor' else 'Non-Color'
        target.image = image; nodes.active = target
        if kind == 'normal':
            links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
            bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT', use_clear=True)
        else:
            if kind == 'basecolor':
                signal = bsdf.inputs['Base Color'].links[0].from_socket
            else:
                combine = nodes.new('ShaderNodeCombineColor'); combine.mode = 'RGB'
                combine.inputs['Red'].default_value = 1
                for channel, source in (('Green', 'Roughness'), ('Blue', 'Metallic')):
                    socket = bsdf.inputs[source]
                    if socket.is_linked: links.new(socket.links[0].from_socket, combine.inputs[channel])
                    else: combine.inputs[channel].default_value = socket.default_value
                signal = combine.outputs[0]
            links.new(signal, emission.inputs['Color']); links.new(emission.outputs[0], out.inputs['Surface'])
            bpy.ops.object.bake(type='EMIT', use_clear=True)
        image.filepath_raw = str(directory/(slug+'-'+kind+'.png'))
        image.file_format = 'PNG'; image.save(); image.pack(); maps[kind] = image
    nodes.clear()
    b = nodes.new('ShaderNodeBsdfPrincipled'); out = nodes.new('ShaderNodeOutputMaterial')
    links.new(b.outputs['BSDF'], out.inputs['Surface'])
    def texture(kind):
        n = nodes.new('ShaderNodeTexImage'); n.image = maps[kind]; n.label = kind; return n
    links.new(texture('basecolor').outputs['Color'], b.inputs['Base Color'])
    channels = nodes.new('ShaderNodeSeparateColor'); channels.mode = 'RGB'
    links.new(texture('orm').outputs['Color'], channels.inputs[0])
    links.new(channels.outputs['Green'], b.inputs['Roughness']); links.new(channels.outputs['Blue'], b.inputs['Metallic'])
    normal = nodes.new('ShaderNodeNormalMap')
    links.new(texture('normal').outputs['Color'], normal.inputs['Color']); links.new(normal.outputs['Normal'], b.inputs['Normal'])
    return {'surface': slug, 'size': size, 'maps': list(maps)}
