using System.Linq;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public static ProvingProfile CreateLunarAuthoring(ArenaDefinition d,int registryVersion=2)
        {
            // Repeated mesh-kit bars/posts are derived from deck/fence bounds, not independent level controls.
            // Export structural geometry and all gameplay anchors/routes; keep kit encoding in the asset manifest.
            var structural=new ArenaDefinition{Solids=d.Solids.Where(s=>!s.Id.Contains("-bar-")&&!s.Id.StartsWith("fence-x")&&!s.Id.StartsWith("fence-z")&&!s.Id.StartsWith("rail-")&&!s.Id.Contains("-outer-post-")).ToArray(),Spawns=d.Spawns,SpawnRegions=d.SpawnRegions,Transitions=d.Transitions,RouteAnchors=d.RouteAnchors,Pickups=d.Pickups};
            var p=CreateCombatBowlAuthoringDefault(structural);p.id="lunar-laboratory-authoring-v1";p.version=registryVersion;
            foreach(var a in p.descriptors){a.Path="lunar.map."+a.Path;a.Description="Лунная лаборатория v4; изменение требует новой проверенной immutable ревизии карты.";}
            foreach(var a in p.values)a.Path="lunar.map."+a.Path;return p;
        }
        internal ProvingProfile BeforeLunarBypass()=>Id=="lunar-laboratory-authoring-v1"&&Version==2?LunarLaboratoryCatalog.BeforeBypassAuthoring():this;
        internal ProvingProfile BeforeLunarFlush()=>Id=="lunar-laboratory-authoring-v1"?LunarLaboratoryCatalog.BeforeFlushAuthoring():this;
        internal ProvingProfile BeforeLunarInterior()=>Id=="lunar-laboratory-authoring-v1"?LunarLaboratoryCatalog.BeforeInteriorAuthoring():this;
        internal ProvingProfile BeforeLunarOpenCentre()=>Id=="lunar-laboratory-authoring-v1"?LunarLaboratoryCatalog.BeforeOpenCentreAuthoring():this;
        internal ProvingProfile BeforeLunarLighting()
        {
            if(Id!="lunar-laboratory-presentation-v1"||Version!=4)return this;
            var old=UnityEngine.JsonUtility.FromJson<ProvingProfile>(UnityEngine.JsonUtility.ToJson(this));
            var added=new[]{"lunar.officeSpotRange","lunar.officeFillRange","lunar.officeInnerAngle","lunar.stairLamp","lunar.stairFill"};
            old.descriptors.RemoveAll(d=>added.Contains(d.Path));old.values.RemoveAll(v=>added.Contains(v.Path));
            old.version=3;old.Set("lunar.officeLamp",18);old.Set("lunar.officeFill",2.5f);
            foreach(var d in old.descriptors)d.DefaultValue=old.Get(d.Path);return old;
        }
        internal ProvingProfile BeforeLunarOffice()
        {
            if(Id!="lunar-laboratory-authoring-v1"&&Id!="lunar-laboratory-presentation-v1")return this;
            var old=UnityEngine.JsonUtility.FromJson<ProvingProfile>(UnityEngine.JsonUtility.ToJson(BeforeLunarLighting()));
            bool Added(string path)=>path.StartsWith("lunar.map.cargo-f2-",System.StringComparison.Ordinal)||path.StartsWith("lunar.map.f2-service-",System.StringComparison.Ordinal)||path=="lunar.officeLamp"||path=="lunar.officeFill";
            old.descriptors.RemoveAll(d=>Added(d.Path));old.values.RemoveAll(v=>Added(v.Path));
            if(Id=="lunar-laboratory-presentation-v1")old.version=2;
            return old;
        }
        internal ProvingProfile BeforeLunarRework()
        {
            if(Id!="lunar-laboratory-authoring-v1"&&Id!="lunar-laboratory-presentation-v1")return this;
            var old=UnityEngine.JsonUtility.FromJson<ProvingProfile>(UnityEngine.JsonUtility.ToJson(BeforeLunarOffice()));
            old.descriptors.RemoveAll(d=>d.Path.StartsWith("lunar.map.cargo-",System.StringComparison.Ordinal)||d.Path=="lunar.glassOpacity");
            old.values.RemoveAll(v=>v.Path.StartsWith("lunar.map.cargo-",System.StringComparison.Ordinal)||v.Path=="lunar.glassOpacity");
            if(Id=="lunar-laboratory-presentation-v1")
            {old.version=1;old.Set("lunar.fill",.4f);old.Set("lunar.sun",.8f);old.Set("lunar.lamp",6);foreach(var d in old.descriptors)d.DefaultValue=old.Get(d.Path);}
            return old;
        }
        public static ProvingProfile CreateLunarPresentation()
        {
            var p=new ProvingProfile{id="lunar-laboratory-presentation-v1",version=4};
            void V(string key,string label,string unit,float min,float max,float step,float value)=>p.Add("lunar."+key,"lunar-presentation",label,"Лунная лаборатория: "+label+"; оформление не меняет физические контракты.",unit,min,max,step,value);
            V("navigationClearance","Запас маршрута от стен","m",.05f,.4f,.01f,.15f);
            V("fill","Рассеянный свет","ratio",.05f,1,.01f,.17f);V("sun","Солнечный свет","intensity",0,3,.05f,.3f);
            V("lamp","Светильники","intensity",0,30,.5f,12);V("range","Дальность света","m",2,30,.5f,10);
            V("officeLamp","Потолочный офисный свет","intensity",0,100,.5f,20);
            V("officeFill","Заполняющий офисный свет","intensity",0,20,.25f,3.5f);
            V("officeSpotRange","Радиус потолочного офисного света","m",2,10,.25f,7);
            V("officeFillRange","Радиус офисного заполнения","m",2,10,.25f,6.5f);
            V("officeInnerAngle","Внутренний угол офисного света","deg",5,124,1,65);
            V("stairLamp","Потолочный свет лестниц","intensity",0,100,.5f,18);
            V("stairFill","Заполняющий свет лестниц","intensity",0,20,.25f,2.5f);
            V("glassOpacity","Непрозрачность бронестекла","ratio",.1f,.6f,.01f,.3f);
            V("signScale","Масштаб маркировки","ratio",.5f,2,.1f,1);
            V("tile","Размер панели","m",.5f,8,.1f,2);V("skyDistance","Глубина фона","m",80,180,1,150);
            V("mountainHeight","Высота дальних гор","m",5,50,1,22);V("earthRadius","Видимый радиус Земли","m",2,15,.5f,8);
            V("sunRadius","Видимый радиус Солнца","m",.5f,5,.1f,1.8f);
            return p;
        }
    }
}
