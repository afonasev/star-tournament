namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        internal void SetIdentityForWell(int revision=3)
        {id="well-authoring-v1";version=revision;foreach(var d in descriptors){d.Path="well.map."+d.Path;d.Description="Колодец, "+(revision==1?"планировка 02":"структура 03")+": immutable геометрия; изменения требуют новой ревизии и QA.";}foreach(var v in values)v.Path="well.map."+v.Path;}
        internal ProvingProfile BeforeWellFurniture()=>id=="well-authoring-v1"?WellArenaRevisionTwo.AuthoringProfile():this;
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
        internal ProvingProfile BeforeWellDepth()
        {
            if(id!="well-gameplay-presentation-v1"||version<3)return this;
            var copy=DetachedCopy();copy.version=2;
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
            if(oldVersion<3&&path=="well.daylight"&&UnityEngine.Mathf.Approximately(value,.5f))return current.DefaultValue;
            if(oldVersion<2&&((path=="well.fill"&&UnityEngine.Mathf.Approximately(value,.8f))||(path=="well.lamp"&&UnityEngine.Mathf.Approximately(value,12))))return current.DefaultValue;
            return value;
        }
        public static ProvingProfile CreateWellDefault()
        {
            var p=new ProvingProfile{id="well-gameplay-presentation-v1",version=3};
            void V(string key,string group,string label,string desc,string unit,float min,float max,float step,float value)=>p.Add("well."+key,group,label,desc,unit,min,max,step,value);
            V("liftSpeed","well-lifts","Скорость лифта","Быстрый рейс; влияет на окно выхода на втором этаже.","m/s",1,8,.1f,4);
            // Delay = exactly 1 second is the explicit approved rule, not an adjustable balance value.
            V("boardingTolerance","well-lifts","Допуск опоры","Контакт ног с платформой, без подхвата прыгающего персонажа.","m",.05f,.4f,.01f,.2f);
            V("botDockRange","well-bots","Радиус выбора лифта","Доступный лифт полезен рядом с ботом; иначе используются лестницы.","m",2,40,.5f,18);
            V("botExitLead","well-bots","Упреждение выхода","Расстояние по высоте до нужного этажа для прыжка к холлу.","m",.1f,1,.05f,.5f);
            V("panelSize","well-art","Размер панелей пола","Частота швов на широких проходах; только оформление.","m",1,6,.25f,3);
            V("grateSpacing","well-art","Шаг решётки","Видимая частота прутьев; физическая опора и огонь сохраняются.","m",.2f,.6f,.05f,.3f);
            V("signScale","well-art","Масштаб маркировки","Читаемость номеров и надписей этажей.","ratio",.5f,2,.1f,1);
            V("daylight","well-light","Дневной свет","Мягкий верхний свет колодца.","intensity",0,3,.05f,.8f);
            V("spotAngle","well-light","Угол офисного света","Ширина светового пятна.","deg",60,160,5,140);
            V("innerAngle","well-light","Внутренний угол света","Плавность края светового пятна.","deg",10,130,5,100);
            V("fill","well-light","Рассеянный свет","Светлая офисная лаборатория.","ratio",.1f,1,.05f,.42f);
            V("lamp","well-light","Офисные светильники","Яркость потолочных источников.","intensity",0,50,.5f,7);
            V("lampRange","well-light","Радиус светильника","Освещение широких залов.","m",3,20,.5f,12);
            V("glassOpacity","well-art","Непрозрачность стекла","Читаемость непробиваемого окна.","ratio",.1f,.6f,.05f,.25f);
            // well-research-detail-v1: presentation only; no authored map revision change.
            V("surfaceGrain","well-art","Фактура материалов","Контраст мелкой фактуры керамики, металла и грунта.","ratio",0,.3f,.01f,.075f);
            V("jointContrast","well-art","Швы панелей","Контраст тонких швов без геометрических щелей.","ratio",0,.5f,.01f,.22f);
            V("surfaceSmoothness","well-art","Гладкость керамики","Блик светлых панелей лаборатории.","ratio",0,.8f,.05f,.28f);
            V("floorSmoothness","well-art","Гладкость пола","Матовый пол без ярких отражений.","ratio",0,.6f,.05f,.18f);
            V("fixtureGlow","well-light","Свечение диффузоров","Видимая яркость существующих светильников.","ratio",.5f,3,.1f,1.3f);
            V("plantHeight","well-art","Высота растений","Компактные листья внутри прежних клумб; не меняет физику.","m",.25f,.65f,.05f,.5f);
            // well-research-depth-v1: calibrated daylight and optical/fitting response.
            V("daylightShadow","well-light","Тени дневного света","При нуле нет теней; единица даёт полный контраст под мостами и в проёмах.","ratio",0,1,.05f,.45f);
            V("glassSmoothness","well-art","Полировка стекла","От размытого блика до чёткого отражения; прозрачность задаётся отдельно.","ratio",.3f,.95f,.05f,.85f);
            V("fittingDepth","well-art","Объём приборов","Масштаб глубины корпуса: от почти плоской панели до объёмного прибора; детали выступают не более 20 см, только на сплошной стене.","m",.06f,.16f,.01f,.14f);
            V("contactShade","well-light","Объём стыков","Затенение рассеянного света в реальных углах: ноль отключает, единица даёт полный контактный объём.","ratio",0,1,.05f,.9f);
            return p;
        }
    }
}
