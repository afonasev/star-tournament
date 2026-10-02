"""Extend the locked glTFast PBR graph without replacing its lighting/maps.

Usage: python3 scripts/presentation/build_identity_graph.py SOURCE.shadergraph
Source: glTFast 6.20.0, Runtime/Shader/glTF-pbrMetallicRoughness.shadergraph.
The generated graph retains package subgraphs (normal, roughness, AO, shadows).
No raster textures or geometry are modified.
"""
import copy
import hashlib
import json
import pathlib
import sys
import uuid

source = pathlib.Path(sys.argv[1])
decoder = json.JSONDecoder()
remaining = source.read_text()
objects = []
while remaining.strip():
    obj, length = decoder.raw_decode(remaining.lstrip())
    objects.append(obj)
    remaining = remaining.lstrip()[length:]
graph = objects[0]
graph['m_Path'] = 'StarTournament'
by_id = {o.get('m_ObjectId'): o for o in objects}

def identity(name):
    return hashlib.md5(('trooper-identity/' + name).encode()).hexdigest()

def template(suffix):
    return copy.deepcopy(next(o for o in objects if o.get('m_Type', '').endswith(suffix)))

def slot(name, kind, number, output=False):
    obj = template(kind + 'MaterialSlot')
    obj.update(m_ObjectId=identity(name), m_Id=number, m_DisplayName=name,
               m_ShaderOutputName=name, m_SlotType=int(output), m_StageCapability=2)
    objects.append(obj)
    return {'m_Id': obj['m_ObjectId']}

def edge(out_node, out_slot, in_node, in_slot):
    graph['m_Edges'].append({'m_OutputSlot': {'m_Node': {'m_Id': out_node}, 'm_SlotId': out_slot},
                            'm_InputSlot': {'m_Node': {'m_Id': in_node}, 'm_SlotId': in_slot}})

function = template('PropertyNode')
function.update(m_Type='UnityEditor.ShaderGraph.CustomFunctionNode', m_ObjectId=identity('function'),
                m_Name='TrooperIdentity', m_SourceType=1, m_FunctionName='TrooperIdentity',
                m_FunctionSource='', m_FunctionBody='''
// Peak reflectance removes the source hue while retaining authored seams/detail.
// This is a color-space invariant; artistic thresholds are supplied by Balance Lab.
$precision detail = max(Source.r, max(Source.g, Source.b));
$precision mask = Mode > 1 ? 1 : smoothstep(Tuning.y, Tuning.y + Tuning.z, detail) * Mode;
$precision panel = max(Tuning.w, saturate(detail * Tuning.x));
Out = lerp(detail.xxx, Tint.rgb * panel, saturate(mask));
''')
function.pop('m_Property')
function['m_Slots'] = [slot('Source', 'Vector3', 0), slot('Tint', 'Vector4', 1),
                       slot('Tuning', 'Vector4', 2), slot('Mode', 'Vector1', 3),
                       slot('Out', 'Vector3', 4, True)]
objects.append(function)
graph['m_Nodes'].append({'m_Id': function['m_ObjectId']})
base = next(o['m_ObjectId'] for o in objects if o.get('m_Name') == 'SurfaceDescription.BaseColor')
connection = next(e for e in graph['m_Edges'] if e['m_InputSlot']['m_Node']['m_Id'] == base)
connection['m_InputSlot'] = {'m_Node': {'m_Id': function['m_ObjectId']}, 'm_SlotId': 0}
edge(function['m_ObjectId'], 4, base, 0)

for name, kind, value, number in [('_IdentityColor', 'Color', {'r': 1, 'g': 1, 'b': 1, 'a': 1}, 1),
                                  ('_IdentitySurface', 'Vector4', {'x': 1, 'y': 0, 'z': 1, 'w': 0}, 2),
                                  ('_IdentityMode', 'Vector1', 0, 3)]:
    prop = template(kind + 'ShaderProperty')
    prop.update(m_ObjectId=identity(name + '-property'), m_Name=name,
                m_DefaultReferenceName=name, m_OverrideReferenceName=name,
                m_Value=value, m_Hidden=False)
    prop['m_Guid'] = {'m_GuidSerialized': str(uuid.UUID(identity(name + '-guid')))}
    if kind == 'Color':
        prop['isMainColor'] = False
    if kind == 'Vector1':
        prop['m_FloatType'] = 0
    objects.append(prop)
    graph['m_Properties'].append({'m_Id': prop['m_ObjectId']})
    node = template('PropertyNode')
    node.update(m_ObjectId=identity(name + '-node'), m_Property={'m_Id': prop['m_ObjectId']},
                m_Slots=[slot(name, 'Vector4' if kind == 'Color' else kind, 0, True)])
    objects.append(node)
    graph['m_Nodes'].append({'m_Id': node['m_ObjectId']})
    edge(node['m_ObjectId'], 0, function['m_ObjectId'], number)

target = pathlib.Path(__file__).resolve().parents[2] / 'unity/Assets/StarTournament/Resources/TrooperIdentity.shadergraph'
target.write_text('\n\n'.join(json.dumps(o, indent=4) for o in objects) + '\n')
print(target)
