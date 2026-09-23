using UnityEngine;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public static ProvingProfile CreateOrbitalLeagueDefault()
        {
            var p=new ProvingProfile{id="orbital-league-v1",version=5};
            void V(string key,string group,string label,string description,string unit,float min,float max,float step,float value)
                =>p.Add(key,group,label,description,unit,min,max,step,value);
            V("surface.tile","surfaces","Размер панели","Повтор металлических панелей и стыков.","m",.5f,4,.1f,2);
            V("surface.wallValue","surfaces","Светлота стен","Светлый окрашенный металл для силуэтов игроков.","ratio",.35f,.95f,.01f,.73f);
            V("surface.floorValue","surfaces","Светлота пола","Матовый графитовый пол под спортивной разметкой; освещённый участок сохраняет контраст тени.","ratio",.06f,.35f,.01f,.28f);
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
            V("light.emission","lighting","Свечение панели","Белые светильники без отдельной realtime-тени.","multiplier",.5f,3,.1f,2.3f);
            V("light.fill","lighting","Рассеянный свет","Равномерная читаемость закрытых помещений.","ratio",.2f,1,.05f,.45f);
            V("light.intensity","lighting","Локальный свет","Сила шести потолочных источников.","intensity",0,12,.5f,11f);
            V("light.studioKey","lighting","Общий направленный свет","Заполнение Combat Bowl, сохраняющее контраст локальных теней.","intensity",.1f,2,.05f,.4f);
            V("light.range","lighting","Радиус света","Зона мягкого локального освещения.","m",3,12,.5f,12);
            V("light.spotAngle","lighting","Угол потолочного света","Ширина направленного пятна, определяющая читаемость тени.","deg",60,120,5,100);
            V("light.targetDrop","lighting","Наклон потолочного света","Смещение цели светильника вниз от центра зоны.","m",1,5,.25f,3);
            V("light.hallAimOffsetZ","lighting","Цель света в зале","Смещение пятна главного зала вдоль маршрута от геометрического центра.","m",-4,4,.5f,-2.5f);
            V("light.shadowCasters","lighting","Источники с тенями","Максимум общих для всех камер потолочных источников с realtime-тенью.","count",0,4,1,4);
            V("light.shadowStrength","lighting","Контраст тени","Мягкая тень оставляет соперника и вход различимыми.","ratio",0,1,.05f,1);
            V("light.shadowResolution","lighting","Разрешение тени","Размер одной карты тени в общем атласе URP.","px",256,512,256,512);
            V("light.shadowBias","lighting","Смещение тени","Подавление самозатенения на игроках и стенах.","m",.01f,.2f,.01f,.02f);
            V("light.shadowNormalBias","lighting","Нормальное смещение","Подавление артефактов на освещённых панелях.","m",0,1,.05f,.1f);
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
            BroadcastDescriptors(p);
            Position("layout.broadcast-screen-0",new Vector3(0,6.25f,7.96f));
            Position("layout.broadcast-screen-1",new Vector3(-19.96f,2.6f,0));
            Position("layout.broadcast-screen-2",new Vector3(-11,2.6f,19.96f));
            Position("layout.broadcast-screen-3",new Vector3(11,2.6f,19.96f));
            Position("layout.broadcast-crest",new Vector3(0,7.77f,0));
            Position("layout.broadcast-fan-0",new Vector3(-2.59f,1.3f,-5));
            Position("layout.broadcast-fan-1",new Vector3(-2.59f,1.3f,5));
            p.Add("ring.gratingPitch","ring","Grating pitch","Spacing of visible metal bars; does not change the continuous actor support.","meters",.15f,1,.05f,.4f);
            p.Add("ring.gratingBar","ring","Grating bar width","Visual thickness of metal bars; shots and LOS pass through the support surface.","meters",.01f,.1f,.01f,.04f);
            p.Add("ring.gratingDepth","ring","Grating bar depth","Visible vertical bar depth; the actor support collider is unchanged.","meters",.01f,.2f,.01f,.05f);
            p.Add("ring.windowFramePitch","ring","Window span","Distance between hull window structural frames.","meters",4,12,.5f,8);
            p.Add("ring.windowFrameWidth","ring","Window frame width","Visible hull frame width within the canonical sealed boundary.","meters",.1f,1,.05f,.4f);
            return p;
        }
        public static ProvingProfile CreateCombatBowlRingPresentationDefault()
        {
            var p=CreateOrbitalLeagueDefault();p.id="orbital-league-ring-v1";p.version=1;
            void Position(string key,Vector3 value)
            {
                for(int i=0;i<3;i++)
                {
                    var path=key+"."+new[]{"x","y","z"}[i];
                    if(p.Descriptor(path)!=null){p.Descriptor(path).DefaultValue=value[i];p.Set(path,value[i]);}
                    else p.Add(path,"ring-layout",path,"Authored R7 visual position; never changes collision.","meters",-40,40,.01f,value[i]);
                }
            }
            Position("layout.label-0",new Vector3(0,7,7.985f));Position("layout.label-1",new Vector3(0,7,-7.985f));
            Position("layout.broadcast-screen-0",new Vector3(0,7,7.98f));
            Position("layout.broadcast-screen-1",new Vector3(-27.78f,5.8f,9));
            Position("layout.broadcast-screen-2",new Vector3(-5,5.8f,23.78f));Position("layout.broadcast-screen-3",new Vector3(5,5.8f,23.78f));
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})
            {
                int number=sz>0?(sx<0?1:2):(sx<0?3:4);
                Position("ring.spawn-mark-"+number,new Vector3(sx*35.94f,6.2f,sz*17));
                int oldIndex=4+(sx<0?0:2)+(sz<0?0:1);Position("layout.label-"+oldIndex,new Vector3(sx*35.94f,6.2f,sz*20));
            }
            Position("layout.label-8",new Vector3(0,5.8f,23.78f));Position("layout.label-9",new Vector3(0,5.8f,-23.78f));
            Position("layout.label-10",new Vector3(-27.78f,5.8f,9));Position("layout.label-11",new Vector3(27.78f,5.8f,-9));
            p.Add("ring.skyValue","ring","Window sky brightness","Brightness of the space backdrop in the sealed panoramic windows.","ratio",.01f,.3f,.01f,.08f);
            return p;
        }
        // Existing serialized scene profiles gain only missing presentation controls.
        public void EnsureOrbitalLeagueDescriptors()
        {
            if(id!="orbital-league-v1")return;
            // Retain designer overrides while upgrading the previous authored defaults.
            if(version<3)
            {
                if(Mathf.Approximately(Get("light.emission"),1.5f))Set("light.emission",2.3f);
                if(Mathf.Approximately(Get("light.intensity"),1.4f))Set("light.intensity",8f);
                if(Mathf.Approximately(Get("light.range"),7f))Set("light.range",10f);
            }
            if(version<4)
            {
                if(Mathf.Approximately(Get("light.fill"),.65f))Set("light.fill",.45f);
                if(Mathf.Approximately(Get("light.intensity"),8f))Set("light.intensity",11f);
                if(Mathf.Approximately(Get("light.studioKey"),.65f))Set("light.studioKey",.4f);
                if(Mathf.Approximately(Get("light.range"),10f))Set("light.range",12f);
                if(Mathf.Approximately(Get("light.shadowStrength"),.65f))Set("light.shadowStrength",1);
                if(Mathf.Approximately(Get("light.shadowBias"),.05f))Set("light.shadowBias",.02f);
                if(Mathf.Approximately(Get("light.shadowNormalBias"),.4f))Set("light.shadowNormalBias",.1f);
            }
            if(version<5 && Mathf.Approximately(Get("surface.floorValue"),.18f))Set("surface.floorValue",.28f);
            var current=CreateOrbitalLeagueDefault();
            foreach(var descriptor in current.descriptors)
            {
                int existing=descriptors.FindIndex(item=>item.Path==descriptor.Path);
                if(existing>=0)
                {
                    // The v2 range capped light intensity at four, below the new authored value.
                    if(version<4 && descriptor.Path.StartsWith("light."))
                        descriptors[existing]=JsonUtility.FromJson<NumericDescriptor>(JsonUtility.ToJson(descriptor));
                    continue;
                }
                descriptors.Add(JsonUtility.FromJson<NumericDescriptor>(JsonUtility.ToJson(descriptor)));
                values.Add(new ProvingProfileValue{Path=descriptor.Path,Value=current.Get(descriptor.Path)});
            }
            version=5;
        }
        static void BroadcastDescriptors(ProvingProfile p)
        {
            void V(string key,string group,string label,string description,string unit,float min,float max,float step,float value)
                =>p.Add(key,group,label,description,unit,min,max,step,value);
            V("broadcast.screenWidth","broadcast","Ширина экрана","Ширина крупных настенных панелей без прохода.","m",2,7,.1f,4.8f);
            V("broadcast.screenHeight","broadcast","Высота экрана","Высота панелей выше линии боя.","m",.8f,2.4f,.1f,1.7f);
            V("broadcast.orbitRadius","broadcast","Радиус орбиты","Размер спокойной орбитальной графики.","m",.2f,.8f,.05f,.5f);
            V("broadcast.orbitSpeed","broadcast","Скорость орбиты","Медленное вращение фона экрана.","deg/s",0,8,.1f,2);
            V("broadcast.fanSpeed","broadcast","Скорость вентилятора","Медленное движение за закрытой решёткой.","deg/s",0,18,.5f,6);
            V("broadcast.screenValue","broadcast","Яркость экранов","Пассивная яркость ниже игровых сигналов.","ratio",.05f,.5f,.01f,.21f);
            V("broadcast.markWidth","broadcast","Ширина ориентиров","Крупная маркировка спавнов и подъёмов.","m",.05f,.3f,.01f,.12f);
            V("broadcast.ventRadius","broadcast","Радиус вентилятора","Размер закрытого технического вентилятора.","m",.2f,.65f,.05f,.4f);
            V("broadcast.spawnMarkHeight","broadcast","Высота номера спавна","Высота маркировки стартовой комнаты.","m",1,3,.1f,2.4f);
            V("broadcast.servicePanelHeight","broadcast","Высота сервисной панели","Высота закрытой панели подвала.","m",.8f,2,.1f,1.6f);
        }
    }
}
