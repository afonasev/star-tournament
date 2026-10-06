namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        internal ProvingProfile BeforeWeaponSwitch()
        {
            var p=BeforeShoulderSwitch();
            p.descriptors.RemoveAll(d=>d.Path=="view.switchClearance");
            p.values.RemoveAll(v=>v.Path=="view.switchClearance");
            if(p.id=="unity-trooper-presentation-v1")p.version=2;
            return p;
        }
        internal ProvingProfile BeforeShoulderSwitch()
        {
            var p=BeforeLowWeaponSwitch();
            foreach(var path in new[]{"view.switchShoulderDegrees","view.switchElbowDegrees"})
            {p.descriptors.RemoveAll(d=>d.Path==path);p.values.RemoveAll(v=>v.Path==path);}
            if(p.id=="unity-trooper-presentation-v1")
            {
                p.version=3;
                p.Add("view.switchClearance","trooper-view","Запас подъёма при смене","Дополнительный подъём над верхней границей кадра после учёта размеров рук и всех оружий. Длительность задаёт weapon.switchSeconds.","meters",.01f,.5f,.01f,.1f);
            }
            return p;
        }
        internal ProvingProfile BeforeLowWeaponSwitch()
        {
            var p=BeforeMediumWeaponSwitch();
            if(p.id=="unity-trooper-presentation-v1"&&p.FindDescriptor("view.switchShoulderDegrees")!=null)
            {
                p.version=4;
                var shoulder=p.FindDescriptor("view.switchShoulderDegrees");
                shoulder.Label="Замах за спину";shoulder.Description="Вращение рук от неподвижных плеч за голову при смене оружия. Весь цикл следует weapon.switchSeconds.";
                shoulder.Minimum=120;shoulder.Maximum=175;shoulder.DefaultValue=145;p.Set(shoulder.Path,145);
                var elbow=p.FindDescriptor("view.switchElbowDegrees");
                elbow.Description="Дополнительный сгиб предплечий при убирании оружия за спину; плечи остаются на месте.";
                elbow.Maximum=60;elbow.DefaultValue=35;p.Set(elbow.Path,35);
            }
            return p;
        }
        internal ProvingProfile BeforeMediumWeaponSwitch()
        {
            var p=BeforeWeaponPickups();
            if(p.id=="unity-trooper-presentation-v1"&&p.FindDescriptor("view.switchShoulderDegrees")!=null)
            {
                p.version=5;
                var shoulder=p.FindDescriptor("view.switchShoulderDegrees");
                shoulder.Description="Небольшой подъём от неподвижных плеч перед камерой; в верхней позе оружие меняется в руках. Весь цикл следует weapon.switchSeconds.";
                shoulder.Maximum=35;shoulder.DefaultValue=25;p.Set(shoulder.Path,25);
            }
            return p;
        }
        internal static float SafeSwitchUpgrade(NumericDescriptor old,NumericDescriptor current,float value)
        {
            if(old.Path!="view.switchShoulderDegrees"&&old.Path!="view.switchElbowDegrees")return value;
            // Old authored defaults follow the revised gesture; custom values stay within its safe range.
            return value==old.DefaultValue?current.DefaultValue:UnityEngine.Mathf.Clamp(value,current.Minimum,current.Maximum);
        }
        public static ProvingProfile CreateTrooperDefault()
        {
            var p=new ProvingProfile{id="unity-trooper-presentation-v1",version=6};
            p.Add("animation.walkThreshold","trooper-animation","Порог шага","Минимальная фактическая горизонтальная скорость для walk.","meters-per-second",.01f,3,.01f,.1f);
            p.Add("animation.runThreshold","trooper-animation","Порог бега","Фактическая горизонтальная скорость переключения walk на run.","meters-per-second",3,15,.1f,6);
            p.Add("animation.blendSeconds","trooper-animation","Переход позы","Время сглаживания весов только визуальных клипов.","seconds",0,.5f,.01f,.12f);
            p.Add("animation.aimHoldSeconds","trooper-animation","Боевая стойка","Сколько удерживать aim после принятого выстрела.","seconds",.4f,5,.1f,1.6f);
            p.Add("view.x","trooper-view","Руки по горизонтали","Смещение всей модели рук и оружия относительно камеры.","meters",-.5f,.5f,.01f,.20f);
            p.Add("view.y","trooper-view","Руки по вертикали","Смещение feet-origin модели рук относительно камеры.","meters",-2,-.5f,.01f,-1.46f);
            p.Add("view.switchShoulderDegrees","trooper-view","Подъём рук при смене","Промежуточный по высоте подъём от неподвижных плеч перед камерой; в верхней позе оружие меняется в руках. Весь цикл следует weapon.switchSeconds.","degrees",0,75,1,60);
            p.Add("view.switchElbowDegrees","trooper-view","Сгиб локтей при смене","Небольшой сгиб предплечий при смене; ограничен, чтобы кисти не проходили через камеру.","degrees",0,10,1,0);
            p.Add("view.z","trooper-view","Руки по глубине","Смещение модели рук вперёд относительно камеры.","meters",-.5f,1,.01f,.05f);
            p.Add("grip.supportRoll","trooper-grip","Разворот опорной кисти","Доворот тыльной стороны левой кисти к внешней стороне цевья после клипа.","degrees",-90,90,1,55);
            p.Add("grip.supportFingerCurl","trooper-grip","Хват опорных пальцев","Добавочный сгиб пальцев вокруг цевья; не меняет стрельбу.","degrees",-30,45,1,12);
            p.Add("grip.supportThumbCurl","trooper-grip","Опорный большой палец","Добавочный сгиб большого пальца в опорном хвате.","degrees",-30,45,1,5);
            p.Add("grip.primaryProximalCurl","trooper-grip","Основания пальцев правой руки","Добавочный сгиб оснований пальцев вокруг рукояти.","degrees",-20,40,1,15);
            p.Add("grip.primaryIntermediateCurl","trooper-grip","Средние фаланги правой руки","Добавочный сгиб средних фаланг вокруг рукояти.","degrees",-20,45,1,28);
            p.Add("grip.primaryDistalCurl","trooper-grip","Кончики пальцев правой руки","Добавочный сгиб кончиков пальцев вокруг рукояти.","degrees",-20,40,1,18);
            p.Add("grip.primaryThumbFactor","trooper-grip","Сгиб большого пальца правой руки","Доля сгиба остальных пальцев для оппозиции большого пальца.","ratio",0,1,.05f,.5f);
            p.Add("view.fillIntensity","trooper-view","Подсветка оборудования","Мягкий свет только на руки своего viewport для читаемости тёмной перчатки.","intensity",0,4,.05f,.05f);
            p.Add("view.fillRange","trooper-view","Дальность подсветки","Радиус локального света оборудования; мир исключён маской слоя.","meters",.5f,3,.1f,2);
            return p;
        }
    }
}
