using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Published P4. Default authoring data is frozen per revision; gameplay/Lab edits never rebuild it.</summary>
    public static class CombatBowlCatalog
    {
        public const string Id="combat-bowl-v1", Revision="6", Identity=Id+"@"+Revision;
        public static bool Contains(string id)=>id==Id;
        public static ArenaFreezeSnapshot Freeze(ProvingProfile navigationProfile)=>ArenaFreezeSnapshot.Create(Build(),navigationProfile);
        public static ArenaDefinition Build()
        {
            var definition=Draft();
            // Every authored spatial value is registered in the same descriptor system as Balance Lab.
            // This published revision always resolves its approved defaults, not mutable gameplay profiles.
            var profile=ProvingProfile.CreateCombatBowlAuthoringDefault(definition);
            if(profile.Validate().Count!=0)throw new InvalidOperationException("Invalid Combat Bowl authoring profile");
            ApplyAuthoring(definition,profile);
            return definition;
        }
        public static ProvingProfile AuthoringProfile()=>ProvingProfile.CreateCombatBowlAuthoringDefault(Draft());
        internal static void ApplyAuthoring(ArenaDefinition d,ProvingProfile p)
        {
            Vector3 Read(string key)=>new Vector3(p.Get(key+".x"),p.Get(key+".y"),p.Get(key+".z"));
            foreach(var s in d.Solids){s.Position=Read(s.Id+".position");s.Size=Read(s.Id+".size");s.Rotation=Quaternion.Euler(Read(s.Id+".rotation"));}
            for(int i=0;i<d.Spawns.Length;i++)d.Spawns[i]=Read("spawn-"+i);
            foreach(var s in d.SpawnRegions){s.Center=Read(s.Id+".center");s.Size=Read(s.Id+".extent");}
            foreach(var t in d.Transitions)for(int i=0;i<t.OrderedFeet.Length;i++)t.OrderedFeet[i]=Read(t.Id+".feet-"+i);
            for(int i=0;i<d.RouteAnchors.Length;i++)d.RouteAnchors[i]=Read("route-"+i);
            d.ArmorPickupAnchor=Read("armor-anchor");d.SpeedPickupAnchor=Read("speed-anchor");d.DamagePickupAnchor=Read("damage-anchor");
        }
        static ArenaDefinition Draft()
        {
            // Authored P4 defaults, all exported as range-validated profile metadata below.
            var solids=new List<ArenaSolid>();var transitions=new List<NativeNavigationTransition>();
            var regions=new List<ArenaSpawnRegion>();var spawns=new List<Vector3>();
            const string loop="support:lower-loop", hall="support:central", basement="support:armor-corridor";
            const float slab=.4f, thickness=.4f;
            void Box(string id,Vector3 pos,Vector3 size,string material="wall",string support="",Quaternion? rotation=null)
                =>solids.Add(new ArenaSolid{Id=id,Position=pos,Size=size,Material=material,Support=support,Layer=ProvingArena.WorldLayer,Rotation=rotation??Quaternion.identity});
            void Floor(string id,float x1,float x2,float z1,float z2,float y,string support)
                =>Box(id,new Vector3((x1+x2)/2,y-slab/2,(z1+z2)/2),new Vector3(x2-x1,slab,z2-z1),"floor",support);
            // Wall coordinates are centre-lines. Portals are widened by wall thickness to retain clear width.
            void Wall(string id,float x1,float z1,float x2,float z2,float bottom,float top)
                =>Box(id,new Vector3((x1+x2)/2,(bottom+top)/2,(z1+z2)/2),new Vector3(Mathf.Abs(x2-x1)+thickness,top-bottom,Mathf.Abs(z2-z1)+thickness));
            void Roof(string id,float x1,float x2,float z1,float z2,float underside)
                =>Box(id,new Vector3((x1+x2)/2,underside+slab/2,(z1+z2)/2),new Vector3(x2-x1,slab,z2-z1));
            void Steps(string id,Vector3 start,Vector3 direction,int count,float rise,float tread,float width,string lower,string upper)
            {
                var feet=new List<Vector3>{start-direction*.2f};
                for(int i=0;i<count;i++)
                {
                    var top=start+direction*((i+.5f)*tread)+Vector3.up*((i+1)*rise);
                    var sz=direction.x!=0?new Vector3(tread,(i+1)*rise,width):new Vector3(width,(i+1)*rise,tread);
                    Box(id+"-step-"+i,new Vector3(top.x,start.y+sz.y/2,top.z),sz,"floor","transition:"+id);feet.Add(top);
                }
                feet.Add(start+direction*(count*tread+.2f)+Vector3.up*(count*rise));
                transitions.Add(new NativeNavigationTransition{Id="transition:"+id,LowerSupport=lower,UpperSupport=upper,OrderedFeet=feet.ToArray()});
            }
            Floor("north-flank",-20,20,10,20,0,loop);Floor("south-gallery",-20,20,-20,-10,0,loop);
            Floor("west-loop",-20,-12,-10,10,0,loop);Floor("east-loop",12,20,-10,10,0,loop);
            Floor("armor-corridor",-2.6f,2.6f,-7,7,-1.2f,basement);
            Steps("north-descent",new Vector3(0,-1.2f,7),Vector3.forward,6,.2f,.5f,5.2f,basement,loop);
            Steps("south-descent",new Vector3(0,-1.2f,-7),Vector3.back,6,.2f,.5f,5.2f,basement,loop);
            Wall("basement-west",-2.8f,-10,-2.8f,10,-1.6f,3.6f);Wall("basement-east",2.8f,-10,2.8f,10,-1.6f,3.6f);
            Floor("central-hall",-6,6,-8,8,4,hall);
            Roof("basement-north-ceiling",-2.6f,2.6f,8,10,3.6f);Roof("basement-south-ceiling",-2.6f,2.6f,-10,-8,3.6f);
            Roof("north-roof",-20,20,10,20,4.2f);Roof("south-roof",-20,20,-20,-10,4.2f);
            Roof("west-roof",-20,-12,-10,10,4.2f);Roof("east-roof",12,20,-10,10,4.2f);Roof("hall-roof",-6.2f,6.2f,-8.2f,8.2f,8);
            Wall("outer-north",-28,20.2f,28,20.2f,0,4.6f);Wall("outer-south",-28,-20.2f,28,-20.2f,0,4.6f);
            for(int sx=-1;sx<=1;sx+=2)
            {
                string side=sx<0?"west":"east";
                Wall(side+"-outer-loop",sx*20.2f,-10,sx*20.2f,10,0,4.6f);
                for(int sz=-1;sz<=1;sz+=2)
                {
                    string name=(sz>0?"north":"south")+side, support="support:spawn-"+name;
                    float xa=Mathf.Min(sx*21.5f,sx*28),xb=Mathf.Max(sx*21.5f,sx*28),za=sz>0?10:-20,zb=sz>0?20:-10;
                    Floor(name+"-spawn",xa,xb,za,zb,.6f,support);
                    Floor(name+"-door-divider-floor",Mathf.Min(sx*20,sx*21.5f),Mathf.Max(sx*20,sx*21.5f),sz>0?14:-16,sz>0?16:-14,.6f,support);
                    Roof(name+"-roof",Mathf.Min(sx*20,sx*28.2f),Mathf.Max(sx*20,sx*28.2f),za,zb,4.6f);
                    Wall(name+"-outer",sx*28.2f,za,sx*28.2f,zb,0,4.6f);
                    Wall(name+"-return",sx*20,sz*9.8f,sx*28.2f,sz*9.8f,0,4.6f);
                    Wall(name+"-divider",sx*20.2f,sz*14.2f,sx*20.2f,sz*15.8f,.6f,4.6f);
                    Wall(name+"-screen",sx*24,sz*14,sx*24,sz*20,.6f,4.6f);
                    foreach(float z in new[]{12f,18f})Steps(name+"-exit-"+z,new Vector3(sx*20,0,sz*z),Vector3.right*sx,3,.2f,.5f,4,loop,support);
                    spawns.Add(new Vector3(sx*26,.6f,sz*17));regions.Add(new ArenaSpawnRegion{Id="spawn-"+name,Support=support,Center=new Vector3(sx*26,.6f,sz*17),Size=new Vector3(2,0,2)});
                }
            }
            // Continuous inner shell: exactly two upper approaches and two basement entrances.
            Wall("inner-north-west",-12,10,-2.8f,10,0,4.2f);Wall("inner-north-east",2.8f,10,12,10,0,4.2f);
            Wall("inner-south-west",-12,-10,-2.8f,-10,0,4.2f);Wall("inner-south-east",2.8f,-10,12,-10,0,4.2f);
            Wall("inner-west-south",-12,-10,-12,1.8f,0,4.2f);Wall("inner-west-north",-12,6.2f,-12,10,0,4.2f);
            Wall("inner-east-south",12,-10,12,-6.2f,0,4.2f);Wall("inner-east-north",12,-1.8f,12,10,0,4.2f);
            Wall("north-baffle-west",-5,14,-5,20,0,4.2f);Wall("north-baffle-east",5,10,5,16,0,4.2f);
            foreach(float x in new[]{-6f,6f}){Wall("south-partition-outer-"+x,x,-20,x,-16.2f,0,4.2f);Wall("south-partition-inner-"+x,x,-11.8f,x,-10,0,4.2f);}
            Wall("hall-north",-6,8.2f,6,8.2f,4,8);Wall("hall-south",-6,-8.2f,6,-8.2f,4,8);
            Wall("hall-west-south",-6.2f,-8,-6.2f,1.8f,4,8);Wall("hall-west-north",-6.2f,6.2f,-6.2f,8,4,8);
            Wall("hall-east-south",6.2f,-8,6.2f,-6.2f,4,8);Wall("hall-east-north",6.2f,-1.8f,6.2f,8,4,8);
            foreach(float x in new[]{-2.5f,2.5f})foreach(float z in new[]{-3.5f,3.5f})Box("column-"+x+"-"+z,new Vector3(x,6,z),new Vector3(1.5f,4,1.5f));
            foreach(int side in new[]{-1,1})
            {
                string id=side<0?"west-rise":"east-rise";float z=-side*4;var start=new Vector3(side*14,0,z);var end=new Vector3(side*6,4,z);
                var along=(end-start).normalized;float angle=Mathf.Atan2(4,8)*Mathf.Rad2Deg*-side;var rotation=Quaternion.Euler(0,0,angle);var normal=rotation*Vector3.up;
                Box(id,start+(end-start)/2-normal*slab/2,new Vector3(Vector3.Distance(start,end),slab,4),"floor","transition:"+id,rotation);
                Wall(id+"-south",side*14,z-2.2f,side*6,z-2.2f,0,8);Wall(id+"-north",side*14,z+2.2f,side*6,z+2.2f,0,8);
                Roof(id+"-roof",Mathf.Min(side*14,side*6),Mathf.Max(side*14,side*6),z-2.2f,z+2.2f,8);
                var feet=new List<Vector3>{start+Vector3.right*side*.2f};for(int i=1;i<8;i++)feet.Add(Vector3.Lerp(start,end,i/8f));feet.Add(end-Vector3.right*side*.2f);
                transitions.Add(new NativeNavigationTransition{Id="transition:"+id,LowerSupport=loop,UpperSupport=hall,OrderedFeet=feet.ToArray()});
            }
            return new ArenaDefinition{GeneratorVersion=Identity,FamilyId=Id,Identity=Identity,ProfileFingerprint="authored:"+Identity,Solids=solids.ToArray(),Spawns=spawns.ToArray(),SpawnRegions=regions.ToArray(),Transitions=transitions.ToArray(),RouteAnchors=new[]{new Vector3(0,-1.2f,0),new Vector3(0,4,0)},ArmorPickupAnchor=new Vector3(0,-1.2f,0),SpeedPickupAnchor=new Vector3(0,0,-14),DamagePickupAnchor=new Vector3(0,4,0),HasDamagePickup=true,DisableSpeedPickup=true};
        }
    }
}
