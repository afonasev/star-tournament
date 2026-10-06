using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace StarTournament.ProvingGround
{
    public sealed partial class OrbitalLeaguePresentation
    {
        // Authored mesh detailing: bolts, seams and repair patterns are fixed art topology,
        // not gameplay tuning. Readability controls live in the veteran profile; solids in catalog.
        void VeteranSlice(ArenaDefinition definition)
        {
            var paint=VeteranMaterial("Veteran / patched ivory",new Color(.58f,.6f,.55f),0);
            var fresh=VeteranMaterial("Veteran / replacement enamel",new Color(.72f,.69f,.59f),0);
            var steel=VeteranMaterial("Veteran / brushed steel",new Color(.26f,.3f,.32f),1);
            var rubber=VeteranMaterial("Veteran / court rubber",Color.white*p.Get("veteran.hallFloorValue"),2);
            var teal=VeteranMaterial("Veteran / league petrol enamel",new Color(.12f,.29f,.32f),0);
            var orange=VeteranMaterial("Veteran / worn ochre markings",new Color(.69f,.42f,.16f),0);
            var dark=VeteranMaterial("Veteran / service recess",new Color(.075f,.095f,.11f),2);
            var warm=Flat("Veteran / service lamp",new Color(1,.76f,.43f));
            warm.EnableKeyword("_EMISSION");warm.SetColor("_EmissionColor",new Color(1,.65f,.3f));
            void B(Material m,Vector3 pos,Vector3 size)=>Box(m,pos,size,Quaternion.identity);
            // All four walls receive the same large-scale material hierarchy. Panel seams and
            // shallow cladding never supply an invisible obstacle or a new navigable surface.
            foreach(var solid in definition.Solids)
            {
                if(!solid.Id.StartsWith("hall-") || solid.Id.Contains("roof") || solid.Id.Contains("rim") || solid.Id.Contains("lintel"))continue;
                bool end=solid.Id.Contains("north-left")||solid.Id.Contains("north-right")||solid.Id.Contains("south-left")||solid.Id.Contains("south-right");
                bool side=solid.Id.Contains("west-")||solid.Id.Contains("east-");if(!end&&!side)continue;
                var normal=end?new Vector3(0,0,-Mathf.Sign(solid.Position.z)):new Vector3(-Mathf.Sign(solid.Position.x),0,0);
                var along=end?Vector3.right:Vector3.forward;
                float length=end?solid.Size.x:solid.Size.z;
                var face=solid.Position+normal*(end?solid.Size.z:solid.Size.x)*.5f;
                int count=Mathf.Max(1,Mathf.RoundToInt(length/1.25f));
                for(int i=0;i<count;i++)
                {
                    var at=face+along*((i+.5f)*length/count-length*.5f);
                    B(i%4==1?fresh:paint,new Vector3(at.x,6.25f,at.z)+normal*.026f,end?new Vector3(length/count-.045f,3.35f,.04f):new Vector3(.04f,3.35f,length/count-.045f));
                    B(teal,new Vector3(at.x,4.52f,at.z)+normal*.035f,end?new Vector3(length/count-.03f,1.02f,.06f):new Vector3(.06f,1.02f,length/count-.03f));
                    // Distinct access plate instead of uniform noisy tiling.
                    if(i%3==0)B(steel,new Vector3(at.x,5.35f,at.z)+normal*.06f,end?new Vector3(.65f,.22f,.025f):new Vector3(.025f,.22f,.65f));
                }
                B(steel,new Vector3(face.x,4.13f,face.z)+normal*.075f,end?new Vector3(length,.22f,.1f):new Vector3(.1f,.22f,length));
            }
            // Tall paired portals: thin vertical trim emphasizes the full 3.5 m clear height.
            foreach(int zsign in new[]{-1,1})
            {
                float z=zsign*7.965f;
                foreach(int side in new[]{-1,1})
                {
                    B(steel,new Vector3(side*2.22f,5.75f,z),new Vector3(.12f,3.5f,.08f));
                    B(warm,new Vector3(side*2.29f,5.75f,z-zsign*.05f),new Vector3(p.Get("veteran.portalLightWidth"),3.35f,.025f));
                }
                B(teal,new Vector3(0,7.75f,z),new Vector3(4.38f,.44f,.06f));
                Label(zsign>0?"02 / NORTH":"04 / SOUTH",new Vector3(0,7.75f,z-zsign*.05f),Quaternion.Euler(0,zsign>0?180:0,0),.32f,HasWayfinding?Palette(zsign>0?"north":"south"):Palette("ink"));
            }
            // Columns become repaired arena infrastructure; their existing collision stays exact.
            foreach(var solid in definition.Solids)
            {
                if(!solid.Id.StartsWith("column-",StringComparison.Ordinal))continue;
                transform.parent.Find(solid.Id).GetComponent<Renderer>().sharedMaterial=paint;
                float band=p.Get("veteran.columnBandHeight");
                foreach(int face in new[]{-1,1})
                {
                    B(teal,new Vector3(solid.Position.x+face*.755f,4+band*.5f,solid.Position.z),new Vector3(.015f,band,1.51f));
                    B(teal,new Vector3(solid.Position.x,4+band*.5f,solid.Position.z+face*.755f),new Vector3(1.51f,band,.015f));
                    B(steel,new Vector3(solid.Position.x+face*.765f,7.7f,solid.Position.z),new Vector3(.025f,.42f,1.53f));
                    B(steel,new Vector3(solid.Position.x,7.7f,solid.Position.z+face*.765f),new Vector3(1.53f,.42f,.025f));
                    B(orange,new Vector3(solid.Position.x,4+band+.1f,solid.Position.z+face*.77f),new Vector3(1.54f,.1f,.02f));
                    Label(solid.Position.z>0?"02":"04",new Vector3(solid.Position.x,6.45f,solid.Position.z+face*.78f),Quaternion.Euler(0,face<0?180:0,0),.65f,Palette("ink"));
                }
            }
            // Flush court surface: large calm rubber fields, central pickup remains uncovered.
            foreach(int side in new[]{-1,1})
            {
                B(rubber,new Vector3(side*4.6f,4.007f,0),new Vector3(2.65f,.01f,15.8f));
                B(rubber,new Vector3(0,4.007f,side*4.8f),new Vector3(3.35f,.01f,6.2f));
                B(orange,new Vector3(side*1.74f,4.017f,0),new Vector3(.065f,.008f,15.7f));
                for(int i=0;i<5;i++)B(paint,new Vector3(side*4.65f,4.017f,-5.8f+i*2.8f),new Vector3(.7f,.008f,.07f));
            }
            // High services follow the long walls, leaving the door silhouette and centre open.
            float serviceY=8-p.Get("veteran.ceilingServiceDepth");
            foreach(int side in new[]{-1,1})
            {
                for(int line=0;line<2;line++)VeteranPipe(steel,new Vector3(side*(5.35f-line*.3f),serviceY,-7.7f),new Vector3(side*(5.35f-line*.3f),serviceY,7.7f),.075f);
                for(int i=0;i<11;i++)
                {
                    float z=-7.3f+i*1.45f;
                    B(steel,new Vector3(side*5.2f,serviceY+.04f,z),new Vector3(1.15f,.12f,.075f));
                    B(orange,new Vector3(side*5.35f,serviceY,z),new Vector3(.18f,.19f,.08f));
                }
            }
            // North service corner retains its real cabinet and edge-bound transport cover.
            var cabinet=transform.parent.Find("veteran-service-cabinet");cabinet.GetComponent<Renderer>().sharedMaterial=steel;
            B(paint,new Vector3(-4.65f,5.35f,7.655f),new Vector3(1.13f,1.59f,.025f));
            B(dark,new Vector3(-4.65f,5.72f,7.63f),new Vector3(.6f,.25f,.025f));
            B(warm,new Vector3(-4.88f,5.72f,7.61f),new Vector3(.055f,.055f,.018f));
            B(steel,new Vector3(-4.28f,5.25f,7.62f),new Vector3(.045f,.24f,.03f));
            for(int i=0;i<7;i++)B(dark,new Vector3(-4.65f,4.8f+i*.055f,7.62f),new Vector3(.68f,.018f,.012f));
            B(steel,new Vector3(-4.65f,6.4f,7.72f),new Vector3(1.5f,.14f,.4f));
            B(warm,new Vector3(-4.65f,6.32f,7.63f),new Vector3(1.23f,.035f,.2f));
            Label("SERVICE / 07",new Vector3(-4.65f,6.08f,7.59f),Quaternion.Euler(0,180,0),.23f,Palette("ink"));
            BasementVent(new Vector3(-4.65f,7.1f,7.89f),Quaternion.identity);
            VeteranPipe(steel,new Vector3(-5.6f,6.25f,7.72f),new Vector3(-5.6f,7.84f,7.72f),.09f);
            VeteranSteam(new Vector3(-5.6f,6.25f,7.72f));
            // South equipment wall is distinct: flush dark cooling bank and patched enamel.
            for(int side=-1;side<=1;side+=2)
            {
                B(dark,new Vector3(side*4.2f,6.25f,-7.925f),new Vector3(2.65f,1.55f,.045f));
                for(int i=0;i<12;i++)B(steel,new Vector3(side*4.2f,5.65f+i*.11f,-7.885f),new Vector3(2.5f,.045f,.03f));
                Label(side<0?"AIR / 03":"POWER / 04",new Vector3(side*4.2f,7.23f,-7.9f),Quaternion.identity,.3f,Palette("ink"));
            }
            foreach(var solid in definition.Solids)
            {
                if(!solid.Id.StartsWith("veteran-case-",StringComparison.Ordinal))continue;
                transform.parent.Find(solid.Id).GetComponent<Renderer>().sharedMaterial=teal;
                var c=solid.Position;var h=solid.Size*.5f;
                foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})B(steel,c+new Vector3(sx*(h.x-.04f),0,sz*(h.z-.04f)),new Vector3(.085f,solid.Size.y+.015f,.085f));
                foreach(float y in new[]{-h.y+.07f,h.y-.07f})foreach(int sz in new[]{-1,1})B(steel,c+new Vector3(0,y,sz*(h.z+.009f)),new Vector3(solid.Size.x,.09f,.025f));
                B(dark,c+new Vector3(0,0,-h.z-.014f),new Vector3(solid.Size.x*.45f,.22f,.018f));
                B(steel,c+new Vector3(0,.025f,-h.z-.03f),new Vector3(solid.Size.x*.3f,.04f,.025f));
                Label("OL / 07",c+new Vector3(0,-h.y*.5f,-h.z-.04f),Quaternion.Euler(0,180,0),.2f,Palette("white"));
            }
            var lamp=new GameObject("Veteran service light");lamp.transform.SetParent(transform,false);lamp.transform.localPosition=new Vector3(-4.65f,6.25f,7.25f);
            var light=lamp.AddComponent<Light>();light.type=LightType.Spot;lamp.transform.localRotation=Quaternion.LookRotation(new Vector3(0,-1,.45f));light.spotAngle=p.Get("light.spotAngle");light.color=new Color(1,.64f,.3f);light.intensity=p.Get("veteran.lightIntensity");light.range=p.Get("veteran.lightRange");light.shadows=LightShadows.None;LocalLightCount++;
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
