namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        internal ProvingProfile BeforeForgePlacement(int soughtVersion)
        {
            if(id=="combat-bowl-ring-r7-authoring-v1"&&version>=14&&soughtVersion<14)
                return CreateCombatBowlAuthoringDefault(CombatBowlCatalog.Draft(true));
            if(id=="lunar-laboratory-authoring-v1"&&version>=4&&soughtVersion<4)
                return CreateLunarAuthoring(LunarLaboratoryCatalog.Draft(previousForgePlacement:true),3);
            if(id=="well-authoring-v1"&&version>=7&&soughtVersion<7)
                return WellArenaCatalog.AuthoringProfile(true);
            return this;
        }
        internal ProvingProfile BeforeForgePickupsIfNeeded(int soughtVersion)
        {
            int first=id=="combat-bowl-ring-r7-authoring-v1"?13:id=="industrial-tunnels-authoring-v1"?2:id=="lunar-laboratory-authoring-v1"?3:id=="well-authoring-v1"?6:0;
            return soughtVersion<first?BeforeForgePickups():this;
        }
        internal ProvingProfile BeforeForgePickups()
        {
            int previous=id=="combat-bowl-ring-r7-authoring-v1"?12:id=="industrial-tunnels-authoring-v1"?1:id=="lunar-laboratory-authoring-v1"?2:id=="well-authoring-v1"?5:0;
            if(previous==0||version<=previous)return this;
            var p=BeforeForgePlacement(0).DetachedCopy();p.version=previous;
            bool Added(string path)=>path.StartsWith("forge-")||path.Contains(".forge-");
            p.descriptors.RemoveAll(d=>Added(d.Path));p.values.RemoveAll(v=>Added(v.Path));return p;
        }
        internal ProvingProfile BeforeForgeRapidFire()
        {
            if(id!="forge-plasma-v1"||version<6)return this;
            var p=DetachedCopy();p.version=5;
            p.RebalanceDefault("plasma.cooldownSeconds",.2f);
            p.FindDescriptor("plasma.cooldownSeconds").Step=.01f;
            return p;
        }
        internal ProvingProfile BeforeForgeHeavyTuning()
        {
            if(id!="forge-plasma-v1"||version<5)return this;
            var p=BeforeForgeRapidFire().DetachedCopy();p.version=4;
            p.RebalanceDefault("plasma.damage",20);p.RebalanceDefault("plasma.speed",24);p.RebalanceDefault("plasma.orbRadius",.117f);
            p.FindDescriptor("plasma.speed").Step=1;p.FindDescriptor("plasma.orbRadius").Step=.001f;
            return p;
        }
        // Trusted pre-tuning registry: historical revisions keep exact values and metadata.
        internal ProvingProfile BeforeForgeCadence()
        {
            if(id!="forge-plasma-v1"||version<4)return this;
            var p=BeforeForgeHeavyTuning().DetachedCopy();p.version=3;
            p.RebalanceDefault("plasma.cooldownSeconds",.1f);
            p.RebalanceDefault("plasma.speed",30);
            p.RebalanceDefault("plasma.orbRadius",.09f);
            p.FindDescriptor("plasma.orbRadius").Step=.01f;
            p.FindDescriptor("plasma.orbRadius").Description="Радиус свечения; не меняет точку gameplay контакта.";
            return p;
        }
        internal ProvingProfile BeforeForgeViewScale()
        {
            if(id!="forge-plasma-v1"||version<2)return this;
            var p=BeforeForgeReactor().DetachedCopy();p.version=1;
            p.descriptors.RemoveAll(d=>d.Path=="plasma.viewScale");p.values.RemoveAll(v=>v.Path=="plasma.viewScale");return p;
        }
        internal ProvingProfile BeforeForgeReactor()
        {
            if(id!="forge-plasma-v1"||version<3)return this;
            var p=BeforeForgeCadence().DetachedCopy();p.version=2;
            p.descriptors.RemoveAll(d=>d.Path.StartsWith("plasma.reactor"));p.values.RemoveAll(v=>v.Path.StartsWith("plasma.reactor"));return p;
        }
        public void EnsurePlasmaDescriptors()
        {
            var current=CreatePlasmaDefault();valueIndex=null;
            foreach(var descriptor in current.Descriptors)
                if(FindDescriptor(descriptor.Path)==null)
                {descriptors.Add(descriptor);values.Add(new ProvingProfileValue{Path=descriptor.Path,Value=current.Get(descriptor.Path)});}
            version=current.Version;valueIndex=null;
        }
        public static ProvingProfile CreatePlasmaDefault()
        {
            var p=new ProvingProfile{id="forge-plasma-v1",version=6};
            p.Add("plasma.damage","plasma","Урон шара FORGE","Прямой урон одного шара до усиления и союзного коэффициента.","HP",0,500,1,24);
            p.Add("plasma.cooldownSeconds","plasma","Интервал FORGE","Пауза между шарами при удержании огня.","s",.02f,2,1f/600f,.2f/1.2f);
            p.Add("plasma.speed","plasma","Скорость шара","Скорость прямого полёта: меньше — легче увернуться.","m/s",1,200,.1f,14.4f);
            p.Add("plasma.pickupAmmo","plasma","Заряд при подборе","Подбор пополняет запас до этого значения.","charge",1,500,1,100);
            p.Add("plasma.modelScale","plasma-effects","Масштаб FORGE","Размер модели на существующем хвате.","ratio",.1f,3,.05f,1.4f);
            p.Add("plasma.orbRadius","plasma-effects","Радиус видимого шара","Радиус шара для видимой оболочки и объёмного контакта; пульсация не меняет gameplay радиус.","m",.01f,.5f,.0001f,.1521f);
            p.Add("plasma.trailLength","plasma-effects","Длина следа","Короткий зелёный след полёта.","m",.01f,3,.01f,.45f);
            p.Add("plasma.impactSeconds","plasma-effects","Длительность вспышки","Время визуального контакта по часам матча.","s",.02f,1,.01f,.16f);
            p.Add("plasma.impactRadius","plasma-effects","Радиус вспышки","Только видимый размер; урона вокруг нет.","m",.01f,1,.01f,.18f);
            p.Add("plasma.muzzleBlendDistance","plasma-effects","Выход из дула","Дистанция перехода от видимого дула к gameplay траектории.","m",.1f,10,.1f,2);
            p.Add("plasma.orbPulseAmplitude","plasma-effects","Пульсация шара","Относительная амплитуда видимого радиуса.","ratio",0,.5f,.01f,.07f);
            p.Add("plasma.orbPulseHz","plasma-effects","Частота пульсации","Частота движения плазмы внутри шара.","Hz",0,20,.1f,5);
            p.Add("plasma.audioGain","plasma","Громкость FORGE","Относительная громкость энергетического выстрела.","ratio",0,4,.05f,1);
            p.Add("plasma.viewScale","plasma-effects","Масштаб FORGE в руках","Дополнительный масштаб только от первого лица; уменьшает перекрытие обзора и сохраняет размер world/pickup модели.","ratio",.4f,1.2f,.05f,.7f);
            p.Add("plasma.reactorSpeed","plasma-reactor","Скорость вихря","Скорость закручивания потоков реактора по часам матча.","rad/s",0,4,.05f,.85f);
            p.Add("plasma.reactorPulse","plasma-reactor","Дыхание реактора","Мягкая нерегулярная пульсация энергии.","ratio",0,.4f,.01f,.12f);
            p.Add("plasma.reactorIntensity","plasma-reactor","Яркость реактора","Яркость зелёных потоков без дополнительного света.","ratio",.1f,3,.05f,1.65f);
            p.Add("plasma.reactorShotGain","plasma-reactor","Импульс выстрела","Дополнительная яркость и сжатие энергии при реальном выстреле.","ratio",0,1,.05f,.45f);
            p.Add("plasma.reactorRecovery","plasma-reactor","Восстановление реактора","Длительность импульса; ограничена реальным cooldown для точного восстановления snapshot.","s",.02f,.3f,.01f,.09f);
            return p;
        }
    }
}
