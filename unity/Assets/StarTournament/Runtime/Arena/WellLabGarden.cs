using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace StarTournament.ProvingGround
{
    public sealed partial class WellLabDetail
    {
        // well-natural-garden-v1. Metres on the existing garden deck; never enters authored map/state/RNG.
        // The only ground-floor portals are east/west, centred on z=0 (the north/south sills remain solid).
        // Fixed cosmetic layout/low silhouette envelope, not balance tuning; it leaves capsule/landing clearance intact.
        public static float GardenPathZ(float x)=>.72f*Mathf.Sin(x*Mathf.PI/WellArenaCatalog.Pit);
        public static bool GardenPlantable(Vector2 p)
        {
            if(Mathf.Abs(p.x)>9.35f||Mathf.Abs(p.y)>9.35f||p.sqrMagnitude<2.6f*2.6f)return false;
            if(Mathf.Abs(p.y-GardenPathZ(p.x))<1.8f)return false;
            foreach(float s in new[]{-1f,1f})if(Mathf.Abs(p.x-s*6)<1.85f&&Mathf.Abs(p.y-s*6)<1.85f)return false;
            return true;
        }
        static float GardenHash(int i,int salt)
        {
            uint h=unchecked((uint)i*747796405u+(uint)salt*2891336453u+277803737u);
            h=(h^(h>>16))*2246822519u;h^=h>>13;return (h&65535)/65535f;
        }
        sealed class GardenMesh
        {
            readonly List<Vector3> points=new List<Vector3>();readonly List<Vector2> uv=new List<Vector2>();readonly List<int> indices=new List<int>();
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector2 u,Vector2 v,Vector2 w)
            {int n=points.Count;points.AddRange(new[]{a,b,c});uv.AddRange(new[]{u,v,w});indices.AddRange(new[]{n,n+1,n+2});}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector2 u,Vector2 v,Vector2 w,Vector2 t)
            {Triangle(a,b,c,u,v,w);Triangle(a,c,d,u,w,t);}
            public void Card(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                int n=points.Count;points.AddRange(new[]{a,b,c,d});uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});
                indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            public void Leaf(Vector3 root,Vector3 end,Vector3 across)
            {
                int n=points.Count;var middle=(root+end)/2+Vector3.up*.012f;
                points.AddRange(new[]{root,middle-across,end,middle+across,middle});
                uv.AddRange(new[]{new Vector2(.5f,0),new Vector2(0,.5f),new Vector2(.5f,1),new Vector2(1,.5f),new Vector2(.5f,.5f)});
                indices.AddRange(new[]{n,n+4,n+1,n+1,n+4,n+2,n+2,n+4,n+3,n+3,n+4,n});
            }
            public Mesh Build(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(points);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
            }
        }
        Material GardenMaterial(string name,Texture2D texture,bool twoSided=false)
        {
            var m=DetailMaterial(name,Color.white,0,.12f);owned.Add(texture);m.SetTexture("_BaseMap",texture);
            if(twoSided)m.SetFloat("_Cull",0);return m;
        }
        readonly List<CombineInstance> gardenMeshes=new List<CombineInstance>();
        readonly List<Material> gardenMaterials=new List<Material>();
        void GardenRender(GardenMesh geometry,Material material,string name)
        {
            var mesh=geometry.Build(name);owned.Add(mesh);gardenMeshes.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.identity});gardenMaterials.Add(material);
        }
        static Vector2 GroundUV(Vector3 p)=>new Vector2((p.x+9.6f)/19.2f,(p.z+9.6f)/19.2f);
        static Texture2D GardenTexture(string kind)
        {
            int n=kind=="ground"?1024:512;var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float a=GardenHash(y*n+x,17),b=GardenHash((y/4)*n+x/4,31),u=x/(float)(n-1),v=y/(float)(n-1);
                if(kind=="ground")
                {
                    float wx=u*19.2f-9.6f,wz=v*19.2f-9.6f;
                    float patch=Mathf.PerlinNoise(wx*.65f+20,wz*.65f+20),fine=Mathf.PerlinNoise(wx*8+20,wz*8+20);
                    var c=Color.Lerp(new Color(.24f,.32f,.105f),new Color(.38f,.44f,.19f),patch);
                    c*=.83f+a*.23f+fine*.17f;
                    // Dense short blades/clover in the ground albedo support the bounded 3D tuft tier.
                    int cellX=x/5,cellY=y/5;float turn=GardenHash(cellY*256+cellX,7)*6.28318f;
                    float dx=x%5-2,dy=y%5-2;
                    float along=dx*Mathf.Cos(turn)+dy*Mathf.Sin(turn),across=-dx*Mathf.Sin(turn)+dy*Mathf.Cos(turn);
                    if(Mathf.Abs(across)<.65f&&Mathf.Abs(along)<2.1f)c=Color.Lerp(c,new Color(.43f,.50f,.23f),.45f+GardenHash(cellX+cellY*256,9)*.3f);
                    float path=1-Mathf.SmoothStep(1.28f,1.75f,Mathf.Abs(wz-GardenPathZ(wx)));
                    c=Color.Lerp(c,new Color(.30f,.285f,.22f)*(.8f+a*.35f),path);
                    pixels[y*n+x]=c;
                }
                else if(kind=="stone")
                {
                    float cloud=Mathf.PerlinNoise(u*8,v*8),grain=a*.12f+b*.08f;
                    // Fine mineral grain with restrained sediment streaks, no architectural panel joints.
                    float seam=Mathf.Pow(Mathf.Max(0,Mathf.Sin(u*31+Mathf.PerlinNoise(u*4,v*4)*8+v*6)),35)*.035f;
                    pixels[y*n+x]=new Color(.40f+cloud*.15f+grain-seam,.41f+cloud*.15f+grain-seam,.39f+cloud*.15f+grain-seam);
                }
            }
            var texture=new Texture2D(n,n,TextureFormat.RGBA32,true){name="Well garden "+kind,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};texture.SetPixels(pixels);texture.Apply(true,true);return texture;
        }
        readonly List<Vector3> meadowBeds=new List<Vector3>();
        public void Plants(Vector3 center)=>meadowBeds.Add(center);
        static void MeadowTuft(GardenMesh blades,Vector3 origin,float height,int seed)
        {
            for(int j=0;j<2;j++)
            {
                float angle=GardenHash(seed,13)*Mathf.PI*2+j*Mathf.PI*.5f;var across=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                float h=height*(.85f+GardenHash(seed,14)*.3f),w=.175f+GardenHash(seed,15)*.035f;
                blades.Card(origin-across*w,origin+across*w,origin+across*w+Vector3.up*h,origin-across*w+Vector3.up*h);
            }
        }
        public void Garden()
        {
            var ground=GardenMaterial("Well meadow ground",GardenTexture("ground"));
            var stone=GardenMaterial("Well garden flagstone",GardenTexture("stone"));
            // This authored Resources material keeps the runtime alpha-cutout shader variant in Player builds.
            var grassTemplate=Resources.Load<Material>("WellMeadowGrassCutout");
            if(!grassTemplate)throw new System.InvalidOperationException("Well meadow cutout material is missing from the build");
            var grass=new Material(grassTemplate){name="Well meadow grass cards"};owned.Add(grass);
            // Imported cutout is shared by Resources; only the per-arena material/meshes are owned here.
            grass.SetTexture("_BaseMap",Resources.Load<Texture2D>("WellMeadowGrass"));
            grass.SetFloat("_Cull",0);grass.SetFloat("_AlphaClip",1);grass.SetFloat("_Cutoff",.38f);grass.EnableKeyword("_ALPHATEST_ON");grass.SetOverrideTag("RenderType","TransparentCutout");grass.renderQueue=2450;
            var clover=GardenMaterial("Well meadow clover",WellLabDetail.Texture("leaf",profile),true);clover.color=new Color(1.24f,1.35f,1.05f);
            var flower=DetailMaterial("Well meadow ivory petals",new Color(.87f,.85f,.68f),0,.1f);flower.SetFloat("_Cull",0);
            var pollen=DetailMaterial("Well meadow ochre centres",new Color(.68f,.47f,.12f),0,.1f);pollen.SetFloat("_Cull",0);
            var bed=new GardenMesh();
            var a=new Vector3(-9.6f,.008f,-9.6f);var b=new Vector3(-9.6f,.008f,9.6f);var c=new Vector3(9.6f,.008f,9.6f);var d=new Vector3(9.6f,.008f,-9.6f);
            bed.Quad(a,b,c,d,GroundUV(a),GroundUV(b),GroundUV(c),GroundUV(d));GardenRender(bed,ground,"Well meadow ground");
            var paving=new GardenMesh();
            for(int row=0;row<24;row++)for(int column=-1;column<=1;column++)
            {
                int id=row*3+column+1;float x=-9.2f+row*.8f;
                if(row>0&&row<23&&column!=0)x+=(GardenHash(id,27)-.5f)*.30f;
                float z=GardenPathZ(x)+column*.89f+(GardenHash(id,1)-.5f)*.15f;
                var centre=new Vector3(x,.024f+GardenHash(id,2)*.003f,z);var rim=new Vector3[7];
                for(int k=0;k<7;k++)
                {
                    float angle=k*Mathf.PI*2/7+(GardenHash(id,29)-.5f)*.38f;float radius=.78f+GardenHash(id*7+k,3)*.21f;
                    var p=centre+new Vector3(Mathf.Cos(angle)*.46f*radius,0,Mathf.Sin(angle)*.51f*radius);
                    // End slabs meet the existing threshold, never extending into the building or its wall.
                    if(row==0&&k>=3&&k<=4)p.x=-9.59f;
                    if(row==23&&(k==0||k==6))p.x=9.59f;
                    rim[k]=p;
                }
                for(int k=0;k<7;k++)
                {
                    int j=(k+1)%7;
                    paving.Triangle(centre,rim[j],rim[k],new Vector2(centre.x,centre.z),new Vector2(rim[j].x,rim[j].z),new Vector2(rim[k].x,rim[k].z));
                    var lo=rim[k];lo.y=.009f;var next=rim[j];next.y=.009f;
                    paving.Quad(rim[k],rim[j],next,lo,new Vector2(0,1),new Vector2(1,1),Vector2.right,Vector2.zero);
                }
            }
            GardenRender(paving,stone,"Well entrance-linked flagstones");
            var blades=new GardenMesh();var petals=new GardenMesh();var centres=new GardenMesh();var leaves=new GardenMesh();
            // Fixed presentation sampling: no Unity Random, colliders, per-blade objects or realtime lights.
            for(int i=0;i<6000;i++)
            {
                var p=new Vector2(Mathf.Lerp(-9.35f,9.35f,GardenHash(i,11)),Mathf.Lerp(-9.35f,9.35f,GardenHash(i,12)));
                if(!GardenPlantable(p))continue;
                float patch=Mathf.PerlinNoise(p.x*.43f+20,p.y*.43f+20);
                bool border=Mathf.Abs(p.x)>8.1f||Mathf.Abs(p.y)>8.1f;
                float height=border?.18f+patch*.12f:.065f+patch*.10f;
                var origin=new Vector3(p.x,.012f,p.y);
                MeadowTuft(blades,origin,height,i);
                if(i%14==0)
                {
                    for(int k=0;k<3;k++)
                    {
                        float angle=k*2.0944f+GardenHash(i,21)*6.28318f;var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                        var baseLeaf=origin+Vector3.up*.035f;var end=baseLeaf+radial*.16f+Vector3.up*.025f;
                        leaves.Leaf(baseLeaf,end,new Vector3(-radial.z,0,radial.x)*.054f);
                    }
                }
                if(i%23!=0||patch<.38f)continue;
                var top=origin+Vector3.up*(height+.025f);
                // Small daisies in sparse groups; the ground and circulation stay legible.
                leaves.Quad(origin-Vector3.right*.004f,origin+Vector3.right*.004f,top+Vector3.right*.003f,top-Vector3.right*.003f,Vector2.zero,Vector2.right,Vector2.one,Vector2.up);
                for(int k=0;k<5;k++)
                {
                    float angle=k*Mathf.PI*2/5;var radial=new Vector3(Mathf.Cos(angle),.1f,Mathf.Sin(angle))*.033f;var tangent=new Vector3(-radial.z,0,radial.x)*.44f;
                    petals.Quad(top,top+radial*.55f-tangent,top+radial,top+radial*.55f+tangent,Vector2.zero,Vector2.right,Vector2.one,Vector2.up);
                }
                centres.Quad(top+new Vector3(-.009f,.002f,-.009f),top+new Vector3(-.009f,.002f,.009f),top+new Vector3(.009f,.002f,.009f),top+new Vector3(.009f,.002f,-.009f),Vector2.zero,Vector2.up,Vector2.one,Vector2.right);
            }
            // Same atlas, material, size and tuft construction as the surrounding meadow, lifted to the bed soil.
            foreach(var centre in meadowBeds)for(int row=0;row<10;row++)for(int column=0;column<10;column++)
            {
                int seed=7000+row*10+column;float x=-1.15f+column*(2.3f/9),z=-1.15f+row*(2.3f/9);
                var origin=centre+new Vector3(x,.345f,z);float patch=Mathf.PerlinNoise(origin.x*.43f+20,origin.z*.43f+20);
                MeadowTuft(blades,origin,.065f+patch*.10f,seed);
            }
            meadowBeds.Clear();
            GardenRender(blades,grass,"Well low meadow grass");GardenRender(petals,flower,"Well sparse meadow flowers");GardenRender(centres,pollen,"Well meadow flower centres");GardenRender(leaves,clover,"Well meadow clover leaves");
            // One renderer with bounded material submeshes preserves the existing 64-renderer/650k-vertex tier.
            var combined=new Mesh{name="Well natural garden",indexFormat=IndexFormat.UInt32};combined.CombineMeshes(gardenMeshes.ToArray(),false,true);owned.Add(combined);
            var go=new GameObject("Well natural garden");go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=combined;go.AddComponent<MeshRenderer>().sharedMaterials=gardenMaterials.ToArray();
            gardenMeshes.Clear();gardenMaterials.Clear();
        }
    }
}
