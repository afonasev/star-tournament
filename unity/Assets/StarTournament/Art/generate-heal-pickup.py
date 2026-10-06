"""Original red Crystal Runes heart; approved concept 2, 2026-09-28.
Renderer only, meters, Y up/Z front after glTF export; ground-center pivot.
Run Blender --background --factory-startup --python this file.
"""
from pathlib import Path
import bpy
ART=Path(__file__).resolve().parent
exec((ART/'generate-crystal-rune-pickups.py').read_text().split('bpy.ops.object.select_all(action="SELECT")')[0])
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
base=root('heal-pickup')
glass=material('heal-red-glass',(.82,.025,.08),.84,.12)
core=material('heal-inner-crystal',(1,.12,.22),.92,.23)
trace=material('heal-white-pulse',(1,.86,.88),1,1.6)
lightmat=material('heal-light-facets',(1,.40,.46),.94,.32)
darkmat=material('heal-deep-facets',(.42,.012,.055),.95,.06)
heart=[(0,.91),(.14,1.11),(.31,1.15),(.47,1.04),(.53,.86),(.48,.65),(.30,.39),(0,.05),(-.30,.39),(-.48,.65),(-.53,.86),(-.47,1.04),(-.31,1.15),(-.14,1.11)]
prism(base,'heal-body',heart,.30,0,glass)
raised_path(base,'heal-outer-trace',heart,.162,.011,trace,True)
inset=[(x*.78,.60+(y-.60)*.78) for x,y in heart]
prism(base,'heal-inset',inset,.18,0,core,.014)
for s in (-1,1):
    facet(base,'heart-facet'+str(s),[(s*.07,.85),(s*.30,1.04),(s*.43,.91),(s*.37,.67),(s*.07,.24)],.155,lightmat if s<0 else darkmat)
raised_path(base,'heartbeat',[(-.35,.65),(-.17,.65),(-.08,.78),(.02,.43),(.12,.65),(.33,.65)],.174,.014,trace)
raised_path(base,'heart-tip',[(-.16,.35),(0,.16),(.16,.35)],.158,.006,trace)
export_one(base,'heal-pickup-crystal-runes-lod0.glb')
