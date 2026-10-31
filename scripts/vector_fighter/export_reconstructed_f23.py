"""F02 helmet export: full source or existing bounded adaptive world sampling."""
import sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).parent))
import optimize_runtime as opt
import optimize_f23_world as adaptive
import author_assault
from export_heavy_agile import retain_current_weapon

source=ROOT/'scripts/vector_fighter/f23-reconstructed.blend'
out=ROOT/'.local/f23-reconstruction'/('high' if '--high' in sys.argv else 'runtime')
out.mkdir(parents=True,exist_ok=True)
if '--high' in sys.argv:
    opt.SOURCE=source;author_assault.palette=lambda:None
    opt.optimize=lambda o,r:{'object':o.name,'before':opt.triangles(o),'after':opt.triangles(o)}
    opt.export('world','balanced',out/'candidate-body.glb')
    retain_current_weapon(out/'candidate-body.glb',ROOT/'unity/Assets/StarTournament/Art/trooper-body-animated.glb')
else:
    adaptive.SOURCE=source;adaptive.OUT=out;adaptive.main()
