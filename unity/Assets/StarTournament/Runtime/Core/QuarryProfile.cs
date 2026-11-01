using UnityEngine;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public static ProvingProfile CreateQuarryAuthoring(ArenaDefinition definition)
        {
            var p=CreateCombatBowlAuthoringDefault(definition);p.id="quarry-prototype-authoring-v"+definition.Revision;p.version=int.Parse(definition.Revision);
            foreach(var d in p.descriptors){d.Path="quarry.map."+d.Path;d.Description="Прототип карьера: авторская геометрия; изменение требует новой map revision.";}
            foreach(var v in p.values)v.Path="quarry.map."+v.Path;
            var colors=new[]{new Color32(135,116,84,255),new Color32(134,98,63,255),new Color32(155,147,126,255),new Color32(106,116,120,255),new Color32(35,39,41,255),new Color32(176,169,149,255),new Color32(210,132,51,255),new Color32(108,124,137,255),new Color32(68,81,86,255),new Color32(28,32,33,255),new Color32(53,97,112,255),new Color32(199,176,137,255),new Color32(255,222,163,255)};
            for(int i=0;i<colors.Length;i++){Color c=colors[i];for(int a=0;a<3;a++)p.Add("quarry.art.color-"+i+"."+a,"quarry-presentation","Материал "+i+" / "+a,"Цвет общих матовых материалов blockout.","ratio",0,1,.001f,c[a]);}
            p.Add("quarry.art.smoothness","quarry-presentation","Гладкость","Матовый отклик поверхностей прототипа.","ratio",0,1,.01f,.15f);
            p.Add("quarry.art.sun","quarry-presentation","Солнце","Читаемый общий дневной свет.","intensity",0,5,.05f,.9f);
            p.Add("quarry.art.ambient","quarry-presentation","Заполнение","Читаемость теней и тоннеля.","ratio",0,1,.01f,.35f);
            p.Add("quarry.navigationClearance","quarry-navigation","Запас маршрута от стен","Запас для физической капсулы на углах скал.","m",.05f,.4f,.01f,.15f);
            p.Add("quarry.art.signHeight","quarry-presentation","Высота букв","Читаемость входов и уровней с пути игрока.","m",.1f,2,.05f,.8f);
            p.Add("quarry.art.arrowWidth","quarry-presentation","Ширина стрелок","Обозначение непрерывного подъёма.","m",.5f,3,.1f,1.8f);
            p.Add("quarry.art.arrowSpacing","quarry-presentation","Шаг стрелок","Повторение указателей на каждой рампе.","m",2,8,.5f,4);
            p.Add("quarry.art.lampIntensity","quarry-presentation","Свет тоннеля","Читаемость крытого прохода без теней от локальных ламп.","intensity",0,5,.1f,2);
            p.Add("quarry.art.lampRange","quarry-presentation","Дальность лампы","Зона света внутри входов.","m",3,15,.5f,10);
            p.Add("quarry.art.stoneCell","quarry-presentation","Масштаб камня","Визуальный рисунок каменных пластов.","m",1,5,.1f,2.8f);
            p.Add("quarry.art.workshopLightIntensity","quarry-presentation","Свет мастерской","Рабочее освещение верхней площадки.","intensity",0,5,.1f,1.4f);
            p.Add("quarry.art.workshopLightRange","quarry-presentation","Дальность света мастерской","Область локального света верхнего уровня.","m",3,15,.5f,9);
            AddPickupMarkings(p,"quarry.art.",1,2,.012f);
            return p;
        }
    }
}
