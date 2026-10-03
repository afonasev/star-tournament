using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace StarTournament.ProvingGround
{
    public sealed partial class OrbitalLeaguePresentation
    {
        // Fixed authored decorative layout and texture encoding. Gameplay dimensions live in the catalog.
        void VeteranSlice(ArenaDefinition definition)
        {
            var paint=VeteranMaterial("Veteran / patched ivory",new Color(.53f,.57f,.55f),0);
            var fresh=VeteranMaterial("Veteran / replacement enamel",new Color(.69f,.66f,.56f),0);
            var steel=VeteranMaterial("Veteran / brushed steel",new Color(.25f,.29f,.3f),1);
            var rubber=VeteranMaterial("Veteran / ribbed rubber",new Color(.105f,.13f,.145f),2);
            var casePaint=VeteranMaterial("Veteran / league transport cases",new Color(.31f,.37f,.35f),0);
            var orange=VeteranMaterial("Veteran / worn ochre markings",new Color(.65f,.37f,.1f),0);
            var dark=VeteranMaterial("Veteran / service recess",new Color(.065f,.08f,.095f),2);
            var warm=Flat("Veteran / service lamp",new Color(1,.67f,.31f));
            warm.EnableKeyword("_EMISSION");warm.SetColor("_EmissionColor",new Color(1,.5f,.16f));
            void B(Material m,Vector3 pos,Vector3 size)=>Box(m,pos,size,Quaternion.identity);
            // Repair panels are inset-looking assemblies on the opaque portal wings.
            foreach(int side in new[]{-1,1})
            {
                float x=side*4.15f;
                B(dark,new Vector3(x,6.04f,7.985f),new Vector3(3.55f,3.72f,.018f));
                for(int row=0;row<3;row++)for(int col=0;col<3;col++)
                {
                    var pos=new Vector3(x+(col-1)*1.13f,4.68f+row*1.08f,7.955f);
                    B(row==1&&col==0?fresh:(row+col)%4==0?steel:paint,pos,new Vector3(1.1f,1.035f,.05f));
                    foreach(int a in new[]{-1,1})foreach(int b in new[]{-1,1})
                        B(steel,pos+new Vector3(a*.46f,b*.42f,-.033f),new Vector3(.045f,.045f,.014f));
                }
                B(steel,new Vector3(x,4.17f,7.90f),new Vector3(3.55f,.3f,.16f));
                B(orange,new Vector3(side*2.31f,5.9f,7.91f),new Vector3(.13f,3.65f,.12f));
                // Flush mat overlays never create floor collision lips.
                B(rubber,new Vector3(x,4.009f,6.2f),new Vector3(3.5f,.012f,3.4f));
                for(int i=0;i<7;i++)B(orange,new Vector3(x-1.45f+i*.46f,4.02f,4.6f),new Vector3(.23f,.01f,.11f));
            }
            foreach(int side in new[]{-1,1})
            {
                Box(steel,new Vector3(side*3.8f,4.85f,7.88f),new Vector3(.62f,.3f,.035f),Quaternion.Euler(0,0,side*5));
                for(int i=0;i<6;i++)
                {
                    float x=side*(2.7f+(i%3)*.42f),y=5.05f+(i/3)*.22f;
                    Box(dark,new Vector3(x,y,7.923f),new Vector3(.17f+i*.027f,.014f,.006f),Quaternion.Euler(0,0,side*(18+i*7)));
                }
            }
            // Overhead exposed service pipes, clamped to the original north wall.
            for(int line=0;line<2;line++)
            {
                float y=7.35f+line*.32f,z=7.62f;
                foreach(int side in new[]{-1,1})VeteranPipe(steel,new Vector3(side*2.65f,y,z),new Vector3(side*5.8f,y,z),.09f);
                for(int i=0;i<8;i++)
                {
                    float x=-5.4f+i*1.55f;if(Mathf.Abs(x)<2.65f)continue;
                    VeteranPipe(orange,new Vector3(x-.05f,y,z),new Vector3(x+.05f,y,z),.12f);
                    B(steel,new Vector3(x,y,7.82f),new Vector3(.1f,.32f,.32f));
                }
            }
            VeteranPipe(steel,new Vector3(-5.55f,6.25f,7.64f),new Vector3(-5.55f,7.36f,7.64f),.09f);
            // Open ladder cable tray above the portal; small cabling sits in the tray.
            for(int i=0;i<18;i++)B(steel,new Vector3(-5.6f+i*.65f,7.91f,7.12f),new Vector3(.055f,.08f,.65f));
            foreach(float z in new[]{6.82f,7.42f})B(steel,new Vector3(0,7.88f,z),new Vector3(11.4f,.15f,.06f));
            for(int i=0;i<3;i++)VeteranPipe(rubber,new Vector3(-5.6f,7.92f,6.97f+i*.14f),new Vector3(5.6f,7.92f,6.97f+i*.14f),.035f);
            // Wall-mounted service cabinet and a protected fan, both outside the portal.
            transform.parent.Find("veteran-service-cabinet").GetComponent<Renderer>().sharedMaterial=steel;
            B(dark,new Vector3(-4.65f,5.35f,7.655f),new Vector3(1.1f,1.56f,.04f));
            B(paint,new Vector3(-4.65f,5.36f,7.62f),new Vector3(1.03f,1.49f,.03f));
            B(steel,new Vector3(-4.3f,5.25f,7.585f),new Vector3(.045f,.24f,.04f));
            for(int i=0;i<7;i++)B(dark,new Vector3(-4.65f,4.8f+i*.055f,7.597f),new Vector3(.68f,.018f,.012f));
            B(dark,new Vector3(-4.65f,5.72f,7.59f),new Vector3(.61f,.24f,.015f));
            B(warm,new Vector3(-4.88f,5.72f,7.575f),new Vector3(.06f,.06f,.018f));
            B(steel,new Vector3(-4.65f,6.35f,7.64f),new Vector3(1.5f,.16f,.35f));
            B(warm,new Vector3(-4.65f,6.26f,7.6f),new Vector3(1.24f,.035f,.22f));
            BasementVent(new Vector3(-2.98f,6.58f,7.87f),Quaternion.identity);
            fans[fans.Count-1].GetComponent<Renderer>().sharedMaterial=steel;
            Label("SERVICE / 07",new Vector3(-4.65f,6.03f,7.56f),Quaternion.Euler(0,180,0),.22f,Palette("white"));
            B(dark,new Vector3(4.15f,6.65f,7.86f),new Vector3(2.6f,.48f,.05f));
            Label("NORTH / 02",new Vector3(4.15f,6.65f,7.825f),Quaternion.Euler(0,180,0),.42f,HasWayfinding?Palette("north"):Palette("ink"));
            // Visible case bodies are exactly the canonical physical solids, with protective trim.
            foreach(var s in definition.Solids)
            {
                if(!s.Id.StartsWith("veteran-case-",StringComparison.Ordinal))continue;
                var body=transform.parent.Find(s.Id);body.GetComponent<Renderer>().sharedMaterial=casePaint;
                var c=s.Position;var h=s.Size*.5f;
                foreach(int signX in new[]{-1,1})foreach(int signZ in new[]{-1,1})
                    B(steel,c+new Vector3(signX*(h.x-.045f),0,signZ*(h.z-.045f)),new Vector3(.1f,s.Size.y+.018f,.1f));
                foreach(float y in new[]{-h.y+.07f,h.y-.07f})
                {
                    B(steel,c+new Vector3(0,y,-h.z-.012f),new Vector3(s.Size.x,.11f,.035f));
                    B(steel,c+new Vector3(0,y,h.z+.012f),new Vector3(s.Size.x,.11f,.035f));
                }
                for(int i=-1;i<=1;i++)B(steel,c+new Vector3(i*h.x*.65f,h.y+.012f,0),new Vector3(.07f,.025f,s.Size.z));
                B(dark,c+new Vector3(0,0,-h.z-.014f),new Vector3(s.Size.x*.5f,.24f,.018f));
                B(steel,c+new Vector3(0,.02f,-h.z-.03f),new Vector3(s.Size.x*.32f,.045f,.025f));
                B(orange,c+new Vector3(-h.x*.6f,.28f,-h.z-.02f),new Vector3(.15f,.26f,.02f));
                Label("OL / 07",c+new Vector3(.16f,-h.y*.45f,-h.z-.04f),Quaternion.Euler(0,180,0),.2f,Palette("white"));
            }
            var lamp=new GameObject("Veteran service light");lamp.transform.SetParent(transform,false);lamp.transform.localPosition=new Vector3(-4.65f,6.25f,7.25f);
            var light=lamp.AddComponent<Light>();light.type=LightType.Spot;lamp.transform.localRotation=Quaternion.LookRotation(new Vector3(0,-1,.45f));light.spotAngle=p.Get("light.spotAngle");light.color=new Color(1,.64f,.3f);light.intensity=p.Get("veteran.lightIntensity");light.range=p.Get("veteran.lightRange");light.shadows=LightShadows.None;LocalLightCount++;
            VeteranSteam(new Vector3(-5.55f,6.25f,7.62f));
        }
        Material VeteranMaterial(string name,Color color,int kind)
        {
            var material=Flat(name,color);material.SetFloat("_Smoothness",kind==1?.42f:1-p.Get("veteran.roughness"));material.SetFloat("_Metallic",kind==1?.7f:.12f);
            const int n=256;var texture=new Texture2D(n,n,TextureFormat.RGBA32,true){name=name+" / wear",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};owned.Add(texture);
            var pixels=new Color[n*n];var rng=new System.Random(7103+kind);float wear=p.Get("veteran.wear");
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float noise=(float)rng.NextDouble();float v=.86f+noise*.14f;
                if(kind==2)v*=((y/5)%2==0?.72f:1);
                if(kind==1)v*=.77f+.23f*Mathf.Abs(Mathf.Sin(y*1.7f));
                int edge=Mathf.Min(x,y,n-1-x,n-1-y);
                if(edge<3)v=.36f;
                else if(edge<9&&noise<wear*2)v=1.28f;
                if((y%43==0||y%67==1)&&noise<wear&&x>12&&x<222)v=.48f;
                pixels[y*n+x]=new Color(v,v,v,1);
            }
            texture.SetPixels(pixels);texture.Apply(true,true);material.SetTexture("_BaseMap",texture);return material;
        }
        void VeteranPipe(Material material,Vector3 from,Vector3 to,float radius)
        {
            // A reusable mesh-only cylinder avoids even transient decoration colliders.
            const int sides=12;var v=new Vector3[sides*2];var triangles=new int[sides*6];
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides;v[i]=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));v[i+sides]=v[i]+Vector3.up;
                int j=(i+1)%sides,k=i*6;triangles[k]=i;triangles[k+1]=i+sides;triangles[k+2]=j;triangles[k+3]=j;triangles[k+4]=i+sides;triangles[k+5]=j+sides;
            }
            var mesh=new Mesh{name="Veteran pipe section"};mesh.vertices=v;mesh.triangles=triangles;mesh.RecalculateNormals();owned.Add(mesh);
            if(!batches.TryGetValue(material,out var list))batches[material]=list=new System.Collections.Generic.List<CombineInstance>();
            list.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(from,Quaternion.FromToRotation(Vector3.up,to-from),new Vector3(radius,(to-from).magnitude,radius))});
        }
        void VeteranSteam(Vector3 position)
        {
            var go=new GameObject("Veteran intermittent service steam");go.transform.SetParent(transform,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.LookRotation(Vector3.down);
            var system=go.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main;main.duration=p.Get("veteran.steamInterval");main.loop=true;main.startLifetime=p.Get("veteran.steamLifetime");main.startSpeed=p.Get("veteran.steamSpeed");main.startSize=p.Get("veteran.steamSize");main.startColor=new Color(.8f,.85f,.87f,p.Get("veteran.steamOpacity"));main.maxParticles=8;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var emission=system.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,6)});
            var shape=system.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=10;shape.radius=.025f;
            var fade=system.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(0,1)});fade.color=gradient;
            // Existing Resources shader is retained in the Player and multiplies vertex alpha.
            var mat=new Material(Shader.Find("StarTournament/VectorParticle")){name="Veteran translucent steam"};owned.Add(mat);
            const int size=32;var t=new Texture2D(size,size);owned.Add(t);var pixels=new Color[size*size];for(int y=0;y<size;y++)for(int x=0;x<size;x++){float d=Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f))/15.5f;pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(1-d)*Mathf.Clamp01(1-d));}t.SetPixels(pixels);t.Apply();mat.SetTexture("_MainTex",t);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial=mat;system.Play();
        }
    }
}
