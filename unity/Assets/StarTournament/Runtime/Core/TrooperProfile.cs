using System.Linq;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        internal ProvingProfile BeforeWeaponGripFits()
        {
            if(id!="unity-trooper-presentation-v1"||version<12)return this;
            var p=DetachedCopy();p.version=11;
            p.descriptors.RemoveAll(d=>System.Enum.GetNames(typeof(WeaponId)).Any(w=>d.Path.StartsWith("grip."+w+"."))||(d.Path.StartsWith("grip.support")&&d.Path.EndsWith("Bias")));
            p.values.RemoveAll(v=>System.Enum.GetNames(typeof(WeaponId)).Any(w=>v.Path.StartsWith("grip."+w+"."))||(v.Path.StartsWith("grip.support")&&v.Path.EndsWith("Bias")));
            return p;
        }
        internal ProvingProfile BeforePrimaryGripFit()
        {
            if(id!="unity-trooper-presentation-v1"||version<11)return this;
            var p=BeforeWeaponGripFits().DetachedCopy();p.version=10;
            p.descriptors.RemoveAll(d=>d.Path=="grip.primaryWristYaw");
            p.values.RemoveAll(v=>v.Path=="grip.primaryWristYaw");
            return p;
        }
        internal ProvingProfile BeforeDiffuseEquipmentFill()
        {
            if(id!="unity-trooper-presentation-v1"||version<10)return this;
            var p=BeforePrimaryGripFit().DetachedCopy();p.version=9;
            p.descriptors.RemoveAll(d=>d.Path=="view.fillBackMeters");
            p.values.RemoveAll(v=>v.Path=="view.fillBackMeters");
            return p;
        }
        internal ProvingProfile BeforeVectorFighter()
        {
            if(id!="unity-trooper-presentation-v1"||version<9)return this;
            var p=BeforeDiffuseEquipmentFill().DetachedCopy();p.version=8;
            p.descriptors.RemoveAll(d=>d.Path.StartsWith("animation.air")||d.Path.StartsWith("animation.land")||d.Path=="view.airMeters");
            p.values.RemoveAll(v=>v.Path.StartsWith("animation.air")||v.Path.StartsWith("animation.land")||v.Path=="view.airMeters");
            return p;
        }
        internal ProvingProfile BeforeEquipmentReadability()
        {
            if(id!="unity-trooper-presentation-v1"||version<8)return this;
            var p=BeforeVectorFighter().DetachedCopy();p.version=7;
            p.FindDescriptor("view.fillIntensity").DefaultValue=.05f;
            p.Set("view.fillIntensity",.05f);
            return p;
        }
        internal ProvingProfile BeforeSmoothFirstPersonWalk()
        {
            if(id!="unity-trooper-presentation-v1"||version<7)return this;
            var p=BeforeEquipmentReadability().DetachedCopy();p.version=6;
            p.descriptors.RemoveAll(d=>d.Path.StartsWith("view.walk"));
            p.values.RemoveAll(v=>v.Path.StartsWith("view.walk"));
            return p;
        }
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
            var p=BeforeWeaponPickups().BeforeSmoothFirstPersonWalk();
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
            // Untouched equipment defaults adopt the revised fill in serialized scenes and Lab;
            // explicit off and other custom strengths stay unchanged. Historical snapshots are never edited.
            if(old.Path=="view.fillIntensity")return value==old.DefaultValue?current.DefaultValue:value;
            if(old.Path!="view.switchShoulderDegrees"&&old.Path!="view.switchElbowDegrees")return value;
            // Old authored defaults follow the revised gesture; custom values stay within its safe range.
            return value==old.DefaultValue?current.DefaultValue:UnityEngine.Mathf.Clamp(value,current.Minimum,current.Maximum);
        }
        public static ProvingProfile CreateTrooperDefault()
        {
            var p=new ProvingProfile{id="unity-trooper-presentation-v1",version=12};
            p.Add("animation.airResponseSeconds","trooper-animation","Переход в прыжок","Плавность визуальной позы при отрыве и возвращении на опору.","seconds",.03f,.5f,.01f,.12f);
            p.Add("animation.airHipDegrees","trooper-animation","Подъём бедра в воздухе","Насколько боец поднимает бедро в прыжке.","degrees",0,65,1,30);
            p.Add("animation.airKneeDegrees","trooper-animation","Сгиб колена в воздухе","Насколько боец поджимает ноги в прыжке.","degrees",0,100,1,55);
            p.Add("animation.landSeconds","trooper-animation","Возврат после приземления","Длительность визуального поглощения приземления.","seconds",.1f,.8f,.01f,.28f);
            p.Add("animation.landDegrees","trooper-animation","Амплитуда приземления","Сгиб ног при касании опоры.","degrees",0,35,1,14);
            p.Add("view.airMeters","trooper-view","Ход рук в прыжке","Мягкое опускание рук в прыжке и при приземлении; камера остаётся стабильной.","meters",0,.08f,.001f,.025f);
            p.Add("animation.walkThreshold","trooper-animation","Порог шага","Минимальная фактическая горизонтальная скорость для walk.","meters-per-second",.01f,3,.01f,.1f);
            p.Add("animation.runThreshold","trooper-animation","Порог бега","Фактическая горизонтальная скорость переключения walk на run.","meters-per-second",3,15,.1f,6);
            p.Add("animation.blendSeconds","trooper-animation","Переход позы","Время сглаживания весов только визуальных клипов.","seconds",0,.5f,.01f,.12f);
            p.Add("animation.aimHoldSeconds","trooper-animation","Боевая стойка","Сколько удерживать aim после принятого выстрела.","seconds",.4f,5,.1f,1.6f);
            p.Add("view.walkCycleSeconds","trooper-view","Ритм покачивания","Длительность полного плавного цикла влево-вправо; два мягких шага за цикл, независимо от клипов тела.","seconds",.8f,3,.05f,1.3f);
            p.Add("view.walkHorizontalMeters","trooper-view","Ширина покачивания","Максимальное смещение рук и оружия в сторону при наземном движении.","meters",0,.03f,.001f,.008f);
            p.Add("view.walkVerticalMeters","trooper-view","Высота покачивания","Максимальное мягкое вертикальное смещение рук и оружия при шаге.","meters",0,.02f,.001f,.005f);
            p.Add("view.walkResponseSeconds","trooper-view","Плавность шага","Время отклика интенсивности покачивания на начало, остановку и отрыв от земли.","seconds",.05f,.8f,.01f,.2f);
            p.Add("view.x","trooper-view","Руки по горизонтали","Смещение всей модели рук и оружия относительно камеры.","meters",-.5f,.5f,.01f,.20f);
            p.Add("view.y","trooper-view","Руки по вертикали","Смещение feet-origin модели рук относительно камеры.","meters",-2,-.5f,.01f,-1.46f);
            p.Add("view.switchShoulderDegrees","trooper-view","Подъём рук при смене","Промежуточный по высоте подъём от неподвижных плеч перед камерой; в верхней позе оружие меняется в руках. Весь цикл следует weapon.switchSeconds.","degrees",0,75,1,60);
            p.Add("view.switchElbowDegrees","trooper-view","Сгиб локтей при смене","Небольшой сгиб предплечий при смене; ограничен, чтобы кисти не проходили через камеру.","degrees",0,10,1,0);
            p.Add("view.z","trooper-view","Руки по глубине","Смещение модели рук вперёд относительно камеры.","meters",-.5f,1,.01f,.05f);
            p.Add("grip.primaryWristYaw","trooper-grip","Подвод ладони к рукояти","Поворот правой кисти вокруг вертикали оружия без смещения самого оружия.","degrees",-25,25,1,-12);
            p.Add("grip.supportRoll","trooper-grip","Разворот опорной кисти","Доворот тыльной стороны левой кисти к внешней стороне цевья после клипа.","degrees",-90,90,1,55);
            p.Add("grip.supportFingerCurl","trooper-grip","Хват опорных пальцев","Добавочный сгиб пальцев вокруг цевья; не меняет стрельбу.","degrees",-30,45,1,12);
            p.Add("grip.supportThumbCurl","trooper-grip","Опорный большой палец","Добавочный сгиб большого пальца в опорном хвате.","degrees",-30,45,1,5);
            p.Add("grip.primaryProximalCurl","trooper-grip","Основания пальцев правой руки","Добавочный сгиб оснований пальцев вокруг рукояти.","degrees",-20,40,1,15);
            p.Add("grip.primaryIntermediateCurl","trooper-grip","Средние фаланги правой руки","Добавочный сгиб средних фаланг вокруг рукояти.","degrees",-20,45,1,28);
            p.Add("grip.primaryDistalCurl","trooper-grip","Кончики пальцев правой руки","Добавочный сгиб кончиков пальцев вокруг рукояти.","degrees",-20,40,1,18);
            p.Add("grip.primaryThumbFactor","trooper-grip","Сгиб большого пальца правой руки","Доля сгиба остальных пальцев для оппозиции большого пальца.","ratio",0,1,.05f,.5f);
            p.Add("view.fillIntensity","trooper-view","Подсветка оборудования","Мягкий свет только на руки своего viewport для читаемости тёмной перчатки.","intensity",0,4,.05f,.35f);
            p.Add("view.fillBackMeters","trooper-view","Удаление подсветки","Расстояние света за камерой: убирает резкий пересвет ближнего рукава, сохраняя подсветку оборудования.","meters",0,1.5f,.05f,.25f);
            p.Add("view.fillRange","trooper-view","Дальность подсветки","Радиус локального света оборудования; мир исключён маской слоя.","meters",.5f,3,.1f,2);
            p.Add("grip.supportProximalBias","trooper-grip","Основания опорных пальцев","Разгиб оснований пальцев для опоры снизу цевья.","degrees",-60,60,1,-25);
            p.Add("grip.supportIntermediateBias","trooper-grip","Средние опорные фаланги","Сгиб вокруг боковой грани цевья.","degrees",-60,60,1,25);
            p.Add("grip.supportDistalBias","trooper-grip","Кончики опорных пальцев","Замыкание хвата вокруг боковой грани.","degrees",-60,60,1,10);
            foreach(WeaponId weapon in System.Enum.GetValues(typeof(WeaponId)))
            {
                var prefix="grip."+weapon+".";var group="trooper-grip-"+weapon;
                p.Add(prefix+"primaryYaw",group,"Доворот правой кисти","Поправка контакта с рукоятью выбранного оружия; оружие остаётся неподвижным.","degrees",-20,20,1,0);
                p.Add(prefix+"primaryCurl",group,"Обхват рукояти","Дополнительный сгиб пальцев правой руки под толщину рукояти.","degrees",-20,20,1,0);
                p.Add(prefix+"supportRoll",group,"Разворот поперёк цевья","Поправка разворота опорной ладони вокруг её продольной оси.","degrees",-90,90,1,-55);
                p.Add(prefix+"supportYaw",group,"Направление опорной ладони","Разворот ладони вокруг точки контакта на цевье.","degrees",-100,100,1,80);
                p.Add(prefix+"supportCurl",group,"Обхват цевья","Поправка сгиба опорных пальцев под корпус оружия.","degrees",-30,90,1,weapon==WeaponId.RocketLauncher||weapon==WeaponId.Cutter?15:20);
                p.Add(prefix+"supportThumb",group,"Опорный большой палец","Поправка большого пальца под корпус оружия.","degrees",-30,60,1,-5);
                p.Add(prefix+"thumbOpposition",group,"Оппозиция большого пальца","Разворот большого пальца вокруг поперечного сечения цевья.","degrees",-90,90,1,weapon==WeaponId.RocketLauncher||weapon==WeaponId.Cutter?90:75);
                foreach(var axis in new[]{"X","Y","Z"})p.Add(prefix+"support"+axis,group,"Контакт цевья "+axis,"Точка контакта оснований пальцев в координатах оружия; длины костей сохраняются.","rig-units",-.8f,.8f,.005f,axis=="Z"?(weapon==WeaponId.Rifle?.30f:weapon==WeaponId.Shotgun?.42f:.40f):axis=="Y"?(weapon==WeaponId.RocketLauncher?-.20f:weapon==WeaponId.Cutter?-.20f:weapon==WeaponId.Shotgun?-.15f:-.12f):(weapon==WeaponId.RocketLauncher||weapon==WeaponId.Cutter?.08f:.06f));
            }
            return p;
        }
    }
}
