namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        // F1 adds renderer controls without rewriting any v5 revision or value.
        internal ProvingProfile BeforeSeamFidelity()
        {
            if(id!="cutter-beam-v1"||version<6)return this;
            var p=DetachedCopy();p.version=5;
            p.descriptors.RemoveAll(d=>d.Path.StartsWith("cutter.seam.detail."));
            p.values.RemoveAll(v=>v.Path.StartsWith("cutter.seam.detail."));
            return p;
        }
        // Project the exact v4 registry before historical hash validation.
        internal ProvingProfile BeforeSeamPresentation()
        {
            if(id!="cutter-beam-v1"||version<5)return this;
            var p=DetachedCopy();p.version=4;
            p.descriptors.RemoveAll(d=>d.Path.StartsWith("cutter.seam."));
            p.values.RemoveAll(v=>v.Path.StartsWith("cutter.seam."));
            return p;
        }
        // Exact first-release registry for immutable Lab history compatibility.
        internal ProvingProfile BeforeCutterThickness()
        {
            var p=UnityEngine.JsonUtility.FromJson<ProvingProfile>(UnityEngine.JsonUtility.ToJson(this));
            if(p.id=="cutter-beam-v1")
            {
                p.version=1;
                p.Set("cutter.referenceDamage",100f);
                p.descriptors.Find(d=>d.Path=="cutter.referenceDamage").DefaultValue=100f;
                p.Set("cutter.width",0.012f);
                p.descriptors.Find(d=>d.Path=="cutter.width").DefaultValue=0.012f;
            }
            return p;
        }
        // Exact v2 balance registry: historical snapshots retain their original hashes and values.
        internal ProvingProfile BeforeCutterDamage()
        {
            // Only the changed registry needs an isolated predecessor; all other entries are read-only.
            if(id!="cutter-beam-v1"||version!=3)return this;
            var p=UnityEngine.JsonUtility.FromJson<ProvingProfile>(UnityEngine.JsonUtility.ToJson(this));
            p.version=2;
            p.Set("cutter.referenceDamage",100f);
            p.descriptors.Find(d=>d.Path=="cutter.referenceDamage").DefaultValue=100f;
            return p;
        }
        public static ProvingProfile CreateCutterDefault()
        {
            var p = new ProvingProfile { id="cutter-beam-v1", version=6 };
            p.Add("cutter.energyCapacity","cutter","Полный заряд","Запас на новую жизнь.","energy",1f,300f,1f,30f);
            p.Add("cutter.energyPerSecond","cutter","Расход","Расход во время роста и полного луча.","energy/s",0.01f,20f,0.01f,1f);
            p.Add("cutter.maxRangeMeters","cutter","Дальность","Максимальная достигнутая длина.","m",0.1f,100f,0.1f,20f);
            p.Add("cutter.extensionSeconds","cutter","Рост","Время линейного роста от дула.","s",0f,3f,0.01f,0.15f);
            p.Add("cutter.contactTickSeconds","cutter","Контактный тик","Непрерывный контакт до очередного урона.","s",0.01f,1f,0.01f,0.1f);
            p.Add("cutter.referenceDamage","cutter","Эталонный урон","Урон за эталонное время контакта.","HP",0f,1000f,1f,300f);
            p.Add("cutter.referenceContactSeconds","cutter","Эталонное время","Время контакта для эталонного урона.","s",0.1f,30f,0.1f,3f);
            p.Add("cutter.hitRadius","cutter","Радиус луча","Расширение gameplay капсулы при запросе.","m",0f,0.5f,0.01f,0.02f);
            p.Add("cutter.botMinimumDistance","cutter-bots","Бот: минимум","Ближняя граница выбора Резака.","m",0f,30f,0.1f,3f);
            p.Add("cutter.botRangeFraction","cutter-bots","Бот: запас дальности","Доля дальности для выбора луча.","ratio",0.1f,1f,0.01f,0.9f);
            p.Add("cutter.width","cutter-effects","Толщина луча","Визуальная толщина сердцевины; не меняет радиус попадания.","m",0.001f,0.3f,0.001f,0.03f);
            p.Add("cutter.jitter","cutter-effects","Изломы","Радиус электрических ветвей вокруг оси.","m",0f,0.5f,0.01f,0.08f);
            p.Add("cutter.frequency","cutter-effects","Частота","Частота движения ветвей.","Hz",0f,100f,1f,24f);
            p.Add("cutter.segments","cutter-effects","Сегменты","Число визуальных электрических сегментов.","count",2f,64f,1f,20f);
            p.Add("cutter.glow","cutter-effects","Свечение","Яркость луча.","ratio",0f,10f,0.1f,1.5f);
            p.Add("cutter.red","cutter-effects","Красный","Цвет луча R.","ratio",0f,1f,0.01f,1f);
            p.Add("cutter.green","cutter-effects","Зелёный","Цвет луча G.","ratio",0f,1f,0.01f,0.55f);
            p.Add("cutter.blue","cutter-effects","Синий","Цвет луча B.","ratio",0f,1f,0.01f,0.08f);
            p.Add("cutter.impactSize","cutter-effects","Контакт","Размер свечения у стены.","m",0f,1f,0.01f,0.12f);
            p.Add("cutter.modelScale","cutter-effects","Масштаб","Размер корпуса на grip mount.","ratio",0.1f,3f,0.05f,1.4f);
            p.Add("cutter.modelX","cutter-effects","Смещение X","Смещение от grip mount.","m",-2f,2f,0.01f,0f);
            p.Add("cutter.modelY","cutter-effects","Смещение Y","Смещение от grip mount.","m",-2f,2f,0.01f,0f);
            p.Add("cutter.modelZ","cutter-effects","Смещение Z","Смещение от grip mount.","m",-2f,2f,0.01f,0f);
            p.Add("cutter.modelPitch","cutter-effects","Поворот Pitch","Угол корпуса относительно grip mount.","degrees",-180f,180f,1f,0f);
            p.Add("cutter.modelYaw","cutter-effects","Поворот Yaw","Угол корпуса относительно grip mount.","degrees",-180f,180f,1f,0f);
            p.Add("cutter.modelRoll","cutter-effects","Поворот Roll","Угол корпуса относительно grip mount.","degrees",-180f,180f,1f,0f);
            p.Add("cutter.arcWidth","cutter-effects","Толщина ветвей","Толщина электрических изломов вокруг сердцевины.","m",0.001f,0.1f,0.001f,0.004f);
            p.Add("cutter.coreWhiten","cutter-effects","Белая сердцевина","Смешивание цвета сердцевины с белым.","ratio",0f,1f,0.01f,0.85f);
            p.Add("cutter.nearFade","cutter-effects","Зазор камеры","Лучи плавно исчезают вблизи камеры, сохраняя gameplay контакт.","m",0.01f,2f,0.01f,0.35f);
            p.Add("cutter.seam.coronaScale","cutter-effects","SEAM: корона","Ширина мягкой оболочки относительно сердцевины.","ratio",1,8,.1f,3);
            p.Add("cutter.seam.coronaAlpha","cutter-effects","SEAM: плотность короны","Прозрачность мягкой оболочки.","ratio",0,1,.01f,.16f);
            p.Add("cutter.seam.sparkCount","cutter-effects","SEAM: искры","Число коротких лучей в локальном контакте.","count",0,32,1,14);
            p.Add("cutter.seam.sparkReach","cutter-effects","SEAM: разлёт искр","Максимальная длина визуального следа; без урона.","m",.01f,1,.01f,.23f);
            p.Add("cutter.seam.sparkSeconds","cutter-effects","SEAM: цикл искр","Время движения искр по часам матча.","s",.02f,1,.01f,.14f);
            p.Add("cutter.seam.contactSeconds","cutter-effects","SEAM: след контакта","Короткое затухание после фактического попадания.","s",.01f,.5f,.01f,.065f);
            p.Add("cutter.seam.shieldRadius","cutter-effects","SEAM: рябь щита","Радиус локальной ряби вместо непрозрачного купола.","m",.01f,1,.01f,.18f);
            p.Add("cutter.seam.muzzleRadius","cutter-effects","SEAM: сопло","Радиус мягкого свечения активного сопла.","m",.01f,.3f,.005f,.065f);
            p.Add("cutter.seam.grip.supportRoll","cutter-effects","SEAM: Разворот ладони","Подгонка кисти к новой рукояти SEAM вместо исторической общей позы; остальные пальцы регулируются профилем бойца.","degrees",-90f,90f,1f,49f);
            p.Add("cutter.seam.grip.supportYaw","cutter-effects","SEAM: Направление ладони","Подгонка кисти к новой рукояти SEAM вместо исторической общей позы; остальные пальцы регулируются профилем бойца.","degrees",-100f,100f,1f,22f);
            p.Add("cutter.seam.grip.supportCurl","cutter-effects","SEAM: Обхват передней рукояти","Подгонка кисти к новой рукояти SEAM вместо исторической общей позы; остальные пальцы регулируются профилем бойца.","degrees",-30f,90f,1f,45f);
            p.Add("cutter.seam.grip.thumbOpposition","cutter-effects","SEAM: Оппозиция большого пальца","Подгонка кисти к новой рукояти SEAM вместо исторической общей позы; остальные пальцы регулируются профилем бойца.","degrees",-90f,90f,1f,-78f);
            p.Add("cutter.seam.grip.supportX","cutter-effects","SEAM: Контакт X","Подгонка кисти к новой рукояти SEAM вместо исторической общей позы; остальные пальцы регулируются профилем бойца.","rig-units",-0.8f,0.8f,0.005f,-0.11f);
            p.Add("cutter.seam.grip.supportY","cutter-effects","SEAM: Контакт Y","Подгонка кисти к новой рукояти SEAM вместо исторической общей позы; остальные пальцы регулируются профилем бойца.","rig-units",-0.8f,0.8f,0.005f,-0.155f);
            p.Add("cutter.seam.grip.supportZ","cutter-effects","SEAM: Контакт Z","Подгонка кисти к новой рукояти SEAM вместо исторической общей позы; остальные пальцы регулируются профилем бойца.","rig-units",-0.8f,0.8f,0.005f,0.415f);
            p.Add("cutter.seam.shieldRed","cutter-effects","SEAM: щит R","Цвет локальной ряби щита.","ratio",0,2,.01f,.13f);
            p.Add("cutter.seam.shieldGreen","cutter-effects","SEAM: щит G","Цвет локальной ряби щита.","ratio",0,2,.01f,.7f);
            p.Add("cutter.seam.shieldBlue","cutter-effects","SEAM: щит B","Цвет локальной ряби щита.","ratio",0,2,.01f,1.3f);
            p.Add("cutter.seam.shieldRingWidth","cutter-effects","SEAM: толщина ряби","Видимая толщина кольца щита без изменения hit volume.","m",.001f,.1f,.001f,.012f);
            p.Add("cutter.seam.muzzleBlendDistance","cutter-effects","SEAM: согласование дула","Короткое соединение видимого сопла с authoritative лучом; контакты и дальность неизменны.","m",.1f,3,.05f,.8f);
            p.Add("cutter.seam.sparkWidth","cutter-effects","SEAM: толщина искр","Ширина коротких контактных следов.","m",.001f,.03f,.001f,.006f);
            p.Add("cutter.seam.contactGlow","cutter-effects","SEAM: яркость контакта","Яркость локальной точки соприкосновения.","ratio",0,5,.1f,2.5f);
            p.Add("cutter.seam.detail.arcSubdivisions","cutter-effects","SEAM: детализация разрядов","Подсегменты электрической нити, не gameplay луча.","count",1,12,1,6);
            p.Add("cutter.seam.detail.sparkLayers","cutter-effects","SEAM: слои искр","Число независимых фаз россыпи.","count",1,4,1,3);
            p.Add("cutter.seam.detail.sparkSpread","cutter-effects","SEAM: ширина россыпи","Множитель видимого разлёта.","ratio",.5f,3,.1f,1.8f);
            p.Add("cutter.seam.detail.shieldSpread","cutter-effects","SEAM: поле щита","Множитель локального поля контакта.","ratio",1,4,.1f,2.4f);
            p.Add("cutter.seam.detail.arcRadiusScale","cutter-effects","SEAM: размах разрядов","Масштаб мелких изломов относительно сохранённого радиуса.","ratio",.05f,1,.05f,.25f);
            p.Add("cutter.seam.detail.arcWidthScale","cutter-effects","SEAM: тонкие нити","Масштаб толщины электрических нитей.","ratio",.1f,1,.05f,.5f);
            p.Add("cutter.seam.detail.warmth","cutter-effects","SEAM: тёплое свечение","Смешивание линейного оттенка для оранжевых разрядов.","ratio",0,1,.05f,1);
            return p;
        }
    }
}
