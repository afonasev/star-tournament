namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        internal void SetIdentityForWell(int revision=5)
        {id="well-authoring-v1";version=revision;foreach(var d in descriptors){d.Path="well.map."+d.Path;d.Description="Колодец, "+(revision==1?"планировка 02":"структура 03")+": immutable геометрия; изменения требуют новой ревизии и QA.";}foreach(var v in values)v.Path="well.map."+v.Path;}
        internal ProvingProfile BeforeWellSecondFloorWeapons()=>id=="well-authoring-v1"?WellArenaRevisionFour.AuthoringProfile():this;
        internal ProvingProfile BeforeWellFurniture()=>id=="well-authoring-v1"?WellArenaRevisionTwo.AuthoringProfile():this;
        internal ProvingProfile BeforeWellCorridorCover()=>id=="well-authoring-v1"?WellArenaRevisionThree.AuthoringProfile():this;
        internal ProvingProfile BeforeWellStructure()=>id=="well-authoring-v1"?WellArenaRevisionOne.AuthoringProfile():this;
        internal void AddWellLiftAuthoring(ArenaLiftDefinition[] lifts)
        {
            foreach(var lift in lifts)
            {
                foreach(var axis in new[]{0,1,2})
                {
                    string name=new[]{"x","y","z"}[axis];
                    Add("well.map.lift-"+lift.Id+".center."+name,"geometry","Лифт "+lift.Id+" / центр "+name,"Immutable размер карты; новая ревизия требует QA.","m",-100,100,.01f,lift.Center[axis]);
                    Add("well.map.lift-"+lift.Id+".size."+name,"geometry","Лифт "+lift.Id+" / размер "+name,"Immutable размер платформы.","m",.01f,100,.01f,lift.Size[axis]);
                }
                Add("well.map.lift-"+lift.Id+".bottom","geometry","Нижний край "+lift.Id,"Immutable высота.","m",0,100,.01f,lift.Bottom);
                Add("well.map.lift-"+lift.Id+".top","geometry","Верхний край "+lift.Id,"Immutable высота.","m",0,100,.01f,lift.Top);
                Add("well.map.lift-"+lift.Id+".initial","geometry","Начальная высота "+lift.Id,"Immutable начальный этаж.","m",0,100,.01f,lift.InitialHeight);
            }
        }
        internal ProvingProfile BeforeWellIdleDeparture()
        {
            if(id!="well-gameplay-presentation-v1"||version<4)return this;
            var copy=BeforeWellDaylight().DetachedCopy();copy.version=3;
            copy.descriptors.RemoveAll(d=>d.Path=="well.idleDepartureSeconds");copy.values.RemoveAll(v=>v.Path=="well.idleDepartureSeconds");
            return copy;
        }
        internal ProvingProfile BeforeWellDepth()
        {
            if(id!="well-gameplay-presentation-v1"||version<3)return this;
            var copy=BeforeWellIdleDeparture().DetachedCopy();copy.version=2;
            var added=new System.Collections.Generic.HashSet<string>{"well.daylightShadow","well.glassSmoothness","well.fittingDepth","well.contactShade"};
            copy.descriptors.RemoveAll(d=>added.Contains(d.Path));copy.values.RemoveAll(v=>added.Contains(v.Path));
            copy.FindDescriptor("well.daylight").DefaultValue=.5f;copy.Set("well.daylight",.5f);
            return copy;
        }
        internal ProvingProfile BeforeWellDetail()
        {
            if(id!="well-gameplay-presentation-v1"||version<2)return this;
            var copy=BeforeWellDepth().DetachedCopy();copy.version=1;
            var added=new System.Collections.Generic.HashSet<string>{"well.surfaceGrain","well.jointContrast","well.surfaceSmoothness","well.floorSmoothness","well.fixtureGlow","well.plantHeight"};
            copy.descriptors.RemoveAll(d=>added.Contains(d.Path));copy.values.RemoveAll(v=>added.Contains(v.Path));
            copy.FindDescriptor("well.fill").DefaultValue=.8f;copy.Set("well.fill",.8f);
            copy.FindDescriptor("well.lamp").DefaultValue=12;copy.Set("well.lamp",12);
            return copy;
        }
        internal static float UpgradeWellDetailValue(string path,int oldVersion,float value,NumericDescriptor current)
        {
            if(oldVersion<5)
            {
                for(int i=0;i<WellDaylightChanged.Length;i++)
                    if(path==WellDaylightChanged[i]&&UnityEngine.Mathf.Approximately(value,WellDaylightPrevious[i]))return current.DefaultValue;
            }
            if(oldVersion<3&&path=="well.daylight"&&UnityEngine.Mathf.Approximately(value,.5f))return current.DefaultValue;
            if(oldVersion<2&&((path=="well.fill"&&UnityEngine.Mathf.Approximately(value,.8f))||(path=="well.lamp"&&UnityEngine.Mathf.Approximately(value,12))))return current.DefaultValue;
            return value;
        }
        public static ProvingProfile CreateWellDefault()
        {
            var p=new ProvingProfile{id="well-gameplay-presentation-v1",version=6};
            void V(string key,string group,string label,string desc,string unit,float min,float max,float step,float value)=>p.Add("well."+key,group,label,desc,unit,min,max,step,value);
            V("liftSpeed","well-lifts","Скорость лифта","Быстрый рейс; влияет на окно выхода на втором этаже.","m/s",1,8,.1f,4);
            V("idleDepartureSeconds","well-lifts","Автоотправка пустого лифта","Игровое время простоя пустой платформы до рейса к противоположному крайнему этажу; посадка сбрасывает таймер.","s",1,120,1,15);
            // Delay = exactly 1 second is the explicit approved rule, not an adjustable balance value.
            V("boardingTolerance","well-lifts","Допуск опоры","Контакт ног с платформой, без подхвата прыгающего персонажа.","m",.05f,.4f,.01f,.2f);
            V("botDockRange","well-bots","Радиус выбора лифта","Доступный лифт полезен рядом с ботом; иначе используются лестницы.","m",2,40,.5f,18);
            V("botExitLead","well-bots","Упреждение выхода","Расстояние по высоте до нужного этажа для прыжка к холлу.","m",.1f,1,.05f,.5f);
            V("panelSize","well-art","Размер панелей пола","Частота швов на широких проходах; только оформление.","m",1,6,.25f,3);
            V("grateSpacing","well-art","Шаг решётки","Видимая частота прутьев; физическая опора и огонь сохраняются.","m",.2f,.6f,.05f,.3f);
            V("signScale","well-art","Масштаб маркировки","Читаемость номеров и надписей этажей.","ratio",.5f,2,.1f,1);
            V("daylight","well-light","Дневной свет","Направленный дневной свет атриума; светлая отделка сохраняет фактуру.","intensity",0,3,.05f,2);
            V("spotAngle","well-light","Угол офисного света","Ширина светового пятна.","deg",60,160,5,140);
            V("innerAngle","well-light","Внутренний угол света","Плавность края светового пятна.","deg",10,130,5,95);
            V("fill","well-light","Рассеянный свет","Прохладное заполнение тени; общий visual ambient lift применяется отдельно.","ratio",.1f,1,.01f,.32f);
            V("lamp","well-light","Офисные светильники","Исходная яркость потолочных источников; Lighting масштабирует её независимо.","intensity",0,50,.5f,7);
            V("lampRange","well-light","Радиус светильника","Освещение широких залов.","m",3,20,.5f,18);
            V("glassOpacity","well-art","Непрозрачность стекла","Читаемость непробиваемого окна.","ratio",.1f,.6f,.05f,.25f);
            // well-research-detail-v1: presentation only; no authored map revision change.
            V("surfaceGrain","well-art","Фактура материалов","Контраст мелкой фактуры керамики, металла и грунта.","ratio",0,.3f,.01f,.075f);
            V("jointContrast","well-art","Швы панелей","Контраст тонких швов без геометрических щелей.","ratio",0,.5f,.01f,.22f);
            V("surfaceSmoothness","well-art","Гладкость керамики","Блик светлых панелей лаборатории.","ratio",0,.8f,.05f,.28f);
            V("floorSmoothness","well-art","Гладкость пола","Матовый пол без ярких отражений.","ratio",0,.6f,.05f,.18f);
            V("fixtureGlow","well-light","Свечение диффузоров","Видимая яркость существующих светильников.","ratio",.5f,3,.1f,1.3f);
            V("plantHeight","well-art","Высота растений","Компактные листья внутри прежних клумб; не меняет физику.","m",.25f,.65f,.05f,.5f);
            // well-research-depth-v1: calibrated daylight and optical/fitting response.
            V("daylightShadow","well-light","Тени дневного света","При нуле нет теней; единица даёт полный контраст под мостами и в проёмах.","ratio",0,1,.05f,.85f);
            V("glassSmoothness","well-art","Полировка стекла","От размытого блика до чёткого отражения; прозрачность задаётся отдельно.","ratio",.3f,.95f,.05f,.85f);
            V("fittingDepth","well-art","Объём приборов","Масштаб глубины корпуса: от почти плоской панели до объёмного прибора; детали выступают не более 20 см, только на сплошной стене.","m",.06f,.16f,.01f,.14f);
            V("contactShade","well-light","Объём стыков","Затенение рассеянного света в реальных углах: ноль отключает, единица даёт полный контактный объём.","ratio",0,1,.05f,.9f);
            WellDaylightDescriptors(p);
            // Maximum outer radius fits the narrowest existing H bridge without moving any pickup.
            V("pickupRingRadius","well-pickups","Радиус круга предмета","Белая напольная разметка всех точек, в стиле Combat Bowl; ограничена шириной Н-моста.","m",.5f,1.4f,.05f,1.35f);
            V("pickupRingWidth","well-pickups","Толщина круга предмета","Ширина постоянной белой линии, не влияет на радиус подбора.","m",.02f,.15f,.01f,.06f);
            V("pickupRingHeight","well-pickups","Высота краски над полом","Устраняет мерцание и скрытие разметки под плитняком сада, без коллизии.","m",.035f,.06f,.005f,.04f);
            return p;
        }

        internal static readonly string[] WellDaylightChanged={"well.daylight","well.innerAngle","well.fill","well.lamp","well.lampRange","well.daylightShadow"};
        internal static readonly float[] WellDaylightPrevious={.8f,100,.42f,7,12,.45f};
        static readonly string[] WellDaylightAdded={"sunPitch","sunYaw","sunRed","sunGreen","sunBlue","fillRed","fillGreen","fillBlue","lampShadow","localLightScale","gradingExposure","bloomScale"};
        internal ProvingProfile BeforeWellPickupMarkings()
        {
            if(id!="well-gameplay-presentation-v1"||version<6)return this;
            var copy=DetachedCopy();copy.version=5;
            foreach(var path in new[]{"well.pickupRingRadius","well.pickupRingWidth","well.pickupRingHeight"})
            {copy.descriptors.RemoveAll(d=>d.Path==path);copy.values.RemoveAll(v=>v.Path==path);}
            return copy;
        }
        internal ProvingProfile BeforeWellDaylight()
        {
            if(id!="well-gameplay-presentation-v1"||version<5)return this;
            var copy=BeforeWellPickupMarkings().DetachedCopy();copy.version=4;
            foreach(var path in WellDaylightAdded){copy.descriptors.RemoveAll(d=>d.Path=="well."+path);copy.values.RemoveAll(v=>v.Path=="well."+path);}
            for(int i=0;i<WellDaylightChanged.Length;i++){copy.FindDescriptor(WellDaylightChanged[i]).DefaultValue=WellDaylightPrevious[i];copy.Set(WellDaylightChanged[i],WellDaylightPrevious[i]);}
            copy.FindDescriptor("well.fill").Step=.05f;copy.FindDescriptor("well.lamp").Maximum=50;
            // Preserve exact historical metadata as part of the trusted registry hash.
            copy.FindDescriptor("well.daylight").Description="Мягкий верхний свет колодца.";
            copy.FindDescriptor("well.fill").Description="Светлая офисная лаборатория.";
            copy.FindDescriptor("well.lamp").Description="Яркость потолочных источников.";
            return copy;
        }
        static void WellDaylightDescriptors(ProvingProfile p)
        {
            void V(string key,string label,string description,string unit,float min,float max,float step,float value)=>p.Add("well."+key,"well-light",label,description,unit,min,max,step,value);
            V("sunPitch","Высота солнца","Угол дневного света над горизонтом.","deg",10,85,1,48);
            V("sunYaw","Направление солнца","Азимут дневного рисунка в атриуме.","deg",-180,180,1,-55);
            foreach(var channel in new[]{"Red","Green","Blue"})
            {
                float sun=channel=="Red"?1:channel=="Green"?.94f:.82f,fill=channel=="Red"?.76f:channel=="Green"?.85f:1;
                V("sun"+channel,"Солнце / "+channel,"Канал цвета прямого дневного света.","ratio",0,1,.01f,sun);
                V("fill"+channel,"Тень / "+channel,"Канал цвета рассеянного заполнения.","ratio",0,1,.01f,fill);
            }
            V("lampShadow","Тени ламп","Сила теней трёх выбранных существующих потолочных источников; общий Shadows off отключает их.","ratio",0,1,.05f,.65f);
            V("localLightScale","Lighting / потолочные лампы","Множитель исходных ламп при Lighting 100%; при нуле эффекта возвращает исходную интенсивность.","ratio",0,2,.05f,2);
            V("gradingExposure","Дневная экспозиция","Дополнительная экспозиция Колодца при ColorGrading 100%; при off/0 выключена.","EV",-1,1,.05f,.7f);
            V("bloomScale","Масштаб свечения","Множитель независимого Bloom Колодца; не влияет на другие карты.","ratio",0,1,.05f,.8f);
        }
    }
}
