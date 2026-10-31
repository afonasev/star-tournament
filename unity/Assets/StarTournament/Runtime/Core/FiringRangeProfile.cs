using UnityEngine;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public const string FiringRangeAuthoringId="firing-range-authoring-v1";
        public static ProvingProfile CreateFiringRangeAuthoring(ArenaDefinition definition)
        {
            var p=CreateCombatBowlAuthoringDefault(definition);p.id=FiringRangeAuthoringId;p.version=1;
            foreach(var d in p.descriptors){d.Path="range.map."+d.Path;d.Description="Полигон: авторская геометрия. Правки требуют новой проверенной immutable revision карты.";}
            foreach(var v in p.values)v.Path="range.map."+v.Path;
            for(int i=0;i<definition.TrainingTargets.Length;i++)for(int axis=0;axis<3;axis++)
                p.Add("range.map.target-"+i+"."+new[]{"x","y","z"}[axis],"range-targets","Мишень "+(i+1)+" / "+new[]{"X","Y","Z"}[axis],"Постоянное место мишени и её респавна; изменение требует новой revision карты.","m",-100,100,.01f,definition.TrainingTargets[i][axis]);
            p.Add("range.map.targetYaw","range-targets","Направление мишеней","Поворот к входу в зал; изменение требует новой revision карты.","degrees",-360,360,1,180);
            AddFiringRangeDecorationAuthoring(p,definition);
            return p;
        }
        public static void AddFiringRangeDecorationAuthoring(ProvingProfile p,ArenaDefinition definition)
        {
            void V(string key,Vector3 v,string unit="m")
            {for(int axis=0;axis<3;axis++)p.Add("range.decor."+key+"."+new[]{"x","y","z"}[axis],"range-decoration",key+" / "+new[]{"X","Y","Z"}[axis],"Оформление полигона; новая геометрия требует отдельной immutable revision.",unit,unit=="ratio"?0:unit=="authoring"?-360:-100,unit=="ratio"?1:unit=="authoring"?360:100,.001f,v[axis]);}
            int box=0,sign=0;
            FiringRangePresentation.Layout(definition,(name,pos,size,material)=>{string key=name+"-"+box++;V(key+".position",pos);V(key+".size",size);},
                (name,text,pos,yaw,height,color)=>{string key=name+"-"+sign++;V(key+".position",pos);V(key+".text",new Vector3(yaw,height,0),"authoring");V(key+".color",new Vector3(color.r,color.g,color.b),"ratio");});
            for(int i=0;i<FiringRangePresentation.Palette.Length;i++)V("material-"+i,new Vector3(FiringRangePresentation.Palette[i].r,FiringRangePresentation.Palette[i].g,FiringRangePresentation.Palette[i].b),"ratio");
            p.Add("range.decor.smoothness","range-decoration","Гладкость поверхностей","Отклик материалов полигона на освещение.","ratio",0,1,.01f,.18f);
        }
    }
}
