namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        // Exact predecessor registry for additive native Lab history migration. Historical snapshots remain immutable.
        internal ProvingProfile BeforePulse()
        {
            var p=BeforeWeaponSwitch();
            p.descriptors.RemoveAll(d=>d.Path.StartsWith("rocket.")||d.Path.StartsWith("presentation.rocket"));
            p.values.RemoveAll(v=>v.Path.StartsWith("rocket.")||v.Path.StartsWith("presentation.rocket"));
            if(p.id==DefaultId)p.version=5;
            if(p.id=="unity-combat-state-v1")p.version=4;
            if(p.id=="unity-native-combat-v1")p.version=3;
            return p;
        }
        internal ProvingProfile BeforeFullHeal()
        {
            var p=BeforePulse();
            p.descriptors.RemoveAll(d=>d.Path.StartsWith("heal.")||d.Path.StartsWith("presentation.heal"));
            p.values.RemoveAll(v=>v.Path.StartsWith("heal.")||v.Path.StartsWith("presentation.heal"));
            if(p.id==DefaultId)p.version=4;
            if(p.id=="unity-combat-state-v1")p.version=3;
            return p;
        }
        static void AddRocketLife(ProvingProfile p)
        {
            p.Add("rocket.startingAmmo","rocket","Стартовые ракеты","Запас Pulse в начале каждой жизни.","ammo",1,200,1,20);
            p.Add("rocket.cooldownSeconds","rocket","Пауза Pulse","Минимальная пауза между отдельными нажатиями огня.","seconds",.1f,5,.05f,1.2f);
        }
        static void AddRocketCombat(ProvingProfile p)
        {
            p.Add("rocket.speed","rocket","Скорость ракеты","Скорость прямого полёта; sweep исключает пропуск контактов.","meters-per-second",1,120,1,30);
            p.Add("rocket.radius","rocket","Радиус взрыва","Радиус линейного урона до ближайшей точки gameplay capsule.","meters",.1f,20,.1f,4);
            p.Add("rocket.maximumDamage","rocket","Максимальный урон","Единый предел прямого и радиального урона, включая усиление.","health",0,500,1,100);
            p.Add("rocket.range","rocket","Дальность полёта","Предельный путь; без контакта ракета гаснет без взрыва.","meters",10,500,1,200);
            p.Add("rocket.botMinimumDistance","rocket","Минимальная дистанция бота","Вблизи бот использует дробовик/автомат вместо Pulse.","meters",1,30,.1f,7);
            p.Add("rocket.botSafetyMargin","rocket","Запас безопасности бота","Дополнительная дистанция от возможного взрыва для себя и союзников.","meters",0,10,.1f,1);
        }
        internal ProvingProfile BeforeShotOrigins()
        {
            // Only the proving profile changed; other immutable registries can be shared.
            if(id!=DefaultId||FindDescriptor("presentation.rocketMuzzleBlendDistance")==null)return this;
            var p=UnityEngine.JsonUtility.FromJson<ProvingProfile>(UnityEngine.JsonUtility.ToJson(this));
            p.descriptors.RemoveAll(d=>d.Path=="presentation.rocketMuzzleBlendDistance");
            p.values.RemoveAll(v=>v.Path=="presentation.rocketMuzzleBlendDistance");
            if(p.id==DefaultId&&p.version>=7)p.version=6;
            return p;
        }
        static void AddRocketPresentation(ProvingProfile p)
        {
            p.Add("presentation.rocketModelScale","rocket-effects","Масштаб Pulse","Масштаб оружия относительно trooper grip mount.","ratio",.5f,3,.05f,1.4f);
            p.Add("presentation.rocketBodyRadius","rocket-effects","Размер ракеты","Видимый радиус снаряда, без изменения collision.","meters",.01f,.4f,.01f,.08f);
            p.Add("presentation.rocketBodyLength","rocket-effects","Длина ракеты","Видимая длина летящего снаряда.","meters",.05f,1,.01f,.35f);
            p.Add("presentation.rocketMuzzleBlendDistance","rocket-effects","Выход ракеты из ствола","Дистанция плавного перехода от видимого ствола к расчётному полёту; не меняет столкновения и урон.","meters",.5f,10,.1f,3f);
            p.Add("presentation.rocketTrailLength","rocket-effects","Длина следа","Длина светящегося следа позади ракеты.","meters",.1f,8,.1f,1.2f);
            p.Add("presentation.rocketTrailWidth","rocket-effects","Толщина следа","Толщина светящегося следа.","meters",.005f,.3f,.005f,.05f);
            p.Add("presentation.rocketExplosionSeconds","rocket-effects","Длительность взрыва","Время визуального импульса по simulation clock.","seconds",.05f,2,.05f,.45f);
            p.Add("presentation.rocketExplosionSize","rocket-effects","Размер вспышки","Видимый радиус импульса; не влияет на урон.","meters",.1f,8,.1f,1.8f);
            p.Add("presentation.rocketRed","rocket-effects","Импульс · красный","Красный канал светящегося импульса и следа.","ratio",0,1,.01f,1);
            p.Add("presentation.rocketGreen","rocket-effects","Импульс · зелёный","Зелёный канал светящегося импульса и следа.","ratio",0,1,.01f,.32f);
            p.Add("presentation.rocketBlue","rocket-effects","Импульс · синий","Синий канал светящегося импульса и следа.","ratio",0,1,.01f,.04f);
        }
    }
}
