using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
namespace StarTournament.ProvingGround
{
    /// <summary>Static renderer-only service kit. Canonical solids remain the sole physics/nav owner.</summary>
    public sealed partial class IndustrialTunnelsPresentation:MonoBehaviour
    {
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly Dictionary<Material,List<CombineInstance>> batches=new Dictionary<Material,List<CombineInstance>>();
        Mesh cube,tube;Material steel,floor,trim,ochre,upper,warm,white,mold;ProvingProfile p;
        Color oldAmbient;AmbientMode oldMode;
        public int LightCount {get;private set;} public int PipeCount {get;private set;}
        float P(string key)=>p.Get("tunnels."+key);
        public void Build(ArenaDefinition definition,ProvingProfile profile)
        { var steps=BuildSteps(definition,profile);while(steps.MoveNext()){} }
        public IEnumerator BuildSteps(ArenaDefinition definition,ProvingProfile profile)
        {
            p=profile;if(p.Id!=ProvingProfile.TunnelsArtId||p.Validate().Count>0)throw new ArgumentException("Invalid tunnel art profile");
            oldAmbient=RenderSettings.ambientLight;oldMode=RenderSettings.ambientMode;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(1,.91f,.79f)*P("light.fill");
            cube=Cube();tube=Cylinder();owned.Add(cube);owned.Add(tube);
            var texture=Resources.Load<Texture2D>("IndustrialTunnels/steel-panels");if(!texture)throw new InvalidOperationException("Missing industrial-tunnel-panels manifest texture");
            steel=Material("Tunnel worn steel",Color.white*P("surface.wall"),texture);
            floor=Material("Tunnel dirty graphite floor",new Color(1,.95f,.86f)*P("surface.floor"),texture);
            trim=Material("Tunnel frame / graphite",new Color(.12f,.135f,.14f));ochre=Material("Tunnel service ochre",new Color(.48f,.30f,.08f));upper=Material("Base upper clean alloy",new Color(.78f,.82f,.81f));
            warm=Emissive("Amber service lamp",new Color(1,.56f,.19f));white=Emissive("White base luminaire",new Color(.86f,.94f,1));
            mold=new Material(Shader.Find("StarTournament/TunnelGrime")){name="Lower wall damp / mold"};mold.SetFloat("_Opacity",P("surface.mold"));owned.Add(mold);
            CreateSectorMaterials();
            int decorated=0;
            foreach(var s in definition.Solids)
            {
                if(decorated++%16==0)yield return null;
                var target=transform.parent.Find(s.Id);var renderer=target.GetComponent<Renderer>();
                if(s.Surface=="tunnel-grille")
                {
                    renderer.enabled=false;Grille(s.Position,s.Size);continue;
                }
                var mesh=Instantiate(target.GetComponent<MeshFilter>().sharedMesh);owned.Add(mesh);var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;
                for(int i=0;i<uv.Length;i++){var v=Vector3.Scale(vertices[i],s.Size);var n=normals[i];uv[i]=(Mathf.Abs(n.y)>.5f?new Vector2(v.x,v.z):Mathf.Abs(n.x)>.5f?new Vector2(v.z,v.y):new Vector2(v.x,v.y))/P("surface.tile");}
                mesh.uv=uv;target.GetComponent<MeshFilter>().sharedMesh=mesh;renderer.sharedMaterial=s.Material=="floor"?floor:steel;
                if(s.Id.StartsWith("shell-",StringComparison.Ordinal))WallDetails(s,definition);
            }
            // Room lights establish readable broad junctions; the high white panels reveal the clean base.
            foreach(var c in new[]{new Vector3(-20,0,16),new Vector3(20,0,16),new Vector3(-20,0,-16),new Vector3(20,0,-16),Vector3.zero})
            {
                Fixture(c+Vector3.up*7.6f,true,true);
                Fixture(c+new Vector3(-3,3.65f,0),false,false);Fixture(c+new Vector3(3,3.65f,0),false,false);
            }
            foreach(float x in new[]{-10f,0,10f})foreach(int side in new[]{-1,1})Fixture(new Vector3(x,3.8f,side*19),false,false);
            foreach(float z in new[]{-6f,0,6f})foreach(int side in new[]{-1,1})Fixture(new Vector3(side*23,3.8f,z),false,false);
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1}){Fixture(new Vector3(sx*10,3.8f,sz*13),false,false);Fixture(new Vector3(sx*5,3.8f,sz*8),false,false);}
            yield return null;Wayfinding();yield return null;
            foreach(var entry in batches)
            {
                var go=new GameObject(entry.Key.name);go.transform.SetParent(transform,false);var mesh=new Mesh{name=go.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(entry.Value.ToArray());owned.Add(mesh);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=entry.Key;r.shadowCastingMode=entry.Key==white||entry.Key==warm||entry.Key==mold?ShadowCastingMode.Off:ShadowCastingMode.On;
            }
        }
        Material Material(string name,Color c,Texture texture=null)
        {var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=c};m.SetFloat("_Metallic",P("surface.metallic"));m.SetFloat("_Smoothness",P("surface.smoothness"));if(texture)m.SetTexture("_BaseMap",texture);owned.Add(m);return m;}
        Material Emissive(string name,Color c)
        {var m=new Material(Shader.Find("StarTournament/OrbitalSurface")){name=name};m.SetColor("_Color",c*P("light.emission"));owned.Add(m);return m;}
        void Shape(Material mat,Mesh mesh,Vector3 pos,Vector3 scale,Quaternion rotation)
        {if(!batches.TryGetValue(mat,out var batch)){batch=new List<CombineInstance>();batches[mat]=batch;}batch.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(pos,rotation,scale)});}
        void Box(Material mat,Vector3 pos,Vector3 size,Quaternion? rot=null)=>Shape(mat,cube,pos,size,rot??Quaternion.identity);
        void Beam(Material mat,Vector3 a,Vector3 b,float width,float depth)
        {Shape(mat,cube,(a+b)*.5f,new Vector3(width,depth,Vector3.Distance(a,b)),Quaternion.LookRotation(b-a));}
        void Pipe(Material mat,Vector3 a,Vector3 b,float radius)
        {Shape(mat,tube,(a+b)*.5f,new Vector3(radius,Vector3.Distance(a,b),radius),Quaternion.FromToRotation(Vector3.up,b-a));PipeCount++;}
        void Grille(Vector3 c,Vector3 size)
        {
            // Fixed kit cross-sections are visual asset geometry; spacing has a profile descriptor.
            float pitch=P("detail.grilleSpacing");
            for(float x=-size.x*.5f+.025f;x<size.x*.5f;x+=pitch)Box(trim,c+new Vector3(x,0,0),new Vector3(.035f,.07f,size.z));
            for(float z=-size.z*.5f+.025f;z<size.z*.5f;z+=pitch)Box(trim,c+new Vector3(0,.02f,z),new Vector3(size.x,.06f,.035f));
            Box(upper,new Vector3(c.x,8,c.z),new Vector3(size.x,.18f,size.z));
            // The visible upper ceiling has a clean modular rhythm above the dark mesh.
            for(float x=-size.x*.5f+1;x<size.x*.5f;x+=3)Box(trim,new Vector3(c.x+x,7.88f,c.z),new Vector3(.025f,.02f,size.z));
        }
        void WallDetails(ArenaSolid s,ArenaDefinition d)
        {
            bool vertical=s.Size.x<s.Size.z;var along=vertical?Vector3.forward:Vector3.right;float length=vertical?s.Size.z:s.Size.x;
            // Locate the interior half-space using canonical floor bounds, not object naming.
            var normal=vertical?Vector3.right:Vector3.forward;var probe=s.Position+normal*.4f;bool inside=false;
            foreach(var floorSolid in d.Solids)if(floorSolid.Material=="floor")
            {var local=Quaternion.Inverse(floorSolid.Rotation)*(probe-floorSolid.Position);if(Mathf.Abs(local.x)<floorSolid.Size.x*.5f&&Mathf.Abs(local.z)<floorSolid.Size.z*.5f){inside=true;break;}}
            if(!inside)normal=-normal;
            var face=s.Position+normal*.11f;
            Vector3 Size(float depth,float height,float run)=>vertical?new Vector3(depth,height,run):new Vector3(run,height,depth);
            Box(upper,new Vector3(s.Position.x,6.45f,s.Position.z),Size(.22f,3.1f,length));
            if(length>4)
            {
                var door=new Vector3(face.x,6.1f,face.z)+normal*.02f;
                Box(trim,door,Size(.05f,2.15f,1.5f));Box(upper,door+normal*.035f,Size(.035f,1.98f,1.3f));
                Box(trim,door+normal*.06f,Size(.01f,1.9f,.025f));Box(white,door+Vector3.up*1.2f+normal*.08f,Size(.06f,.1f,1.6f));
            }
            Box(ochre,new Vector3(face.x,.18f,face.z)+normal*.025f,Size(.07f,.16f,length));
            Box(mold,new Vector3(face.x,-.08f,face.z)+normal*.006f,Size(.015f,.95f,length));
            var edgeFloor=new Vector3(face.x,-.59f,face.z)+normal*.4f;Box(mold,edgeFloor,Size(.8f,.008f,length));
            for(int i=0;i<3;i++)
            {
                float radius=i==0?P("detail.pipeRadius"):.055f;var c=new Vector3(face.x,2.6f+i*.38f,face.z)+normal*(radius+.07f);
                Pipe(i==1?ochre:trim,c-along*length*.5f,c+along*length*.5f,radius);
            }
            Box(trim,new Vector3(face.x,3.8f,face.z)+normal*.22f,Size(.48f,.1f,length));
            for(int i=0;i<3;i++)
            {var c=new Vector3(face.x,3.88f,face.z)+normal*(.1f+i*.12f);Pipe(i==1?ochre:trim,c-along*length*.5f,c+along*length*.5f,.035f);}
            SectorWall(face,along,normal,length);
            int count=Mathf.Max(1,Mathf.RoundToInt(length/P("detail.ribSpacing")));
            for(int i=0;i<count;i++)
            {
                var c=face+along*((i+.5f)*length/count-length*.5f);
                Box(trim,new Vector3(c.x,1.3f,c.z)+normal*.09f,Size(.2f,3.6f,.18f));
                // Angled haunches and foot chamfers make an octagonal portal language.
                Beam(trim,new Vector3(c.x,3.05f,c.z)+normal*.1f,new Vector3(c.x,4.55f,c.z)+normal*.85f,.22f,.24f);
                Beam(trim,new Vector3(c.x,-.5f,c.z)+normal*.35f,new Vector3(c.x,.15f,c.z)+normal*.1f,.25f,.25f);
                int sector=SectorAt(c);
                Box(sector>=0?sectors[sector]:ochre,new Vector3(c.x,.65f,c.z)+normal*.205f,Size(.03f,.8f,.19f));
                for(int pipe=0;pipe<3;pipe++)Box(steel,new Vector3(c.x,2.6f+pipe*.38f,c.z)+normal*.2f,Size(.45f,.055f,.13f));
                if(i%2==0)Box(steel,new Vector3(c.x,1.65f,c.z)+normal*.2f,Size(.3f,.55f,.44f));
            }
        }
        void Fixture(Vector3 c,bool high,bool shadow)
        {
            Box(trim,c,new Vector3(high?2.7f:1.4f,.16f,high?1.15f:.42f));Box(high?white:warm,c-Vector3.up*.09f,new Vector3(high?2.5f:1.2f,.03f,high?.95f:.26f));
            var go=new GameObject(high?"White base light":"Amber tunnel light");go.transform.SetParent(transform,false);go.transform.localPosition=c-Vector3.up*.2f;
            var l=go.AddComponent<Light>();l.type=LightType.Point;l.color=high?new Color(.85f,.93f,1):new Color(1,.67f,.32f);l.intensity=P(high?"light.white":"light.warm");l.range=P("light.range")+(high?3:0);l.shadows=shadow&&LightCount==0?LightShadows.Soft:LightShadows.None;LightCount++;
        }
        static Mesh Cube()
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            foreach(var n in new[]{Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back})
            {var u=Vector3.Cross(n,Mathf.Abs(n.y)>.5f?Vector3.forward:Vector3.up);var w=Vector3.Cross(n,u);int k=v.Count;foreach(var q in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)}){v.Add((n+u*q.x+w*q.y)*.5f);uv.Add((q+Vector2.one)*.5f);}t.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}
            var m=new Mesh{name="Tunnel kit cube"};m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateNormals();return m;
        }
        static Mesh Cylinder()
        {
            const int sides=12;var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            for(int i=0;i<=sides;i++){float a=i*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(a),-.5f,Mathf.Sin(a)));v.Add(new Vector3(Mathf.Cos(a),.5f,Mathf.Sin(a)));uv.Add(new Vector2(i/(float)sides,0));uv.Add(new Vector2(i/(float)sides,1));if(i<sides){int k=i*2;t.AddRange(new[]{k,k+1,k+2,k+1,k+3,k+2});}}
            var m=new Mesh{name="Tunnel twelve-sided pipe"};m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateNormals();return m;
        }
        void OnDestroy(){RenderSettings.ambientLight=oldAmbient;RenderSettings.ambientMode=oldMode;foreach(var o in owned)if(o)DestroyImmediate(o);}
    }
}
