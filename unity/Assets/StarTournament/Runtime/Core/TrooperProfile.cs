using System.Linq;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        // Reconstruct exact @19 digit defaults/ranges for saved Lab snapshots.
        internal ProvingProfile BeforeArticulatedFingerGrips()
        {
            if(id!="unity-trooper-presentation-v1"||version<20)return this;
            var p=DetachedCopy();p.version=19;
            foreach(var old in CreateTrooperDefault(false).Descriptors.Where(d=>d.Path.StartsWith("grip.")&&(d.Path.Contains(".left.")||d.Path.Contains(".right."))))
            {
                var d=p.FindDescriptor(old.Path);d.Minimum=old.Minimum;d.Maximum=old.Maximum;d.DefaultValue=old.DefaultValue;p.Set(d.Path,old.DefaultValue);
            }
            return p;
        }
        // Preserve exact @18 digit poses for existing Lab histories.
        internal ProvingProfile BeforeRefinedHandAnatomy()
        {
            if(id!="unity-trooper-presentation-v1"||version<19)return this;
            var p=BeforeArticulatedFingerGrips().DetachedCopy();p.version=18;
            var weapons=new[]{WeaponId.Rifle,WeaponId.Shotgun,WeaponId.RocketLauncher,WeaponId.Cutter};
            var old=new float[,]{{-2,4,2,0},{0,6,3,0},{3,8,4,0},{2,7,5,0}};
            var joints=new[]{"Proximal","Intermediate","Distal","Spread"};
            for(int w=0;w<weapons.Length;w++)for(int j=0;j<joints.Length;j++)
            {
                var path="grip."+weapons[w]+".right.Thumb."+joints[j];
                p.FindDescriptor(path).DefaultValue=old[w,j];p.Set(path,old[w,j]);
            }
            return p;
        }
        // Exact @17 factory poses remain available to saved Lab snapshots.
        internal ProvingProfile BeforeStableSupportGrip()
        {
            if(id!="unity-trooper-presentation-v1"||version<18)return this;
            var p=BeforeRefinedHandAnatomy().DetachedCopy();p.version=17;
            foreach(var weapon in new[]{WeaponId.RocketLauncher,WeaponId.Cutter})
            {
                var prefix="grip."+weapon+".";
                void Restore(string name,float value){var d=p.FindDescriptor(prefix+name);d.DefaultValue=value;p.Set(d.Path,value);}
                Restore("wristAlignment",1);Restore("supportPitch",8);Restore("supportYaw",1);
                Restore("supportX",-.105f);Restore("supportY",-.11f);Restore("supportZ",weapon==WeaponId.RocketLauncher?.46f:.375f);
            }
            return p;
        }
        // Exact @16 schema/defaults remain available to saved Lab snapshots.
        internal ProvingProfile BeforeAlignedSupportWrist()
        {
            if(id!="unity-trooper-presentation-v1"||version<17)return this;
            var p=BeforeStableSupportGrip().DetachedCopy();p.version=16;
            p.descriptors.RemoveAll(d=>d.Path.EndsWith(".wristAlignment"));
            p.values.RemoveAll(v=>v.Path.EndsWith(".wristAlignment"));
            return p;
        }
        // Exact factory defaults before the fitted closed vertical grips (@16).
        internal ProvingProfile BeforeClosedVerticalGrips()
        {
            if(id!="unity-trooper-presentation-v1"||version<16)return this;
            var p=BeforeAlignedSupportWrist().DetachedCopy();p.version=15;
            foreach(var weapon in new[]{WeaponId.RocketLauncher,WeaponId.Cutter})
            {
                var prefix="grip."+weapon+".";
                void Restore(string name,float value){var d=p.FindDescriptor(prefix+name);d.DefaultValue=value;p.Set(d.Path,value);}
                Restore("supportPitch",0);Restore("supportYaw",weapon==WeaponId.RocketLauncher?40:35);
                Restore("supportRoll",-55);Restore("supportX",.06f);Restore("supportY",-.20f);Restore("supportZ",.48f);
                Restore("supportCurl",15);Restore("thumbOpposition",weapon==WeaponId.RocketLauncher?39:42);
                float[,] digits=weapon==WeaponId.RocketLauncher?new float[,] {{45,-33,0,-60},{-6,-6,45,-32},{-11,35,26,-29},{5,45,12,-43},{3,44,5,-29}}:
                    new float[,] {{45,-37,3,-64},{-6,-9,45,-36},{-10,35,25,-33},{7,45,16,-48},{8,45,7,-32}};
                var names=new[]{"Thumb","Index","Middle","Ring","Little"};var joints=new[]{"Proximal","Intermediate","Distal","Spread"};
                for(int digit=0;digit<5;digit++)for(int joint=0;joint<4;joint++)Restore("left."+names[digit]+"."+joints[joint],digits[digit,joint]);
            }
            return p;
        }
        internal ProvingProfile BeforePlantedSteps()
        {
            if(id!="unity-trooper-presentation-v1"||version<15)return this;
            var p=BeforeClosedVerticalGrips().DetachedCopy();p.version=14;
            p.descriptors.RemoveAll(d=>d.Path.StartsWith("animation.step"));
            p.values.RemoveAll(v=>v.Path.StartsWith("animation.step"));
            var threshold=p.FindDescriptor("animation.walkThreshold");threshold.Label="Порог шага";threshold.Description="Минимальная фактическая горизонтальная скорость для walk.";
            return p;
        }
        internal ProvingProfile BeforeVerticalSupportGrips()
        {
            if(id!="unity-trooper-presentation-v1"||version<14)return this;
            var p=BeforePlantedSteps().DetachedCopy();p.version=13;
            p.descriptors.RemoveAll(d=>d.Path.EndsWith(".supportPitch"));
            p.values.RemoveAll(v=>v.Path.EndsWith(".supportPitch"));
            return p;
        }
        internal ProvingProfile BeforeAnatomicalGrips()
        {
            if(id!="unity-trooper-presentation-v1"||version<13)return this;
            var p=BeforeVerticalSupportGrips().DetachedCopy();p.version=12;
            p.descriptors.RemoveAll(d=>d.Path.Contains(".left.")||d.Path.Contains(".right."));
            p.values.RemoveAll(v=>v.Path.Contains(".left.")||v.Path.Contains(".right."));
            foreach(WeaponId weapon in System.Enum.GetValues(typeof(WeaponId)))
            {
                string prefix="grip."+weapon+".";
                void Restore(string name,float value){var d=p.FindDescriptor(prefix+name);d.DefaultValue=value;p.Set(d.Path,value);}
                Restore("supportYaw",80);Restore("thumbOpposition",weapon==WeaponId.RocketLauncher||weapon==WeaponId.Cutter?90:75);
                Restore("supportX",weapon==WeaponId.RocketLauncher||weapon==WeaponId.Cutter?.08f:.06f);
                Restore("supportZ",weapon==WeaponId.Rifle?.30f:weapon==WeaponId.Shotgun?.42f:.40f);
            }
            return p;
        }
        internal ProvingProfile BeforeWeaponGripFits()
        {
            if(id!="unity-trooper-presentation-v1"||version<12)return this;
            var p=BeforeAnatomicalGrips().DetachedCopy();p.version=11;
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
        public static ProvingProfile CreateTrooperDefault()=>CreateTrooperDefault(true);
        static ProvingProfile CreateTrooperDefault(bool articulated)
        {
            var p=new ProvingProfile{id="unity-trooper-presentation-v1",version=19};
            p.Add("animation.stepSeconds","trooper-animation","Длительность короткого шага","Время переноса стопы при коротком движении; на скорости шаг ускоряется по длине.","seconds",.1f,.6f,.01f,.28f);
            p.Add("animation.stepLengthMeters","trooper-animation","Длина шага","Предельное продвижение за шаг; опорная стопа остаётся на месте в мире.","meters",.2f,1.2f,.05f,.65f);
            p.Add("animation.stepHeightMeters","trooper-animation","Подъём стопы","Высота переноса при полном шаге; короткие движения поднимают стопу пропорционально.","meters",.01f,.2f,.01f,.09f);
            p.Add("animation.airResponseSeconds","trooper-animation","Переход в прыжок","Плавность визуальной позы при отрыве и возвращении на опору.","seconds",.03f,.5f,.01f,.12f);
            p.Add("animation.airHipDegrees","trooper-animation","Подъём бедра в воздухе","Насколько боец поднимает бедро в прыжке.","degrees",0,65,1,30);
            p.Add("animation.airKneeDegrees","trooper-animation","Сгиб колена в воздухе","Насколько боец поджимает ноги в прыжке.","degrees",0,100,1,55);
            p.Add("animation.landSeconds","trooper-animation","Возврат после приземления","Длительность визуального поглощения приземления.","seconds",.1f,.8f,.01f,.28f);
            p.Add("animation.landDegrees","trooper-animation","Амплитуда приземления","Сгиб ног при касании опоры.","degrees",0,35,1,14);
            p.Add("view.airMeters","trooper-view","Ход рук в прыжке","Мягкое опускание рук в прыжке и при приземлении; камера остаётся стабильной.","meters",0,.08f,.001f,.025f);
            p.Add("animation.walkThreshold","trooper-animation","Порог покачивания рук","Минимальная скорость для покачивания рук от первого лица; стопы тела реагируют на любое фактическое перемещение.","meters-per-second",.01f,3,.01f,.1f);
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
                p.Add(prefix+"wristAlignment",group,"Выпрямление опорного запястья","Поворот локтя вокруг оси плечо–кисть к продолжению ладони; контакт пальцев и длины костей сохраняются.","ratio",0,1,.05f,0);
                p.Add(prefix+"supportPitch",group,"Наклон опорной ладони","Поворот кисти вокруг поперечной оси оружия для вертикальной передней рукояти.","degrees",-120,120,1,0);
                p.Add(prefix+"supportYaw",group,"Направление опорной ладони","Разворот ладони вокруг точки контакта на цевье.","degrees",-100,100,1,weapon==WeaponId.Rifle?35:weapon==WeaponId.Shotgun?30:weapon==WeaponId.RocketLauncher?40:35);
                p.Add(prefix+"supportCurl",group,"Обхват цевья","Поправка сгиба опорных пальцев под корпус оружия.","degrees",-30,90,1,weapon==WeaponId.RocketLauncher||weapon==WeaponId.Cutter?15:20);
                p.Add(prefix+"supportThumb",group,"Опорный большой палец","Поправка большого пальца под корпус оружия.","degrees",-30,60,1,-5);
                p.Add(prefix+"thumbOpposition",group,"Оппозиция большого пальца","Разворот большого пальца вокруг поперечного сечения цевья.","degrees",-90,90,1,weapon==WeaponId.Rifle?56:weapon==WeaponId.Shotgun?39:weapon==WeaponId.RocketLauncher?39:42);
                foreach(var axis in new[]{"X","Y","Z"})p.Add(prefix+"support"+axis,group,"Контакт цевья "+axis,"Точка контакта оснований пальцев в координатах оружия; длины костей сохраняются.","rig-units",-.8f,.8f,.005f,axis=="Z"?(weapon==WeaponId.Rifle?.42f:weapon==WeaponId.Shotgun?.52f:.48f):axis=="Y"?(weapon==WeaponId.RocketLauncher?-.20f:weapon==WeaponId.Cutter?-.20f:weapon==WeaponId.Shotgun?-.15f:-.12f):(weapon==WeaponId.RocketLauncher||weapon==WeaponId.Cutter?.06f:.04f));

                // Each row is a separately authored digit: MCP, PIP, DIP and MCP spread.
                // Values are exposed below as independent Lab descriptors, not a shared curl multiplier.
                float[,] left=weapon==WeaponId.Rifle?new float[,] {{-25,45,-36,-62},{-11,-14,45,-36},{-15,30,25,-32},{2,45,4,-48},{2,45,-3,-32}}:
                    weapon==WeaponId.Shotgun?new float[,] {{10,33,-40,-65},{-10,-22,45,-41},{-15,24,33,-37},{1,45,3,-52},{4,45,-2,-36}}:
                    weapon==WeaponId.RocketLauncher?new float[,] {{45,-33,0,-60},{-6,-6,45,-32},{-11,35,26,-29},{5,45,12,-43},{3,44,5,-29}}:
                    new float[,] {{45,-37,3,-64},{-6,-9,45,-36},{-10,35,25,-33},{7,45,16,-48},{8,45,7,-32}};
                float[,] right=weapon==WeaponId.Rifle?new float[,]{{-2,4,2,0},{-8,-12,-8,3},{0,0,0,0},{3,4,2,-2},{6,6,4,-3}}:
                    weapon==WeaponId.Shotgun?new float[,]{{0,6,3,0},{-10,-14,-10,4},{0,0,0,0},{4,3,4,-2},{8,7,5,-4}}:
                    weapon==WeaponId.RocketLauncher?new float[,]{{3,8,4,0},{-6,-10,-6,2},{0,0,0,0},{2,5,3,-1},{5,8,5,-3}}:
                    new float[,]{{2,7,5,0},{-7,-9,-5,2},{0,0,0,0},{3,6,4,-2},{7,9,6,-4}};
                var digitNames=new[]{"Thumb","Index","Middle","Ring","Little"};
                var digitLabels=new[]{"Большой","Указательный","Средний","Безымянный","Мизинец"};
                var jointNames=new[]{"Proximal","Intermediate","Distal","Spread"};
                var jointLabels=new[]{"основание","средний сустав","кончик","разведение"};
                foreach(var side in new[]{"left","right"})
                    for(int digit=0;digit<5;digit++)for(int joint=0;joint<4;joint++)
                        p.Add(prefix+side+"."+digitNames[digit]+"."+jointNames[joint],group,
                            (side=="left"?"Левая: ":"Правая: ")+digitLabels[digit]+" — "+jointLabels[joint],
                            "Индивидуальная поправка сустава для геометрии "+weapon+". Сгиб не меняет стрельбу или положение оружия.",
                            "degrees",joint==3?-70:-40,joint==3?70:45,1,(side=="left"?left:right)[digit,joint]);
            }
            void Fit(string path,float value){p.FindDescriptor(path).DefaultValue=value;p.Set(path,value);}
            Fit("grip.RocketLauncher.supportY",-0.155f);
            Fit("grip.RocketLauncher.left.Thumb.Proximal",25);
            Fit("grip.RocketLauncher.left.Thumb.Intermediate",7);
            Fit("grip.RocketLauncher.left.Thumb.Distal",33);
            Fit("grip.RocketLauncher.left.Thumb.Spread",-32);
            Fit("grip.RocketLauncher.thumbOpposition",-63);
            Fit("grip.RocketLauncher.left.Index.Proximal",2);
            Fit("grip.RocketLauncher.left.Index.Intermediate",-6);
            Fit("grip.RocketLauncher.left.Index.Distal",9);
            Fit("grip.RocketLauncher.left.Index.Spread",-12);
            Fit("grip.RocketLauncher.left.Middle.Proximal",7);
            Fit("grip.RocketLauncher.left.Middle.Intermediate",1);
            Fit("grip.RocketLauncher.left.Middle.Distal",-10);
            Fit("grip.RocketLauncher.left.Middle.Spread",-4);
            Fit("grip.RocketLauncher.left.Ring.Proximal",22);
            Fit("grip.RocketLauncher.left.Ring.Intermediate",-21);
            Fit("grip.RocketLauncher.left.Ring.Distal",-12);
            Fit("grip.RocketLauncher.left.Ring.Spread",-9);
            Fit("grip.RocketLauncher.left.Little.Proximal",10);
            Fit("grip.RocketLauncher.left.Little.Intermediate",-33);
            Fit("grip.RocketLauncher.left.Little.Distal",10);
            Fit("grip.RocketLauncher.left.Little.Spread",1);
            Fit("grip.Cutter.supportY",-0.155f);
            Fit("grip.Cutter.left.Thumb.Proximal",19);
            Fit("grip.Cutter.left.Thumb.Intermediate",12);
            Fit("grip.Cutter.left.Thumb.Distal",43);
            Fit("grip.Cutter.left.Thumb.Spread",-31);
            Fit("grip.Cutter.thumbOpposition",-78);
            Fit("grip.Cutter.left.Index.Proximal",2);
            Fit("grip.Cutter.left.Index.Intermediate",-7);
            Fit("grip.Cutter.left.Index.Distal",10);
            Fit("grip.Cutter.left.Index.Spread",-12);
            Fit("grip.Cutter.left.Middle.Proximal",8);
            Fit("grip.Cutter.left.Middle.Intermediate",-2);
            Fit("grip.Cutter.left.Middle.Distal",-17);
            Fit("grip.Cutter.left.Middle.Spread",-5);
            Fit("grip.Cutter.left.Ring.Proximal",22);
            Fit("grip.Cutter.left.Ring.Intermediate",-22);
            Fit("grip.Cutter.left.Ring.Distal",-17);
            Fit("grip.Cutter.left.Ring.Spread",-9);
            Fit("grip.Cutter.left.Little.Proximal",4);
            Fit("grip.Cutter.left.Little.Intermediate",-37);
            Fit("grip.Cutter.left.Little.Distal",-4);
            Fit("grip.Cutter.left.Little.Spread",-1);
            Fit("grip.RocketLauncher.supportPitch",0f);
            Fit("grip.RocketLauncher.supportYaw",18f);
            Fit("grip.RocketLauncher.supportRoll",49);
            Fit("grip.RocketLauncher.supportX",-0.11f);
            Fit("grip.RocketLauncher.supportZ",0.49f);
            Fit("grip.RocketLauncher.supportCurl",45);
            Fit("grip.Cutter.supportPitch",-2f);
            Fit("grip.Cutter.supportYaw",22f);
            Fit("grip.Cutter.supportRoll",49);
            Fit("grip.Cutter.supportX",-0.11f);
            Fit("grip.Cutter.supportZ",0.415f);
            Fit("grip.Cutter.supportCurl",45);
            // Thumb opposition closes the grip without moving the wrist or arm.
            Fit("grip.Rifle.right.Thumb.Proximal",-15);
            Fit("grip.Rifle.right.Thumb.Intermediate",10);
            Fit("grip.Rifle.right.Thumb.Distal",8);
            Fit("grip.Rifle.right.Thumb.Spread",-10);
            Fit("grip.Shotgun.right.Thumb.Proximal",-13);
            Fit("grip.Shotgun.right.Thumb.Intermediate",12);
            Fit("grip.Shotgun.right.Thumb.Distal",9);
            Fit("grip.Shotgun.right.Thumb.Spread",-10);
            Fit("grip.RocketLauncher.right.Thumb.Proximal",-10);
            Fit("grip.RocketLauncher.right.Thumb.Intermediate",14);
            Fit("grip.RocketLauncher.right.Thumb.Distal",10);
            Fit("grip.RocketLauncher.right.Thumb.Spread",-10);
            Fit("grip.Cutter.right.Thumb.Proximal",-11);
            Fit("grip.Cutter.right.Thumb.Intermediate",13);
            Fit("grip.Cutter.right.Thumb.Distal",11);
            Fit("grip.Cutter.right.Thumb.Spread",-10);
            if(articulated)
            {
                p.version=20;
                // Only the reviewed per-digit offsets change; wrists, arm targets
                // and weapon mounting retain exact @19 defaults.
                void Articulate(string path,float value)
                {
                    var d=p.FindDescriptor(path);d.Minimum=System.Math.Min(d.Minimum,value);d.Maximum=System.Math.Max(d.Maximum,value);d.DefaultValue=value;p.Set(path,value);
                }
                Articulate("grip.Rifle.left.Thumb.Proximal",-30);
                Articulate("grip.Rifle.left.Thumb.Intermediate",45);
                Articulate("grip.Rifle.left.Thumb.Distal",19);
                Articulate("grip.Rifle.left.Thumb.Spread",-41);
                Articulate("grip.Rifle.left.Index.Proximal",-21);
                Articulate("grip.Rifle.left.Index.Intermediate",4);
                Articulate("grip.Rifle.left.Index.Distal",2);
                Articulate("grip.Rifle.left.Index.Spread",-43);
                Articulate("grip.Rifle.left.Middle.Proximal",-3);
                Articulate("grip.Rifle.left.Middle.Intermediate",37);
                Articulate("grip.Rifle.left.Middle.Distal",12);
                Articulate("grip.Rifle.left.Middle.Spread",-36);
                Articulate("grip.Rifle.left.Ring.Proximal",37);
                Articulate("grip.Rifle.left.Ring.Intermediate",37);
                Articulate("grip.Rifle.left.Ring.Distal",2);
                Articulate("grip.Rifle.left.Ring.Spread",-37);
                Articulate("grip.Rifle.left.Little.Proximal",46);
                Articulate("grip.Rifle.left.Little.Intermediate",38);
                Articulate("grip.Rifle.left.Little.Distal",3);
                Articulate("grip.Rifle.left.Little.Spread",-26);
                Articulate("grip.Rifle.right.Thumb.Proximal",-17);
                Articulate("grip.Rifle.right.Thumb.Intermediate",40);
                Articulate("grip.Rifle.right.Thumb.Distal",31);
                Articulate("grip.Rifle.right.Thumb.Spread",18);
                Articulate("grip.Rifle.right.Index.Proximal",-30);
                Articulate("grip.Rifle.right.Index.Intermediate",14);
                Articulate("grip.Rifle.right.Index.Distal",22);
                Articulate("grip.Rifle.right.Index.Spread",18);
                Articulate("grip.Rifle.right.Middle.Proximal",-18);
                Articulate("grip.Rifle.right.Middle.Intermediate",14);
                Articulate("grip.Rifle.right.Middle.Distal",-22);
                Articulate("grip.Rifle.right.Middle.Spread",13);
                Articulate("grip.Rifle.right.Ring.Proximal",-37);
                Articulate("grip.Rifle.right.Ring.Intermediate",12);
                Articulate("grip.Rifle.right.Ring.Distal",-18);
                Articulate("grip.Rifle.right.Ring.Spread",9);
                Articulate("grip.Rifle.right.Little.Proximal",-17);
                Articulate("grip.Rifle.right.Little.Intermediate",-1);
                Articulate("grip.Rifle.right.Little.Distal",-7);
                Articulate("grip.Rifle.right.Little.Spread",-11);
                Articulate("grip.Shotgun.left.Thumb.Proximal",-34);
                Articulate("grip.Shotgun.left.Thumb.Intermediate",26);
                Articulate("grip.Shotgun.left.Thumb.Distal",41);
                Articulate("grip.Shotgun.left.Thumb.Spread",-20);
                Articulate("grip.Shotgun.left.Index.Proximal",-20);
                Articulate("grip.Shotgun.left.Index.Intermediate",19);
                Articulate("grip.Shotgun.left.Index.Distal",-24);
                Articulate("grip.Shotgun.left.Index.Spread",-47);
                Articulate("grip.Shotgun.left.Middle.Proximal",-17);
                Articulate("grip.Shotgun.left.Middle.Intermediate",47);
                Articulate("grip.Shotgun.left.Middle.Distal",-24);
                Articulate("grip.Shotgun.left.Middle.Spread",-41);
                Articulate("grip.Shotgun.left.Ring.Proximal",35);
                Articulate("grip.Shotgun.left.Ring.Intermediate",22);
                Articulate("grip.Shotgun.left.Ring.Distal",8);
                Articulate("grip.Shotgun.left.Ring.Spread",-44);
                Articulate("grip.Shotgun.left.Little.Proximal",49);
                Articulate("grip.Shotgun.left.Little.Intermediate",19);
                Articulate("grip.Shotgun.left.Little.Distal",7);
                Articulate("grip.Shotgun.left.Little.Spread",-29);
                Articulate("grip.Shotgun.right.Thumb.Proximal",-17);
                Articulate("grip.Shotgun.right.Thumb.Intermediate",40);
                Articulate("grip.Shotgun.right.Thumb.Distal",31);
                Articulate("grip.Shotgun.right.Thumb.Spread",18);
                Articulate("grip.Shotgun.right.Index.Proximal",-20);
                Articulate("grip.Shotgun.right.Index.Intermediate",14);
                Articulate("grip.Shotgun.right.Index.Distal",22);
                Articulate("grip.Shotgun.right.Index.Spread",33);
                Articulate("grip.Shotgun.right.Middle.Proximal",-18);
                Articulate("grip.Shotgun.right.Middle.Intermediate",14);
                Articulate("grip.Shotgun.right.Middle.Distal",-22);
                Articulate("grip.Shotgun.right.Middle.Spread",17);
                Articulate("grip.Shotgun.right.Ring.Proximal",-37);
                Articulate("grip.Shotgun.right.Ring.Intermediate",12);
                Articulate("grip.Shotgun.right.Ring.Distal",-18);
                Articulate("grip.Shotgun.right.Ring.Spread",9);
                Articulate("grip.Shotgun.right.Little.Proximal",-17);
                Articulate("grip.Shotgun.right.Little.Intermediate",-1);
                Articulate("grip.Shotgun.right.Little.Distal",-7);
                Articulate("grip.Shotgun.right.Little.Spread",-11);
                Articulate("grip.RocketLauncher.left.Thumb.Proximal",-18);
                Articulate("grip.RocketLauncher.left.Thumb.Intermediate",4);
                Articulate("grip.RocketLauncher.left.Thumb.Distal",7);
                Articulate("grip.RocketLauncher.left.Thumb.Spread",9);
                Articulate("grip.RocketLauncher.left.Index.Proximal",-32);
                Articulate("grip.RocketLauncher.left.Index.Intermediate",-7);
                Articulate("grip.RocketLauncher.left.Index.Distal",-18);
                Articulate("grip.RocketLauncher.left.Index.Spread",-21);
                Articulate("grip.RocketLauncher.left.Middle.Proximal",-31);
                Articulate("grip.RocketLauncher.left.Middle.Intermediate",9);
                Articulate("grip.RocketLauncher.left.Middle.Distal",-32);
                Articulate("grip.RocketLauncher.left.Middle.Spread",-13);
                Articulate("grip.RocketLauncher.left.Ring.Proximal",-15);
                Articulate("grip.RocketLauncher.left.Ring.Intermediate",0);
                Articulate("grip.RocketLauncher.left.Ring.Distal",-48);
                Articulate("grip.RocketLauncher.left.Ring.Spread",-16);
                Articulate("grip.RocketLauncher.left.Little.Proximal",-22);
                Articulate("grip.RocketLauncher.left.Little.Intermediate",-25);
                Articulate("grip.RocketLauncher.left.Little.Distal",-21);
                Articulate("grip.RocketLauncher.left.Little.Spread",19);
                Articulate("grip.RocketLauncher.right.Thumb.Proximal",8);
                Articulate("grip.RocketLauncher.right.Thumb.Intermediate",-5);
                Articulate("grip.RocketLauncher.right.Thumb.Distal",-4);
                Articulate("grip.RocketLauncher.right.Thumb.Spread",-8);
                Articulate("grip.RocketLauncher.right.Index.Proximal",-27);
                Articulate("grip.RocketLauncher.right.Index.Intermediate",10);
                Articulate("grip.RocketLauncher.right.Index.Distal",35);
                Articulate("grip.RocketLauncher.right.Index.Spread",18);
                Articulate("grip.RocketLauncher.right.Middle.Proximal",-16);
                Articulate("grip.RocketLauncher.right.Middle.Intermediate",10);
                Articulate("grip.RocketLauncher.right.Middle.Distal",-7);
                Articulate("grip.RocketLauncher.right.Middle.Spread",14);
                Articulate("grip.RocketLauncher.right.Ring.Proximal",-34);
                Articulate("grip.RocketLauncher.right.Ring.Intermediate",6);
                Articulate("grip.RocketLauncher.right.Ring.Distal",-1);
                Articulate("grip.RocketLauncher.right.Ring.Spread",2);
                Articulate("grip.RocketLauncher.right.Little.Proximal",-12);
                Articulate("grip.RocketLauncher.right.Little.Intermediate",-15);
                Articulate("grip.RocketLauncher.right.Little.Distal",1);
                Articulate("grip.RocketLauncher.right.Little.Spread",-25);
                Articulate("grip.Cutter.left.Thumb.Proximal",-38);
                Articulate("grip.Cutter.left.Thumb.Intermediate",8);
                Articulate("grip.Cutter.left.Thumb.Distal",-4);
                Articulate("grip.Cutter.left.Thumb.Spread",-17);
                Articulate("grip.Cutter.left.Index.Proximal",-36);
                Articulate("grip.Cutter.left.Index.Intermediate",-9);
                Articulate("grip.Cutter.left.Index.Distal",-16);
                Articulate("grip.Cutter.left.Index.Spread",-22);
                Articulate("grip.Cutter.left.Middle.Proximal",-35);
                Articulate("grip.Cutter.left.Middle.Intermediate",9);
                Articulate("grip.Cutter.left.Middle.Distal",-32);
                Articulate("grip.Cutter.left.Middle.Spread",-14);
                Articulate("grip.Cutter.left.Ring.Proximal",-18);
                Articulate("grip.Cutter.left.Ring.Intermediate",2);
                Articulate("grip.Cutter.left.Ring.Distal",-50);
                Articulate("grip.Cutter.left.Ring.Spread",-17);
                Articulate("grip.Cutter.left.Little.Proximal",-25);
                Articulate("grip.Cutter.left.Little.Intermediate",-17);
                Articulate("grip.Cutter.left.Little.Distal",-25);
                Articulate("grip.Cutter.left.Little.Spread",11);
                Articulate("grip.Cutter.right.Thumb.Proximal",-10);
                Articulate("grip.Cutter.right.Thumb.Intermediate",31);
                Articulate("grip.Cutter.right.Thumb.Distal",5);
                Articulate("grip.Cutter.right.Thumb.Spread",15);
                Articulate("grip.Cutter.right.Index.Proximal",-27);
                Articulate("grip.Cutter.right.Index.Intermediate",5);
                Articulate("grip.Cutter.right.Index.Distal",35);
                Articulate("grip.Cutter.right.Index.Spread",18);
                Articulate("grip.Cutter.right.Middle.Proximal",-16);
                Articulate("grip.Cutter.right.Middle.Intermediate",4);
                Articulate("grip.Cutter.right.Middle.Distal",-2);
                Articulate("grip.Cutter.right.Middle.Spread",13);
                Articulate("grip.Cutter.right.Ring.Proximal",-33);
                Articulate("grip.Cutter.right.Ring.Intermediate",-1);
                Articulate("grip.Cutter.right.Ring.Distal",4);
                Articulate("grip.Cutter.right.Ring.Spread",2);
                Articulate("grip.Cutter.right.Little.Proximal",-11);
                Articulate("grip.Cutter.right.Little.Intermediate",-19);
                Articulate("grip.Cutter.right.Little.Distal",-1);
                Articulate("grip.Cutter.right.Little.Spread",-24);
            }
            return p;
        }
    }
}
