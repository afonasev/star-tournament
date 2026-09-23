using UnityEngine;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public static ProvingProfile CreateOrbitalLeagueDefault()
        {
            var p=new ProvingProfile{id="orbital-league-v1",version=1};
            void V(string key,string group,string label,string description,string unit,float min,float max,float step,float value)
                =>p.Add(key,group,label,description,unit,min,max,step,value);
            V("surface.tile","surfaces","Размер панели","Повтор металлических панелей и стыков.","m",.5f,4,.1f,2);
            V("surface.wallValue","surfaces","Светлота стен","Светлый окрашенный металл для силуэтов игроков.","ratio",.35f,.95f,.01f,.73f);
            V("surface.floorValue","surfaces","Светлота пола","Матовый графитовый пол под спортивной разметкой.","ratio",.06f,.35f,.01f,.18f);
            V("surface.metallic","surfaces","Металличность","Доля металлического отражения покрытия.","ratio",0,.7f,.01f,.25f);
            V("surface.smoothness","surfaces","Гладкость стен","Ширина блика на окрашенном металле.","ratio",0,.6f,.01f,.28f);
            V("surface.floorSmoothness","surfaces","Гладкость пола","Слабый блик без зеркальных отражений.","ratio",0,.4f,.01f,.13f);
            V("surface.wear","surfaces","Износ","Амплитуда тонких царапин и неоднородности.","ratio",0,.15f,.005f,.035f);
            V("detail.panelGap","details","Ширина стыка","Тонкий металлический шов панели.","m",.005f,.04f,.005f,.015f);
            V("detail.trimHeight","details","Высота плинтуса","Графитовый нижний пояс стен.","m",.1f,.5f,.01f,.24f);
            V("detail.bandHeight","details","Высота полосы","Голубая навигационная полоса стены.","m",.8f,2,.05f,1.05f);
            V("detail.bandWidth","details","Ширина полосы","Читаемость спортивной навигации.","m",.03f,.2f,.01f,.09f);
            V("detail.lineWidth","details","Разметка пола","Ширина спортивных линий.","m",.02f,.15f,.01f,.06f);
            V("detail.inset","details","Отступ разметки","Расстояние линии от края пола.","m",.15f,1,.05f,.45f);
            V("detail.labelSize","details","Размер номера","Масштаб маркировки сектора.","m",.05f,.4f,.01f,.1f);
            V("detail.ventWidth","details","Ширина вентиляции","Размер встроенной вентиляционной решётки.","m",.3f,2,.1f,1.2f);
            V("light.fixtureSpacing","lighting","Шаг светильников","Плотность потолочных световых модулей.","m",3,10,.5f,5);
            V("light.fixtureWidth","lighting","Ширина светильника","Широкая белая потолочная панель.","m",.3f,2,.1f,.9f);
            V("light.fixtureLength","lighting","Длина светильника","Длина белой панели.","m",1,4,.1f,2.8f);
            V("light.emission","lighting","Свечение панели","Белые светильники без отдельной realtime-тени.","multiplier",.5f,3,.1f,1.5f);
            V("light.fill","lighting","Рассеянный свет","Равномерная читаемость закрытых помещений.","ratio",.2f,1,.05f,.65f);
            V("light.intensity","lighting","Локальный свет","Сила ограниченного набора световых акцентов.","intensity",0,4,.1f,1.4f);
            V("light.range","lighting","Радиус света","Зона мягкого локального освещения.","m",3,12,.5f,7);
            V("surface.ceilingValue","surfaces","Светлота потолка","Доля светлоты стен для потолочных панелей.","ratio",.3f,1,.05f,.7f);
            V("surface.detailSmoothness","surfaces","Гладкость деталей","Ширина блика плинтуса и разметки.","ratio",0,.5f,.05f,.1f);
            V("detail.headerInset","details","Потолочный пояс","Отступ верхнего пояса от потолка.","m",.1f,.5f,.01f,.2f);
            V("detail.headerWidth","details","Ширина верхнего пояса","Высота графитового потолочного профиля.","m",.05f,.3f,.01f,.15f);
            V("detail.lightStripWidth","details","Световая полоса","Толщина встроенного белого света.","m",.01f,.1f,.005f,.035f);
            V("detail.ventHeight","details","Высота решётки","Высота вентиляционного модуля.","m",.1f,.6f,.05f,.3f);
            V("detail.ventElevation","details","Положение решётки","Высота центра решётки над основанием стены.","m",.3f,.8f,.05f,.5f);
            V("detail.columnStripHeight","details","Полоса колонны","Высота голубой вставки колонны.","m",1,3,.1f,2.1f);
            V("detail.columnStripWidth","details","Ширина вставки","Ширина голубой вставки колонны.","m",.03f,.15f,.01f,.07f);
            V("detail.ringRadius","details","Центральный круг","Радиус спортивного круга вокруг точки A.","m",1,2,.05f,1.65f);
            V("light.fixtureFrame","lighting","Рамка панели","Толщина рамки светового модуля.","m",.05f,.3f,.01f,.18f);
            V("light.fixtureDepth","lighting","Глубина панели","Выступ потолочного модуля вниз.","m",.03f,.15f,.01f,.08f);
            // Layout coordinates are authoring controls only; collider and pickup positions remain immutable.
            void Position(string key,Vector3 value)
            {for(int i=0;i<3;i++)V(key+"."+new[]{"x","y","z"}[i],"layout",key+" / "+new[]{"x","y","z"}[i],"Положение элемента оформления; не изменяет геометрию карты.","m",-40,40,.01f,value[i]);}
            var lights=new[]{new Vector3(0,7,0),new Vector3(0,2,0),new Vector3(-16,3,0),new Vector3(16,3,0),new Vector3(0,3,15),new Vector3(0,3,-15)};
            for(int i=0;i<lights.Length;i++)Position("layout.fill-"+i,lights[i]);
            var labels=new[]{new Vector3(0,6.3f,7.985f),new Vector3(0,6.3f,-7.985f),new Vector3(-2.585f,1.2f,0),new Vector3(2.585f,1.2f,0),new Vector3(-27.985f,2.6f,-16),new Vector3(-27.985f,2.6f,16),new Vector3(27.985f,2.6f,-16),new Vector3(27.985f,2.6f,16),new Vector3(-11,2.5f,19.985f),new Vector3(11,2.5f,-19.985f),new Vector3(-19.985f,2.5f,0),new Vector3(19.985f,2.5f,0)};
            for(int i=0;i<labels.Length;i++)Position("layout.label-"+i,labels[i]);
            void ColorValue(string key,Color value)
            {for(int i=0;i<3;i++)V("color."+key+"."+new[]{"r","g","b"}[i],"palette",key+" / "+new[]{"R","G","B"}[i],"Компонента палитры Orbital League.","ratio",0,1,.005f,value[i]);}
            ColorValue("trim",new Color(.095f,.12f,.14f));ColorValue("cyan",new Color(.035f,.55f,.72f));ColorValue("white",new Color(.92f,.97f,1));ColorValue("ink",new Color(.06f,.37f,.52f));ColorValue("fill",new Color(.85f,.94f,1));
            return p;
        }
    }
}
