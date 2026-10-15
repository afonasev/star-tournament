namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        internal void SetIdentityForWell()
        {id="well-authoring-v1";version=1;foreach(var d in descriptors){d.Path="well.map."+d.Path;d.Description="Колодец, планировка 02: immutable геометрия; изменения требуют новой ревизии и QA.";}foreach(var v in values)v.Path="well.map."+v.Path;}
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
        public static ProvingProfile CreateWellDefault()
        {
            var p=new ProvingProfile{id="well-gameplay-presentation-v1",version=1};
            void V(string key,string group,string label,string desc,string unit,float min,float max,float step,float value)=>p.Add("well."+key,group,label,desc,unit,min,max,step,value);
            V("liftSpeed","well-lifts","Скорость лифта","Быстрый рейс; влияет на окно выхода на втором этаже.","m/s",1,8,.1f,4);
            // Delay = exactly 1 second is the explicit approved rule, not an adjustable balance value.
            V("boardingTolerance","well-lifts","Допуск опоры","Контакт ног с платформой, без подхвата прыгающего персонажа.","m",.05f,.4f,.01f,.2f);
            V("botDockRange","well-bots","Радиус выбора лифта","Доступный лифт полезен рядом с ботом; иначе используются лестницы.","m",2,40,.5f,18);
            V("botExitLead","well-bots","Упреждение выхода","Расстояние по высоте до нужного этажа для прыжка к холлу.","m",.1f,1,.05f,.5f);
            V("panelSize","well-art","Размер панелей пола","Частота швов на широких проходах; только оформление.","m",1,6,.25f,3);
            V("grateSpacing","well-art","Шаг решётки","Видимая частота прутьев; физическая опора и огонь сохраняются.","m",.2f,.6f,.05f,.3f);
            V("signScale","well-art","Масштаб маркировки","Читаемость номеров и надписей этажей.","ratio",.5f,2,.1f,1);
            V("daylight","well-light","Дневной свет","Мягкий верхний свет колодца.","intensity",0,3,.05f,.5f);
            V("spotAngle","well-light","Угол офисного света","Ширина светового пятна.","deg",60,160,5,140);
            V("innerAngle","well-light","Внутренний угол света","Плавность края светового пятна.","deg",10,130,5,100);
            V("fill","well-light","Рассеянный свет","Светлая офисная лаборатория.","ratio",.1f,1,.05f,.8f);
            V("lamp","well-light","Офисные светильники","Яркость потолочных источников.","intensity",0,50,.5f,12);
            V("lampRange","well-light","Радиус светильника","Освещение широких залов.","m",3,20,.5f,12);
            V("glassOpacity","well-art","Непрозрачность стекла","Читаемость непробиваемого окна.","ratio",.1f,.6f,.05f,.25f);
            return p;
        }
    }
}
