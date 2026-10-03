using UnityEngine;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public const string TunnelsArtId="industrial-tunnels-presentation-v1",TunnelsAuthoringId="industrial-tunnels-authoring-v1";
        public static ProvingProfile CreateIndustrialTunnelsAuthoring(ArenaDefinition d)
        {
            var p=CreateCombatBowlAuthoringDefault(d);p.id=TunnelsAuthoringId;p.version=1;
            foreach(var descriptor in p.descriptors){descriptor.Path="tunnels.map."+descriptor.Path;descriptor.Description="Геометрия технических тоннелей; изменение требует новой immutable map revision.";}
            foreach(var value in p.values)value.Path="tunnels.map."+value.Path;
            return p;
        }
        public static ProvingProfile CreateIndustrialTunnelsPresentation()
        {
            var p=new ProvingProfile{id=TunnelsArtId,version=2};
            void V(string key,string group,string label,string unit,float min,float max,float step,float value)
                =>p.Add("tunnels."+key,group,label,"Технические тоннели: "+label+". Управляет оформлением, не меняет collision или предметы.",unit,min,max,step,value);
            V("light.fill","tunnel-light","Рассеянный свет","ratio",.05f,.8f,.01f,.32f);
            V("light.warm","tunnel-light","Тёплые светильники","intensity",0,30,.5f,12);
            V("light.white","tunnel-light","Белый верхний свет","intensity",0,30,.5f,14);
            V("light.range","tunnel-light","Дальность света","m",3,18,.5f,9);
            V("light.key","tunnel-light","Общий свет","intensity",0,2,.05f,.25f);
            V("light.emission","tunnel-light","Свечение плафонов","ratio",0,8,.1f,3);
            V("surface.tile","tunnel-detail","Повтор текстуры","m",1,8,.1f,3);
            V("surface.wall","tunnel-surface","Светлота стен","ratio",.2f,1.5f,.05f,.85f);
            V("surface.floor","tunnel-surface","Светлота пола","ratio",.2f,1.5f,.05f,.65f);
            V("surface.metallic","tunnel-surface","Металличность","ratio",0,1,.05f,.45f);
            V("surface.smoothness","tunnel-surface","Гладкость","ratio",0,.9f,.05f,.3f);
            V("surface.mold","tunnel-surface","Плесень у основания","ratio",0,1,.05f,.35f);
            V("detail.ribSpacing","tunnel-detail","Шаг рам","m",2,6,.25f,3);
            V("detail.pipeRadius","tunnel-detail","Радиус трубы","m",.05f,.4f,.01f,.16f);
            V("detail.grilleSpacing","tunnel-detail","Ячейка решётки","m",.15f,.6f,.05f,.3f);
            V("wayfinding.paint","tunnel-wayfinding","Светлота секторной краски","ratio",.5f,1.5f,.05f,1);
            V("wayfinding.bandHeight","tunnel-wayfinding","Ширина полосы стен","m",.3f,1.2f,.05f,.65f);
            V("wayfinding.numberHeight","tunnel-wayfinding","Высота номера зала","m",1,2.2f,.1f,1.8f);
            V("wayfinding.signHeight","tunnel-wayfinding","Высота указателя выхода","m",.35f,.75f,.05f,.55f);
            V("wayfinding.approach","tunnel-wayfinding","Продолжение краски в тоннель","m",1,4,.5f,3);
            V("wayfinding.ringRadius","tunnel-wayfinding","Радиус маркировки центра","m",1.5f,3,.1f,2.2f);
            return p;
        }
        internal ProvingProfile BeforeTunnelWayfinding()
        {
            if(Id!=TunnelsArtId||Version<2)return this;
            var previous=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(this));previous.version=1;
            previous.descriptors.RemoveAll(d=>d.Path.StartsWith("tunnels.wayfinding.",System.StringComparison.Ordinal));
            previous.values.RemoveAll(v=>v.Path.StartsWith("tunnels.wayfinding.",System.StringComparison.Ordinal));
            return previous;
        }
    }
}
