namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        static void VeteranDescriptors(ProvingProfile p)
        {
            void V(string key,string label,string description,string unit,float min,float max,float step,float value)
                =>p.Add("veteran."+key,"veteran-league",label,description,unit,min,max,step,value);
            V("hallFloorValue","Пол зала","Светлота резиновых спортивных дорожек.","ratio",.08f,.5f,.01f,.22f);
            V("columnBandHeight","Пояс колонн","Высота защитного окрашенного пояса над полом.","m",.5f,2.5f,.1f,1.2f);
            V("ceilingServiceDepth","Потолочные коммуникации","Отступ кабельных лотков от потолка; выше игровых силуэтов.","m",.05f,.35f,.01f,.16f);
            V("portalLightWidth","Свет портала","Ширина светового акцента по краям высоких входов.","m",.015f,.08f,.005f,.035f);
            V("wear","Износ образца","Потёртые кромки и царапины материалов центрального зала.","ratio",0,.6f,.01f,.28f);
            V("roughness","Матовые покрытия","Ширина блика на старой краске и резине.","ratio",.1f,.9f,.01f,.72f);
            V("lightIntensity","Сервисный свет","Тёплый свет у шкафа; без дополнительной карты теней.","intensity",0,8,.1f,3.2f);
            V("lightRange","Дальность сервисного света","Размер локального тёплого пятна.","m",1,8,.1f,4);
            V("steamInterval","Интервал пара","Пауза между малыми выпусками у вентиляционной трубы.","s",4,30,1,12);
            V("steamLifetime","Длительность пара","Время жизни малой струи вне портала.","s",.2f,2,.1f,1.1f);
            V("steamSpeed","Скорость пара","Скорость малой струи у сервисной трубы.","m/s",.05f,.5f,.01f,.18f);
            V("steamSize","Размер пара","Диаметр частиц возле технической ниши.","m",.03f,.3f,.01f,.14f);
            V("steamOpacity","Прозрачность пара","Слабая видимость пара без непрозрачного облака.","ratio",0,.3f,.01f,.12f);
        }
    }
}
