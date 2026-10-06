using UnityEngine;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public static ProvingProfile CreateCombatBowlAuthoringDefault(ArenaDefinition d)
        {
            var p=new ProvingProfile{id="combat-bowl-ring-r7-authoring-v1",version=12};
            void V(string path,Vector3 value,string group,string unit="meters",bool positive=false)
            {
                var axes=new[]{"x","y","z"};
                for(int i=0;i<3;i++)p.Add(path+"."+axes[i],group,path+" / "+axes[i],"Authored R7 "+path+"; changes require an explicit rebuild and a new reviewed map revision.",unit,positive?.01f:unit=="degrees"?-360:-100,unit=="degrees"?360:100,unit=="degrees"?.1f:.01f,value[i]);
            }
            foreach(var s in d.Solids){V(s.Id+".position",s.Position,"geometry");V(s.Id+".size",s.Size,"geometry",positive:true);V(s.Id+".rotation",s.Rotation.eulerAngles,"geometry","degrees");}
            for(int i=0;i<d.Spawns.Length;i++)V("spawn-"+i,d.Spawns[i],"spawn");
            foreach(var s in d.SpawnRegions){V(s.Id+".center",s.Center,"spawn");V(s.Id+".extent",s.Size,"spawn");}
            foreach(var t in d.Transitions)for(int i=0;i<t.OrderedFeet.Length;i++)V(t.Id+".feet-"+i,t.OrderedFeet[i],"transitions");
            for(int i=0;i<d.RouteAnchors.Length;i++)V("route-"+i,d.RouteAnchors[i],"routes");
            foreach(var item in d.Pickups)V(item.Id+".anchor",item.Anchor,"bonuses");
            return p;
        }
    }
}
