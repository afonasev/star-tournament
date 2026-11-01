using UnityEngine;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public static ProvingProfile CreateQuarryAuthoring(ArenaDefinition definition)
        {
            var p=CreateCombatBowlAuthoringDefault(definition);p.id="quarry-prototype-authoring-v1";p.version=1;
            foreach(var d in p.descriptors){d.Path="quarry.map."+d.Path;d.Description="Прототип карьера: авторская геометрия; изменение требует новой map revision.";}
            foreach(var v in p.values)v.Path="quarry.map."+v.Path;
            var colors=new[]{new Color32(183,137,91,255),new Color32(151,103,65,255),new Color32(207,176,128,255),new Color32(204,215,216,255),new Color32(245,245,235,255)};
            for(int i=0;i<colors.Length;i++){Color c=colors[i];for(int a=0;a<3;a++)p.Add("quarry.art.color-"+i+"."+a,"quarry-presentation","Материал "+i+" / "+a,"Цвет общих матовых материалов blockout.","ratio",0,1,.001f,c[a]);}
            p.Add("quarry.art.smoothness","quarry-presentation","Гладкость","Матовый отклик поверхностей прототипа.","ratio",0,1,.01f,.15f);
            p.Add("quarry.art.sun","quarry-presentation","Солнце","Читаемый общий дневной свет.","intensity",0,5,.05f,1.2f);
            p.Add("quarry.art.ambient","quarry-presentation","Заполнение","Читаемость теней и тоннеля.","ratio",0,1,.01f,.65f);
            p.Add("quarry.navigationClearance","quarry-navigation","Запас маршрута от стен","Запас для физической капсулы на углах скал.","m",.05f,.4f,.01f,.15f);
            AddPickupMarkings(p,"quarry.art.",1,2,.012f);
            return p;
        }
    }
}
