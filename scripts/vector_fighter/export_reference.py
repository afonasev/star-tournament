"""Export/review F19 in isolation; explicit --promote publishes only world GLB."""
import sys,json,hashlib,subprocess,shutil
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import optimize_runtime as opt
import author_assault
from export_heavy_agile import retain_current_weapon
ROOT=Path(__file__).resolve().parents[2]
BASELINE='9345bb2a795fb7d09070d2d81fae82de07859cf7'

def main():
    reference='--reference' in sys.argv
    if reference and '--promote' in sys.argv:raise ValueError('Reference cannot be promoted')
    output_root=Path(sys.argv[sys.argv.index('--output-root')+1]) if '--output-root' in sys.argv else ROOT/'.local/vector-reference'
    output=output_root/('reference' if reference else 'candidate');output.mkdir(parents=True,exist_ok=True)
    source=Path(sys.argv[sys.argv.index('--source')+1]).resolve() if '--source' in sys.argv else ROOT/'scripts/vector_fighter/f19-reference.blend'
    if '--source' in sys.argv and not reference:raise ValueError('Study sources are art-only; use --reference')
    opt.SOURCE=source
    author_assault.palette=lambda:None
    original=opt.optimize
    def reduce(obj,ratio):
        if reference:return original(obj,1)
        if obj.name.startswith('VECTOR R '):return original(obj,1)
        if 'continuous lower garment' in obj.name:return original(obj,.20)
        if 'torso fabric' in obj.name:return original(obj,.16)
        if 'UpperArm fabric' in obj.name:return original(obj,.10)
        return original(obj,ratio)
    opt.optimize=reduce
    rows=opt.export('world','balanced',output/'candidate-body.glb')
    pinned=output/'baseline-equipment.glb';pinned.write_bytes(subprocess.check_output(['git','show',BASELINE+':unity/Assets/StarTournament/Art/trooper-body-animated.glb'],cwd=ROOT))
    retain_current_weapon(output/'candidate-body.glb',pinned)
    report={'source':str(source.relative_to(ROOT)),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'objects':rows,'before_triangles':sum(r['before'] for r in rows),'after_triangles':sum(r['after'] for r in rows),'strategy':'unreduced art source; FP and weapon exact baseline' if reference else 'authored plates/hardware retained; cloth/sleeves/world gloves reduced; FP and weapon exact baseline'}
    (output/'optimization.json').write_text(json.dumps(report,indent=2)+'\n')
    if '--promote' in sys.argv:
        art=ROOT/'unity/Assets/StarTournament/Art';target=art/'trooper-body-animated.glb';shutil.copy2(output/'candidate-body.glb',target);sha=hashlib.sha256(target.read_bytes()).hexdigest()
        manifest=json.loads((art/'asset-manifest.json').read_text());mounts=json.loads((art/'trooper-mounts.json').read_text())
        entry=next(e for e in manifest['entries'] if e['key']=='player-body');entry['sha256']=sha;entry['source']='VECTOR F19 reference reconstruction; scripts/vector_fighter/author_reference.py + export_reference.py';mounts['body']['sha256']=sha
        (art/'asset-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');(art/'trooper-mounts.json').write_text(json.dumps(mounts,indent=2)+'\n')
    print('REFERENCE_TRIANGLES',report['before_triangles'],report['after_triangles'])
if __name__=='__main__':main()
