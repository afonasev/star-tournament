namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        static void AddQuarryWarmArt(ProvingProfile p)
        {
            // Named revision-6 presentation metadata; no authoring coordinates or simulation values change.
            void Add(string key,string title,string description,string unit,float min,float max,float step,float value)
                =>p.Add("quarry.warm."+key,"quarry-warm-limestone-a",title,description,unit,min,max,step,value);
            Add("stoneTile","Масштаб известняка","Размер текстуры породы в мировых метрах.","m",1,12,.25f,6);
            Add("gravelTile","Масштаб щебня","Размер текстуры утрамбованного грунта.","m",1,12,.25f,5);
            Add("relief","Рельеф поверхности","Сила мелкого рельефа материала без изменения коллизий.","m",0,.3f,.005f,.02f);
            Add("metalSmoothness","Гладкость стали","Блики потёртого металла и окрашенной техники.","ratio",0,1,.01f,.32f);
            Add("metallic","Металлический отклик","Отражение стальных и окрашенных деталей.","ratio",0,1,.01f,.45f);
            Add("paintMetallic","Металл под краской","Мягкий отклик окрашенного корпуса техники.","ratio",0,1,.01f,.08f);
            Add("glassSmoothness","Стекло кабины","Гладкость окон погрузчика и мастерской.","ratio",0,1,.01f,.7f);
            Add("sun","Солнце A","Сила тёплого дневного света.","intensity",0,5,.05f,1.1f);
            Add("sunElevation","Высота солнца","Высота дневного света над карьером.","degrees",15,85,1,48);
            Add("sunAzimuth","Направление солнца","Азимут теней существующих объектов.","degrees",-180,180,1,-38);
            Add("shadowStrength","Плотность тени","Контраст опор и объём скал без затемнения маршрутов.","ratio",0,1,.05f,.75f);
            Add("sunRed","Солнце R","Красная составляющая дневного света.","ratio",0,1,.01f,1);
            Add("sunGreen","Солнце G","Зелёная составляющая дневного света.","ratio",0,1,.01f,.95f);
            Add("sunBlue","Солнце B","Синяя составляющая дневного света.","ratio",0,1,.01f,.86f);
        }
    }
}
