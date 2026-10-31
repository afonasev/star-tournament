using System;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        internal const string CobaltFxPrefix="presentation.cobalt.";
        void AddCobaltShotDescriptors()
        {
            void F(string key,string label,string description,string unit,float min,float max,float step,float value)
                =>Add(CobaltFxPrefix+key,"cobalt-shot-effects",label,description,unit,min,max,step,value);
            F("enabled","COBALT · эффекты","Вспышка и контактные эффекты; физические пули и урон не меняются.","boolean",0,1,1,1);
            F("flashSeconds","Вспышка · время","Затухание дульного импульса по часам матча.","seconds",.01f,.12f,.005f,.045f);
            F("flashSize","Вспышка · размер","Диаметр лепестков у среза ствола.","meters",.02f,.5f,.01f,.19f);
            F("flashOffset","Вспышка · вынос","Вынос центра вспышки вперёд от реального дула.","meters",0,.2f,.005f,.035f);
            F("flashRadiance","Вспышка · яркость","Яркость белого ядра и тёплых лепестков.","ratio",.1f,8,.1f,3.2f);
            F("lightSeconds","Свет · время","Длительность локального импульса освещения.","seconds",.01f,.15f,.005f,.065f);
            F("lightIntensity","Свет · сила","Пиковый свет на окружении и бойцах.","intensity",0,20,.005f,.06f);
            F("viewLightIntensity","Свет · близкая модель","Сила света на собственном оружии и руках с учётом малого расстояния до дула.","intensity",0,1,.005f,.025f);
            F("lightRange","Свет · дальность","Дальность короткого локального света.","meters",.1f,8,.1f,2.8f);
            F("lightBack","Свет · отступ","Смещение света назад от дула для бликов на оружии.","meters",0,.4f,.01f,.12f);
            F("maxLights","Свет · лимит","Максимум одновременных источников; local view имеет приоритет.","count",0,16,1,6);
            F("red","Импульс · красный","Красный канал вспышки, искр и света.","ratio",0,1,.01f,1);
            F("green","Импульс · зелёный","Зелёный канал вспышки, искр и света.","ratio",0,1,.01f,.48f);
            F("blue","Импульс · синий","Синий канал вспышки, искр и света.","ratio",0,1,.01f,.12f);
            F("tracerRadiance","Пуля · яркость","Яркость короткого следа; цвет, длина и толщина задаются настройками трассера.","ratio",.1f,8,.1f,2.8f);
            F("tracerCore","Пуля · светлое ядро","Примесь белого к цвету трассера у головной части.","ratio",0,1,.05f,.8f);
            F("smokeSeconds","Дуло · дым","Время лёгкого остаточного дыма после импульса.","seconds",.02f,1,.02f,.24f);
            F("smokeSize","Дуло · размер дыма","Максимальный диаметр дымка.","meters",.01f,.5f,.01f,.14f);
            F("smokeOpacity","Дым · прозрачность","Непрозрачность лёгкого дульного дымка.","ratio",0,.5f,.01f,.12f);
            F("impactSeconds","Контакт · время","Время контактных искр и пыли, без дополнительного урона.","seconds",.02f,1,.02f,.28f);
            F("impactSize","Контакт · размер","Диаметр начальной контактной вспышки на поверхности.","meters",.01f,.5f,.01f,.09f);
            F("sparkCount","Контакт · искры","Число коротких искр на одно попадание в окружение.","count",1,24,1,8);
            F("sparkSpeed","Искры · скорость","Скорость только визуальных искр от поверхности.","meters-per-second",.1f,10,.1f,3.4f);
            F("sparkSize","Искры · размер","Толщина маленьких светящихся искр.","meters",.002f,.05f,.001f,.009f);
            F("sparkGravity","Искры · оседание","Вертикальное оседание декоративных искр.","meters-per-second-squared",0,20,.5f,8);
            F("dustSize","Контакт · пыль","Максимальный диаметр мягкого пылевого облачка.","meters",.02f,.6f,.01f,.18f);
            F("dustOpacity","Контакт · плотность пыли","Непрозрачность пыли на твёрдом окружении.","ratio",0,.8f,.01f,.22f);
            F("maxContacts","Контакт · лимит","Число одновременно хранимых эффектов; новый заменяет самый старый.","count",1,64,1,24);
        }
        internal ProvingProfile BeforeCobaltShotEffects()
        {
            if(id!=DefaultId||Descriptor(CobaltFxPrefix+"enabled")==null)return this;
            var copy=DetachedCopy();
            copy.descriptors.RemoveAll(d=>d.Path.StartsWith(CobaltFxPrefix,StringComparison.Ordinal));
            copy.values.RemoveAll(v=>v.Path.StartsWith(CobaltFxPrefix,StringComparison.Ordinal));return copy;
        }
    }
}
