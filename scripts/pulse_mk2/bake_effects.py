"""Deterministic volume-integrated fire/smoke art. Run with Blender Python.
Bakes RGBA 4x4 atlas; no runtime noise generation or gameplay randomness.
"""
import bpy,numpy as np,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'unity/Assets/StarTournament/Resources/PulseMk2';OUT.mkdir(parents=True,exist_ok=True)
N=256
v=np.linspace(-1,1,N,dtype=np.float32);x,y=np.meshgrid(v,v)
def noise(x,y,z):
 # Smooth deterministic value noise with independent lattice phases.
 ix=np.floor(x);iy=np.floor(y);iz=np.floor(z);fx=x-ix;fy=y-iy;fz=z-iz
 fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy);fz=fz*fz*(3-2*fz);r=np.zeros_like(x)
 for a in (0,1):
  for b in (0,1):
   for c in (0,1):
    h=np.sin((ix+a)*127.1+(iy+b)*311.7+(iz+c)*74.7)*43758.5453;h=h-np.floor(h)
    r+=h*(fx if a else 1-fx)*(fy if b else 1-fy)*(fz if c else 1-fz)
 return r

def frame(phase,smoke=False):
 rgb=np.zeros((N,N,3),np.float32);alpha=np.zeros_like(x)
 for z in np.linspace(-.95,.95,28):
  # Evolving buoyant curling volume; all dimensions are normalized authored art.
  yy=y-phase*.10
  n=noise(x*3+phase*.7,yy*3,z*3+phase*.5)
  fine=noise(x*9+n,yy*9-phase*1.3,z*9+n)*.27+noise(x*23,yy*23,z*23+phase)*.12
  radius=np.sqrt(x*x+yy*yy+z*z)
  boundary=.68+.24*n+.08*np.sin(y*9+phase*3)
  density=np.clip((boundary-radius)*8+n*.35+fine-.35,0,1)
  # Dense soot pockets separate small bright cellular surfaces.
  heat=np.clip((1-radius)*.75+n*.7+fine*.65-.26,0,1)
  heat=np.clip(heat+.12*np.sin(phase*2+radius*5),0,1)
  if smoke:
   color=np.stack([.28+.48*n+.12*fine]*3,axis=-1);a=density*.12
  else:
   hot=np.clip((heat-.40)*2.8,0,1);white=np.clip((heat-.68)*4,0,1)
   color=np.stack([.20+.80*np.clip(heat*2,0,1),.018+.48*hot+.45*white,.002+.055*hot+.55*white],axis=-1)
   a=density*.16
  rgb+=(1-alpha)[...,None]*a[...,None]*color;alpha+=(1-alpha)*a
 rgb=np.divide(rgb,alpha[...,None],out=np.zeros_like(rgb),where=alpha[...,None]>.0001)
 return np.concatenate([np.clip(rgb,0,1),alpha[...,None]],axis=-1)
def save(name,arr):
 im=bpy.data.images.new(name,width=arr.shape[1],height=arr.shape[0],alpha=True);im.pixels.foreach_set(arr.ravel());im.update();im.filepath_raw=str(OUT/(name+'.png'));im.file_format='PNG';im.save()
atlas=np.zeros((N*4,N*4,4),np.float32)
for i in range(16):
 atlas[(i//4)*N:(i//4+1)*N,(i%4)*N:(i%4+1)*N]=frame(i/16);print('FIRE_FRAME',i,flush=True)
# Selected detailed fire art is authored with imagegen; do not overwrite it.
save('Smoke',frame(.3,True))
# Axial exhaust: irregular hot taper, with transparent radial edges.
u=(x+1)/2;width=.46*(1-u)**.7+.025;n=noise(x*7,y*11,.7)
alpha=np.clip((width-np.abs(y))*9,0,1)*(1-u)**.7
heat=np.clip(1-u*.7-np.abs(y)*1.8+n*.15,0,1)
rgb=np.stack([np.ones_like(x),.20+.75*heat**2,.015+.7*heat**5],axis=-1)
# Selected Exhaust.png is imagegen art, immutable provenance in exhaust-art-source.json.
# Keep the deterministic earlier recipe above for reference; do not overwrite it.
