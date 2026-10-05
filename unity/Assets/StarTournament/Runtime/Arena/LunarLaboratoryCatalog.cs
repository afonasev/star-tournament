using System;
using System.Collections.Generic;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    /// <summary>Plan v4, north = +Z. Metres; physical geometry owns all obstructions.</summary>
    public static class LunarLaboratoryCatalog
    {
        public const string Id="lunar-laboratory-v1", Revision="7", Identity=Id+"@"+Revision;
        public const float Upper=4.5f;
        public static ArenaDefinition Build()=>Draft();
        public static ProvingProfile AuthoringProfile()=>ProvingProfile.CreateLunarAuthoring(Draft());
        internal static ProvingProfile BeforeOpenCentreAuthoring()=>ProvingProfile.CreateLunarAuthoring(Draft(true,true,beforeBypass:true),1);
        internal static ProvingProfile BeforeInteriorAuthoring()=>ProvingProfile.CreateLunarAuthoring(Draft(false,true,beforeBypass:true),1);
        internal static ProvingProfile BeforeFlushAuthoring()=>ProvingProfile.CreateLunarAuthoring(Draft(false,false,true,true),1);
        internal static ProvingProfile BeforeBypassAuthoring()=>ProvingProfile.CreateLunarAuthoring(Draft(beforeBypass:true),1);
        static ArenaDefinition Draft(bool includeFormerCentre=false,bool beforeInterior=false,bool beforeFlush=false,bool beforeBypass=false)
        {
            var s=new List<ArenaSolid>();var ts=new List<NativeNavigationTransition>();
            void Box(string id,Vector3 c,Vector3 size,string mat="ivory",string support="",string surface=null,int layer=ProvingArena.WorldLayer)
                =>s.Add(new ArenaSolid{Id=id,Position=c,Size=size,Rotation=Quaternion.identity,Material=mat,Support=support,Surface=surface,Layer=layer});
            void Floor(string id,float x,float z,float w,float d,float y,string support,string surface=null)
                =>Box(id,new Vector3(x,y-.15f,z),new Vector3(w,.3f,d),"floor",support,surface,surface=="lunar-grate"?ProvingArena.MovementOnlyLayer:ProvingArena.WorldLayer);
            void Wall(string id,float x,float z,float w,float d,float y=0,float h=4.2f)
                =>Box(id,new Vector3(x,y+h*.5f,z),new Vector3(w,h,d));
            Floor("yard",0,0,64,64,0,"support:ground","lunar-sand");
            Floor("lower",0,0,24,24,.02f,"support:ground","lunar-graphite");
            Floor("upper",0,0,24,24,Upper,"support:upper","lunar-light");
            Floor("roof",0,0,24.5f,24.5f,9,"support:roof","lunar-light");
            // Walls are segmented around approved doors; never invisible doorway colliders.
            foreach(int side in new[]{-1,1})
            {
                string k=side<0?"s":"n";
                Wall(k+"-ground-left",-7,side*12,10,.35f);Wall(k+"-ground-right",7,side*12,10,.35f);
                Wall(k+"-ground-header",0,side*12,4,.35f,3,1.2f);
                // Upper north has two balcony doors; south has one.
                var spans=side>0?new[]{new Vector2(-12,-8),new Vector2(-5,5),new Vector2(8,12)}:new[]{new Vector2(-12,-2),new Vector2(2,12)};
                int n=0;foreach(var span in spans)
                {
                    float x=(span.x+span.y)*.5f,w=span.y-span.x;
                    Wall(k+"-upper-sill-"+n,x,side*12,w,.3f,Upper,.85f);
                    Box(k+"-armored-window-"+n,new Vector3(x,Upper+2,side*12),new Vector3(w,2.3f,.12f),"glass",surface:"lunar-glass");
                    Wall(k+"-upper-cap-"+n,x,side*12,w,.3f,Upper+3.15f,1.05f);n++;
                }
                Wall(k+"-upper-door-header",0,side*12,24,.3f,Upper+3.2f,1);
                string wside=side<0?"w":"e";
                foreach(int half in new[]{-1,1})
                {
                    Wall(wside+"-ground-"+half,side*12,half*7.75f,.35f,8.5f);
                    Wall(wside+"-open-sill-"+half,side*12,half*7.75f,.35f,8.5f,Upper,.8f);
                    Wall(wside+"-open-header-"+half,side*12,half*7.75f,.35f,8.5f,Upper+3.2f,1);
                    Wall(wside+"-open-pillar-"+half,side*12,half*11.8f,.45f,.45f,Upper,4.2f);
                }
                // Closed switchback annex: two independent ordered marches and a broad landing.
                Floor(wside+"-annex-floor",side*16.5f,0,9,7,0,"support:ground","lunar-graphite");
                Wall(wside+"-stair-spine",side*15,0,6,.22f,0,4.2f);
                Floor(wside+"-mid",side*19.5f,0,3,7,Upper*.5f,"support:"+wside+"-mid","lunar-light");
                Floor(wside+"-annex-roof",side*16.5f,0,9,7,9,"support:roof","lunar-light");
                Wall(wside+"-annex-back",side*21,0,.35f,7,0,9);
                foreach(int z in new[]{-1,1})Wall(wside+"-annex-side-"+z,side*16.5f,z*3.5f,9,.35f,0,9);
                void Stairs(string id,Vector3 low,Vector3 high,float width,string lower,string upper,int count)
                {
                    var run=high-low;run.y=0;float length=run.magnitude;var dir=run.normalized;float rise=(high.y-low.y)/count;
                    var feet=new List<Vector3>{low-dir*.2f};
                    for(int i=0;i<count;i++)
                    {
                        float top=low.y+rise*(i+1);var c=low+dir*(length/count*(i+.5f));c.y=top-.12f;
                        var size=Mathf.Abs(dir.x)>.5f?new Vector3(length/count,.24f,width):new Vector3(width,.24f,length/count);
                        Box(id+"-step-"+i,c,size,"floor",id,"lunar-step");feet.Add(new Vector3(c.x,top,c.z));
                    }
                    feet.Add(high+dir*.2f);ts.Add(new NativeNavigationTransition{Id=id,LowerSupport=lower,UpperSupport=upper,OrderedFeet=feet.ToArray()});
                }
                Stairs("transition:"+wside+"-inside-low",new Vector3(side*12,0,1.75f),new Vector3(side*18,Upper*.5f,1.75f),3.15f,"support:ground","support:"+wside+"-mid",9);
                Stairs("transition:"+wside+"-inside-high",new Vector3(side*18,Upper*.5f,-1.75f),new Vector3(side*12,Upper,-1.75f),3.15f,"support:"+wside+"-mid","support:upper",9);
                // Perimeter balcony follows the union of building and annex footprints.
                Floor(wside+"-balcony-side-n",side*13.5f,9.25f,3,11.5f,Upper,"support:upper","lunar-grate");
                Floor(wside+"-balcony-side-s",side*13.5f,-9.25f,3,11.5f,Upper,"support:upper","lunar-grate");
                Floor(wside+"-balcony-annex-n",side*18,5,12,3,Upper,"support:upper","lunar-grate");
                Floor(wside+"-balcony-annex-s",side*18,-5,12,3,Upper,"support:upper","lunar-grate");
                Floor(wside+"-balcony-annex-end",side*22.5f,0,3,7,Upper,"support:upper","lunar-grate");
                float zside=-side;
                Stairs("transition:"+wside+"-outside",new Vector3(side*18,0,zside*17.3f),new Vector3(side*18,Upper,zside*6.5f),3.2f,"support:ground","support:upper",18);
                // External stair stringers and handrails are physical authored metal, not decorative blockers.
                foreach(int edge in new[]{-1,1})
                {
                    var low=new Vector3(side*18+edge*1.65f,0,zside*17.3f);var high=new Vector3(low.x,Upper,zside*6.5f);
                    void Beam(string name,Vector3 a,Vector3 b,float width,float depth)
                    {Box(name,(a+b)*.5f,new Vector3(width,depth,Vector3.Distance(a,b)),"metal");s[s.Count-1].Rotation=Quaternion.LookRotation(b-a);}
                    Beam(wside+"-outer-handrail-"+edge,low+Vector3.up*.95f,high+Vector3.up*.95f,.08f,.08f);
                    Beam(wside+"-outer-stringer-"+edge,low-Vector3.up*.2f,high-Vector3.up*.2f,.13f,.24f);
                    for(int i=0;i<=6;i++)Box(wside+"-outer-post-"+edge+"-"+i,Vector3.Lerp(low,high,i/6f)+Vector3.up*.45f,new Vector3(.07f,.9f,.07f),"metal");
                }
                // Fence defines play boundary. Vertical slats have actual shot collisions.
                Wall("fence-base-x"+side,side*32,0,.3f,64,0,.65f);Wall("fence-base-z"+side,0,side*32,64,.3f,0,.65f);
                Wall("fence-rail-x"+side,side*32,0,.12f,64,2.8f,.1f);Wall("fence-rail-z"+side,0,side*32,64,.12f,2.8f,.1f);
                for(int q=-32;q<=32;q++)
                {Wall("fence-x"+side+"-"+q,side*32,q,.09f,.09f,.65f,2.2f);Wall("fence-z"+side+"-"+q,q,side*32,.09f,.09f,.65f,2.2f);}
            }
            Floor("balcony-n",0,13.5f,24,3,Upper,"support:upper","lunar-grate");Floor("balcony-s",0,-13.5f,24,3,Upper,"support:upper","lunar-grate");
            // Authored grating cells: continuous movement-only support, real shot-blocking metal bars.
            // Fixed .2m kit pitch and .035m cross-section are physical asset geometry, exported in authoring metadata.
            foreach(var deck in s.ToArray())if(deck.Surface=="lunar-grate")
            {
                int i=0;for(float x=-deck.Size.x*.5f+.025f;x<deck.Size.x*.5f;x+=.2f)
                    Box(deck.Id+"-bar-x-"+(i++),deck.Position+new Vector3(x,.1f,0),new Vector3(.035f,.1f,deck.Size.z),"metal",deck.Support);
                i=0;for(float z=-deck.Size.z*.5f+.025f;z<deck.Size.z*.5f;z+=.2f)
                    Box(deck.Id+"-bar-z-"+(i++),deck.Position+new Vector3(0,.1f,z),new Vector3(deck.Size.x,.1f,.035f),"metal",deck.Support);
            }
            void Rail(string id,Vector3 a,Vector3 b)
            {
                var delta=b-a;float length=delta.magnitude;bool alongX=Mathf.Abs(delta.x)>.1f;
                Box("rail-"+id,(a+b)*.5f+Vector3.up*.9f,alongX?new Vector3(length,.07f,.07f):new Vector3(.07f,.07f,length),"metal");
                int n=Mathf.CeilToInt(length/2);for(int i=0;i<=n;i++)Box("rail-"+id+"-post-"+i,Vector3.Lerp(a,b,i/(float)n)+Vector3.up*.45f,new Vector3(.08f,.9f,.08f),"metal");
            }
            foreach(int a in new[]{-1,1})
            {
                Rail("north-south-"+a,new Vector3(-15,Upper,a*15),new Vector3(15,Upper,a*15));
                Rail("annex-end-"+a,new Vector3(a*24,Upper,-6.5f),new Vector3(a*24,Upper,6.5f));
                foreach(int z in new[]{-1,1})
                {
                    Rail("side-"+a+"-"+z,new Vector3(a*15,Upper,z*6.5f),new Vector3(a*15,Upper,z*15));
                    // Opening aligned with the external staircase, no rail across its arrival.
                    if(z==-a){Rail("annex-inner-"+a+"-"+z,new Vector3(a*15,Upper,z*6.5f),new Vector3(a*16.35f,Upper,z*6.5f));Rail("annex-outer-"+a+"-"+z,new Vector3(a*19.65f,Upper,z*6.5f),new Vector3(a*24,Upper,z*6.5f));}
                    else Rail("annex-full-"+a+"-"+z,new Vector3(a*15,Upper,z*6.5f),new Vector3(a*24,Upper,z*6.5f));
                }
            }
            // Continuous armoured strip between asymmetric screens.
            Wall("divider-base",0,0,16,.3f,0,1.05f);Box("divider-glass",new Vector3(0,1.8f,0),new Vector3(16,1.5f,.12f),"glass",surface:"lunar-glass");Wall("divider-top",0,0,16,.3f,2.55f,1.65f);
            Wall("screen-west",-8,-1.75f,.3f,3.8f);Wall("screen-east",8,1.75f,.3f,3.8f);
            // Storage rooms NW / SE, two open portals each.
            foreach(int a in new[]{-1,1})
            {Wall("store-long-"+a,a*10.25f,-a*6,3.5f,.25f);Wall("store-short-"+a,a*6,-a*10.5f,.25f,3);Wall("store-corner-"+a,a*6,-a*6.5f,.25f,1);}
            // Upper corridor, broad portals into each room, wide common north door.
            foreach(int a in new[]{-1,1})
            {Wall("lab-front-outer-"+a,a*10,3.5f,4,.25f,Upper);Wall("lab-front-inner-"+a,a*2.5f,3.5f,5,.25f,Upper);}
            Wall("labs-common-n",0,10,.25f,4,Upper);Wall("labs-common-s",0,4,.25f,1,Upper);
            Wall("hall-front-w",-7.5f,-3.5f,9,.25f,Upper);Wall("hall-front-e",7.5f,-3.5f,9,.25f,Upper);
            // Explicit equipment solids: presentation adds no unannounced obstacles.
            foreach(var c in new[]{new Vector3(-3,0,7),new Vector3(7,0,7),new Vector3(-3,0,-7),new Vector3(3,0,-6),new Vector3(-25,0,10),new Vector3(25,0,-10),new Vector3(-7,0,24),new Vector3(7,0,-24)})
                Box("equipment-"+s.Count,c+Vector3.up*(c.x==7&&c.z==7?1.1f:.65f),new Vector3(2.3f,c.x==7&&c.z==7?2.2f:1.3f,1.4f),"equipment");
            // F1 cargo walls: authored colliders also feed navigation and Balance Lab geometry.
            // Keep the centre pickup strip, storage portals and corner spawns clear.
            void Cargo(string id,float x,float z,float w,float h,float d)
                =>Box("cargo-"+id,new Vector3(x,.02f+h*.5f,z),new Vector3(w,h,d),"equipment");
            // F4 removes these two solids from the live map. Retain the prior registry only
            // to read existing Balance Lab snapshots without rewriting their hashes.
            if(includeFormerCentre)
            {
                Cargo("north-spine",2.8f,6.4f,1.6f,2.8f,4.2f);
                Cargo("north-low",-.7f,8.7f,2.2f,.85f,1.5f);
            }
            Cargo("north-west",-4.4f,4.3f,2.8f,2.5f,1.3f);
            Cargo("north-east",7.8f,beforeBypass?4.6f:9f,2.5f,1.7f,1.4f);
            Cargo("south-spine",-4.5f,-6.6f,1.5f,2.9f,4.6f);
            Cargo("south-east",2.8f,-9,2,2.6f,1.6f);
            Cargo("south-low",-.2f,-5.6f,1.4f,.85f,1.5f);
            Cargo("south-west",-8.6f,beforeBypass?-5.4f:-7.6f,2.5f,1.8f,1.2f);
            // F2 adds distinct islands of cargo; centre line, storage doors and spawn pockets stay open.
            Cargo("f2-north-entry-w",-2.8f,10.5f,1.2f,1.3f,1.4f);
            Cargo("f2-north-entry-e",3.1f,10.5f,1.3f,1.1f,1.2f);
            Cargo("f2-north-inner",-.8f,4.3f,1.1f,2.4f,1.2f);
            Cargo("f2-north-east-wall",10.6f,7.1f,.8f,2.7f,1.2f);
            Cargo("f2-north-west-store",-10.4f,10.8f,1.2f,2.3f,1.1f);
            Cargo("f2-north-east-stack",5.2f,9.4f,1.1f,1.6f,1.4f);
            Cargo("f2-south-entry-w",-2.5f,-10.5f,1.3f,1.2f,1.3f);
            Cargo("f2-south-entry-e",.2f,-9.1f,1.2f,1.8f,1.1f);
            Cargo("f2-south-inner",3.7f,-3.4f,1.6f,2.5f,1.3f);
            Cargo("f2-south-west-stack",-7.3f,-9.4f,1.1f,2.6f,1.2f);
            Cargo("f2-south-east-store",10.6f,-10.8f,1.1f,2.4f,1.1f);
            // F10: retain cargo while clearing the capsule bypass beside the closed stair wall.
            Cargo("f2-south-west-wall",-10.5f,beforeBypass?-3.8f:-8f,1.1f,1.7f,1.5f);
            foreach(int side in new[]{-1,1})
            {
                foreach(int row in new[]{-1,1})
                {
                    Cargo("f2-yard-main-"+side+"-"+row,side*21,row*21,3.2f,2.8f,2.1f);
                    Cargo("f2-yard-low-"+side+"-"+row,side*24.6f,row*21,2.1f,1.1f,1.7f);
                    Cargo("f2-yard-end-"+side+"-"+row,side*21,row*24.4f,1.6f,1.8f,1.5f);
                    Cargo("f2-yard-building-"+side+"-"+row,side*10,row*18,2.8f,1.5f,1.9f);
                    Box("f2-service-duct-"+side+"-"+row,new Vector3(side*30,.5f,row*15),new Vector3(.9f,1,9),"equipment");
                }
            }
            Box("scanner",new Vector3(-8,Upper+.55f,7),new Vector3(2.2f,1.1f,2.2f),"equipment");
            Box("samples",new Vector3(10.8f,Upper+1.1f,7),new Vector3(.8f,2.2f,4),"equipment");
            if(!beforeInterior)
            {
                // F6 retains perimeter cover, removing the dense centre on both halves.
                var removed=new HashSet<string>{"cargo-north-west","cargo-south-spine","cargo-south-east","cargo-south-low","cargo-f2-north-entry-w","cargo-f2-north-entry-e","cargo-f2-north-inner","cargo-f2-north-east-stack","cargo-f2-south-entry-w","cargo-f2-south-entry-e","cargo-f2-south-inner"};
                s.RemoveAll(v=>removed.Contains(v.Id)||(v.Id.StartsWith("equipment-")&&Mathf.Abs(v.Position.x)<8&&Mathf.Abs(v.Position.z)<12));
                // Peripheral spawn cover retains the hidden S5/S6 contract through optical glass.
                Box("cargo-ground-ne-cover",new Vector3(9,1.12f,8.3f),new Vector3(2.3f,2.2f,1.4f),"equipment");
                Box("cargo-upper-nw",new Vector3(-8.8f,Upper+.7f,10.7f),new Vector3(1.4f,1.4f,1.4f),"equipment");
                Box("cargo-upper-se",new Vector3(8.8f,Upper+.7f,-10.7f),new Vector3(1.4f,1.4f,1.4f),"equipment");
                // Solid riser volumes close every underside without changing any tread top.
                foreach(var step in s.ToArray())if(step.Id.Contains("-inside-")&&step.Surface=="lunar-step")
                {
                    float top=step.Position.y-step.Size.y*.5f;
                    if(top<=.02f)continue; // First riser is already enclosed by the ground slab.
                    Box(step.Id+"-infill",new Vector3(step.Position.x,top*.5f,step.Position.z),new Vector3(step.Size.x,top,step.Size.z));
                }
                foreach(int side in new[]{-1,1})
                {
                    if(!beforeFlush)
                    {
                        // Derive the front closure from adjoining canonical bounds, not a visual offset.
                        // Its top meets the slab underside, avoiding coplanar overlap with the upper floor.
                        string prefix=side<0?"w":"e";
                        var wall=s.Find(v=>v.Id==prefix+"-ground--1");
                        var spine=s.Find(v=>v.Id==prefix+"-stair-spine");
                        var slab=s.Find(v=>v.Id=="upper");
                        float lowZ=wall.Position.z+wall.Size.z*.5f;
                        float highZ=spine.Position.z;
                        float top=slab.Position.y-slab.Size.y*.5f;
                        Box(prefix+"-front-infill",new Vector3(wall.Position.x,top*.5f,(lowZ+highZ)*.5f),new Vector3(wall.Size.x,top,highZ-lowZ));
                    }
                    Box((side<0?"w":"e")+"-landing-infill",new Vector3(side*19.5f,.975f,0),new Vector3(3,1.95f,7));
                    Wall("store-header-long-"+side,side*7.25f,-side*6,2.5f,.25f,3,1.2f);
                    Wall("store-header-short-"+side,side*6,-side*8,.25f,2,3,1.2f);
                    Wall("stair-ground-header-"+side,side*12,1.75f,.35f,3.5f,3,1.2f);
                    Wall("stair-upper-header-"+side,side*12,0,.35f,7,Upper+3,1.2f);
                    Wall("lab-door-header-"+side,side*6.5f,3.5f,3,.25f,Upper+3,1.2f);
                    for(int q=-28;q<=28;q+=14)
                    {
                        Box("lamp-x-"+side+"-"+q,new Vector3(side*31,2,q),new Vector3(.24f,4,.24f),"metal");
                        Box("lamp-z-"+side+"-"+q,new Vector3(q,2,side*31),new Vector3(.24f,4,.24f),"metal");
                    }
                }
                Wall("labs-common-header",0,6.25f,.25f,3.5f,Upper+3,1.2f);
                Wall("hall-door-header",0,-3.5f,6,.25f,Upper+3,1.2f);
            }
            var spawns=new[]{new Vector3(-4,0,18),new Vector3(4,0,-18),new Vector3(-26,0,0),new Vector3(26,0,0),new Vector3(10,.02f,10),new Vector3(-10,.02f,-10),new Vector3(-3,Upper,10),new Vector3(3,Upper,10)};
            var regions=new List<ArenaSpawnRegion>();for(int i=0;i<8;i++)regions.Add(new ArenaSpawnRegion{Id="S"+(i+1),Center=spawns[i],Size=new Vector3(1,0,1),Support=i<4?"support:ground":i<6?"support:ground":"support:upper"});
            var picks=new List<ArenaPickupDefinition>();
            void Pick(string id,ArenaPickupKind kind,float x,float y,float z,string support)=>picks.Add(new ArenaPickupDefinition{Id=id,Kind=kind,Anchor=new Vector3(x,y,z),Support="support:"+(support=="yard"||support=="lower"?"ground":support=="balcony"?"upper":support)});
            Pick("W1",ArenaPickupKind.Shotgun,-9,.02f,9,"lower");Pick("W2",ArenaPickupKind.Shotgun,9,.02f,-9,"lower");
            Pick("W3",ArenaPickupKind.Pulse,28,0,-28,"yard");Pick("W4",ArenaPickupKind.Pulse,-28,0,28,"yard");
            Pick("W5",ArenaPickupKind.Cutter,-13.5f,Upper,13.5f,"balcony");Pick("W6",ArenaPickupKind.Cutter,13.5f,Upper,-13.5f,"balcony");
            Pick("A1",ArenaPickupKind.Armor,28,0,28,"yard");Pick("A2",ArenaPickupKind.Armor,-28,0,-28,"yard");
            Pick("H",ArenaPickupKind.FullHeal,0,.02f,1.5f,"lower");Pick("V",ArenaPickupKind.Speed,0,.02f,-1.5f,"lower");Pick("D",ArenaPickupKind.Damage,0,Upper,-7,"upper");
            return new ArenaDefinition{MapId=Id,Revision=Revision,Identity=Identity,ProfileFingerprint="authored:"+Identity,Solids=s.ToArray(),Spawns=spawns,SpawnRegions=regions.ToArray(),Pickups=picks.ToArray(),Transitions=ts.ToArray(),RouteAnchors=new[]{spawns[0],new Vector3(0,Upper,0)}};
        }
    }
}
