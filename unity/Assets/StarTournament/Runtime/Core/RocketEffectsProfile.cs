namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public const string RocketEffectsId="pulse-effects-v1";
        public ProvingProfile BeforePulseVisibility()
        {
            if(id!=RocketEffectsId||version<3)return this;
            var p=UnityEngine.JsonUtility.FromJson<ProvingProfile>(UnityEngine.JsonUtility.ToJson(this));
            p.descriptors.RemoveAll(d=>d.Path=="pulseFx.blastRadiusScale");p.values.RemoveAll(v=>v.Path=="pulseFx.blastRadiusScale");p.version=2;return p;
        }
        public ProvingProfile BeforePulseRefinement()
        {
            if(id!=RocketEffectsId||version<2)return this;
            var p=UnityEngine.JsonUtility.FromJson<ProvingProfile>(UnityEngine.JsonUtility.ToJson(this));
            var added=new System.Collections.Generic.HashSet<string>{"pulseFx.blastRadiusScale","pulseFx.fireFramesPerSecond","pulseFx.lightIntensity","pulseFx.lightSeconds","pulseFx.lightRangeScale","pulseFx.lightWarmth","pulseFx.lightOffsetScale","pulseFx.maxLights"};
            p.descriptors.RemoveAll(d=>added.Contains(d.Path));p.values.RemoveAll(v=>added.Contains(v.Path));p.version=1;return p;
        }
        public void EnsureRocketEffectsDescriptors()
        {
            valueIndex=null;
            var current=CreateRocketEffectsDefault();
            foreach(var descriptor in current.Descriptors)
                if(FindDescriptor(descriptor.Path)==null)
                {descriptors.Add(descriptor);values.Add(new ProvingProfileValue{Path=descriptor.Path,Value=current.Get(descriptor.Path)});}
            version=current.Version;
        }
        public static ProvingProfile CreateRocketEffectsDefault()
        {
            var p=new ProvingProfile{id=RocketEffectsId,version=3};
            p.Add("pulseFx.exhaustLength","rocket-effects","Пламя сопла · длина","Длина плотной горячей струи позади сопла.","meters",.05f,2,.01f,.46f);
            p.Add("pulseFx.exhaustWidth","rocket-effects","Пламя сопла · ширина","Ширина горячей струи у сопла.","meters",.01f,.5f,.01f,.12f);
            p.Add("pulseFx.trailSmokeSize","rocket-effects","Дым следа · размер","Диаметр лёгкого облачка позади ракеты.","meters",.02f,.6f,.01f,.13f);
            p.Add("pulseFx.trailSmokeOpacity","rocket-effects","Дым следа · плотность","Прозрачность короткого следа.","ratio",0,1,.01f,.16f);
            p.Add("pulseFx.trailSmokeCount","rocket-effects","Дым следа · облачка","Максимум облачков на одну летящую ракету.","count",0,24,1,7);
            p.Add("pulseFx.fireCount","rocket-effects","Взрыв · лепестки","Число неровных огненных объёмов.","count",3,24,1,9);
            p.Add("pulseFx.fireLobeScale","rocket-effects","Взрыв · объём лепестка","Диаметр огненного лепестка относительно радиуса вспышки.","ratio",.1f,2,.05f,.75f);
            p.Add("pulseFx.coreSeconds","rocket-effects","Ядро · время","Время короткой бело-жёлтой вспышки.","seconds",.02f,.5f,.01f,.12f);
            p.Add("pulseFx.coreScale","rocket-effects","Ядро · размер","Диаметр яркого ядра относительно радиуса вспышки.","ratio",.05f,1,.05f,.45f);
            p.Add("pulseFx.sparkCount","rocket-effects","Взрыв · искры","Число редких расходящихся искр.","count",0,40,1,12);
            p.Add("pulseFx.sparkSeconds","rocket-effects","Искры · время","Время затухания искр по часам матча.","seconds",.05f,2,.05f,.55f);
            p.Add("pulseFx.sparkSpeed","rocket-effects","Искры · скорость","Скорость визуального разлёта; не влияет на damage.","meters-per-second",.1f,20,.1f,5);
            p.Add("pulseFx.sparkSize","rocket-effects","Искры · толщина","Размер светящейся искры.","meters",.005f,.15f,.005f,.035f);
            p.Add("pulseFx.sparkStretch","rocket-effects","Искры · вытяжка","Длина следа по скорости разлёта.","seconds",0,.3f,.01f,.08f);
            p.Add("pulseFx.smokeCount","rocket-effects","Взрыв · дым","Число полупрозрачных облачков после вспышки.","count",0,20,1,5);
            p.Add("pulseFx.smokeSeconds","rocket-effects","Дым · время","Время рассеивания дыма после вспышки.","seconds",.1f,3,.05f,.85f);
            p.Add("pulseFx.smokeDelay","rocket-effects","Дым · задержка","Мягкое появление дыма за огнём.","seconds",0,.5f,.01f,.08f);
            p.Add("pulseFx.smokeScale","rocket-effects","Дым · размер","Диаметр облачка относительно радиуса вспышки.","ratio",.1f,2,.05f,.7f);
            p.Add("pulseFx.smokeOpacity","rocket-effects","Дым · плотность","Прозрачность дыма; меньшие значения сохраняют обзор.","ratio",0,1,.01f,.24f);
            p.Add("pulseFx.smokeRise","rocket-effects","Дым · подъём","Скорость медленного подъёма дыма.","meters-per-second",0,3,.05f,.4f);
            p.Add("pulseFx.smokeShade","rocket-effects","Дым · светлота","Нейтральный серый оттенок дымовых облачков.","ratio",.05f,1,.01f,.52f);
            p.Add("pulseFx.maxBursts","rocket-effects","Взрывы · лимит","Число одновременно хранимых VFX; при заполнении заменяется самый старый.","count",1,64,1,24);
            p.Add("pulseFx.fireSpreadScale","rocket-effects","Огонь · разлёт","Разлёт лепестков относительно радиуса вспышки.","ratio",.1f,1.5f,.05f,.55f);
            p.Add("pulseFx.fireSizeVariation","rocket-effects","Огонь · неоднородность","Разница размеров огненных лепестков.","ratio",0,.8f,.05f,.2f);
            p.Add("pulseFx.sparkSpeedVariation","rocket-effects","Искры · неоднородность","Разница скоростей разлёта искр.","ratio",0,.8f,.05f,.35f);
            p.Add("pulseFx.smokeSpreadScale","rocket-effects","Дым · разлёт","Разлёт облачков относительно радиуса вспышки.","ratio",0,1.5f,.05f,.5f);
            p.Add("pulseFx.smokeInitialScale","rocket-effects","Дым · начальный размер","Начальный диаметр облачка относительно его заданного размера.","ratio",.1f,1,.05f,.5f);
            p.Add("pulseFx.coreWarmth","rocket-effects","Ядро · теплота","Примесь оранжевого цвета Pulse в белом ядре.","ratio",0,1,.01f,.12f);
            p.Add("pulseFx.exhaustCoreRatio","rocket-effects","Сопло · длина ядра","Длина яркого ядра относительно пламени сопла.","ratio",.1f,1,.05f,.7f);
            p.Add("pulseFx.fireFramesPerSecond","rocket-effects","Огонь · анимация","Частота смены деталей огня по часам матча.","frames-per-second",1,60,1,30);
            p.Add("pulseFx.lightIntensity","rocket-effects","Свет · яркость","Пиковая интенсивность тёплой вспышки на поверхностях.","intensity",0,30,.1f,8);
            p.Add("pulseFx.lightSeconds","rocket-effects","Свет · время","Время затухания света по часам матча.","seconds",.02f,1,.01f,.26f);
            p.Add("pulseFx.lightRangeScale","rocket-effects","Свет · дальность","Дальность света относительно реального радиуса взрыва.","ratio",.1f,2,.05f,1.25f);
            p.Add("pulseFx.lightWarmth","rocket-effects","Свет · теплота","Примесь оранжевого Pulse в белом свете.","ratio",0,1,.01f,.65f);
            p.Add("pulseFx.lightOffsetScale","rocket-effects","Свет · отступ","Сдвиг обратно по траектории от контакта, относительно радиуса; улучшает освещение поверхности.","ratio",0,.4f,.01f,.15f);
            p.Add("pulseFx.maxLights","rocket-effects","Свет · лимит","Максимум одновременно активных вспышек; приоритет свежим.","count",0,8,1,4);
            p.Add("pulseFx.blastRadiusScale","rocket-effects","Взрыв · визуальный радиус","Масштаб огня, ядра и дыма относительно игрового радиуса; меньший масштаб сохраняет обзор, не меняя урон и свет.","ratio",.1f,1,.05f,.8f);
            return p;
        }
    }
}
