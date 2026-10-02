namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public static ProvingProfile CreateNativeCombatDefault()
        {
            var p=CreateLegacyNativeCombatDefault();
            p.UpgradeBodyDamage();
            p.version=7;
            p.RebalanceDefault("rifle.damage",10);p.RebalanceDefault("rifle.spread",1.1f);
            p.RebalanceDefault("shot.damage",85);p.RebalanceDefault("shot.pellets",18);p.RebalanceDefault("shot.spread",6);
            p.RebalanceDefault("rocket.maximumDamage",85);
            p.FindDescriptor("rocket.maximumDamage").Description="Базовый прямой урон и splash у эпицентра до усиления.";
            return p;
        }
        void UpgradeBodyDamage()
        {
            float rifle=FindDescriptor("rifle.torso")!=null?Get("rifle.torso"):20;
            float shot=FindDescriptor("shot.torso")!=null?Get("shot.torso"):70;
            foreach(var prefix in new[]{"rifle.","shot."})
                foreach(var zone in new[]{"head","torso","limb"})
                { descriptors.RemoveAll(d=>d.Path==prefix+zone);values.RemoveAll(v=>v.Path==prefix+zone); }
            valueIndex=null;
            if(FindDescriptor("rifle.damage")==null){Add("rifle.damage","rifle","Урон автомата","Урон одной пули независимо от части тела.","health",0,200,1,20);Set("rifle.damage",rifle);}
            if(FindDescriptor("shot.damage")==null){Add("shot.damage","shotgun","Урон дробовика","Суммарный урон всех попавших дробин независимо от части тела.","health",0,500,1,70);Set("shot.damage",shot);}
            if(FindDescriptor("damage.friendlyMultiplier")==null)Add("damage.friendlyMultiplier","damage-policy","Урон союзникам","Доля рассчитанного урона союзнику до поглощения щитом.","ratio",0,1,.05f,.5f);
            if(FindDescriptor("damage.selfMultiplier")==null)Add("damage.selfMultiplier","damage-policy","Урон себе","Доля урона себе там, где возможен self contact, до щита.","ratio",0,1,.05f,.5f);
            version=FindDescriptor("rifle.speed")!=null?6:5;
        }
        public ProvingProfile BeforeUnifiedBodyDamage()
        {
            if(id!="unity-native-combat-v1"||FindDescriptor("rifle.damage")==null)return this;
            var legacy=CreateLegacyNativeCombatDefault();
            // Reconstruct trusted historical metadata; saved snapshots supply their own unchanged values.
            var copy=UnityEngine.JsonUtility.FromJson<ProvingProfile>(UnityEngine.JsonUtility.ToJson(this));
            copy.descriptors.RemoveAll(d=>d.Path=="rifle.damage"||d.Path=="shot.damage"||d.Path.StartsWith("damage.",System.StringComparison.Ordinal));
            copy.values.RemoveAll(v=>v.Path=="rifle.damage"||v.Path=="shot.damage"||v.Path.StartsWith("damage.",System.StringComparison.Ordinal));
            foreach(var d in legacy.descriptors)
                if((d.Path=="rifle.head"||d.Path=="rifle.torso"||d.Path=="rifle.limb"||d.Path=="shot.head"||d.Path=="shot.torso"||d.Path=="shot.limb")&&copy.FindDescriptor(d.Path)==null)
                {copy.descriptors.Add(UnityEngine.JsonUtility.FromJson<NumericDescriptor>(UnityEngine.JsonUtility.ToJson(d)));copy.values.Add(new ProvingProfileValue{Path=d.Path,Value=legacy.Get(d.Path)});}
            copy.version=System.Math.Min(copy.version,copy.FindDescriptor("rifle.speed")!=null?5:4);copy.valueIndex=null;return copy;
        }
        static ProvingProfile CreateLegacyNativeCombatDefault()
        {
            var p = new ProvingProfile { id = "unity-native-combat-v1", version = 5 };
            p.Add("shot.pellets", "shotgun", "Дробины", "Число лучей одного двуствольного выстрела.", "count", 1, 64, 1, 12);
            p.Add("shot.spread", "shotgun", "Разброс", "Угловой радиус распределения дроби.", "degrees", 0, 45, .1f, 7);
            p.Add("shot.range", "shotgun", "Дальность", "Максимальная дальность каждой дробины.", "meters", 1, 200, .5f, 35);
            p.Add("shot.head", "shotgun", "Урон головы", "Урон при полном попадании всех дробин в голову.", "health", 0, 500, 1, 100);
            p.Add("shot.torso", "shotgun", "Урон корпуса", "Урон при полном попадании всех дробин в корпус.", "health", 0, 500, 1, 70);
            p.Add("shot.limb", "shotgun", "Урон конечностей", "Урон при полном попадании всех дробин в конечности.", "health", 0, 500, 1, 30);
            p.Add("rifle.spread", "rifle", "Разброс винтовки", "Угловой радиус одного выстрела.", "degrees", 0, 10, .1f, 1.2f);
            p.Add("rifle.speed", "rifle", "Скорость пули", "Скорость прямолинейного полёта; урон только при контакте, дальность не ограничена.", "meters-per-second", 1, 1000, 1, 100);
            p.Add("rifle.head", "rifle", "Урон головы", "Урон одной пули в голову.", "health", 0, 200, 1, 30);
            p.Add("rifle.torso", "rifle", "Урон корпуса", "Урон одной пули в корпус.", "health", 0, 200, 1, 20);
            p.Add("rifle.limb", "rifle", "Урон конечностей", "Урон одной пули в конечность.", "health", 0, 200, 1, 12);
            // Volumes use fractions of motor height, preserving one feet anchor across capsule tuning.
            p.Add("zone.headY", "hit-zones", "Высота головы", "Центр head sphere относительно высоты капсулы.", "height-ratio", .5f, 1.2f, .01f, .9f);
            p.Add("zone.headRadius", "hit-zones", "Радиус головы", "Радиус head sphere относительно высоты капсулы.", "height-ratio", .01f, .3f, .01f, .12f);
            p.Add("zone.torsoY", "hit-zones", "Высота корпуса", "Центр torso box относительно высоты капсулы.", "height-ratio", .2f, .9f, .01f, .6f);
            p.Add("zone.torsoX", "hit-zones", "Полуширина корпуса", "Горизонтальная половина torso box.", "height-ratio", .01f, .4f, .01f, .17f);
            p.Add("zone.torsoHeight", "hit-zones", "Полувысота корпуса", "Вертикальная половина torso box.", "height-ratio", .01f, .4f, .01f, .2f);
            p.Add("zone.torsoZ", "hit-zones", "Полуглубина корпуса", "Глубина половины torso box.", "height-ratio", .01f, .4f, .01f, .11f);
            p.Add("zone.armX", "hit-zones", "Разнос рук", "Смещение каждой arm capsule от оси тела.", "height-ratio", .1f, .5f, .01f, .24f);
            p.Add("zone.armTop", "hit-zones", "Верх руки", "Верхняя точка оси arm capsule.", "height-ratio", .5f, 1, .01f, .75f);
            p.Add("zone.armBottom", "hit-zones", "Низ руки", "Нижняя точка оси arm capsule.", "height-ratio", .1f, .6f, .01f, .41f);
            p.Add("zone.armRadius", "hit-zones", "Радиус руки", "Радиус arm capsule.", "height-ratio", .01f, .2f, .01f, .06f);
            p.Add("zone.legX", "hit-zones", "Разнос ног", "Смещение каждой leg capsule от оси тела.", "height-ratio", .01f, .3f, .01f, .1f);
            p.Add("zone.legTop", "hit-zones", "Верх ноги", "Верхняя точка оси leg capsule.", "height-ratio", .2f, .6f, .01f, .36f);
            p.Add("zone.legBottom", "hit-zones", "Низ ноги", "Нижняя точка оси leg capsule.", "height-ratio", .01f, .2f, .01f, .07f);
            p.Add("zone.legRadius", "hit-zones", "Радиус ноги", "Радиус leg capsule.", "height-ratio", .01f, .2f, .01f, .07f);
            p.Add("spawn.bodySampleRadiusFraction","spawn","Body width sampling","Radius fraction used to conservatively test exposed body sides.","ratio",.5f,1,.05f,.9f);
            p.Add("spawn.bodySampleLow","spawn","Lower body sampling","Capsule-height fraction for lower-body visibility.","ratio",.05f,.3f,.01f,.1f);
            p.Add("spawn.bodySampleMiddle","spawn","Body middle sampling","Capsule-height fraction for torso visibility.","ratio",.3f,.7f,.01f,.5f);
            p.Add("spawn.bodySampleHigh","spawn","Upper body sampling","Capsule-height fraction for upper-body visibility.","ratio",.7f,.99f,.01f,.95f);
            p.Add("spawn.groundProbeDistance","spawn","Ground probe distance","Vertical query range for distance ranking of airborne live participants.","meters",4,100,1,20);
            p.Add("spawn.sample", "spawn", "Радиус поиска NavMesh", "Локальный поиск опоры без перехода на соседний этаж.", "meters", .05f, 1, .05f, .5f);
            p.Add("spawn.floorTolerance", "spawn", "Допуск этажа", "Предельная разница authored floor и найденной опоры.", "meters", .01f, .5f, .01f, .15f);
            p.Add("death.corpseSeconds", "death", "Время тела", "Срок non-colliding тела по времени сессии.", "seconds", 1, 30, .5f, 8);
            p.Add("death.bodyHeight", "death", "Высота лежащего тела", "Высота pivot повёрнутого GLB над опорой.", "meters", .05f, .6f, .01f, .25f);
            p.Add("killcam.orbitRadius", "killcam", "Радиус облёта", "Дистанция no-killer камеры от тела.", "meters", 1, 6, .1f, 2.5f);
            p.Add("killcam.orbitHeight", "killcam", "Высота облёта", "Вертикальное смещение камеры над телом.", "meters", .5f, 3, .1f, 1.2f);
            p.Add("killcam.orbitSpeed", "killcam", "Скорость облёта", "Угловая скорость камеры без убийцы.", "degrees-per-second", 1, 45, 1, 12);
            p.Add("killcam.clipRadius", "killcam", "Зазор камеры", "Радиус sphere query для ограничения камеры стенами.", "meters", .05f, .5f, .01f, .15f);
            p.Add("killcam.killerTransitionSeconds", "killcam", "Переход к убийце", "Время плавного выхода из death-eye на орбиту убийцы.", "seconds", .1f, 3f, .1f, .7f);
            p.Add("killcam.killerOrbitRadius", "killcam", "Радиус вокруг убийцы", "Дистанция камеры от конкретной жизни убийцы.", "meters", 1f, 6f, .1f, 2.4f);
            p.Add("killcam.killerAboveEyes", "killcam", "Высота над глазами убийцы", "Небольшой угол сверху при облёте убийцы.", "meters", 0f, 2f, .05f, .35f);
            p.Add("killcam.killerOrbitSpeed", "killcam", "Скорость вокруг убийцы", "Угловая скорость камеры вокруг убийцы.", "degrees-per-second", 1f, 90f, 1f, 35f);
            AddRocketCombat(p);
            return p;
        }
    }
}
