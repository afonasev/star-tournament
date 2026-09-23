namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public static ProvingProfile CreateNativeCombatDefault()
        {
            var p = new ProvingProfile { id = "unity-native-combat-v1", version = 2 };
            p.Add("shot.pellets", "shotgun", "Дробины", "Число лучей одного двуствольного выстрела.", "count", 1, 64, 1, 12);
            p.Add("shot.spread", "shotgun", "Разброс", "Угловой радиус распределения дроби.", "degrees", 0, 45, .1f, 7);
            p.Add("shot.range", "shotgun", "Дальность", "Максимальная дальность каждой дробины.", "meters", 1, 200, .5f, 35);
            p.Add("shot.head", "shotgun", "Урон головы", "Урон при полном попадании всех дробин в голову.", "health", 0, 500, 1, 100);
            p.Add("shot.torso", "shotgun", "Урон корпуса", "Урон при полном попадании всех дробин в корпус.", "health", 0, 500, 1, 70);
            p.Add("shot.limb", "shotgun", "Урон конечностей", "Урон при полном попадании всех дробин в конечности.", "health", 0, 500, 1, 30);
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
            return p;
        }
    }
}
