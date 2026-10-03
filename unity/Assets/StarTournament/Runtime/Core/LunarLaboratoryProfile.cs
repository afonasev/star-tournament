using System.Linq;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public static ProvingProfile CreateLunarAuthoring(ArenaDefinition d)
        {
            // Repeated mesh-kit bars/posts are derived from deck/fence bounds, not independent level controls.
            // Export structural geometry and all gameplay anchors/routes; keep kit encoding in the asset manifest.
            var structural=new ArenaDefinition{Solids=d.Solids.Where(s=>!s.Id.Contains("-bar-")&&!s.Id.StartsWith("fence-x")&&!s.Id.StartsWith("fence-z")&&!s.Id.StartsWith("rail-")&&!s.Id.Contains("-outer-post-")).ToArray(),Spawns=d.Spawns,SpawnRegions=d.SpawnRegions,Transitions=d.Transitions,RouteAnchors=d.RouteAnchors,Pickups=d.Pickups};
            var p=CreateCombatBowlAuthoringDefault(structural);p.id="lunar-laboratory-authoring-v1";p.version=1;
            foreach(var a in p.descriptors){a.Path="lunar.map."+a.Path;a.Description="Лунная лаборатория v4; изменение требует новой проверенной immutable ревизии карты.";}
            foreach(var a in p.values)a.Path="lunar.map."+a.Path;return p;
        }
        public static ProvingProfile CreateLunarPresentation()
        {
            var p=new ProvingProfile{id="lunar-laboratory-presentation-v1",version=1};
            void V(string key,string label,string unit,float min,float max,float step,float value)=>p.Add("lunar."+key,"lunar-presentation",label,"Лунная лаборатория: "+label+"; оформление не меняет физические контракты.",unit,min,max,step,value);
            V("navigationClearance","Запас маршрута от стен","m",.05f,.4f,.01f,.15f);
            V("fill","Рассеянный свет","ratio",.05f,1,.01f,.4f);V("sun","Солнечный свет","intensity",0,3,.05f,.8f);
            V("lamp","Светильники","intensity",0,30,.5f,6);V("range","Дальность света","m",2,30,.5f,10);
            V("signScale","Масштаб маркировки","ratio",.5f,2,.1f,1);
            V("tile","Размер панели","m",.5f,8,.1f,2);V("skyDistance","Глубина фона","m",80,180,1,150);
            V("mountainHeight","Высота дальних гор","m",5,50,1,22);V("earthRadius","Видимый радиус Земли","m",2,15,.5f,8);
            V("sunRadius","Видимый радиус Солнца","m",.5f,5,.1f,1.8f);
            return p;
        }
    }
}
