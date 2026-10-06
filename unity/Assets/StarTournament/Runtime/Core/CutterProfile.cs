namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
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
            var p = new ProvingProfile { id="cutter-beam-v1", version=4 };
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
            return p;
        }
    }
}
