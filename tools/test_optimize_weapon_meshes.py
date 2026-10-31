import copy
import struct
import unittest
import optimize_weapon_meshes as opt
from audit_weapon_optimization import verify


class WeaponOptimizationTests(unittest.TestCase):
    def fixture(self, alpha='OPAQUE', seam=False):
        position = [(0.,0.,0.),(1.,0.,0.),(0.,1.,0.),(0.,0.,0.)]
        normals=[(0.,0.,1.)]*4
        if seam:normals[3]=(0.,1.,0.)
        data = struct.pack('<24f', *(x for p in position+normals for x in p)) + struct.pack('<6H',0,1,2,3,1,2)
        doc = {'asset': {'version':'2.0'}, 'buffers':[{'byteLength':len(data)}],
               'bufferViews':[{'buffer':0,'byteLength':48},{'buffer':0,'byteOffset':48,'byteLength':48},{'buffer':0,'byteOffset':96,'byteLength':12}],
               'accessors':[{'bufferView':0,'componentType':5126,'count':4,'type':'VEC3'},
                            {'bufferView':1,'componentType':5126,'count':4,'type':'VEC3'},
                            {'bufferView':2,'componentType':5123,'count':6,'type':'SCALAR'}],
               'materials':[{'name':'metal','alphaMode':alpha}],
               'nodes':[{'name':'muzzle','translation':[1,2,3]}],
               'meshes':[{'name':'weapon','primitives':[{'attributes':{'POSITION':0,'NORMAL':1},'indices':2,'material':0}]}]}
        js = opt.json.dumps(doc).encode();js += b' '*(-len(js)%4)
        return struct.pack('<4sII', b'glTF',2,28+len(js)+len(data))+struct.pack('<I4s',len(js),b'JSON')+js+struct.pack('<I4s',len(data),b'BIN\0')+data

    def test_weld_preserves_winding_multiplicity_and_nodes(self):
        raw=self.fixture(); before,_=opt.read(raw); result,rows=opt.optimize(raw);after,_=opt.read(result)
        self.assertEqual(rows[0]['vertices_before'],4)
        self.assertEqual(rows[0]['vertices_after'],3)
        self.assertEqual(rows[0]['triangles'],2)
        self.assertEqual(before['nodes'],after['nodes'])
        self.assertEqual(before['materials'],after['materials'])
        again,_=opt.optimize(result)
        self.assertEqual(result,again)
        verify(raw,result)
        doc,binary=opt.read(result);doc["nodes"][0]["translation"][0]=99
        js=opt.json.dumps(doc).encode();js+=b" "*(-len(js)%4)
        damaged=struct.pack("<4sII",b"glTF",2,28+len(js)+len(binary))+struct.pack("<I4s",len(js),b"JSON")+js+struct.pack("<I4s",len(binary),b"BIN\0")+binary
        with self.assertRaises(AssertionError):verify(raw,damaged)

    def test_transparent_order_and_vertices_untouched(self):
        raw=self.fixture('BLEND');candidate,rows=opt.optimize(raw)
        self.assertEqual(rows,[])
        a,b=opt.read(raw);c,d=opt.read(candidate)
        self.assertEqual(a['meshes'],c['meshes']);self.assertEqual(b,d)
        self.assertEqual(opt.values(a,b,0),opt.values(c,d,0))

    def test_signature_includes_winding_and_attributes(self):
        attrs={'POSITION':[b'a',b'b',b'c'], 'NORMAL':[b'x',b'y',b'z']}
        self.assertNotEqual(opt.signature(attrs,[0,1,2]),opt.signature(attrs,[0,2,1]))
        other=copy.deepcopy(attrs);other['NORMAL'][0]=b'v'
        self.assertNotEqual(opt.signature(attrs,[0,1,2]),opt.signature(other,[0,1,2]))
        self.assertNotEqual(opt.signature(attrs,[0,1,2]),opt.signature(attrs,[0,1,2,0,1,2]))

    def test_equal_positions_with_different_normals_stay_split(self):
        raw=self.fixture(seam=True);result,rows=opt.optimize(raw)
        self.assertEqual(rows[0]['vertices_after'],4)
        verify(raw,result)

    def test_cache_nonregression(self):
        order=[v for t in range(60) for v in (t,t+1,t+2)]
        result=opt.reorder(order,62)
        self.assertEqual(opt.Counter(tuple(result[i:i+3]) for i in range(0,len(result),3)),
                         opt.Counter(tuple(order[i:i+3]) for i in range(0,len(order),3)))
        for size in (16,32): self.assertLessEqual(opt.misses(result,size),opt.misses(order,size))

if __name__ == '__main__': unittest.main()
